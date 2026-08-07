using Microsoft.EntityFrameworkCore;
using Amazon.S3;
using Serilog;
using Shared.Interceptors;
using Shared.Security;
using MailService.Application.Interfaces;
using MailService.Application.Pipeline;
using MailService.Application.Pipeline.Stages;
using MailService.Domain.Entities;
using MailService.GrpcServices;
using MailService.Infrastructure.AI;
using MailService.Infrastructure.Cache;
using MailService.Infrastructure.Persistence;
using MailService.Infrastructure.Persistence.Repositories;
using MailService.Infrastructure.Security;
using MailService.Infrastructure.Storage;
using MailService.Infrastructure.Stalwart;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog Structured Logging
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore", Serilog.Events.LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("service", "email-security")
    .WriteTo.Console()
    .CreateLogger();

builder.Host.UseSerilog();

// Add gRPC services with AuthInterceptor
builder.Services.AddGrpc(options =>
{
    options.Interceptors.Add<AuthInterceptor>();
});

// Configure MediatR & FluentValidation
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));

// Configure EF Core PostgreSQL
string connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Host=localhost;Port=5434;Database=aurora_mail_service;Username=postgres;Password=postgres";

builder.Services.AddDbContext<MailServiceDbContext>(options =>
    options.UseNpgsql(connectionString));

// Register Scoped Identity & Repositories
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
builder.Services.AddScoped<IEmailDraftRepository, EmailDraftRepository>();

// Register Infrastructure Services
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

builder.Services.AddScoped<IR2StorageClient, R2StorageClient>();
builder.Services.AddScoped<IRateLimitService, RedisCacheService>();
builder.Services.AddScoped<IClamAvClient, ClamAvClient>();
builder.Services.AddScoped<ISpamAssassinClient, SpamAssassinClient>();
builder.Services.AddScoped<IDnsLookupService, DnsLookupService>();
builder.Services.AddScoped<SpfEvaluator>();
builder.Services.AddScoped<DkimVerifier>();
builder.Services.AddScoped<DmarcEvaluator>();

// Register AI Services & Resilience Clients
builder.Services.AddScoped<IAiGovernanceClient, AiGovernanceGrpcClient>();
builder.Services.AddScoped<IPhishingDetectionService, SemanticKernelPhishingService>();
builder.Services.AddScoped<SemanticKernelRiskScoringService>();

builder.Services.AddScoped<IEmailClassifier, SimpleClassifier>();

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

// Health Checks
builder.Services.AddHealthChecks()
    .AddNpgsql(connectionString, name: "postgresql", tags: new[] { "critical" });

var app = builder.Build();

// Map gRPC services
app.MapGrpcService<MailManagementService>();
app.MapGrpcService<MailSecurityService>();

// Map Health Endpoints
app.MapHealthChecks("/health");
app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions { Predicate = hc => hc.Tags.Contains("critical") });

app.MapGet("/", () => "Mail Platform Security Service running.");

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
