using MassTransit;
using Microsoft.EntityFrameworkCore;
using Notification.Application.Interfaces;
using Notification.Domain.Entities;
using Notification.Domain.Enums;
using Notification.Infrastructure.Persistences;

namespace Notification.Infrastructure.Messaging;

public sealed class NotificationEventProcessor(
    NotificationDbContext db,
    IRecipientResolver recipients,
    IFcmPushProvider pushProvider,
    ILogger<NotificationEventProcessor> logger,
    ISystemClock? clock = null)
{
    private const int MaxDeliveryAttempts = 5;
    public Task ProcessAsync(Guid eventId, Guid tenantId, Guid? shipmentId, string type, string title, string body, string? shipmentNumber, DateTimeOffset occurredAt, CancellationToken ct) =>
        ProcessCoreAsync(eventId, tenantId, shipmentId, type, title, body, shipmentNumber, occurredAt, ct);

    private async Task ProcessCoreAsync(Guid eventId, Guid tenantId, Guid? shipmentId, string type, string title, string body, string? shipmentNumber, DateTimeOffset occurredAt, CancellationToken ct)
    {
        var safeType = BoundText(type, 64, "NOTIFICATION");
        var safeTitle = BoundText(title, 200, "Notification");
        var safeBody = BoundText(body, 2000, "You have a new notification.");
        var safeShipmentNumber = string.IsNullOrWhiteSpace(shipmentNumber) ? null : BoundText(shipmentNumber, 128, null);
        var rule = $"notification:{safeType}";
        var userIds = (await recipients.ResolveAsync(tenantId, shipmentId, ct)).Distinct().ToArray();
        var notifications = new List<Notification.Domain.Entities.Notification>();

        await using (var transaction = await db.Database.BeginTransactionAsync(ct))
        {
            if (await db.ProcessedEvents.AnyAsync(x => x.TenantId == tenantId && x.EventId == eventId && x.Rule == rule, ct))
                return;

            var actionUrl = shipmentId is null ? "/notifications" : $"/shipments/{shipmentId}";
            foreach (var userId in userIds)
            {
                notifications.Add(Notification.Domain.Entities.Notification.Create(
                    tenantId, userId, safeType, safeTitle, safeBody, shipmentId, safeShipmentNumber, actionUrl, NotificationPriority.Info));
            }

            db.ProcessedEvents.Add(ProcessedNotificationEvent.Create(
                eventId,
                tenantId,
                rule,
                userIds.Length == 0 ? ProcessedNotificationEventOutcome.NoRecipient : ProcessedNotificationEventOutcome.AudienceResolved,
                userIds.Length));
            db.Notifications.AddRange(notifications);
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }

        foreach (var notification in notifications)
        {
            var devices = await db.DevicesFor(tenantId, notification.UserId)
                .Where(x => x.IsActive)
                .ToListAsync(ct);
            var delivered = false;

            foreach (var device in devices)
            {
                var attempt = NotificationDeliveryAttempt.Create(notification.Id, device.Id);
                db.DeliveryAttempts.Add(attempt);
                var result = await pushProvider.SendAsync(device, new FcmMessage(safeTitle, safeBody, new Dictionary<string, string>
                {
                    ["notificationId"] = notification.Id.ToString(), ["type"] = safeType,
                    ["shipmentId"] = shipmentId?.ToString() ?? string.Empty,
                    ["actionUrl"] = notification.ActionUrl ?? "/notifications"
                }), ct);
                if (result.Status == FcmSendStatus.Sent) { attempt.Sent(result.ProviderMessageId); delivered = true; }
                else if (result.Status == FcmSendStatus.InvalidToken) { attempt.Failed(result.ErrorCode ?? "invalid_token", true); device.Deactivate(); }
                else if (result.Status == FcmSendStatus.TransientFailure)
                    ScheduleRetry(attempt, result.ErrorCode ?? "transient_failure");
                else attempt.Failed(result.ErrorCode ?? "provider_failure", false);
                await db.SaveChangesAsync(ct);
                logger.LogInformation(
                    "Notification delivery attempt {AttemptId} completed for event {EventId} and notification {NotificationId} with status {Status}",
                    attempt.Id, eventId, notification.Id, attempt.Status);
            }
            if (delivered) notification.MarkSent();
            await db.SaveChangesAsync(ct);
            logger.LogInformation("Processed notification event {EventId} for shipment {ShipmentId} and notification {NotificationId}", eventId, shipmentId, notification.Id);
        }
    }

    private static string BoundText(string value, int maxLength, string? fallback)
    {
        var normalized = value?.Trim() ?? string.Empty;
        if (normalized.Length == 0) return fallback ?? string.Empty;
        return normalized.Length <= maxLength ? normalized : normalized[..maxLength];
    }

    private void ScheduleRetry(NotificationDeliveryAttempt attempt, string errorCode)
    {
        if (attempt.AttemptCount >= MaxDeliveryAttempts)
        {
            attempt.Failed("retry_exhausted", invalidToken: false);
            return;
        }

        var delaySeconds = Math.Min(300, 15 * Math.Pow(2, Math.Max(0, attempt.AttemptCount - 1)));
        attempt.Retry(errorCode, (clock?.UtcNow ?? DateTimeOffset.UtcNow).AddSeconds(delaySeconds));
    }
}
