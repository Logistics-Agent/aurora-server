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
using Audit.Grpc;
using System.Net.Http.Headers;
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
using MailService.Application.Interfaces.Transport;
using MailService.Application.Options;
using MailService.Application.Pipeline;
using MailService.Application.Pipeline.Stages;
using MailService.Domain.Entities;
using MailService.GrpcServices;
using MailService.Infrastructure.AI;
using MailService.Infrastructure.Cache;
using MailService.Infrastructure.Classification;
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
using MailService.Infrastructure.Transport;

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

// Configure Database and Redis Connection Strings
string connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Host=localhost;Port=5432;Database=aurora_mail_service;Username=postgres;Password=postgres";

string? explicitRedisConn = builder.Configuration.GetConnectionString("Redis")
    ?? builder.Configuration["Redis:ConnectionString"];

var redisHost = builder.Configuration["Redis:Host"] ?? builder.Configuration["Redis__Host"];
var redisPassword = builder.Configuration["Redis:Password"] ?? builder.Configuration["Redis__Password"];
var redisSsl = builder.Configuration.GetValue<bool>("Redis:Ssl", false);
var redisAbort = builder.Configuration.GetValue<bool>("Redis:AbortConnect", false);

if (string.IsNullOrWhiteSpace(explicitRedisConn) && !string.IsNullOrWhiteSpace(redisHost))
{
    explicitRedisConn = $"{redisHost},abortConnect={redisAbort.ToString().ToLower()},ssl={redisSsl.ToString().ToLower()}";
    if (!string.IsNullOrEmpty(redisPassword))
    {
        explicitRedisConn += $",password={redisPassword}";
    }
}
string redisConnection = explicitRedisConn ?? "localhost:6379,abortConnect=false";

// Register and validate production MailServiceOptions
builder.Services.Configure<MailServiceOptions>(options =>
{
    options.DatabaseConnectionString = connectionString;
    options.RedisConnectionString = redisConnection;
    options.RedisHost = redisHost;

    options.RabbitMqHost = builder.Configuration["RabbitMQ:Host"];
    options.RabbitMqPort = int.TryParse(builder.Configuration["RabbitMQ:Port"], out int p) ? p : 5672;
    options.RabbitMqUsername = builder.Configuration["RabbitMQ:Username"];
    options.RabbitMqPassword = builder.Configuration["RabbitMQ:Password"];
    options.RabbitMqVirtualHost = builder.Configuration["RabbitMQ:VirtualHost"] ?? "mail";

    options.StalwartBaseUrl = builder.Configuration["Stalwart:BaseUrl"];
    options.StalwartAdminUrl = builder.Configuration["Stalwart:AdminUrl"];
    options.StalwartAdminApiKey = builder.Configuration["Stalwart:AdminApiKey"] ?? builder.Configuration["Stalwart:AdminToken"];
    options.StalwartWebhookSecret = builder.Configuration["Stalwart:WebhookSecret"];
    options.StalwartSmtpHost = builder.Configuration["Stalwart:SmtpHost"];
    options.StalwartSmtpPort = int.TryParse(builder.Configuration["Stalwart:SmtpPort"], out int sp) ? sp : 25;
    options.StalwartSmtpUser = builder.Configuration["Stalwart:SmtpUser"];
    options.StalwartSmtpPassword = builder.Configuration["Stalwart:SmtpPassword"];

    options.MailTransportProvider = builder.Configuration["MailTransport:Provider"] ?? builder.Configuration["Mail:OutboundProvider"] ?? "Brevo";
    options.BrevoSmtpHost = builder.Configuration["Brevo:SmtpHost"] ?? "smtp-relay.brevo.com";
    options.BrevoSmtpPort = int.TryParse(builder.Configuration["Brevo:SmtpPort"], out int bp) ? bp : 587;
    options.BrevoSmtpUsername = builder.Configuration["Brevo:SmtpUsername"] ?? builder.Configuration["Brevo:SmtpUser"];
    options.BrevoSmtpPassword = builder.Configuration["Brevo:SmtpPassword"] ?? builder.Configuration["Brevo:SmtpKey"];
    options.CloudflareWebhookSecret = builder.Configuration["CloudflareInbound:WebhookSecret"] ?? builder.Configuration["Cloudflare:WebhookSecret"];

    options.ClamAvHost = builder.Configuration["ClamAV:Host"];
    options.ClamAvPort = int.TryParse(builder.Configuration["ClamAV:Port"], out int cp) ? cp : 3310;
    options.ClamAvEnabled = !string.Equals(builder.Configuration["ClamAV:Enabled"], "false", StringComparison.OrdinalIgnoreCase);
    options.ClamAvFailOpen = string.Equals(builder.Configuration["ClamAV:FailOpen"], "true", StringComparison.OrdinalIgnoreCase);

    options.SpamAssassinHost = builder.Configuration["SpamAssassin:Host"];
    options.SpamAssassinPort = int.TryParse(builder.Configuration["SpamAssassin:Port"], out int sap) ? sap : 783;

    options.AiGovernanceEndpoint = builder.Configuration["AiGovernance:GrpcEndpoint"]
        ?? builder.Configuration["AiGovernance:ServiceUrl"];

    options.R2AccountId = builder.Configuration["R2:AccountId"];
    options.R2AccessKey = builder.Configuration["R2:AccessKey"];
    options.R2SecretKey = builder.Configuration["R2:SecretKey"];
    options.R2BucketName = builder.Configuration["R2:BucketName"] ?? "aurora-mail-platform";
});

builder.Services.Configure<BrevoOptions>(builder.Configuration.GetSection(BrevoOptions.SectionName));
builder.Services.Configure<MailTransportOptions>(builder.Configuration.GetSection(MailTransportOptions.SectionName));
builder.Services.Configure<CloudflareInboundOptions>(builder.Configuration.GetSection(CloudflareInboundOptions.SectionName));

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
    x.AddConsumer<SendSystemEmailConsumer>();
    x.AddConsumer<InboundEmailWebhookConsumer>();
});

// Configure MediatR & FluentValidation
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));
builder.Services.AddControllers();
builder.Services.AddMemoryCache();

// Configure EF Core PostgreSQL (Managed Neon connection)
builder.Services.AddDbContext<MailServiceDbContext>(options =>
    options.UseNpgsql(connectionString, npgsql =>
    {
        npgsql.EnableRetryOnFailure(3);
    }));

// Register one scoped identity instance behind both interfaces. AuthInterceptor
// populates ICurrentUserContext and downstream handlers read ICurrentUserService,
// so resolving separate CurrentUserService instances would lose the propagated
// tenant/user identity.
builder.Services.AddScoped<CurrentUserService>();
builder.Services.AddScoped<ICurrentUserService>(sp => sp.GetRequiredService<CurrentUserService>());
builder.Services.AddScoped<ICurrentUserContext>(sp => sp.GetRequiredService<CurrentUserService>());
builder.Services.AddScoped<IEmailDraftRepository, EmailDraftRepository>();
builder.Services.AddScoped<IOutboxWriter, OutboxWriter>();
builder.Services.AddScoped<InboundWebhookEventRepository>();
builder.Services.AddScoped<IMailboxResolver, MailboxResolver>();
builder.Services.AddHostedService<OutboxProcessorBackgroundService>();
builder.Services.AddHostedService<MailboxReconciliationWorker>();

// Register Infrastructure HTTP Clients & S3 / R2
var stalwartBaseUrl = builder.Configuration["Stalwart:BaseUrl"]
    ?? builder.Configuration["Stalwart:AdminUrl"]
    ?? "http://localhost:8080";

builder.Services.AddHttpClient();
builder.Services.AddHttpClient<IStalwartManagementClient, StalwartManagementClient>(client =>
{
    client.BaseAddress = new Uri(stalwartBaseUrl);
    var adminApiKey = builder.Configuration["Stalwart:AdminApiKey"];
    if (!string.IsNullOrWhiteSpace(adminApiKey))
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminApiKey);
});

builder.Services.AddHttpClient<IStalwartJmapClient, StalwartJmapClient>(client =>
{
    client.BaseAddress = new Uri(stalwartBaseUrl);
    var adminApiKey = builder.Configuration["Stalwart:AdminApiKey"];
    if (!string.IsNullOrWhiteSpace(adminApiKey))
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminApiKey);
});

builder.Services.AddSingleton<IAmazonS3>(sp =>
{
    var mailOpts = sp.GetRequiredService<IOptions<MailServiceOptions>>().Value;
    var accessKey = !string.IsNullOrWhiteSpace(mailOpts.R2AccessKey) ? mailOpts.R2AccessKey : (builder.Configuration["R2:AccessKey"] ?? "dev");
    var secretKey = !string.IsNullOrWhiteSpace(mailOpts.R2SecretKey) ? mailOpts.R2SecretKey : (builder.Configuration["R2:SecretKey"] ?? "dev");
    var accountId = !string.IsNullOrWhiteSpace(mailOpts.R2AccountId) ? mailOpts.R2AccountId : (builder.Configuration["R2:AccountId"] ?? "dev");
    var serviceUrl = $"https://{accountId}.r2.cloudflarestorage.com";

    return new AmazonS3Client(
        accessKey,
        secretKey,
        new AmazonS3Config
        {
            ServiceURL = serviceUrl,
            ForcePathStyle = true,
            Timeout = TimeSpan.FromSeconds(5),
            MaxErrorRetry = 1
        });
});

// Register Redis Connection Multiplexer
builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
{
    var mailOpts = sp.GetRequiredService<IOptions<MailServiceOptions>>().Value;
    string connStr = !string.IsNullOrWhiteSpace(mailOpts.RedisConnectionString)
        ? mailOpts.RedisConnectionString
        : "localhost:6379,abortConnect=false";

    var config = ConfigurationOptions.Parse(connStr);
    config.AbortOnConnectFail = false;
    config.ConnectRetry = 3;
    config.ConnectTimeout = 3000;
    return ConnectionMultiplexer.Connect(config);
});

builder.Services.AddScoped<IR2StorageClient, R2StorageClient>();
builder.Services.AddScoped<IRateLimitService, RedisCacheService>();

builder.Services.AddScoped<IClamAvClient>(sp =>
{
    var opts = sp.GetRequiredService<IOptions<MailServiceOptions>>().Value;
    var host = !string.IsNullOrWhiteSpace(opts.ClamAvHost) ? opts.ClamAvHost : "clamav";
    var port = opts.ClamAvPort > 0 ? opts.ClamAvPort : 3310;
    var logger = sp.GetService<ILogger<ClamAvClient>>();
    return new ClamAvClient(host, port, logger);
});
builder.Services.AddScoped<ISpamAssassinClient, SpamAssassinClient>();
builder.Services.AddScoped<IDnsLookupService, DnsLookupService>();
builder.Services.AddScoped<SpfEvaluator>();
builder.Services.AddScoped<DkimVerifier>();
builder.Services.AddScoped<DmarcEvaluator>();

// Register AI Governance Services & Fallback Client
builder.Services.Configure<AiGovernanceOptions>(builder.Configuration.GetSection(AiGovernanceOptions.SectionName));
var aiGovernanceUrl = builder.Configuration["AiGovernance:GrpcEndpoint"]
    ?? builder.Configuration["AiGovernance:ServiceUrl"]
    ?? builder.Configuration["Grpc:AiGovernance:Url"]
    ?? "http://localhost:5005";

builder.Services.AddGrpcClient<AiGovernanceService.AiGovernanceServiceClient>(o =>
{
    o.Address = new Uri(aiGovernanceUrl);
});
builder.Services.AddGrpcClient<AiExecutionService.AiExecutionServiceClient>(o =>
{
    o.Address = new Uri(aiGovernanceUrl);
});

var auditServiceUrl = builder.Configuration["Grpc:AuditService:Url"] ?? "http://audit-service:9086";
builder.Services.AddGrpcClient<AuditLogService.AuditLogServiceClient>(o => o.Address = new Uri(auditServiceUrl));

builder.Services.AddScoped<IAiGovernanceClient, AiGovernanceGrpcClient>();
builder.Services.AddScoped<IPhishingDetectionService, GovernedPhishingDetectionService>();
builder.Services.AddScoped<IRiskScoringService, GovernedRiskScoringService>();

builder.Services.AddScoped<IEmailClassifier, SimpleClassifier>();

// Register Outbound Mail Transports (Brevo is primary, Stalwart is legacy/fallback)
builder.Services.AddScoped<BrevoMailTransport>();
builder.Services.AddScoped<StalwartMailTransport>();
builder.Services.AddScoped<IMailTransport, BrevoMailTransport>();
builder.Services.AddScoped<IMailTransport, StalwartMailTransport>();
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
    .AddCheck<StalwartHealthCheck>("stalwart", tags: new[] { "ready" })
    .AddCheck<ClamAvHealthCheck>("clamav", tags: new[] { "general" })
    .AddCheck<SpamAssassinHealthCheck>("spamassassin", tags: new[] { "general" })
    .AddCheck<AiGovernanceHealthCheck>("ai-governance", tags: new[] { "general" });

var app = builder.Build();

// Map gRPC services (Port 5003 HTTP/2)
app.MapGrpcService<MailManagementService>();
app.MapGrpcService<MailSecurityService>();

// Map REST Controllers (Port 9090 HTTP/1.1)
app.MapControllers();

// Map Health Endpoints (Port 9090 HTTP/1.1)
// 1. General health overview (full diagnostics)
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

// 4. Default Kubernetes liveness alias
app.MapHealthChecks("/healthz", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("live")
});

app.MapGet("/", () => "Aurora Mail Platform Security Service running.");

app.Run();

