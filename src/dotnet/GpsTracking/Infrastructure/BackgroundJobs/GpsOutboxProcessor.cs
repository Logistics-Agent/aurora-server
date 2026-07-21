using Microsoft.Extensions.Options;

namespace GpsTracking.Infrastructure.BackgroundJobs;

public sealed class GpsOutboxProcessor
{
    private const int MaximumErrorLength = 2_000;
    private readonly IGpsOutboxBatchStore _store;
    private readonly IGpsIntegrationEventPublisher _publisher;
    private readonly TimeProvider _timeProvider;
    private readonly GpsOutboxPublisherOptions _options;
    private readonly ILogger<GpsOutboxProcessor> _logger;

    public GpsOutboxProcessor(
        IGpsOutboxBatchStore store,
        IGpsIntegrationEventPublisher publisher,
        TimeProvider timeProvider,
        IOptions<GpsOutboxPublisherOptions> options,
        ILogger<GpsOutboxProcessor> logger)
    {
        _store = store;
        _publisher = publisher;
        _timeProvider = timeProvider;
        _options = options.Value;
        _options.Validate();
        _logger = logger;
    }

    public async Task<int> ProcessBatchAsync(CancellationToken cancellationToken = default)
    {
        await using var batch = await _store.LockPendingBatchAsync(
            _options.BatchSize, _options.MaxRetries, cancellationToken);
        var publishedCount = 0;

        foreach (var message in batch.Messages)
        {
            try
            {
                var integrationEvent = GpsIntegrationEventTypeRegistry.Deserialize(
                    message.EventType, message.Content);
                await _publisher.PublishAsync(integrationEvent, cancellationToken);
                message.MarkProcessed(_timeProvider.GetUtcNow());
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
                message.RecordFailure(Truncate(error), _timeProvider.GetUtcNow());
                _logger.LogWarning(
                    exception,
                    "Failed to publish GPS outbox message {MessageId} ({EventType}), attempt {RetryCount}.",
                    message.Id,
                    message.EventType,
                    message.RetryCount);
            }
        }

        await batch.CommitAsync(cancellationToken);
        if (batch.Messages.Count > 0)
        {
            _logger.LogInformation(
                "Processed GPS outbox batch of {BatchCount}; {PublishedCount} messages published.",
                batch.Messages.Count,
                publishedCount);
        }
        return batch.Messages.Count;
    }

    private static string Truncate(string value) =>
        value.Length <= MaximumErrorLength ? value : value[..MaximumErrorLength];
}
