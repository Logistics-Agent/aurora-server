using Microsoft.Extensions.Options;

namespace GpsTracking.Infrastructure.BackgroundJobs;

public sealed class GpsOutboxPublisherBackgroundService(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    IOptions<GpsOutboxPublisherOptions> options,
    ILogger<GpsOutboxPublisherBackgroundService> logger) : BackgroundService
{
    private readonly GpsOutboxPublisherOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _options.Validate();
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var processor = scope.ServiceProvider.GetRequiredService<GpsOutboxProcessor>();
                await processor.ProcessBatchAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Unexpected failure while processing the GPS outbox.");
            }

            try
            {
                await Task.Delay(_options.PollingInterval, timeProvider, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }
}
