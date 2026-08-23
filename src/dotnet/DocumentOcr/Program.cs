using DocumentOcr.Application.Jobs;
using DocumentOcr.Application.Providers;
using DocumentOcr.GrpcServices;
using DocumentOcr.Infrastructure.BackgroundJobs;
using DocumentOcr.Infrastructure.Persistences;
using DocumentOcr.Infrastructure.Providers;
using Microsoft.EntityFrameworkCore;
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

builder.Services.AddDbContext<DocumentOcrDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        npgsql => npgsql.MigrationsAssembly("DocumentOcr")));

builder.Services.AddSharedMassTransit(builder.Configuration);
builder.Services.AddSingleton(TimeProvider.System);

var processingOptions = builder.Configuration
    .GetSection(DocumentProcessingOptions.SectionName)
    .Get<DocumentProcessingOptions>() ?? new DocumentProcessingOptions();
processingOptions.Validate();
builder.Services.AddSingleton(processingOptions);

var workerOptions = builder.Configuration
    .GetSection(DocumentOcrWorkerOptions.SectionName)
    .Get<DocumentOcrWorkerOptions>() ?? new DocumentOcrWorkerOptions();
workerOptions.Validate();
builder.Services.AddSingleton(workerOptions);

var outboxOptions = builder.Configuration
    .GetSection(DocumentOcrOutboxPublisherOptions.SectionName)
    .Get<DocumentOcrOutboxPublisherOptions>() ?? new DocumentOcrOutboxPublisherOptions();
outboxOptions.Validate();
builder.Services.AddSingleton(outboxOptions);

var aiGovernanceUrl = builder.Configuration["Grpc:AiGovernance:Url"] ?? "http://localhost:9090";
builder.Services.AddGrpcClient<AiGovernance.Grpc.AiExecutionService.AiExecutionServiceClient>(o =>
{
    o.Address = new Uri(aiGovernanceUrl);
});

builder.Services.AddScoped<DocumentOcr.Application.Storage.IArtifactStorageService, DocumentOcr.Infrastructure.Storage.FileSystemArtifactStorageService>();
builder.Services.AddScoped<DocumentInputPolicy>();
builder.Services.AddScoped<IDocumentContentReader, DeterministicDocumentContentReader>();
builder.Services.AddScoped<IOcrProvider>(services =>
    processingOptions.Provider.Equals("AiGovernance", StringComparison.OrdinalIgnoreCase)
        ? ActivatorUtilities.CreateInstance<AiGovernanceOcrProvider>(services)
        : ActivatorUtilities.CreateInstance<DeterministicOcrProvider>(services));

builder.Services.AddScoped<DocumentOcrJobService>();
builder.Services.AddScoped<IDocumentOcrJobService>(services =>
    services.GetRequiredService<DocumentOcrJobService>());
builder.Services.AddScoped<IDocumentOcrJobProcessor>(services =>
    services.GetRequiredService<DocumentOcrJobService>());
builder.Services.AddScoped<IDocumentOcrJobBatchStore, DocumentOcrJobBatchStore>();
builder.Services.AddHostedService<DocumentOcrJobBackgroundService>();

builder.Services.AddScoped<IDocumentOcrOutboxBatchStore, DocumentOcrOutboxBatchStore>();
builder.Services.AddScoped<IDocumentOcrIntegrationEventPublisher, DocumentOcrIntegrationEventPublisher>();
builder.Services.AddScoped<DocumentOcrOutboxProcessor>();
builder.Services.AddHostedService<DocumentOcrOutboxPublisherBackgroundService>();

var app = builder.Build();

app.MapGrpcService<DocumentOcrGrpcService>();
app.MapGet("/", () => "Document OCR gRPC Service");

app.Run();
