using System;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Amazon.S3;
using Serilog;
using StackExchange.Redis;
using AiGovernance.Grpc;
using Shared.Extensions;
using Shared.Interceptors;
using Shared.Security;
using MailService.Application.Interfaces.AI;
using MailService.Application.Interfaces.Classification;
using MailService.Application.Interfaces.Messaging;
using MailService.Application.Interfaces.Persistence;
using MailService.Application.Interfaces.RateLimiting;
using MailService.Application.Interfaces.Security;
using MailService.Application.Interfaces.Stalwart;
using MailService.Application.Interfaces.Storage;
using MailService.Application.Options;
using MailService.Application.Pipeline;
using MailService.Application.Pipeline.Stages;
using MailService.Domain.Entities;
using MailService.GrpcServices;
using MailService.Infrastructure.AI;
using MailService.Infrastructure.Cache;
using MailService.Infrastructure.Health;
using MailService.Infrastructure.Messaging;
using MailService.Infrastructure.Messaging.Consumers;
using MailService.Infrastructure.Persistence;
using MailService.Infrastructure.Persistence.Repositories;
using MailService.Infrastructure.Security.Dns;
using MailService.Infrastructure.Security.Spf;
using MailService.Infrastructure.Security.Dkim;
using MailService.Infrastructure.Security.Dmarc;
using MailService.Infrastructure.Security.Malware;
using MailService.Infrastructure.Security.Spam;
using MailService.Infrastructure.Stalwart;
using MailService.Infrastructure.Storage;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog Structured Logging
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore", Serilog.Events.LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("service", "mail-service")
    .WriteTo.Console()
    .CreateLogger();

builder.Host.UseSerilog();

// Register and validate production MailServiceOptions
builder.Services.Configure<MailServiceOptions>(options =>
{
    options.DatabaseConnectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    options.RedisConnectionString = builder.Configuration.GetConnectionString("Redis")
        ?? builder.Configuration["Redis:ConnectionString"];
    options.RabbitMqHost = builder.Configuration["RabbitMQ:Host"];
    options.RabbitMqPort = int.TryParse(builder.Configuration["RabbitMQ:Port"], out int p) ? p : 5672;
    options.RabbitMqUsername = builder.Configuration["RabbitMQ:Username"];
    options.RabbitMqPassword = builder.Configuration["RabbitMQ:Password"];
    options.RabbitMqVirtualHost = builder.Configuration["RabbitMQ:VirtualHost"] ?? "mail";

    options.StalwartBaseUrl = builder.Configuration["Stalwart:BaseUrl"];
    options.StalwartSmtpHost = builder.Configuration["Stalwart:SmtpHost"];
    options.StalwartSmtpPort = int.TryParse(builder.Configuration["Stalwart:SmtpPort"], out int sp) ? sp : 25;

    options.ClamAvHost = builder.Configuration["ClamAV:Host"];
    options.ClamAvPort = int.TryParse(builder.Configuration["ClamAV:Port"], out int cp) ? cp : 3310;

    options.SpamAssassinHost = builder.Configuration["SpamAssassin:Host"];
    options.SpamAssassinPort = int.TryParse(builder.Configuration["SpamAssassin:Port"], out int sap) ? sap : 783;

    options.AiGovernanceEndpoint = builder.Configuration["AiGovernance:GrpcEndpoint"]
        ?? builder.Configuration["AiGovernance:ServiceUrl"];
});
builder.Services.AddSingleton<IValidateOptions<MailServiceOptions>>(sp =>
    new MailServiceOptionsValidator(builder.Environment.IsProduction()));

// Add gRPC services with AuthInterceptor and GrpcExceptionInterceptor
builder.Services.AddSingleton<MailService.Infrastructure.Interceptors.GrpcExceptionInterceptor>();
builder.Services.AddGrpc(options =>
{
    options.Interceptors.Add<AuthInterceptor>();
    options.Interceptors.Add<MailService.Infrastructure.Interceptors.GrpcExceptionInterceptor>();
    options.MaxReceiveMessageSize = 75 * 1024 * 1024; // 75 MB Max gRPC payload
    options.MaxSendMessageSize = 75 * 1024 * 1024;    // 75 MB Max gRPC payload
});

// Configure MassTransit & RabbitMQ Consumers
builder.Services.AddSharedMassTransit(builder.Configuration, x =>
{
    x.AddConsumer<TenantUserProvisionedConsumer>();
    x.AddConsumer<SendSystemEmailConsumer>();
});

// Configure MediatR & FluentValidation
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));

// Configure EF Core PostgreSQL (Managed Neon connection)
string connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Host=localhost;Port=5432;Database=aurora_mail_service;Username=postgres;Password=postgres";

builder.Services.AddDbContext<MailServiceDbContext>(options =>
    options.UseNpgsql(connectionString, npgsql =>
    {
        npgsql.EnableRetryOnFailure(3);
    }));

// Register Scoped Identity & Repositories & Outbox
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
builder.Services.AddScoped<IEmailDraftRepository, EmailDraftRepository>();
builder.Services.AddScoped<IOutboxWriter, OutboxWriter>();
builder.Services.AddHostedService<OutboxProcessorBackgroundService>();

// Register Infrastructure HTTP Clients & S3 / R2
builder.Services.AddHttpClient();
builder.Services.AddHttpClient<IStalwartManagementClient, StalwartManagementClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Stalwart:BaseUrl"] ?? "http://localhost:8080");
});

builder.Services.AddSingleton<IAmazonS3>(sp => new AmazonS3Client(
    builder.Configuration["R2:AccessKey"] ?? "dev",
    builder.Configuration["R2:SecretKey"] ?? "dev",
    new AmazonS3Config
    {
        ServiceURL = $"https://{builder.Configuration["R2:AccountId"] ?? "dev"}.r2.cloudflarestorage.com",
        ForcePathStyle = true
    }));

// Register Redis Connection Multiplexer
string redisConnection = builder.Configuration.GetConnectionString("Redis")
    ?? builder.Configuration["Redis:ConnectionString"]
    ?? "localhost:6379,abortConnect=false";

builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
{
    var config = ConfigurationOptions.Parse(redisConnection);
    config.AbortOnConnectFail = false;
    config.ConnectRetry = 3;
    config.ConnectTimeout = 3000;
    return ConnectionMultiplexer.Connect(config);
});

builder.Services.AddScoped<IR2StorageClient, R2StorageClient>();
builder.Services.AddScoped<IRateLimitService, RedisCacheService>();

builder.Services.AddScoped<IClamAvClient, ClamAvClient>();
builder.Services.AddScoped<ISpamAssassinClient, SpamAssassinClient>();
builder.Services.AddScoped<IDnsLookupService, DnsLookupService>();
builder.Services.AddScoped<SpfEvaluator>();
builder.Services.AddScoped<DkimVerifier>();
builder.Services.AddScoped<DmarcEvaluator>();

// Register AI Governance Services & Fallback Client
builder.Services.Configure<AiGovernanceOptions>(builder.Configuration.GetSection(AiGovernanceOptions.SectionName));
var aiGovernanceUrl = builder.Configuration["AiGovernance:GrpcEndpoint"]
    ?? builder.Configuration["AiGovernance:ServiceUrl"]
    ?? "http://localhost:5005";

builder.Services.AddGrpcClient<AiGovernanceService.AiGovernanceServiceClient>(o =>
{
    o.Address = new Uri(aiGovernanceUrl);
});
builder.Services.AddGrpcClient<AiExecutionService.AiExecutionServiceClient>(o =>
{
    o.Address = new Uri(aiGovernanceUrl);
});

builder.Services.AddScoped<IAiGovernanceClient, AiGovernanceGrpcClient>();
builder.Services.AddScoped<IPhishingDetectionService, GovernedPhishingDetectionService>();
builder.Services.AddScoped<IRiskScoringService, GovernedRiskScoringService>();

builder.Services.AddScoped<IEmailClassifier, SimpleClassifier>();
builder.Services.AddScoped<ISmtpDeliveryService, MailKitSmtpDeliveryService>();

// Register Pipeline Stages & Runners
builder.Services.AddScoped<IInboundPipelineStage, TlsVerificationStage>();
builder.Services.AddScoped<IInboundPipelineStage, HeaderParsingStage>();
builder.Services.AddScoped<IInboundPipelineStage, RecipientValidationStage>();
builder.Services.AddScoped<IInboundPipelineStage, SpfValidationStage>();
builder.Services.AddScoped<IInboundPipelineStage, DkimValidationStage>();
builder.Services.AddScoped<IInboundPipelineStage, DmarcEvaluationStage>();
builder.Services.AddScoped<IInboundPipelineStage, TenantValidationStage>();
builder.Services.AddScoped<IInboundPipelineStage, AttachmentValidationStage>();
builder.Services.AddScoped<IInboundPipelineStage, SpamScoringStage>();
builder.Services.AddScoped<IInboundPipelineStage, AiPhishingDetectionStage>();
builder.Services.AddScoped<IInboundPipelineStage, HeaderForgeryAnalysisStage>();
builder.Services.AddScoped<IInboundPipelineStage, ClassificationStage>();
builder.Services.AddScoped<InboundPipelineRunner>();

builder.Services.AddScoped<IOutboundPipelineStage, OutboundAttachmentValidationStage>();
builder.Services.AddScoped<IOutboundPipelineStage, PolicyValidationStage>();
builder.Services.AddScoped<IOutboundPipelineStage, AiRiskScoringStage>();
builder.Services.AddScoped<IOutboundPipelineStage, RateLimitCheckStage>();
builder.Services.AddScoped<IOutboundPipelineStage, AuditCreationStage>();
builder.Services.AddScoped<IOutboundPipelineStage, StalwartSmtpSubmissionStage>();
builder.Services.AddScoped<OutboundPipelineRunner>();

// Health Checks Registration with Distinct Tags
builder.Services.AddHealthChecks()
    .AddCheck("self", () => HealthCheckResult.Healthy("MailService process is alive"), tags: new[] { "live" })
    .AddNpgSql(connectionString, name: "neon-postgres", tags: new[] { "ready", "critical" })
    .AddRedis(redisConnection, name: "redis", tags: new[] { "ready", "critical" })
    .AddCheck<StalwartHealthCheck>("stalwart", tags: new[] { "ready", "critical" })
    .AddCheck<ClamAvHealthCheck>("clamav", tags: new[] { "ready", "critical" })
    .AddCheck<SpamAssassinHealthCheck>("spamassassin", tags: new[] { "general" })
    .AddCheck<AiGovernanceHealthCheck>("ai-governance", tags: new[] { "general" });

var app = builder.Build();

// Map gRPC services (Port 5003 HTTP/2)
app.MapGrpcService<MailManagementService>();
app.MapGrpcService<MailSecurityService>();

// Map Health Endpoints (Port 9090 HTTP/1.1)
// 1. General health overview
app.MapHealthChecks("/health", new HealthCheckOptions
{
    Predicate = _ => true,
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        var response = new
        {
            status = report.Status.ToString(),
            duration = report.TotalDuration.TotalMilliseconds,
            entries = report.Entries.ToDictionary(
                kvp => kvp.Key,
                kvp => new
                {
                    status = kvp.Value.Status.ToString(),
                    description = kvp.Value.Description,
                    duration = kvp.Value.Duration.TotalMilliseconds
                })
        };
        await context.Response.WriteAsync(JsonSerializer.Serialize(response));
    }
});

// 2. Liveness probe: only checks runtime process health
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("live")
});

// 3. Readiness probe: checks critical infrastructure dependencies
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});

app.MapGet("/", () => "Aurora Mail Platform Security Service running.");

app.Run();

public class SimpleClassifier : IEmailClassifier
{
    public Task<MailService.Domain.Enums.EmailCategory> ClassifyAsync(string subject, string body, CancellationToken cancellationToken = default)
    {
        string text = $"{subject} {body}".ToLowerInvariant();
        if (text.Contains("booking")) return Task.FromResult(MailService.Domain.Enums.EmailCategory.BookingRequest);
        if (text.Contains("shipment") || text.Contains("tracking")) return Task.FromResult(MailService.Domain.Enums.EmailCategory.ShipmentUpdate);
        if (text.Contains("quote") || text.Contains("price")) return Task.FromResult(MailService.Domain.Enums.EmailCategory.Quotation);
        if (text.Contains("complaint")) return Task.FromResult(MailService.Domain.Enums.EmailCategory.Complaint);
        return Task.FromResult(MailService.Domain.Enums.EmailCategory.Unknown);
    }
}
