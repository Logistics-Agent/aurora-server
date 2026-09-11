using DocumentOcr.Application.Providers;
using DocumentOcr.Application.Storage;
using DocumentOcr.Application.Uploads;
using DocumentOcr.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Shared.Security;

namespace DocumentOcr.Tests.Integration;

[Collection(DocumentOcrPostgresCollection.Name)]
public sealed class DocumentUploadPostgresTests(DocumentOcrPostgresFixture database)
{
    [Fact]
    public async Task MigrationPersistsUploadSessionAndEnforcesTenantScopedIdempotency()
    {
        await database.ResetAsync();
        var tenantId = Guid.CreateVersion7();
        var currentUser = CurrentUser(tenantId);
        await using var context = database.CreateContext(currentUser);
        var service = new DocumentUploadService(
            context,
            currentUser,
            TimeProvider.System,
            new DocumentInputPolicy(new DocumentProcessingOptions()),
            new FakeStorage(),
            new DocumentUploadOptions());

        var first = await service.CreateAsync(new CreateDocumentUploadInput(
            "postgres-upload", "invoice.pdf", "application/pdf", 1_024, null));
        var replay = await service.CreateAsync(new CreateDocumentUploadInput(
            "postgres-upload", "invoice.pdf", "application/pdf", 1_024, null));

        Assert.Equal(first.UploadId, replay.UploadId);
        Assert.Equal(1, await context.UploadSessions.CountAsync());
        Assert.Empty(await context.Database.GetPendingMigrationsAsync());
    }

    private static CurrentUserService CurrentUser(Guid tenantId)
    {
        var currentUser = new CurrentUserService();
        currentUser.Populate(Guid.CreateVersion7(), tenantId, null, null, null, []);
        return currentUser;
    }

    private sealed class FakeStorage : IDocumentInputStorage
    {
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
            Guid tenantId, string objectKey, CancellationToken cancellationToken = default) =>
            Task.FromResult<DocumentObjectMetadata?>(null);

        public Task DeleteAsync(Guid tenantId, string objectKey, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
