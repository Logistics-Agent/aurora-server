namespace DocumentOcr.Infrastructure.BackgroundJobs;

public sealed class DocumentOcrOutboxPublisherBackgroundService(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    DocumentOcrOutboxPublisherOptions options,
    ILogger<DocumentOcrOutboxPublisherBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        options.Validate();
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var processor = scope.ServiceProvider.GetRequiredService<DocumentOcrOutboxProcessor>();
                await processor.ProcessBatchAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Unexpected failure while processing the Document OCR outbox.");
            }

            try
            {
                await Task.Delay(options.PollingInterval, timeProvider, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }
}
