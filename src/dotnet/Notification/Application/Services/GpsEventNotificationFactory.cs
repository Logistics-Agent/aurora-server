using GpsTracking.Contracts.Events;
using Notification.Domain.Enums;

namespace Notification.Application.Services;

public static class GpsEventNotificationFactory
{
    public static IntegrationEventNotificationEnvelope Create(GpsMonitoringAlertRaisedEvent message)
    {
        ArgumentNullException.ThrowIfNull(message);

        return new IntegrationEventNotificationEnvelope(
            message.EventId,
            message.ContractVersion,
            message.TenantId,
            message.ShipmentId,
            nameof(GpsMonitoringAlertRaisedEvent),
            NotificationEventType.GpsMonitoringAlertRaised,
            "GPS monitoring alert",
            NotificationContent.BoundBody(
                $"{message.AlertType} alert for vehicle {message.VehicleId}: {message.Message}"),
            message.OccurredAt);
    }
}
