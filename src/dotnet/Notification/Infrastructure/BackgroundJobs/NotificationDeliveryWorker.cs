using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Notification.Application.Delivery;
using Notification.Domain.Enums;
using Notification.Infrastructure.Persistences;

namespace Notification.Infrastructure.BackgroundJobs;

public sealed class NotificationDeliveryWorkerOptions
{
    public int BatchSize { get; init; } = 50;
    public TimeSpan PollInterval { get; init; } = TimeSpan.FromSeconds(5);
}

public sealed class NotificationDeliveryWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<NotificationDeliveryWorkerOptions> options,
    TimeProvider timeProvider,
    ILogger<NotificationDeliveryWorker> logger) : BackgroundService
{
    private readonly NotificationDeliveryWorkerOptions _options = Validate(options.Value);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var notificationIds = await LoadDueNotificationIdsAsync(stoppingToken);
                foreach (var notificationId in notificationIds)
                {
                    try
                    {
                        await using var scope = scopeFactory.CreateAsyncScope();
                        var deliveryService = scope.ServiceProvider
                            .GetRequiredService<INotificationDeliveryService>();
                        await deliveryService.DeliverAsync(notificationId, stoppingToken);
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch (Exception exception)
                    {
                        logger.LogError(
                            exception,
                            "Notification delivery failed for {NotificationId}.",
                            notificationId);
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Notification delivery polling failed.");
            }

            await Task.Delay(_options.PollInterval, timeProvider, stoppingToken);
        }
    }

    private async Task<IReadOnlyList<Guid>> LoadDueNotificationIdsAsync(
        CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
        var now = timeProvider.GetUtcNow();

        return await dbContext.Notifications
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(item => item.Status == NotificationStatus.Pending
                || (item.Status == NotificationStatus.Failed
                    && item.NextAttemptAt != null
                    && item.NextAttemptAt <= now))
            .OrderBy(item => item.NextAttemptAt ?? item.CreatedAt)
            .ThenBy(item => item.Id)
            .Select(item => item.Id)
            .Take(_options.BatchSize)
            .ToListAsync(cancellationToken);
    }

    private static NotificationDeliveryWorkerOptions Validate(
        NotificationDeliveryWorkerOptions options)
    {
        if (options.BatchSize <= 0 || options.BatchSize > 1000)
            throw new InvalidOperationException("NotificationDelivery BatchSize must be between 1 and 1000.");
        if (options.PollInterval <= TimeSpan.Zero)
            throw new InvalidOperationException("NotificationDelivery PollInterval must be positive.");

        return options;
    }
}
