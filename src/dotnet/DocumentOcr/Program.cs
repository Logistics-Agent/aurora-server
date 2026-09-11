using Amazon.S3;
using DocumentOcr.Application.Jobs;
using DocumentOcr.Application.Providers;
using DocumentOcr.Application.Storage;
using DocumentOcr.Application.Uploads;
using DocumentOcr.GrpcServices;
using DocumentOcr.Infrastructure.BackgroundJobs;
using DocumentOcr.Infrastructure.Persistences;
using DocumentOcr.Infrastructure.Providers;
using DocumentOcr.Infrastructure.Storage;
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

var uploadOptions = builder.Configuration
    .GetSection(DocumentUploadOptions.SectionName)
    .Get<DocumentUploadOptions>() ?? new DocumentUploadOptions();
uploadOptions.Validate();
builder.Services.AddSingleton(uploadOptions);

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
var inputStorageProvider = builder.Configuration["Storage:InputProvider"] ?? "FileSystem";
if (inputStorageProvider.Equals("S3", StringComparison.OrdinalIgnoreCase))
{
    var s3Config = new AmazonS3Config
    {
        ServiceURL = builder.Configuration["Storage:S3:ServiceUrl"],
        ForcePathStyle = builder.Configuration.GetValue("Storage:S3:ForcePathStyle", true),
        AuthenticationRegion = builder.Configuration["Storage:S3:Region"] ?? "us-east-1"
    };
    builder.Services.AddSingleton<IAmazonS3>(_ => new AmazonS3Client(
        builder.Configuration["Storage:S3:AccessKey"],
        builder.Configuration["Storage:S3:SecretKey"],
        s3Config));
    builder.Services.AddScoped<IDocumentInputStorage, S3DocumentInputStorage>();
}
else
{
    builder.Services.AddScoped<IDocumentInputStorage, FileSystemDocumentInputStorage>();
}
builder.Services.AddScoped<DocumentInputPolicy>();
builder.Services.AddScoped<DocumentUploadService>();
builder.Services.AddHostedService<ExpiredUploadCleanupService>();
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
app.MapGet("/healthz", () => Results.Ok("Healthy"));

app.Run();
