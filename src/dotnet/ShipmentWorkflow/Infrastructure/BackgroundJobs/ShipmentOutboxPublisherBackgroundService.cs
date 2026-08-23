using Microsoft.Extensions.Options;

namespace ShipmentWorkflow.Infrastructure.BackgroundJobs;

public sealed class ShipmentOutboxPublisherBackgroundService(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    IOptions<ShipmentOutboxPublisherOptions> options,
    ILogger<ShipmentOutboxPublisherBackgroundService> logger) : BackgroundService
{
    private readonly ShipmentOutboxPublisherOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var processor = scope.ServiceProvider
                    .GetRequiredService<ShipmentOutboxProcessor>();
                await processor.ProcessBatchAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(
                    exception,
                    "Unexpected failure while processing the Shipment outbox.");
            }

            try
            {
                await Task.Delay(
                    _options.PollingInterval,
                    timeProvider,
                    stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }
}
