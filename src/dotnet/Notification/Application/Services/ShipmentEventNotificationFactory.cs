using Notification.Domain.Enums;
using Shipment.Contracts.Events;

namespace Notification.Application.Services;

public sealed record ShipmentNotificationEnvelope(
    Guid EventId,
    int ContractVersion,
    Guid TenantId,
    Guid ShipmentId,
    string SourceEventType,
    NotificationEventType EventType,
    string Title,
    string Body,
    DateTimeOffset OccurredAt);

public static class ShipmentEventNotificationFactory
{
    public static ShipmentNotificationEnvelope Create(ShipmentCreatedEvent message)
    {
        ArgumentNullException.ThrowIfNull(message);

        return new ShipmentNotificationEnvelope(
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

    public static ShipmentNotificationEnvelope Create(ShipmentStatusChangedEvent message)
    {
        ArgumentNullException.ThrowIfNull(message);

        var body = $"Shipment {message.ShipmentId} changed from {message.OldStatus} to {message.NewStatus}.";
        if (!string.IsNullOrWhiteSpace(message.Note))
            body = $"{body} {message.Note.Trim()}";

        return new ShipmentNotificationEnvelope(
            message.EventId,
            message.ContractVersion,
            message.TenantId,
            message.ShipmentId,
            nameof(ShipmentStatusChangedEvent),
            NotificationEventType.ShipmentStatusChanged,
            "Shipment status updated",
            body,
            message.ChangedAt);
    }

    public static ShipmentNotificationEnvelope Create(ShipmentCancelledEvent message)
    {
        ArgumentNullException.ThrowIfNull(message);

        var body = $"Shipment {message.ShipmentId} was cancelled.";
        if (!string.IsNullOrWhiteSpace(message.Reason))
            body = $"{body} Reason: {message.Reason.Trim()}";

        return new ShipmentNotificationEnvelope(
            message.EventId,
            message.ContractVersion,
            message.TenantId,
            message.ShipmentId,
            nameof(ShipmentCancelledEvent),
            NotificationEventType.ShipmentCancelled,
            "Shipment cancelled",
            body,
            message.CancelledAt);
    }
}
