using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using RegulatoryCompliance.Application.Embeddings;
using RegulatoryCompliance.Application.Evaluations;
using RegulatoryCompliance.Application.Ingestion;
using RegulatoryCompliance.Application.Retrieval;
using RegulatoryCompliance.GrpcServices;
using RegulatoryCompliance.Infrastructure.BackgroundJobs;
using RegulatoryCompliance.Infrastructure.Persistences;
using Shared.Extensions;
using Shared.Interceptors;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddGrpc(options =>
{
    options.Interceptors.Add<AuthInterceptor>();
    options.Interceptors.Add<ExceptionInterceptor>();
});
builder.Services.AddSharedServices(builder.Configuration);
builder.Services.AddTransient<ExceptionInterceptor>();
builder.Services.AddDbContext<RegulatoryComplianceDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        npgsql => npgsql.MigrationsAssembly("RegulatoryCompliance")));
builder.Services.AddSharedMassTransit(builder.Configuration);
builder.Services.AddSingleton(TimeProvider.System);

var runtimeOptions = builder.Configuration
    .GetSection(RegulatoryComplianceRuntimeOptions.SectionName)
    .Get<RegulatoryComplianceRuntimeOptions>() ?? new RegulatoryComplianceRuntimeOptions();
runtimeOptions.Validate();
builder.Services.AddSingleton(runtimeOptions);

builder.Services.AddScoped<IRegulatoryChunker, DeterministicRegulatoryChunker>();
builder.Services.AddScoped<IRegulatoryIngestionService, RegulatoryIngestionService>();
builder.Services.AddSingleton<IEmbeddingProvider, DeterministicEmbeddingProvider>();
builder.Services.AddScoped<IRegulationVectorStore, EfRegulationVectorStore>();
builder.Services.AddScoped<IEmbeddingBatchProcessor, EmbeddingBatchProcessor>();
builder.Services.AddScoped<IRegulationRetrievalService, RegulationRetrievalService>();
builder.Services.AddScoped<IComplianceEvaluationService, ComplianceEvaluationService>();
builder.Services.AddHostedService<ComplianceEmbeddingBackgroundService>();

builder.Services.AddScoped<IComplianceOutboxBatchStore, ComplianceOutboxBatchStore>();
builder.Services.AddScoped<IComplianceIntegrationEventPublisher, ComplianceIntegrationEventPublisher>();
builder.Services.AddScoped<ComplianceOutboxProcessor>();
builder.Services.AddHostedService<ComplianceOutboxBackgroundService>();

builder.Services.AddHealthChecks()
    .AddCheck<RegulatoryComplianceDbHealthCheck>("regulatory-compliance-db");

var app = builder.Build();

app.MapGrpcService<RegulatoryComplianceGrpcService>();
app.MapHealthChecks("/health", new HealthCheckOptions());
app.MapGet("/", () => "Regulatory Compliance RAG gRPC Service");

app.Run();
