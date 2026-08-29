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
    ILogger<NotificationEventProcessor> logger)
{
    public Task ProcessAsync(Guid eventId, Guid tenantId, Guid? shipmentId, string type, string title, string body, string? shipmentNumber, DateTimeOffset occurredAt, CancellationToken ct) =>
        ProcessCoreAsync(eventId, tenantId, shipmentId, type, title, body, shipmentNumber, occurredAt, ct);

    private async Task ProcessCoreAsync(Guid eventId, Guid tenantId, Guid? shipmentId, string type, string title, string body, string? shipmentNumber, DateTimeOffset occurredAt, CancellationToken ct)
    {
        var userIds = await recipients.ResolveAsync(tenantId, shipmentId, ct);
        foreach (var userId in userIds)
        {
            const string rule = "shipment-status";
            if (await db.ProcessedEvents.AnyAsync(x => x.EventId == eventId && x.Rule == rule && x.UserId == userId, ct)) continue;
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            if (await db.ProcessedEvents.AnyAsync(x => x.EventId == eventId && x.Rule == rule && x.UserId == userId, ct)) { await transaction.RollbackAsync(ct); continue; }
            var actionUrl = shipmentId is null ? "/notifications" : $"/shipments/{shipmentId}";
            var notification = Notification.Domain.Entities.Notification.Create(tenantId, userId, type, title, body, shipmentId, shipmentNumber, actionUrl, NotificationPriority.Info);
            db.Notifications.Add(notification);
            db.ProcessedEvents.Add(ProcessedNotificationEvent.Create(eventId, tenantId, userId, rule));
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            var devices = await db.Devices.Where(x => x.TenantId == tenantId && x.UserId == userId && x.IsActive).ToListAsync(ct);
            var delivered = false;
            foreach (var device in devices)
            {
                var attempt = NotificationDeliveryAttempt.Create(notification.Id, device.Id);
                db.DeliveryAttempts.Add(attempt);
                var result = await pushProvider.SendAsync(device, new FcmMessage(title, body, new Dictionary<string, string>
                {
                    ["notificationId"] = notification.Id.ToString(), ["type"] = type,
                    ["shipmentId"] = shipmentId?.ToString() ?? string.Empty, ["actionUrl"] = actionUrl
                }), ct);
                if (result.Status == FcmSendStatus.Sent) { attempt.Sent(result.ProviderMessageId); delivered = true; }
                else if (result.Status == FcmSendStatus.InvalidToken) { attempt.Failed(result.ErrorCode ?? "invalid_token", true); device.Deactivate(); }
                else if (result.Status == FcmSendStatus.TransientFailure) attempt.Retry(result.ErrorCode ?? "transient_failure");
                else attempt.Failed(result.ErrorCode ?? "provider_failure", false);
                await db.SaveChangesAsync(ct);
            }
            if (delivered) notification.MarkSent();
            await db.SaveChangesAsync(ct);
            logger.LogInformation("Processed notification event {EventId} for shipment {ShipmentId} and user {UserId}", eventId, shipmentId, userId);
        }
    }
}
