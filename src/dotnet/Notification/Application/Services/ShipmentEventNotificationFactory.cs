using Notification.Domain.Enums;
using Shipment.Contracts.Events;

namespace Notification.Application.Services;

public static class ShipmentEventNotificationFactory
{
    public static IntegrationEventNotificationEnvelope Create(ShipmentCreatedEvent message)
    {
        ArgumentNullException.ThrowIfNull(message);

        return new IntegrationEventNotificationEnvelope(
            message.EventId,
            message.ContractVersion,
            message.TenantId,
            message.ShipmentId,
            nameof(ShipmentCreatedEvent),
            NotificationEventType.ShipmentCreated,
            "Shipment created",
            $"Shipment {message.ShipmentNumber} was created.",
            message.CreatedAt);
    }

    public static IntegrationEventNotificationEnvelope Create(ShipmentStatusChangedEvent message)
    {
        ArgumentNullException.ThrowIfNull(message);

        var body = $"Shipment {message.ShipmentId} changed from {message.OldStatus} to {message.NewStatus}.";
        if (!string.IsNullOrWhiteSpace(message.Note))
            body = $"{body} {message.Note.Trim()}";

        return new IntegrationEventNotificationEnvelope(
            message.EventId,
            message.ContractVersion,
            message.TenantId,
            message.ShipmentId,
            nameof(ShipmentStatusChangedEvent),
            NotificationEventType.ShipmentStatusChanged,
            "Shipment status updated",
            NotificationContent.BoundBody(body),
            message.ChangedAt);
    }

    public static IntegrationEventNotificationEnvelope Create(ShipmentCancelledEvent message)
    {
        ArgumentNullException.ThrowIfNull(message);

        var body = $"Shipment {message.ShipmentId} was cancelled.";
        if (!string.IsNullOrWhiteSpace(message.Reason))
            body = $"{body} Reason: {message.Reason.Trim()}";

        return new IntegrationEventNotificationEnvelope(
            message.EventId,
            message.ContractVersion,
            message.TenantId,
            message.ShipmentId,
            nameof(ShipmentCancelledEvent),
            NotificationEventType.ShipmentCancelled,
            "Shipment cancelled",
            NotificationContent.BoundBody(body),
            message.CancelledAt);
    }

    public static IntegrationEventNotificationEnvelope Create(ShipmentSubmittedEvent message)
    {
        ArgumentNullException.ThrowIfNull(message);

        return CreateLifecycle(
            message.EventId, message.ContractVersion, message.TenantId, message.ShipmentId,
            nameof(ShipmentSubmittedEvent), NotificationEventType.ShipmentSubmitted,
            "Shipment submitted", message.ShipmentNumber, "submitted", message.SubmittedAt);
    }

    public static IntegrationEventNotificationEnvelope Create(ShipmentPickedUpEvent message)
    {
        ArgumentNullException.ThrowIfNull(message);

        return CreateLifecycle(
            message.EventId, message.ContractVersion, message.TenantId, message.ShipmentId,
            nameof(ShipmentPickedUpEvent), NotificationEventType.ShipmentPickedUp,
            "Shipment picked up", message.ShipmentNumber, "picked up", message.PickedUpAt);
    }

    public static IntegrationEventNotificationEnvelope Create(ShipmentDeliveredEvent message)
    {
        ArgumentNullException.ThrowIfNull(message);

        return CreateLifecycle(
            message.EventId, message.ContractVersion, message.TenantId, message.ShipmentId,
            nameof(ShipmentDeliveredEvent), NotificationEventType.ShipmentDelivered,
            "Shipment delivered", message.ShipmentNumber, "delivered", message.DeliveredAt);
    }

    public static IntegrationEventNotificationEnvelope Create(ShipmentCompletedEvent message)
    {
        ArgumentNullException.ThrowIfNull(message);

        return CreateLifecycle(
            message.EventId, message.ContractVersion, message.TenantId, message.ShipmentId,
            nameof(ShipmentCompletedEvent), NotificationEventType.ShipmentCompleted,
            "Shipment completed", message.ShipmentNumber, "completed", message.CompletedAt);
    }

    public static IntegrationEventNotificationEnvelope Create(DocumentAttachedEvent message)
    {
        ArgumentNullException.ThrowIfNull(message);

        return new IntegrationEventNotificationEnvelope(
            message.EventId,
            message.ContractVersion,
            message.TenantId,
            message.ShipmentId,
            nameof(DocumentAttachedEvent),
            NotificationEventType.DocumentAttached,
            "Shipment document attached",
            NotificationContent.BoundBody(
                $"Document {message.FileName} ({message.DocumentType}) was attached to shipment {message.ShipmentId}."),
            message.AttachedAt);
    }

    private static IntegrationEventNotificationEnvelope CreateLifecycle(
        Guid eventId,
        int contractVersion,
        Guid tenantId,
        Guid shipmentId,
        string sourceEventType,
        NotificationEventType eventType,
        string title,
        string shipmentNumber,
        string action,
        DateTimeOffset occurredAt) =>
        new(
            eventId,
            contractVersion,
            tenantId,
            shipmentId,
            sourceEventType,
            eventType,
            title,
            NotificationContent.BoundBody($"Shipment {shipmentNumber} was {action}."),
            occurredAt);
}
