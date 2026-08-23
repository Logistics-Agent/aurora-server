namespace DocumentOcr.Infrastructure.BackgroundJobs;

public sealed class DocumentOcrOutboxProcessor(
    IDocumentOcrOutboxBatchStore store,
    IDocumentOcrIntegrationEventPublisher publisher,
    TimeProvider timeProvider,
    DocumentOcrOutboxPublisherOptions options,
    ILogger<DocumentOcrOutboxProcessor> logger)
{
    private const int MaximumErrorLength = 2_000;

    public async Task<int> ProcessBatchAsync(CancellationToken cancellationToken = default)
    {
        options.Validate();
        await using var batch = await store.LockPendingBatchAsync(
            options.BatchSize,
            options.MaxRetries,
            cancellationToken);
        var publishedCount = 0;

        foreach (var message in batch.Messages)
        {
            try
            {
                var integrationEvent = DocumentOcrIntegrationEventTypeRegistry.Deserialize(
                    message.EventType,
                    message.Content);
                await publisher.PublishAsync(integrationEvent, cancellationToken);
                message.MarkProcessed(timeProvider.GetUtcNow());
                publishedCount++;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                var error = string.IsNullOrWhiteSpace(exception.Message)
                    ? exception.GetType().Name
                    : exception.Message;
                message.RecordFailure(Truncate(error), timeProvider.GetUtcNow());
                logger.LogWarning(
                    exception,
                    "Failed to publish Document OCR outbox message {MessageId} ({EventType}), attempt {RetryCount}.",
                    message.Id,
                    message.EventType,
                    message.RetryCount);
            }
        }

        await batch.CommitAsync(cancellationToken);
        if (batch.Messages.Count > 0)
        {
            logger.LogInformation(
                "Processed Document OCR outbox batch of {BatchCount}; {PublishedCount} messages published.",
                batch.Messages.Count,
                publishedCount);
        }
        return batch.Messages.Count;
    }

    private static string Truncate(string value) =>
        value.Length <= MaximumErrorLength ? value : value[..MaximumErrorLength];
}
