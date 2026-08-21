using System.Text.Json;
using DocumentOcr.Application.Jobs;
using DocumentOcr.Contracts.Events;
using DocumentOcr.Domain.Entities;
using DocumentOcr.Domain.Enums;
using DocumentOcr.Infrastructure.BackgroundJobs;
using DocumentOcr.Infrastructure.Persistences;
using DocumentOcr.Infrastructure.Providers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Shared.Interceptors;
using Shared.Security;

namespace DocumentOcr.Tests.Infrastructure;

public sealed class DocumentOcrBackgroundProcessingTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 22, 5, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task MultipleClaimersDoNotClaimTheSameJob()
    {
        var databaseName = $"ocr-claim-{Guid.CreateVersion7()}";
        var tenantId = Guid.CreateVersion7();
        await using (var seed = CreateContext(databaseName))
        {
            seed.Jobs.Add(CreateJob(tenantId, "claim-1"));
            await seed.SaveChangesAsync();
        }

        await using var firstContext = CreateContext(databaseName);
        await using var secondContext = CreateContext(databaseName);
        var firstStore = CreateStore(firstContext, new MutableTimeProvider(Now));
        var secondStore = CreateStore(secondContext, new MutableTimeProvider(Now));

        var first = await firstStore.ClaimPendingAsync();
        var second = await secondStore.ClaimPendingAsync();

        Assert.Single(first);
        Assert.Empty(second);
        Assert.Equal(first[0].JobId, await firstContext.Jobs.IgnoreQueryFilters().Select(job => job.Id).SingleAsync());
    }

    [Fact]
    public async Task ExpiredLeaseSchedulesTransientRetry()
    {
        var databaseName = $"ocr-recovery-{Guid.CreateVersion7()}";
        var time = new MutableTimeProvider(Now);
        await using var context = CreateContext(databaseName);
        context.Jobs.Add(CreateJob(Guid.CreateVersion7(), "recover-1"));
        await context.SaveChangesAsync();
        var store = CreateStore(context, time);
        await store.ClaimPendingAsync();
        time.Advance(TimeSpan.FromMinutes(3));

        var recovered = await store.RecoverExpiredAsync();

        Assert.Equal(1, recovered);
        var job = await context.Jobs.IgnoreQueryFilters().Include(item => item.Attempts).SingleAsync();
        Assert.Equal(DocumentOcrJobStatus.Queued, job.Status);
        Assert.Equal(OcrAttemptOutcome.TransientFailure, Assert.Single(job.Attempts).Outcome);
        Assert.True(job.NextAttemptAt > time.GetUtcNow());
        Assert.Empty(await context.OutboxMessages.IgnoreQueryFilters().ToListAsync());
    }

    [Fact]
    public async Task HeartbeatRenewsActiveLease()
    {
        var databaseName = $"ocr-heartbeat-{Guid.CreateVersion7()}";
        var time = new MutableTimeProvider(Now);
        await using var context = CreateContext(databaseName);
        context.Jobs.Add(CreateJob(Guid.CreateVersion7(), "heartbeat-1"));
        await context.SaveChangesAsync();
        var store = CreateStore(context, time);
        var claim = Assert.Single(await store.ClaimPendingAsync());
        var originalExpiry = (await context.Jobs.IgnoreQueryFilters().SingleAsync()).LeaseExpiresAt;
        time.Advance(TimeSpan.FromSeconds(30));

        var renewed = await store.RenewLeaseAsync(claim.TenantId, claim.JobId);

        var job = await context.Jobs.IgnoreQueryFilters().SingleAsync();
        Assert.True(renewed);
        Assert.True(job.LeaseExpiresAt > originalExpiry);
        Assert.Equal(time.GetUtcNow(), job.HeartbeatAt);
    }

    [Fact]
    public async Task ExpiredFinalLeaseWritesFailureEvent()
    {
        var databaseName = $"ocr-terminal-{Guid.CreateVersion7()}";
        var time = new MutableTimeProvider(Now);
        var options = new DocumentOcrWorkerOptions { MaxAttempts = 1 };
        await using var context = CreateContext(databaseName);
        context.Jobs.Add(CreateJob(Guid.CreateVersion7(), "terminal-1"));
        await context.SaveChangesAsync();
        var store = CreateStore(context, time, options);
        await store.ClaimPendingAsync();
        time.Advance(TimeSpan.FromMinutes(3));

        await store.RecoverExpiredAsync();

        var job = await context.Jobs.IgnoreQueryFilters().SingleAsync();
        Assert.Equal(DocumentOcrJobStatus.Failed, job.Status);
        var message = await context.OutboxMessages.IgnoreQueryFilters().SingleAsync();
        Assert.Equal(nameof(DocumentOcrFailedEvent), message.EventType);
    }

    [Fact]
    public void RetryDelayIsDeterministicAndBounded()
    {
        var options = new DocumentOcrWorkerOptions
        {
            BaseRetryDelay = TimeSpan.FromSeconds(10),
            MaxRetryDelay = TimeSpan.FromSeconds(30),
            MaxRetryJitter = TimeSpan.FromSeconds(5)
        };
        var jobId = Guid.Parse("0190f000-0000-7000-8000-000000000001");

        var first = DocumentOcrRetryPolicy.GetDelay(jobId, 1, options);
        var replay = DocumentOcrRetryPolicy.GetDelay(jobId, 1, options);
        var capped = DocumentOcrRetryPolicy.GetDelay(jobId, 10, options);

        Assert.Equal(first, replay);
        Assert.InRange(first, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(15));
        Assert.Equal(TimeSpan.FromSeconds(30), capped);
    }

    [Fact]
    public async Task OutboxProcessorPublishesAllowlistedEventWithOriginalEventId()
    {
        var integrationEvent = new DocumentOcrCompletedEvent
        {
            TenantId = Guid.CreateVersion7(),
            JobId = Guid.CreateVersion7(),
            ExternalDocumentId = Guid.CreateVersion7(),
            DetectedDocumentType = "CommercialInvoice",
            NormalizedJson = "{}",
            Confidence = 0.99m,
            NeedsReview = false,
            OccurredAt = Now
        };
        var message = OutboxMessage.Create(
            integrationEvent.TenantId,
            integrationEvent.EventId,
            nameof(DocumentOcrCompletedEvent),
            JsonSerializer.Serialize(integrationEvent),
            Now);
        var batch = new FakeOutboxBatch(message);
        var publisher = new CapturingPublisher();
        var processor = new DocumentOcrOutboxProcessor(
            new FakeOutboxStore(batch),
            publisher,
            new MutableTimeProvider(Now),
            new DocumentOcrOutboxPublisherOptions(),
            NullLogger<DocumentOcrOutboxProcessor>.Instance);

        await processor.ProcessBatchAsync();

        var published = Assert.IsType<DocumentOcrCompletedEvent>(Assert.Single(publisher.Messages));
        Assert.Equal(integrationEvent.EventId, published.EventId);
        Assert.Equal(Now, message.ProcessedAt);
        Assert.True(batch.Committed);
    }

    [Fact]
    public async Task OutboxProcessorRejectsNonAllowlistedEventAndRecordsFailure()
    {
        var message = OutboxMessage.Create(
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            "UnexpectedEvent",
            "{}",
            Now);
        var batch = new FakeOutboxBatch(message);
        var processor = new DocumentOcrOutboxProcessor(
            new FakeOutboxStore(batch),
            new CapturingPublisher(),
            new MutableTimeProvider(Now),
            new DocumentOcrOutboxPublisherOptions(),
            NullLogger<DocumentOcrOutboxProcessor>.Instance);

        await processor.ProcessBatchAsync();

        Assert.Equal(1, message.RetryCount);
        Assert.Null(message.ProcessedAt);
        Assert.Contains("Unsupported Document OCR outbox event type", message.Error);
        Assert.True(batch.Committed);
    }

    private static DocumentOcrJobBatchStore CreateStore(
        DocumentOcrDbContext context,
        TimeProvider timeProvider,
        DocumentOcrWorkerOptions? options = null) => new(
            context,
            new DeterministicOcrProvider(),
            timeProvider,
            options ?? new DocumentOcrWorkerOptions());

    private static DocumentOcrJob CreateJob(Guid tenantId, string key) => DocumentOcrJob.Create(
        tenantId,
        key,
        "objects/tenant/invoice.pdf",
        "invoice.pdf",
        "application/pdf",
        1_024,
        OcrDocumentType.CommercialInvoice,
        Guid.CreateVersion7(),
        null,
        Now);

    private static DocumentOcrDbContext CreateContext(string databaseName)
    {
        var currentUser = new CurrentUserService();
        var options = new DbContextOptionsBuilder<DocumentOcrDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;
        return new DocumentOcrDbContext(
            options,
            currentUser,
            new AuditSaveChangesInterceptor(currentUser));
    }

    private sealed class MutableTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
        public void Advance(TimeSpan duration) => now = now.Add(duration);
    }

    private sealed class FakeOutboxStore(IDocumentOcrOutboxBatch batch) : IDocumentOcrOutboxBatchStore
    {
        public Task<IDocumentOcrOutboxBatch> LockPendingBatchAsync(
            int batchSize,
            int maxRetries,
            CancellationToken cancellationToken) => Task.FromResult(batch);
    }

    private sealed class FakeOutboxBatch(params OutboxMessage[] messages) : IDocumentOcrOutboxBatch
    {
        public IReadOnlyList<OutboxMessage> Messages { get; } = messages;
        public bool Committed { get; private set; }

        public Task CommitAsync(CancellationToken cancellationToken)
        {
            Committed = true;
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class CapturingPublisher : IDocumentOcrIntegrationEventPublisher
    {
        public List<object> Messages { get; } = [];

        public Task PublishAsync(object message, CancellationToken cancellationToken)
        {
            Messages.Add(message);
            return Task.CompletedTask;
        }
    }
}
