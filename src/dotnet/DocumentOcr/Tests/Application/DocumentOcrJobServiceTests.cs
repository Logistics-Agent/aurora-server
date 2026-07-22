using DocumentOcr.Application.Jobs;
using DocumentOcr.Application.Providers;
using DocumentOcr.Domain.Enums;
using DocumentOcr.Infrastructure.Persistences;
using DocumentOcr.Infrastructure.Providers;
using Microsoft.EntityFrameworkCore;
using Shared.Exceptions;
using Shared.Interceptors;
using Shared.Security;

namespace DocumentOcr.Tests.Application;

public sealed class DocumentOcrJobServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 22, 4, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task SubmitIsIdempotentAndUsesAuthenticatedTenant()
    {
        var tenantId = Guid.CreateVersion7();
        var currentUser = CreateCurrentUser(tenantId);
        await using var context = CreateContext(currentUser);
        var service = CreateService(context, currentUser);

        var first = await service.SubmitAsync(CreateInput("request-001"));
        var replay = await service.SubmitAsync(CreateInput("request-001"));

        Assert.Equal(first.Id, replay.Id);
        Assert.Equal(tenantId, first.TenantId);
        Assert.Equal(1, await context.Jobs.CountAsync());
    }

    [Fact]
    public async Task MissingTenantContextRejectsSubmission()
    {
        var currentUser = new CurrentUserService();
        await using var context = CreateContext(currentUser);
        var service = CreateService(context, currentUser);

        await Assert.ThrowsAsync<DomainException>(() => service.SubmitAsync(CreateInput("request-001")));
    }

    [Fact]
    public async Task ProcessCompletesJobAndWritesOutboxAtomically()
    {
        var tenantId = Guid.CreateVersion7();
        var currentUser = CreateCurrentUser(tenantId);
        await using var context = CreateContext(currentUser);
        var service = CreateService(context, currentUser);
        var job = await service.SubmitAsync(CreateInput("request-001"));

        var processed = await service.ProcessAsync(tenantId, job.Id);

        Assert.NotNull(processed);
        Assert.Equal(DocumentOcrJobStatus.Completed, processed!.Status);
        Assert.Equal(0.98m, processed.Confidence);
        Assert.False(processed.NeedsReview);
        Assert.Contains("\"schemaVersion\":1", processed.NormalizedJson);
        Assert.Contains("\"value\":\"DOC-001\"", processed.NormalizedJson);
        var outbox = Assert.Single(await context.OutboxMessages.ToListAsync());
        Assert.Equal("DocumentOcrCompletedEvent", outbox.EventType);
        Assert.Contains(job.Id.ToString(), outbox.Content);
    }

    [Fact]
    public async Task RequiredFieldRuleMarksOtherwiseConfidentResultForReview()
    {
        var tenantId = Guid.CreateVersion7();
        var currentUser = CreateCurrentUser(tenantId);
        await using var context = CreateContext(currentUser);
        var service = CreateService(context, currentUser, new MissingRequiredFieldProvider());
        var job = await service.SubmitAsync(CreateInput("request-001"));

        var processed = await service.ProcessAsync(tenantId, job.Id);

        Assert.True(processed!.NeedsReview);
        Assert.Equal(0.99m, processed.Confidence);
    }

    [Fact]
    public async Task ProviderFailureIsClassifiedWithoutCompletionOutbox()
    {
        var tenantId = Guid.CreateVersion7();
        var currentUser = CreateCurrentUser(tenantId);
        await using var context = CreateContext(currentUser);
        var service = CreateService(
            context,
            currentUser,
            new DeterministicOcrProvider(OcrProviderFailureKind.Transient));
        var job = await service.SubmitAsync(CreateInput("request-001"));

        var processed = await service.ProcessAsync(tenantId, job.Id);

        Assert.Equal(DocumentOcrJobStatus.Failed, processed!.Status);
        Assert.Equal(OcrAttemptOutcome.TransientFailure, Assert.Single(processed.Attempts).Outcome);
        Assert.Empty(await context.OutboxMessages.ToListAsync());
    }

    [Fact]
    public async Task GetAndListDoNotLeakAcrossTenants()
    {
        var databaseName = $"ocr-pipeline-{Guid.CreateVersion7()}";
        var tenantA = Guid.CreateVersion7();
        var currentUserA = CreateCurrentUser(tenantA);
        Guid jobId;
        await using (var contextA = CreateContext(currentUserA, databaseName))
        {
            var serviceA = CreateService(contextA, currentUserA);
            jobId = (await serviceA.SubmitAsync(CreateInput("request-001"))).Id;
        }

        var currentUserB = CreateCurrentUser(Guid.CreateVersion7());
        await using var contextB = CreateContext(currentUserB, databaseName);
        var serviceB = CreateService(contextB, currentUserB);

        await Assert.ThrowsAsync<NotFoundException>(() => serviceB.GetAsync(jobId));
        var page = await serviceB.ListAsync(new ListDocumentJobsInput(1, 20, null, null, null));
        Assert.Empty(page.Items);
    }

    private static DocumentOcrJobService CreateService(
        DocumentOcrDbContext context,
        CurrentUserService currentUser,
        IOcrProvider? provider = null)
    {
        var options = new DocumentProcessingOptions();
        var policy = new DocumentInputPolicy(options);
        return new DocumentOcrJobService(
            context,
            currentUser,
            new FixedTimeProvider(Now),
            options,
            policy,
            new DeterministicDocumentContentReader(policy),
            provider ?? new DeterministicOcrProvider());
    }

    private static SubmitDocumentJobInput CreateInput(string idempotencyKey) => new(
        idempotencyKey,
        "objects/tenant/invoice.pdf",
        "invoice.pdf",
        "application/pdf",
        1_024,
        OcrDocumentType.CommercialInvoice,
        Guid.CreateVersion7(),
        Guid.CreateVersion7());

    private static CurrentUserService CreateCurrentUser(Guid tenantId)
    {
        var currentUser = new CurrentUserService();
        currentUser.Populate(Guid.CreateVersion7(), tenantId, null, null, [], []);
        return currentUser;
    }

    private static DocumentOcrDbContext CreateContext(
        CurrentUserService currentUser,
        string? databaseName = null)
    {
        var options = new DbContextOptionsBuilder<DocumentOcrDbContext>()
            .UseInMemoryDatabase(databaseName ?? Guid.CreateVersion7().ToString())
            .Options;
        return new DocumentOcrDbContext(
            options, currentUser, new AuditSaveChangesInterceptor(currentUser));
    }

    private sealed class MissingRequiredFieldProvider : IOcrProvider
    {
        public string Name => "missing-required-field";

        public Task<OcrProviderResult> ExtractAsync(
            OcrProviderRequest request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(OcrProviderResult.Create(
                OcrDocumentType.CommercialInvoice,
                [OcrExtractedField.Create("documentNumber", "INV-1", 0.99m)],
                "request-1",
                null,
                null,
                null));
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset value) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => value;
    }
}
