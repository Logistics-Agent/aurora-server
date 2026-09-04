using MassTransit;
using Notification.Infrastructure.Messaging;
using Shipment.Contracts.Events;

namespace Notification.Infrastructure.Messaging.Consumers;

public sealed class ShipmentStatusChangedConsumer(NotificationEventProcessor processor) : IConsumer<ShipmentStatusChangedEvent>
{
    public Task Consume(ConsumeContext<ShipmentStatusChangedEvent> context) => processor.ProcessAsync(
        context.Message.EventId, context.Message.TenantId, context.Message.ShipmentId, "SHIPMENT_STATUS_CHANGED",
        $"Shipment {context.Message.NewStatus}", $"Shipment status changed from {context.Message.OldStatus} to {context.Message.NewStatus}.", null, context.Message.ChangedAt, context.CancellationToken);
}
