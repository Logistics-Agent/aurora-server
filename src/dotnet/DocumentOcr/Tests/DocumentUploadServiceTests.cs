using DocumentOcr.Application.Providers;
using DocumentOcr.Application.Storage;
using DocumentOcr.Application.Uploads;
using DocumentOcr.Domain.Enums;
using DocumentOcr.Infrastructure.Persistences;
using DocumentOcr.Infrastructure.BackgroundJobs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Shared.Interceptors;
using Shared.Security;

namespace DocumentOcr.Tests;

public sealed class DocumentUploadServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 11, 7, 0, 0, TimeSpan.Zero);
    private static readonly Guid TenantId = Guid.CreateVersion7();

    [Fact]
    public async Task CreateSessionUsesExactlyFifteenMinuteExpiryAndTenantScopedObjectKey()
    {
        await using var context = CreateContext(TenantId);
        var storage = new FakeDocumentInputStorage();
        var service = CreateService(context, TenantId, storage);

        var receipt = await service.CreateAsync(new CreateDocumentUploadInput(
            "upload-001", "invoice.pdf", "application/pdf", 1_024, null));

        Assert.Equal(Now.AddMinutes(15), receipt.ExpiresAt);
        Assert.Equal($"objects/{TenantId}/{receipt.UploadId}/invoice.pdf", receipt.StorageReference);
        Assert.Equal(DocumentUploadStatus.Pending, receipt.Status);
        Assert.Equal(10 * 1024 * 1024, receipt.MaximumSizeBytes);
    }

    [Fact]
    public async Task VerifyRejectsObjectWithAnotherTenantPrefix()
    {
        await using var context = CreateContext(TenantId);
        var storage = new FakeDocumentInputStorage();
        var service = CreateService(context, TenantId, storage);
        var receipt = await service.CreateAsync(Input());
        var session = await context.UploadSessions.SingleAsync();
        session.ObjectKey = $"objects/{Guid.CreateVersion7()}/{session.Id}/invoice.pdf";
        storage.HeadResult = ValidHead(session.ObjectKey);

        var exception = await Assert.ThrowsAsync<DocumentUploadValidationException>(() =>
            service.VerifyAsync(receipt.UploadId));

        Assert.Equal("UPLOAD_TENANT_MISMATCH", exception.Code);
        Assert.Equal(0, storage.HeadCalls);
    }

    [Fact]
    public async Task VerifyRejectsSizeMismatchBeforeMarkingUploaded()
    {
        await using var context = CreateContext(TenantId);
        var storage = new FakeDocumentInputStorage
        {
            HeadResult = new DocumentObjectMetadata(
                "objects/placeholder", "application/pdf", 2_048, null)
        };
        var service = CreateService(context, TenantId, storage);
        var receipt = await service.CreateAsync(Input());
        storage.HeadResult = ValidHead(receipt.StorageReference) with { SizeBytes = 2_048 };

        var exception = await Assert.ThrowsAsync<DocumentUploadValidationException>(() =>
            service.VerifyAsync(receipt.UploadId));

        Assert.Equal("UPLOAD_SIZE_MISMATCH", exception.Code);
        Assert.Equal(DocumentUploadStatus.Pending, (await context.UploadSessions.SingleAsync()).Status);
    }

    [Fact]
    public async Task VerifyRejectsHashMismatchBeforeMarkingUploaded()
    {
        await using var context = CreateContext(TenantId);
        var contentHash = new string('a', 64);
        var storage = new FakeDocumentInputStorage();
        var service = CreateService(context, TenantId, storage);
        var receipt = await service.CreateAsync(
            new CreateDocumentUploadInput("upload-hash", "invoice.pdf", "application/pdf", 1_024, contentHash));
        storage.HeadResult = ValidHead(receipt.StorageReference) with { ContentSha256 = new string('b', 64) };

        var exception = await Assert.ThrowsAsync<DocumentUploadValidationException>(() =>
            service.VerifyAsync(receipt.UploadId));

        Assert.Equal("UPLOAD_HASH_MISMATCH", exception.Code);
        Assert.Equal(DocumentUploadStatus.Pending, (await context.UploadSessions.SingleAsync()).Status);
    }

    [Fact]
    public async Task VerifyExpiresSessionAtTheFifteenMinuteBoundary()
    {
        await using var context = CreateContext(TenantId);
        var storage = new FakeDocumentInputStorage();
        var service = CreateService(context, TenantId, storage, Now);
        var receipt = await service.CreateAsync(Input());
        var expiredService = CreateService(context, TenantId, storage, Now.AddMinutes(15));

        var exception = await Assert.ThrowsAsync<DocumentUploadValidationException>(() =>
            expiredService.VerifyAsync(receipt.UploadId));

        Assert.Equal("UPLOAD_EXPIRED", exception.Code);
        Assert.Equal(DocumentUploadStatus.Expired, (await context.UploadSessions.SingleAsync()).Status);
    }

    [Fact]
    public async Task SameIdempotencyKeyAndBodyReplaysOriginalSessionButDifferentBodyConflicts()
    {
        await using var context = CreateContext(TenantId);
        var service = CreateService(context, TenantId, new FakeDocumentInputStorage());

        var first = await service.CreateAsync(Input());
        var replay = await service.CreateAsync(Input());

        Assert.Equal(first.UploadId, replay.UploadId);
        Assert.Equal(first.WriteUrl, replay.WriteUrl);
        Assert.Equal(1, await context.UploadSessions.CountAsync());

        await Assert.ThrowsAsync<UploadSessionConflictException>(() => service.CreateAsync(
            new CreateDocumentUploadInput("upload-001", "packing-list.pdf", "application/pdf", 1_024, null)));
    }

    [Fact]
    public async Task CrossTenantSessionIsNotEnumerable()
    {
        await using var context = CreateContext(TenantId);
        var service = CreateService(context, TenantId, new FakeDocumentInputStorage());
        var receipt = await service.CreateAsync(Input());

        var otherTenant = Guid.CreateVersion7();
        var otherService = CreateService(context, otherTenant, new FakeDocumentInputStorage());

        await Assert.ThrowsAsync<Shared.Exceptions.NotFoundException>(() =>
            otherService.GetAsync(receipt.UploadId));
    }

    [Fact]
    public async Task CleanupExpiresOnlyUnconsumedSessionsAndIsRetrySafe()
    {
        await using var context = CreateContext(TenantId);
        var storage = new FakeDocumentInputStorage();
        var service = CreateService(context, TenantId, storage);
        var pending = await service.CreateAsync(Input());
        var uploaded = await service.CreateAsync(
            new CreateDocumentUploadInput("upload-002", "packing-list.pdf", "application/pdf", 1_024, null));
        var consumed = await service.CreateAsync(
            new CreateDocumentUploadInput("upload-003", "bl.pdf", "application/pdf", 1_024, null));
        storage.HeadResult = ValidHead(uploaded.StorageReference);
        await service.VerifyAsync(uploaded.UploadId);
        storage.HeadResult = ValidHead(consumed.StorageReference);
        await service.VerifyAsync(consumed.UploadId);
        await service.ConsumeAsync(consumed.UploadId);

        var firstRun = await ExpiredUploadCleanupService.CleanupExpiredAsync(
            context, storage, Now.AddMinutes(16));
        var secondRun = await ExpiredUploadCleanupService.CleanupExpiredAsync(
            context, storage, Now.AddMinutes(16));

        Assert.Equal(2, firstRun);
        Assert.Equal(0, secondRun);
        Assert.Equal(DocumentUploadStatus.Expired,
            (await context.UploadSessions.SingleAsync(x => x.Id == pending.UploadId)).Status);
        Assert.Equal(DocumentUploadStatus.Expired,
            (await context.UploadSessions.SingleAsync(x => x.Id == uploaded.UploadId)).Status);
        Assert.Equal(DocumentUploadStatus.Consumed,
            (await context.UploadSessions.SingleAsync(x => x.Id == consumed.UploadId)).Status);
        Assert.Equal(2, storage.DeletedKeys.Count);
    }

    [Fact]
    public async Task FilesystemAdapterReportsTenantSafeObjectMetadataAndDeletesObject()
    {
        var root = Path.Combine(Path.GetTempPath(), "document-input-tests", Guid.CreateVersion7().ToString());
        Directory.CreateDirectory(root);
        try
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Storage:InputPath"] = root
                })
                .Build();
            var storage = new global::DocumentOcr.Infrastructure.Storage.FileSystemDocumentInputStorage(configuration);
            var tenantId = Guid.CreateVersion7();
            var uploadId = Guid.CreateVersion7();
            var key = $"objects/{tenantId}/{uploadId}/invoice.pdf";
            var target = await storage.CreateSignedWriteTargetAsync(
                tenantId, uploadId, key, "invoice.pdf", "application/pdf", 1_024,
                Now.AddMinutes(15), null);
            var path = new Uri(target.Url).LocalPath;
            await File.WriteAllBytesAsync(path, new byte[1_024]);

            var head = await storage.HeadAsync(tenantId, key);

            Assert.Equal(key, head!.ObjectKey);
            Assert.Equal("application/pdf", head.ContentType);
            Assert.Equal(1_024, head.SizeBytes);
            await storage.DeleteAsync(tenantId, key);
            Assert.Null(await storage.HeadAsync(tenantId, key));
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    private static CreateDocumentUploadInput Input() =>
        new("upload-001", "invoice.pdf", "application/pdf", 1_024, null);

    private static DocumentObjectMetadata ValidHead(string key) =>
        new(key, "application/pdf", 1_024, null);

    private static DocumentUploadService CreateService(
        DocumentOcrDbContext context,
        Guid tenantId,
        IDocumentInputStorage storage,
        DateTimeOffset? now = null)
    {
        var currentUser = new CurrentUserService();
        currentUser.Populate(Guid.CreateVersion7(), tenantId, null, null, null, []);
        return new DocumentUploadService(
            context,
            currentUser,
            new FixedTimeProvider(now ?? Now),
            new DocumentInputPolicy(new DocumentProcessingOptions()),
            storage,
            new DocumentUploadOptions());
    }

    private static DocumentOcrDbContext CreateContext(Guid tenantId)
    {
        var currentUser = new CurrentUserService();
        currentUser.Populate(Guid.CreateVersion7(), tenantId, null, null, null, []);
        var options = new DbContextOptionsBuilder<DocumentOcrDbContext>()
            .UseInMemoryDatabase(Guid.CreateVersion7().ToString())
            .Options;
        return new DocumentOcrDbContext(options, currentUser, new AuditSaveChangesInterceptor(currentUser));
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class FakeDocumentInputStorage : IDocumentInputStorage
    {
        public DocumentObjectMetadata? HeadResult { get; set; }
        public int HeadCalls { get; private set; }
        public List<string> DeletedKeys { get; } = [];

        public Task<SignedWriteTarget> CreateSignedWriteTargetAsync(
            Guid tenantId, Guid uploadId, string objectKey, string fileName,
            string mimeType, long maximumSizeBytes, DateTimeOffset expiresAt,
            string? contentSha256, CancellationToken cancellationToken = default) =>
            Task.FromResult(new SignedWriteTarget(
                $"https://upload.test/{objectKey}",
                new Dictionary<string, string> { ["Content-Type"] = mimeType },
                expiresAt,
                maximumSizeBytes));

        public Task<DocumentObjectMetadata?> HeadAsync(
            Guid tenantId, string objectKey, CancellationToken cancellationToken = default)
        {
            HeadCalls++;
            return Task.FromResult(HeadResult);
        }

        public Task DeleteAsync(Guid tenantId, string objectKey, CancellationToken cancellationToken = default)
        {
            DeletedKeys.Add(objectKey);
            return Task.CompletedTask;
        }
    }
}
