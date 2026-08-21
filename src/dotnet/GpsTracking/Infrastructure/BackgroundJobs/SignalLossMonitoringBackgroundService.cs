using GpsTracking.Application.Monitoring;
using Microsoft.Extensions.DependencyInjection;

namespace GpsTracking.Infrastructure.BackgroundJobs;

public sealed class SignalLossMonitoringBackgroundService(
    IServiceScopeFactory scopeFactory,
    MonitoringOptions options,
    ILogger<SignalLossMonitoringBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var monitor = scope.ServiceProvider.GetRequiredService<SignalLossMonitor>();
                var raised = await monitor.ScanAsync(stoppingToken);
                if (raised > 0)
                    logger.LogInformation("GPS signal-loss scan raised {AlertCount} alerts.", raised);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "GPS signal-loss scan failed.");
            }

            await Task.Delay(options.SignalLossScanInterval, stoppingToken);
        }
    }
}
