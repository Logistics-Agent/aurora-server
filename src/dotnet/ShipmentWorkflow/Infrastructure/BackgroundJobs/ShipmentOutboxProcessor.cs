using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ShipmentWorkflow.Infrastructure.Persistences;

namespace ShipmentWorkflow.Infrastructure.BackgroundJobs;

public sealed class ShipmentOutboxProcessor(
    ShipmentWorkflowDbContext dbContext,
    IShipmentIntegrationEventPublisher publisher,
    TimeProvider timeProvider,
    IOptions<ShipmentOutboxPublisherOptions> options,
    ILogger<ShipmentOutboxProcessor> logger)
{
    private const int MaximumErrorLength = 2_000;
    private readonly ShipmentOutboxPublisherOptions _options = options.Value;

    public async Task<int> ProcessBatchAsync(CancellationToken cancellationToken = default)
    {
        await using var transaction =
            await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var messages = await dbContext.OutboxMessages
            .FromSqlInterpolated($"""
                SELECT *
                FROM outbox_messages
                WHERE "ProcessedAt" IS NULL
                  AND "RetryCount" < {_options.MaxRetries}
                ORDER BY "CreatedAt", "Id"
                LIMIT {_options.BatchSize}
                FOR UPDATE SKIP LOCKED
                """)
            .ToListAsync(cancellationToken);

        foreach (var message in messages)
        {
            try
            {
                var integrationEvent = ShipmentIntegrationEventTypeRegistry.Deserialize(
                    message.EventType,
                    message.Payload);
                await publisher.PublishAsync(integrationEvent, cancellationToken);
                message.ProcessedAt = timeProvider.GetUtcNow();
                message.Error = null;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                message.RetryCount++;
                message.Error = Truncate(exception.Message);
                logger.LogWarning(
                    exception,
                    "Failed to publish Shipment outbox message {MessageId} ({EventType}), attempt {RetryCount}.",
                    message.Id,
                    message.EventType,
                    message.RetryCount);
            }
        }

        if (messages.Count > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return messages.Count;
    }

    private static string Truncate(string value) =>
        value.Length <= MaximumErrorLength
            ? value
            : value[..MaximumErrorLength];
}
