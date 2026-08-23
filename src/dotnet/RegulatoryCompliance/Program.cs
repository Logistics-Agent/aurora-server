using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using RegulatoryCompliance.Application.Embeddings;
using RegulatoryCompliance.Application.Evaluations;
using RegulatoryCompliance.Application.Ingestion;
using RegulatoryCompliance.Application.Retrieval;
using RegulatoryCompliance.GrpcServices;
using RegulatoryCompliance.Infrastructure.BackgroundJobs;
using RegulatoryCompliance.Infrastructure.Persistences;
using RegulatoryCompliance.Infrastructure.Providers;
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
        npgsql =>
        {
            npgsql.UseVector();
            npgsql.MigrationsAssembly("RegulatoryCompliance");
        }));
builder.Services.AddSharedMassTransit(builder.Configuration);
builder.Services.AddSingleton(TimeProvider.System);

var aiGovernanceUrl = builder.Configuration["Grpc:AiGovernance:Url"] ?? "http://localhost:9090";
builder.Services.AddGrpcClient<AiGovernance.Grpc.AiExecutionService.AiExecutionServiceClient>(o =>
{
    o.Address = new Uri(aiGovernanceUrl);
});

var runtimeOptions = builder.Configuration
    .GetSection(RegulatoryComplianceRuntimeOptions.SectionName)
    .Get<RegulatoryComplianceRuntimeOptions>() ?? new RegulatoryComplianceRuntimeOptions();
runtimeOptions.Validate();
builder.Services.AddSingleton(runtimeOptions);

builder.Services.AddScoped<IRegulatoryChunker, DeterministicRegulatoryChunker>();
builder.Services.AddScoped<IRegulatoryIngestionService, RegulatoryIngestionService>();
builder.Services.AddScoped<IKnowledgeIngestionService, KnowledgeIngestionService>();

if (runtimeOptions.EmbeddingProvider.Equals("AiGovernance", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddScoped<IEmbeddingProvider, AiGovernanceEmbeddingProvider>();
    builder.Services.AddScoped<IRegulationVectorStore, PgVectorRegulationVectorStore>();
    builder.Services.AddScoped<IKnowledgeVectorStore, PgVectorKnowledgeVectorStore>();
}
else
{
    builder.Services.AddSingleton<IEmbeddingProvider, DeterministicEmbeddingProvider>();
    builder.Services.AddScoped<IRegulationVectorStore, EfRegulationVectorStore>();
    builder.Services.AddScoped<IKnowledgeVectorStore, PgVectorKnowledgeVectorStore>();
}

builder.Services.AddScoped<IEmbeddingBatchProcessor, EmbeddingBatchProcessor>();
builder.Services.AddScoped<IRegulationRetrievalService, RegulationRetrievalService>();
builder.Services.AddScoped<IComplianceEvaluationService, ComplianceEvaluationService>();
builder.Services.AddScoped<RegulatoryCompliance.Application.Assistant.IGroundedAnswerPromptBuilder, RegulatoryCompliance.Application.Assistant.GroundedAnswerPromptBuilder>();
builder.Services.AddScoped<RegulatoryCompliance.Application.Assistant.IDeterministicCitationValidator, RegulatoryCompliance.Application.Assistant.DeterministicCitationValidator>();
builder.Services.AddScoped<RegulatoryCompliance.Application.Assistant.IGroundedAnswerService, RegulatoryCompliance.Application.Assistant.GroundedAnswerService>();
builder.Services.AddHostedService<ComplianceEmbeddingBackgroundService>();

builder.Services.AddScoped<IComplianceOutboxBatchStore, ComplianceOutboxBatchStore>();
builder.Services.AddScoped<IComplianceIntegrationEventPublisher, ComplianceIntegrationEventPublisher>();
builder.Services.AddScoped<ComplianceOutboxProcessor>();
builder.Services.AddHostedService<ComplianceOutboxBackgroundService>();
builder.Services.AddScoped<RegulatoryCompliance.Application.Events.DocumentOcrIntegrationConsumer>();

builder.Services.AddHealthChecks()
    .AddCheck<RegulatoryComplianceDbHealthCheck>("regulatory-compliance-db");

var app = builder.Build();

app.MapGrpcService<RegulatoryComplianceGrpcService>();
app.MapHealthChecks("/health", new HealthCheckOptions());
app.MapGet("/", () => "Regulatory Compliance RAG gRPC Service");

app.Run();
