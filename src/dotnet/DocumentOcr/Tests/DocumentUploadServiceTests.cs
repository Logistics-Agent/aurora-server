using DocumentOcr.Application.Providers;
using DocumentOcr.Application.Storage;
using DocumentOcr.Application.Uploads;
using DocumentOcr.Domain.Enums;
using DocumentOcr.Infrastructure.Persistences;
using DocumentOcr.Infrastructure.BackgroundJobs;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
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
    public async Task VerifyRejectsStoredMimeThatDoesNotMatchTheFileExtension()
    {
        await using var context = CreateContext(TenantId);
        var storage = new FakeDocumentInputStorage();
        var service = CreateService(context, TenantId, storage);
        var receipt = await service.CreateAsync(
            new CreateDocumentUploadInput("upload-extension", "invoice.pdf", "image/png", 1_024, null));
        storage.HeadResult = ValidHead(receipt.StorageReference) with { ContentType = "image/png" };

        var exception = await Assert.ThrowsAsync<DocumentUploadValidationException>(() =>
            service.VerifyAsync(receipt.UploadId));

        Assert.Equal("UPLOAD_MIME_MISMATCH", exception.Code);
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
        Assert.NotEmpty(first.WriteUrl);
        Assert.NotEmpty(replay.WriteUrl);
        Assert.NotEqual(first.WriteUrl, replay.WriteUrl);
        Assert.Equal(1, await context.UploadSessions.CountAsync());

        await Assert.ThrowsAsync<UploadSessionConflictException>(() => service.CreateAsync(
            new CreateDocumentUploadInput("upload-001", "packing-list.pdf", "application/pdf", 1_024, null)));
    }

    [Fact]
    public async Task SameBodyReplayOfAnExpiredSessionReturnsTheStableExpiryFailure()
    {
        await using var context = CreateContext(TenantId);
        var storage = new FakeDocumentInputStorage();
        var service = CreateService(context, TenantId, storage, Now);
        var receipt = await service.CreateAsync(Input());
        var session = await context.UploadSessions.SingleAsync();
        session.MarkExpired(Now.AddMinutes(15));
        await context.SaveChangesAsync();

        var expiredService = CreateService(context, TenantId, storage, Now.AddMinutes(16));
        var exception = await Assert.ThrowsAsync<DocumentUploadValidationException>(() =>
            expiredService.CreateAsync(Input()));

        Assert.Equal(receipt.UploadId, session.Id);
        Assert.Equal("UPLOAD_EXPIRED", exception.Code);
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
    public async Task CleanupPersistsDeletePendingBeforeADeleteFailureAndRetriesIt()
    {
        await using var context = CreateContext(TenantId);
        var storage = new FakeDocumentInputStorage { DeleteFailuresRemaining = 1 };
        var service = CreateService(context, TenantId, storage);
        var receipt = await service.CreateAsync(Input());

        await Assert.ThrowsAsync<IOException>(() => ExpiredUploadCleanupService.CleanupExpiredAsync(
            context, storage, Now.AddMinutes(16)));

        var failedSession = await context.UploadSessions.SingleAsync();
        Assert.Equal(DocumentUploadStatus.Expired, failedSession.Status);
        Assert.Equal(DocumentUploadCleanupStatus.DeletePending, failedSession.CleanupStatus);

        var deleted = await ExpiredUploadCleanupService.CleanupExpiredAsync(context, storage, Now.AddMinutes(17));

        Assert.Equal(1, deleted);
        Assert.Equal(DocumentUploadCleanupStatus.Deleted,
            (await context.UploadSessions.SingleAsync(session => session.Id == receipt.UploadId)).CleanupStatus);
        Assert.Single(storage.DeletedKeys);
    }

    [Fact]
    public async Task ConcurrentCleanupMakesAnInProgressExpiredVerificationFailClosed()
    {
        var databaseName = Guid.CreateVersion7().ToString();
        await using var verificationContext = CreateContext(TenantId, databaseName);
        var headStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseHead = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var storage = new FakeDocumentInputStorage
        {
            HeadStarted = headStarted,
            HeadBlocker = releaseHead.Task
        };
        var service = CreateService(verificationContext, TenantId, storage, Now);
        var receipt = await service.CreateAsync(Input());
        storage.HeadResult = ValidHead(receipt.StorageReference);

        var verification = service.VerifyAsync(receipt.UploadId);
        await headStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        await using var cleanupContext = CreateContext(TenantId, databaseName);
        var deleted = await ExpiredUploadCleanupService.CleanupExpiredAsync(
            cleanupContext, storage, Now.AddMinutes(16));
        releaseHead.SetResult();

        var exception = await Assert.ThrowsAsync<DocumentUploadValidationException>(() => verification);
        Assert.Equal("UPLOAD_EXPIRED", exception.Code);
        Assert.Equal(1, deleted);
        Assert.Equal(DocumentUploadStatus.Expired,
            (await cleanupContext.UploadSessions.SingleAsync(session => session.Id == receipt.UploadId)).Status);
    }

    [Fact]
    public async Task VerifyRejectsFilesystemBytesWithMagicAndHashMismatches()
    {
        var root = Path.Combine(Path.GetTempPath(), "document-input-tests", Guid.CreateVersion7().ToString());
        Directory.CreateDirectory(root);
        try
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Storage:InputPath"] = root,
                    ["Storage:InputBridge:PublicBaseUrl"] = "https://ocr.test",
                    ["Storage:InputBridge:SigningKey"] = "test-only-signing-key-with-at-least-32-bytes"
                })
                .Build();
            var storage = new global::DocumentOcr.Infrastructure.Storage.FileSystemDocumentInputStorage(configuration);
            await using var context = CreateContext(TenantId);
            var service = CreateService(context, TenantId, storage);
            var png = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x01 };
            var magicReceipt = await service.CreateAsync(
                new CreateDocumentUploadInput("filesystem-magic", "invoice.pdf", "application/pdf", png.Length, null));
            await storage.WriteAsync(TenantId, magicReceipt.StorageReference, new MemoryStream(png), 10 * 1024 * 1024);

            var magicException = await Assert.ThrowsAsync<DocumentUploadValidationException>(() =>
                service.VerifyAsync(magicReceipt.UploadId));

            var pdf = "%PDF-test"u8.ToArray();
            var hashReceipt = await service.CreateAsync(
                new CreateDocumentUploadInput("filesystem-hash", "packing-list.pdf", "application/pdf", pdf.Length,
                    new string('0', 64)));
            await storage.WriteAsync(TenantId, hashReceipt.StorageReference, new MemoryStream(pdf), 10 * 1024 * 1024);

            var hashException = await Assert.ThrowsAsync<DocumentUploadValidationException>(() =>
                service.VerifyAsync(hashReceipt.UploadId));

            Assert.Equal("UPLOAD_MIME_MISMATCH", magicException.Code);
            Assert.Equal("UPLOAD_HASH_MISMATCH", hashException.Code);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
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
                    ["Storage:InputPath"] = root,
                    ["Storage:InputBridge:PublicBaseUrl"] = "https://ocr.test",
                    ["Storage:InputBridge:SigningKey"] = "test-only-signing-key-with-at-least-32-bytes"
                })
                .Build();
            var storage = new global::DocumentOcr.Infrastructure.Storage.FileSystemDocumentInputStorage(configuration);
            var tenantId = Guid.CreateVersion7();
            var uploadId = Guid.CreateVersion7();
            var key = $"objects/{tenantId}/{uploadId}/invoice.pdf";
            var target = await storage.CreateSignedWriteTargetAsync(
                tenantId, uploadId, key, "invoice.pdf", "application/pdf", 1_024,
                Now.AddMinutes(15), null);
            var path = Path.Combine(root, key.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var pdfBytes = new byte[1_024];
            pdfBytes[0] = 0x25;
            pdfBytes[1] = 0x50;
            pdfBytes[2] = 0x44;
            pdfBytes[3] = 0x46;
            pdfBytes[4] = 0x2D;
            await File.WriteAllBytesAsync(path, pdfBytes);

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

    [Fact]
    public async Task FilesystemAdapterCreatesSignedHttpPutTargetInsteadOfExposingAFilePath()
    {
        var root = Path.Combine(Path.GetTempPath(), "document-input-tests", Guid.CreateVersion7().ToString());
        Directory.CreateDirectory(root);
        try
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Storage:InputPath"] = root,
                    ["Storage:InputBridge:PublicBaseUrl"] = "https://ocr.test",
                    ["Storage:InputBridge:SigningKey"] = "test-only-signing-key-with-at-least-32-bytes"
                })
                .Build();
            var storage = new global::DocumentOcr.Infrastructure.Storage.FileSystemDocumentInputStorage(configuration);
            var tenantId = Guid.CreateVersion7();
            var uploadId = Guid.CreateVersion7();
            var key = $"objects/{tenantId}/{uploadId}/invoice.pdf";

            var target = await storage.CreateSignedWriteTargetAsync(
                tenantId, uploadId, key, "invoice.pdf", "application/pdf", 1_024,
                Now.AddMinutes(15), null);

            var targetUri = new Uri(target.Url);
            Assert.Equal("https", targetUri.Scheme);
            Assert.Equal("ocr.test", targetUri.Host);
            Assert.DoesNotContain(root, target.Url, StringComparison.Ordinal);
            Assert.NotEmpty(targetUri.Query);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task FilesystemAdapterDetectsMimeFromStoredBytesInsteadOfTheFileExtension()
    {
        var root = Path.Combine(Path.GetTempPath(), "document-input-tests", Guid.CreateVersion7().ToString());
        Directory.CreateDirectory(root);
        try
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Storage:InputPath"] = root,
                    ["Storage:InputBridge:PublicBaseUrl"] = "https://ocr.test",
                    ["Storage:InputBridge:SigningKey"] = "test-only-signing-key-with-at-least-32-bytes"
                })
                .Build();
            var storage = new global::DocumentOcr.Infrastructure.Storage.FileSystemDocumentInputStorage(configuration);
            var tenantId = Guid.CreateVersion7();
            var uploadId = Guid.CreateVersion7();
            var key = $"objects/{tenantId}/{uploadId}/invoice.pdf";
            var path = Path.Combine(root, key.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await File.WriteAllBytesAsync(path, [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);

            var metadata = await storage.HeadAsync(tenantId, key);

            Assert.Equal("image/png", metadata!.ContentType);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task S3AdapterInspectsResponseBytesInsteadOfSpoofableObjectMetadata()
    {
        var tenantId = Guid.CreateVersion7();
        var objectKey = $"objects/{tenantId}/{Guid.CreateVersion7()}/invoice.pdf";
        var pngBytes = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x01 };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Storage:S3:Bucket"] = "documents" })
            .Build();
        var storage = new global::DocumentOcr.Infrastructure.Storage.S3DocumentInputStorage(
            new StreamBackedAmazonS3(pngBytes), configuration);

        var metadata = await storage.HeadAsync(tenantId, objectKey);

        Assert.Equal("image/png", metadata!.ContentType);
        Assert.Equal(pngBytes.Length, metadata.SizeBytes);
        Assert.Equal(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(pngBytes)).ToLowerInvariant(),
            metadata.ContentSha256);
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

    private static DocumentOcrDbContext CreateContext(Guid tenantId, string? databaseName = null)
    {
        var currentUser = new CurrentUserService();
        currentUser.Populate(Guid.CreateVersion7(), tenantId, null, null, null, []);
        var options = new DbContextOptionsBuilder<DocumentOcrDbContext>()
            .UseInMemoryDatabase(databaseName ?? Guid.CreateVersion7().ToString())
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
        public int DeleteFailuresRemaining { get; set; }
        public TaskCompletionSource? HeadStarted { get; set; }
        public Task? HeadBlocker { get; set; }
        public List<string> DeletedKeys { get; } = [];

        public Task<SignedWriteTarget> CreateSignedWriteTargetAsync(
            Guid tenantId, Guid uploadId, string objectKey, string fileName,
            string mimeType, long maximumSizeBytes, DateTimeOffset expiresAt,
            string? contentSha256, CancellationToken cancellationToken = default) =>
            Task.FromResult(new SignedWriteTarget(
                $"https://upload.test/{objectKey}?target={Guid.NewGuid():N}",
                new Dictionary<string, string> { ["Content-Type"] = mimeType },
                expiresAt,
                maximumSizeBytes));

        public async Task<DocumentObjectMetadata?> HeadAsync(
            Guid tenantId, string objectKey, CancellationToken cancellationToken = default)
        {
            HeadCalls++;
            HeadStarted?.TrySetResult();
            if (HeadBlocker is not null)
                await HeadBlocker.WaitAsync(cancellationToken);
            return HeadResult;
        }

        public Task WriteAsync(
            Guid tenantId,
            string objectKey,
            Stream content,
            long maximumSizeBytes,
            CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task DeleteAsync(Guid tenantId, string objectKey, CancellationToken cancellationToken = default)
        {
            if (DeleteFailuresRemaining > 0)
            {
                DeleteFailuresRemaining--;
                throw new IOException("Transient delete failure.");
            }
            DeletedKeys.Add(objectKey);
            return Task.CompletedTask;
        }
    }

    private sealed class StreamBackedAmazonS3(byte[] bytes) : AmazonS3Client(
        new AnonymousAWSCredentials(),
        new AmazonS3Config { ServiceURL = "https://s3.test", AuthenticationRegion = "us-east-1" })
    {
        public override Task<GetObjectResponse> GetObjectAsync(
            GetObjectRequest request,
            CancellationToken cancellationToken = default)
        {
            var response = new GetObjectResponse { ResponseStream = new MemoryStream(bytes, writable: false) };
            response.Headers["Content-Type"] = "application/pdf";
            response.Headers["x-amz-meta-content-sha256"] = new string('0', 64);
            return Task.FromResult(response);
        }
    }
}
