using DocumentOcr.Application.Providers;
using DocumentOcr.Application.Jobs;
using DocumentOcr.Application.Storage;
using DocumentOcr.Application.Uploads;
using DocumentOcr.Domain.Entities;
using DocumentOcr.GrpcServices;
using DocumentOcr.Infrastructure.Persistences;
using Grpc.Core;
using Microsoft.EntityFrameworkCore;
using Shared.Security;
using Shared.Interceptors;
using OcrGrpc = DocumentOcr.Grpc;

namespace DocumentOcr.Tests.Grpc;

public sealed class DocumentUploadConsumeGrpcTests
{
    [Fact]
    public async Task Consume_upload_session_is_tenant_scoped_and_idempotent()
    {
        var tenantId = Guid.CreateVersion7();
        var currentUser = new CurrentUserService();
        currentUser.Populate(Guid.CreateVersion7(), tenantId, null, null, null, []);
        await using var context = new DocumentOcrDbContext(
            new DbContextOptionsBuilder<DocumentOcrDbContext>()
                .UseInMemoryDatabase(Guid.CreateVersion7().ToString())
                .Options,
            currentUser,
            new AuditSaveChangesInterceptor(currentUser));
        var storage = new FakeDocumentInputStorage
        {
            HeadResult = new DocumentObjectMetadata("objects/tenant/upload.pdf", "application/pdf", 1_024, "hash")
        };
        var uploads = new DocumentUploadService(
            context,
            currentUser,
            TimeProvider.System,
            new DocumentInputPolicy(new DocumentProcessingOptions()),
            storage,
            new DocumentUploadOptions());
        var receipt = await uploads.CreateAsync(new CreateDocumentUploadInput(
            "consume-key", "upload.pdf", "application/pdf", 1_024, null));
        storage.HeadResult = storage.HeadResult with { ObjectKey = receipt.StorageReference };
        await uploads.VerifyAsync(receipt.UploadId);
        var service = new DocumentOcrGrpcService(new NullJobService(), currentUser, uploads);

        var first = await service.ConsumeUploadSession(
            new OcrGrpc.ConsumeUploadSessionRequest { UploadId = receipt.UploadId.ToString() },
            TestServerCallContext.Create());
        var replay = await service.ConsumeUploadSession(
            new OcrGrpc.ConsumeUploadSessionRequest { UploadId = receipt.UploadId.ToString() },
            TestServerCallContext.Create());

        Assert.Equal(OcrGrpc.DocumentUploadStatus.Consumed, first.Status);
        Assert.Equal(first.UploadId, replay.UploadId);
        Assert.Equal(OcrGrpc.DocumentUploadStatus.Consumed, replay.Status);
    }

    [Fact]
    public async Task Consume_unverified_upload_returns_stable_trailer()
    {
        var tenantId = Guid.CreateVersion7();
        var currentUser = new CurrentUserService();
        currentUser.Populate(Guid.CreateVersion7(), tenantId, null, null, null, []);
        await using var context = new DocumentOcrDbContext(
            new DbContextOptionsBuilder<DocumentOcrDbContext>()
                .UseInMemoryDatabase(Guid.CreateVersion7().ToString())
                .Options,
            currentUser,
            new AuditSaveChangesInterceptor(currentUser));
        var uploads = new DocumentUploadService(
            context,
            currentUser,
            TimeProvider.System,
            new DocumentInputPolicy(new DocumentProcessingOptions()),
            new FakeDocumentInputStorage(),
            new DocumentUploadOptions());
        var receipt = await uploads.CreateAsync(new CreateDocumentUploadInput(
            "pending-consume", "upload.pdf", "application/pdf", 1_024, null));
        var service = new DocumentOcrGrpcService(new NullJobService(), currentUser, uploads);

        var exception = await Assert.ThrowsAsync<RpcException>(() => service.ConsumeUploadSession(
            new OcrGrpc.ConsumeUploadSessionRequest { UploadId = receipt.UploadId.ToString() },
            TestServerCallContext.Create()));

        Assert.Equal(StatusCode.FailedPrecondition, exception.StatusCode);
        Assert.Equal("UPLOAD_NOT_VERIFIED", exception.Trailers.GetValue("document-upload-validation-code"));
    }
}

internal sealed class FakeDocumentInputStorage : IDocumentInputStorage
{
    public DocumentObjectMetadata? HeadResult { get; set; }

    public Task<SignedWriteTarget> CreateSignedWriteTargetAsync(
        Guid tenantId,
        Guid uploadId,
        string objectKey,
        string fileName,
        string mimeType,
        long maximumSizeBytes,
        DateTimeOffset expiresAt,
        string? contentSha256,
        CancellationToken cancellationToken = default) => Task.FromResult(new SignedWriteTarget(
        $"https://upload.test/{objectKey}",
        new Dictionary<string, string> { ["Content-Type"] = mimeType },
        expiresAt,
        maximumSizeBytes));

    public Task<DocumentObjectMetadata?> HeadAsync(
        Guid tenantId,
        string objectKey,
        CancellationToken cancellationToken = default) => Task.FromResult(HeadResult);

    public Task WriteAsync(
        Guid tenantId,
        string objectKey,
        Stream content,
        long maximumSizeBytes,
        CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task DeleteAsync(
        Guid tenantId,
        string objectKey,
        CancellationToken cancellationToken = default) => Task.CompletedTask;
}

internal sealed class NullJobService : IDocumentOcrJobService
{
    public Task<DocumentOcrJob> SubmitAsync(SubmitDocumentJobInput input, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<DocumentOcrJob> SubmitOcrAsync(SubmitOcrJobInput input, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<DocumentOcrJob> GetAsync(Guid jobId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<DocumentOcrJobPage> ListAsync(ListDocumentJobsInput input, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<DocumentOcrJob> CancelAsync(Guid jobId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<DocumentOcrJob> RetryAsync(Guid jobId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<DocumentOcrJob> ReviewAsync(Guid jobId, string action, string? correctedJson, string? comment, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<DocumentOcrJob?> ProcessAsync(Guid tenantId, Guid jobId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
}
