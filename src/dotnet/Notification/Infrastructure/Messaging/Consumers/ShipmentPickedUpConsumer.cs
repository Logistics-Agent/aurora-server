using MassTransit;
using Shipment.Contracts.Events;

namespace Notification.Infrastructure.Messaging.Consumers;

public sealed class ShipmentPickedUpConsumer(NotificationEventProcessor processor) : IConsumer<ShipmentPickedUpEvent>
{
    public Task Consume(ConsumeContext<ShipmentPickedUpEvent> context) => processor.ProcessAsync(
        context.Message.EventId, context.Message.TenantId, context.Message.ShipmentId,
        "SHIPMENT_PICKED_UP", "Shipment picked up",
        $"Shipment {context.Message.ShipmentNumber} was picked up.",
        context.Message.ShipmentNumber, context.Message.PickedUpAt, context.CancellationToken);
}
