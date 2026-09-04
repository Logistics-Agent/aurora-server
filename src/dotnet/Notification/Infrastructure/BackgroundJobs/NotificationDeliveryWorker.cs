using Microsoft.EntityFrameworkCore;
using Notification.Application.Interfaces;
using Notification.Domain.Enums;
using Notification.Infrastructure.Persistences;

namespace Notification.Infrastructure.BackgroundJobs;

public sealed class NotificationDeliveryWorker(IServiceScopeFactory scopes, ILogger<NotificationDeliveryWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(15));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try { await ProcessRetriesAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (Exception ex) { logger.LogError(ex, "Notification retry worker failed"); }
        }
    }

    private async Task ProcessRetriesAsync(CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
        var provider = scope.ServiceProvider.GetRequiredService<IFcmPushProvider>();
        var now = DateTimeOffset.UtcNow;
        var attempts = await db.DeliveryAttempts
            .Where(x => x.Status == DeliveryAttemptStatus.Retrying && x.AttemptCount <= 5 && x.NextAttemptAt <= now)
            .OrderBy(x => x.NextAttemptAt)
            .Take(100)
            .ToListAsync(ct);
        foreach (var attempt in attempts)
        {
            var notification = await db.Notifications.SingleOrDefaultAsync(x => x.Id == attempt.NotificationId, ct);
            var device = await db.Devices.SingleOrDefaultAsync(x => x.Id == attempt.DeviceId && x.IsActive, ct);
            if (notification is null || device is null) continue;
            var result = await provider.SendAsync(device, new FcmMessage(notification.Title, notification.Body, new Dictionary<string, string>
            {
                ["notificationId"] = notification.Id.ToString(), ["type"] = notification.Type,
                ["shipmentId"] = notification.ShipmentId?.ToString() ?? string.Empty,
                ["actionUrl"] = notification.ActionUrl ?? "/notifications"
            }), ct);
            if (result.Status == FcmSendStatus.Sent) attempt.Sent(result.ProviderMessageId);
            else if (result.Status == FcmSendStatus.InvalidToken) { attempt.Failed(result.ErrorCode ?? "invalid_token", true); device.Deactivate(); }
            else if (attempt.AttemptCount >= 5) attempt.Failed(result.ErrorCode ?? "retry_exhausted", false);
            else
            {
                var delaySeconds = Math.Min(300, 15 * Math.Pow(2, Math.Max(0, attempt.AttemptCount - 1)));
                attempt.Retry(result.ErrorCode ?? "transient_failure", now.AddSeconds(delaySeconds));
            }
            await db.SaveChangesAsync(ct);
            logger.LogInformation(
                "Notification retry attempt {AttemptId} completed for notification {NotificationId} with status {Status}",
                attempt.Id, notification.Id, attempt.Status);
        }
    }
}
