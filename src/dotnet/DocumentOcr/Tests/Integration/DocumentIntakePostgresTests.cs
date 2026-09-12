using DocumentOcr.Application.Intake;
using DocumentOcr.Application.Providers;
using DocumentOcr.Application.Storage;
using DocumentOcr.Application.Uploads;
using DocumentOcr.Contracts.Events;
using DocumentOcr.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Shared.Interceptors;
using Shared.Security;

namespace DocumentOcr.Tests.Integration;

[Collection(DocumentOcrPostgresCollection.Name)]
public sealed class DocumentIntakePostgresTests(DocumentOcrPostgresFixture database)
{
    [Fact]
    public async Task MigrationPersistsAtomicIntakeAndReplaysItAcrossContexts()
    {
        await database.ResetAsync();
        var tenantId = Guid.CreateVersion7();
        var uploadId = await CreateUploadAsync(tenantId);

        Guid firstJobId;
        await using (var context = database.CreateContext(CurrentUser(tenantId)))
        {
            var service = CreateIntake(context, tenantId);
            var job = await service.CreateAsync(Input(uploadId));
            firstJobId = job.Id;

            Assert.Equal(DocumentUploadStatus.Consumed,
                (await context.UploadSessions.SingleAsync()).Status);
            Assert.Equal(uploadId, (await context.Jobs.SingleAsync()).UploadId);
            Assert.Empty(await context.Database.GetPendingMigrationsAsync());
        }

        await using (var context = database.CreateContext(CurrentUser(tenantId)))
        {
            var replay = await CreateIntake(context, tenantId).CreateAsync(Input(uploadId));

            Assert.Equal(firstJobId, replay.Id);
            Assert.Equal(1, await context.Jobs.CountAsync());
        }
    }

    private async Task<Guid> CreateUploadAsync(Guid tenantId)
    {
        var currentUser = CurrentUser(tenantId);
        await using var context = database.CreateContext(currentUser);
        var upload = await CreateUploadService(context, currentUser).CreateAsync(
            new CreateDocumentUploadInput(
                "upload-postgres-intake",
                "invoice.pdf",
                "application/pdf",
                1_024,
                null));
        return upload.UploadId;
    }

    private DocumentIntakeService CreateIntake(
        DocumentOcr.Infrastructure.Persistences.DocumentOcrDbContext context,
        Guid tenantId)
    {
        var currentUser = CurrentUser(tenantId);
        return new DocumentIntakeService(
            context,
            CreateUploadService(context, currentUser),
            currentUser,
            TimeProvider.System);
    }

    private static DocumentUploadService CreateUploadService(
        DocumentOcr.Infrastructure.Persistences.DocumentOcrDbContext context,
        CurrentUserService currentUser) =>
        new(
            context,
            currentUser,
            TimeProvider.System,
            new DocumentInputPolicy(new DocumentProcessingOptions()),
            new UploadedObjectStorage(),
            new DocumentUploadOptions());

    private static CreateDocumentIntakeInput Input(Guid uploadId) =>
        new(
            uploadId,
            "intake-postgres-001",
            OcrDocumentType.CommercialInvoice,
            DocumentOcrPurpose.GeneralDocument,
            "opaque-reference");

    private static CurrentUserService CurrentUser(Guid tenantId)
    {
        var currentUser = new CurrentUserService();
        currentUser.Populate(Guid.CreateVersion7(), tenantId, null, null, null, []);
        return currentUser;
    }

    private sealed class UploadedObjectStorage : IDocumentInputStorage
    {
        public Task<SignedWriteTarget> CreateSignedWriteTargetAsync(
            Guid tenantId,
            Guid uploadId,
            string objectKey,
            string fileName,
            string mimeType,
            long maximumSizeBytes,
            DateTimeOffset expiresAt,
            string? contentSha256,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new SignedWriteTarget(
                $"https://upload.test/{objectKey}",
                new Dictionary<string, string> { ["Content-Type"] = mimeType },
                expiresAt,
                maximumSizeBytes));

        public Task<DocumentObjectMetadata?> HeadAsync(
            Guid tenantId,
            string objectKey,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<DocumentObjectMetadata?>(
                new DocumentObjectMetadata(objectKey, "application/pdf", 1_024, null));

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
}
