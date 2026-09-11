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
var usesFileSystemInputStorage = inputStorageProvider.Equals("FileSystem", StringComparison.OrdinalIgnoreCase);
if (usesFileSystemInputStorage)
{
    var bridgeOptions = DocumentUploadBridgeOptions.FromConfiguration(builder.Configuration);
    bridgeOptions.GetPublicBaseUri();
    bridgeOptions.GetSigningKey();
    builder.Services.AddSingleton(bridgeOptions);
    builder.Services.AddSingleton<DocumentUploadBridgeTokenService>();
    builder.Services.AddScoped<IDocumentInputStorage, FileSystemDocumentInputStorage>();
    builder.Services.AddScoped<DocumentUploadHttpBridge>();
}
else if (inputStorageProvider.Equals("S3", StringComparison.OrdinalIgnoreCase))
{
    var serviceUrl = RequiredStorageSetting(builder.Configuration, "Storage:S3:ServiceUrl");
    if (!Uri.TryCreate(serviceUrl, UriKind.Absolute, out _))
        throw new InvalidOperationException("Storage:S3:ServiceUrl must be an absolute URL.");
    var s3Config = new AmazonS3Config
    {
        ServiceURL = serviceUrl,
        ForcePathStyle = builder.Configuration.GetValue("Storage:S3:ForcePathStyle", true),
        AuthenticationRegion = RequiredStorageSetting(builder.Configuration, "Storage:S3:Region")
    };
    builder.Services.AddSingleton<IAmazonS3>(_ => new AmazonS3Client(
        RequiredStorageSetting(builder.Configuration, "Storage:S3:AccessKey"),
        RequiredStorageSetting(builder.Configuration, "Storage:S3:SecretKey"),
        s3Config));
    builder.Services.AddScoped<IDocumentInputStorage, S3DocumentInputStorage>();
}
else
{
    throw new InvalidOperationException("Storage:InputProvider must be either 'FileSystem' or 'S3'.");
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
if (usesFileSystemInputStorage)
{
    app.MapPut("/api/internal/document-uploads/{tenantId:guid}/{uploadId:guid}",
        (Guid tenantId, Guid uploadId, string? token, HttpRequest request,
            DocumentUploadHttpBridge bridge, CancellationToken cancellationToken) =>
            bridge.PutAsync(tenantId, uploadId, token, request, cancellationToken));
}
app.MapGet("/", () => "Document OCR gRPC Service");
app.MapGet("/healthz", () => Results.Ok("Healthy"));

app.Run();

static string RequiredStorageSetting(IConfiguration configuration, string key) =>
    configuration[key] is { Length: > 0 } value
        ? value
        : throw new InvalidOperationException($"{key} is required for S3 input storage.");
