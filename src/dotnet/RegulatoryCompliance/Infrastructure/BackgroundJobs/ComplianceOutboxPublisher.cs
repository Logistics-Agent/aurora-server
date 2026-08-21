using System.Text.Json;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using RegulatoryCompliance.Contracts.Events;
using RegulatoryCompliance.Domain.Entities;
using RegulatoryCompliance.Infrastructure.Persistences;

namespace RegulatoryCompliance.Infrastructure.BackgroundJobs;

public static class ComplianceIntegrationEventRegistry
{
    private static readonly IReadOnlyDictionary<string, Type> EventTypes =
        new Dictionary<string, Type>(StringComparer.Ordinal)
        {
            [typeof(ComplianceEvaluationCompletedEvent).FullName!] =
                typeof(ComplianceEvaluationCompletedEvent),
            [typeof(ComplianceEvaluationFailedEvent).FullName!] =
                typeof(ComplianceEvaluationFailedEvent)
        };

    public static object Deserialize(string eventType, string content)
    {
        if (!EventTypes.TryGetValue(eventType, out var type))
            throw new InvalidOperationException($"Unsupported Compliance outbox event type '{eventType}'.");
        return JsonSerializer.Deserialize(content, type)
               ?? throw new JsonException($"Compliance outbox event '{eventType}' deserialized to null.");
    }
}

public interface IComplianceIntegrationEventPublisher
{
    Task PublishAsync(object message, CancellationToken cancellationToken);
}

public sealed class ComplianceIntegrationEventPublisher(IPublishEndpoint publishEndpoint)
    : IComplianceIntegrationEventPublisher
{
    public Task PublishAsync(object message, CancellationToken cancellationToken) =>
        publishEndpoint.Publish(message, message.GetType(), cancellationToken);
}

public interface IComplianceOutboxBatch : IAsyncDisposable
{
    IReadOnlyList<OutboxMessage> Messages { get; }
    Task CommitAsync(CancellationToken cancellationToken);
}

public interface IComplianceOutboxBatchStore
{
    Task<IComplianceOutboxBatch> LockPendingBatchAsync(
        int batchSize,
        int maxRetries,
        CancellationToken cancellationToken);
}

public sealed class ComplianceOutboxBatchStore(RegulatoryComplianceDbContext dbContext)
    : IComplianceOutboxBatchStore
{
    public async Task<IComplianceOutboxBatch> LockPendingBatchAsync(
        int batchSize,
        int maxRetries,
        CancellationToken cancellationToken)
    {
        var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var messages = await dbContext.OutboxMessages
                .FromSqlInterpolated($"""
                    SELECT *
                    FROM outbox_messages
                    WHERE "ProcessedAt" IS NULL
                      AND "RetryCount" < {maxRetries}
                    ORDER BY "OccurredAt", "Id"
                    LIMIT {batchSize}
                    FOR UPDATE SKIP LOCKED
                    """)
                .IgnoreQueryFilters()
                .ToListAsync(cancellationToken);
            return new ComplianceOutboxBatch(dbContext, transaction, messages);
        }
        catch
        {
            await transaction.DisposeAsync();
            throw;
        }
    }

    private sealed class ComplianceOutboxBatch(
        RegulatoryComplianceDbContext dbContext,
        IDbContextTransaction transaction,
        IReadOnlyList<OutboxMessage> messages) : IComplianceOutboxBatch
    {
        private bool _committed;
        public IReadOnlyList<OutboxMessage> Messages => messages;

        public async Task CommitAsync(CancellationToken cancellationToken)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            _committed = true;
        }

        public async ValueTask DisposeAsync()
        {
            if (!_committed)
                await transaction.RollbackAsync();
            await transaction.DisposeAsync();
        }
    }
}

public sealed class ComplianceOutboxProcessor(
    IComplianceOutboxBatchStore store,
    IComplianceIntegrationEventPublisher publisher,
    TimeProvider timeProvider,
    RegulatoryComplianceRuntimeOptions options,
    ILogger<ComplianceOutboxProcessor> logger)
{
    public async Task<int> ProcessBatchAsync(CancellationToken cancellationToken = default)
    {
        await using var batch = await store.LockPendingBatchAsync(
            options.OutboxBatchSize, options.OutboxMaxRetries, cancellationToken);
        foreach (var message in batch.Messages)
        {
            try
            {
                await publisher.PublishAsync(
                    ComplianceIntegrationEventRegistry.Deserialize(message.EventType, message.Content),
                    cancellationToken);
                message.MarkProcessed(timeProvider.GetUtcNow());
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                message.RecordFailure(
                    exception.Message.Length <= 2_000 ? exception.Message : exception.Message[..2_000],
                    timeProvider.GetUtcNow());
                logger.LogWarning(exception, "Compliance outbox publish failed for {MessageId}.", message.Id);
            }
        }
        await batch.CommitAsync(cancellationToken);
        return batch.Messages.Count;
    }
}

public sealed class ComplianceOutboxBackgroundService(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    RegulatoryComplianceRuntimeOptions options,
    ILogger<ComplianceOutboxBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        options.Validate();
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<ComplianceOutboxProcessor>()
                    .ProcessBatchAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Unexpected failure in the Compliance outbox worker.");
            }
            try
            {
                await Task.Delay(options.OutboxPollingInterval, timeProvider, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }
}
