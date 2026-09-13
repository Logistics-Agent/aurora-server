using DocumentOcr.Application.Intake;
using DocumentOcr.Application.Providers;
using DocumentOcr.Application.Storage;
using DocumentOcr.Application.Uploads;
using DocumentOcr.Contracts.Events;
using DocumentOcr.Domain.Enums;
using DocumentOcr.Infrastructure.Persistences;
using Microsoft.EntityFrameworkCore;
using Shared.Exceptions;
using Shared.Interceptors;
using Shared.Security;

namespace DocumentOcr.Tests.Application;

public sealed class DocumentIntakeServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 12, 5, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task VerifiesAPendingUploadBeforeCreatingAndConsumingAJob()
    {
        var fixture = await CreateFixtureAsync();

        var job = await fixture.Intake.CreateAsync(Input(fixture.Receipt.UploadId, "intake-001"));

        var session = await fixture.Context.UploadSessions.SingleAsync();
        Assert.Equal(DocumentUploadStatus.Consumed, session.Status);
        Assert.Equal(fixture.Receipt.UploadId, job.UploadId);
    }

    [Fact]
    public async Task DoesNotConsumeOrCreateAJobWhenVerificationFails()
    {
        var fixture = await CreateFixtureAsync(objectExists: false);

        await Assert.ThrowsAsync<DocumentUploadValidationException>(() => fixture.Intake.CreateAsync(
            Input(fixture.Receipt.UploadId, "intake-001")));

        var session = await fixture.Context.UploadSessions.SingleAsync();
        Assert.Equal(DocumentUploadStatus.Pending, session.Status);
        Assert.Empty(await fixture.Context.Jobs.ToListAsync());
    }

    [Fact]
    public async Task CreatesAndConsumesTheVerifiedUploadInOneDocumentOcrOperation()
    {
        var fixture = await CreateFixtureAsync(verify: true);

        var job = await fixture.Intake.CreateAsync(Input(fixture.Receipt.UploadId, "intake-001"));

        var session = await fixture.Context.UploadSessions.SingleAsync();
        Assert.Equal(fixture.Receipt.UploadId, job.UploadId);
        Assert.Equal(DocumentUploadStatus.Consumed, session.Status);
        Assert.Equal(job.Id, (await fixture.Context.Jobs.SingleAsync()).Id);
    }

    [Fact]
    public async Task ReplaysTheSameJobForTheSameIntakeRequest()
    {
        var fixture = await CreateFixtureAsync(verify: true);
        var input = Input(fixture.Receipt.UploadId, "intake-001");

        var first = await fixture.Intake.CreateAsync(input);
        var replay = await fixture.Intake.CreateAsync(input);

        Assert.Equal(first.Id, replay.Id);
        Assert.Single(await fixture.Context.Jobs.ToListAsync());
    }

    [Fact]
    public async Task CorpusPurposesRequestBothStructuredAndFullTextExtraction()
    {
        var fixture = await CreateFixtureAsync(verify: true);

        var job = await fixture.Intake.CreateAsync(Input(
            fixture.Receipt.UploadId, "corpus-intake-001", DocumentOcrPurpose.KnowledgeCorpus));

        Assert.Equal(OcrExtractionMode.Both, job.ExtractionMode);
    }

    [Fact]
    public async Task RejectsADifferentRequestThatReusesAnIdempotencyKey()
    {
        var fixture = await CreateFixtureAsync(verify: true);
        await fixture.Intake.CreateAsync(Input(fixture.Receipt.UploadId, "intake-001"));

        await Assert.ThrowsAsync<ConflictException>(() => fixture.Intake.CreateAsync(
            Input(fixture.Receipt.UploadId, "intake-001") with
            {
                DocumentTypeHint = OcrDocumentType.PackingList
            }));
    }

    private static CreateDocumentIntakeInput Input(
        Guid uploadId,
        string idempotencyKey,
        DocumentOcrPurpose purpose = DocumentOcrPurpose.ShipmentDocument) =>
        new(
            uploadId,
            idempotencyKey,
            OcrDocumentType.CommercialInvoice,
            purpose,
            "external-reference-001");

    private static async Task<Fixture> CreateFixtureAsync(bool verify = false, bool objectExists = true)
    {
        var tenantId = Guid.CreateVersion7();
        var currentUser = new CurrentUserService();
        currentUser.Populate(Guid.CreateVersion7(), tenantId, null, null, null, []);
        var context = new DocumentOcrDbContext(
            new DbContextOptionsBuilder<DocumentOcrDbContext>()
                .UseInMemoryDatabase(Guid.CreateVersion7().ToString())
                .Options,
            currentUser,
            new AuditSaveChangesInterceptor(currentUser));
        var storage = new FakeDocumentInputStorage();
        var policy = new DocumentInputPolicy(new DocumentProcessingOptions());
        var uploads = new DocumentUploadService(
            context,
            currentUser,
            new FixedTimeProvider(Now),
            policy,
            storage,
            new DocumentUploadOptions());
        var receipt = await uploads.CreateAsync(
            new CreateDocumentUploadInput("upload-001", "invoice.pdf", "application/pdf", 1_024, null));
        storage.HeadResult = objectExists
            ? new DocumentObjectMetadata(receipt.StorageReference, "application/pdf", 1_024, null)
            : null;
        if (verify)
            await uploads.VerifyAsync(receipt.UploadId);

        return new Fixture(
            context,
            receipt,
            new DocumentIntakeService(context, uploads, currentUser, new FixedTimeProvider(Now)));
    }

    private sealed record Fixture(
        DocumentOcrDbContext Context,
        DocumentUploadReceipt Receipt,
        DocumentIntakeService Intake);

    private sealed class FixedTimeProvider(DateTimeOffset value) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => value;
    }

    private sealed class FakeDocumentInputStorage : IDocumentInputStorage
    {
        public DocumentObjectMetadata? HeadResult { get; set; }

        public Task<SignedWriteTarget> CreateSignedWriteTargetAsync(
            Guid tenantId, Guid uploadId, string objectKey, string fileName, string mimeType,
            long maximumSizeBytes, DateTimeOffset expiresAt, string? contentSha256,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new SignedWriteTarget(
                $"https://upload.test/{objectKey}",
                new Dictionary<string, string> { ["Content-Type"] = mimeType },
                expiresAt,
                maximumSizeBytes));

        public Task<DocumentObjectMetadata?> HeadAsync(
            Guid tenantId, string objectKey, CancellationToken cancellationToken = default) =>
            Task.FromResult(HeadResult);

        public Task WriteAsync(
            Guid tenantId, string objectKey, Stream content, long maximumSizeBytes,
            CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task DeleteAsync(
            Guid tenantId, string objectKey, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
