using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using RegulatoryCompliance.Contracts.Events;
using RegulatoryCompliance.Domain.Entities;
using RegulatoryCompliance.Infrastructure.BackgroundJobs;

namespace RegulatoryCompliance.Tests.Infrastructure;

public sealed class ComplianceRuntimeTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 23, 16, 0, 0, TimeSpan.Zero);

    [Fact]
    public void RuntimeOptionsValidateProviderRetrievalAndWorkerBounds()
    {
        new RegulatoryComplianceRuntimeOptions().Validate();

        Assert.Throws<InvalidOperationException>(() =>
            new RegulatoryComplianceRuntimeOptions { EmbeddingDimension = 63 }.Validate());
        Assert.Throws<InvalidOperationException>(() =>
            new RegulatoryComplianceRuntimeOptions { EmbeddingBatchSize = 0 }.Validate());
        Assert.Throws<InvalidOperationException>(() =>
            new RegulatoryComplianceRuntimeOptions { RetrievalMaximumTopK = 21 }.Validate());
        Assert.Throws<InvalidOperationException>(() =>
            new RegulatoryComplianceRuntimeOptions { RetrievalMinimumScore = 1.1m }.Validate());
        Assert.Throws<InvalidOperationException>(() =>
            new RegulatoryComplianceRuntimeOptions { OutboxMaxRetries = 0 }.Validate());
    }

    [Fact]
    public void EventRegistryDeserializesOnlyApprovedContracts()
    {
        var completed = new ComplianceEvaluationCompletedEvent
        {
            TenantId = Guid.CreateVersion7(),
            EvaluationId = Guid.CreateVersion7(),
            ExternalShipmentId = Guid.CreateVersion7(),
            OccurredAt = Now
        };

        var deserialized = ComplianceIntegrationEventRegistry.Deserialize(
            typeof(ComplianceEvaluationCompletedEvent).FullName!,
            JsonSerializer.Serialize(completed));

        Assert.IsType<ComplianceEvaluationCompletedEvent>(deserialized);
        Assert.Throws<InvalidOperationException>(() =>
            ComplianceIntegrationEventRegistry.Deserialize("Unapproved.Event", "{}"));
    }

    [Fact]
    public async Task OutboxProcessorRecordsBoundedPublishFailureAndCommitsRetry()
    {
        var message = OutboxMessage.Create(
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            typeof(ComplianceEvaluationCompletedEvent).FullName!,
            JsonSerializer.Serialize(new ComplianceEvaluationCompletedEvent()),
            Now);
        var batch = new FakeBatch([message]);
        var processor = new ComplianceOutboxProcessor(
            new FakeBatchStore(batch),
            new FailingPublisher(),
            new FixedTimeProvider(Now),
            new RegulatoryComplianceRuntimeOptions(),
            NullLogger<ComplianceOutboxProcessor>.Instance);

        Assert.Equal(1, await processor.ProcessBatchAsync());

        Assert.True(batch.Committed);
        Assert.Equal(1, message.RetryCount);
        Assert.Contains("RabbitMQ unavailable", message.Error);
        Assert.Null(message.ProcessedAt);
    }

    private sealed class FakeBatchStore(FakeBatch batch) : IComplianceOutboxBatchStore
    {
        public Task<IComplianceOutboxBatch> LockPendingBatchAsync(
            int batchSize,
            int maxRetries,
            CancellationToken cancellationToken) => Task.FromResult<IComplianceOutboxBatch>(batch);
    }

    private sealed class FakeBatch(IReadOnlyList<OutboxMessage> messages) : IComplianceOutboxBatch
    {
        public IReadOnlyList<OutboxMessage> Messages => messages;
        public bool Committed { get; private set; }

        public Task CommitAsync(CancellationToken cancellationToken)
        {
            Committed = true;
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class FailingPublisher : IComplianceIntegrationEventPublisher
    {
        public Task PublishAsync(object message, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("RabbitMQ unavailable");
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
