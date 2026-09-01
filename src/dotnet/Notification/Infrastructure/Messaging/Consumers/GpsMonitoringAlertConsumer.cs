using GpsTracking.Contracts.Events;
using MassTransit;

namespace Notification.Infrastructure.Messaging.Consumers;

public sealed class GpsMonitoringAlertConsumer(NotificationEventProcessor processor) : IConsumer<GpsMonitoringAlertRaisedEvent>
{
    public Task Consume(ConsumeContext<GpsMonitoringAlertRaisedEvent> context) => processor.ProcessAsync(
        context.Message.EventId, context.Message.TenantId, context.Message.ShipmentId,
        "GPS_MONITORING_ALERT_RAISED", "GPS monitoring alert",
        $"{context.Message.AlertType} alert for vehicle {context.Message.VehicleId}: {context.Message.Message}",
        null, context.Message.OccurredAt, context.CancellationToken);
}
