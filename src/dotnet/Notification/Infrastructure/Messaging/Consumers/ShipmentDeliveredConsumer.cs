using MassTransit;
using Notification.Infrastructure.Messaging;
using Shipment.Contracts.Events;

namespace Notification.Infrastructure.Messaging.Consumers;

public sealed class ShipmentDeliveredConsumer(NotificationEventProcessor processor) : IConsumer<ShipmentDeliveredEvent>
{
    public Task Consume(ConsumeContext<ShipmentDeliveredEvent> context) => processor.ProcessAsync(
        context.Message.EventId, context.Message.TenantId, context.Message.ShipmentId, "SHIPMENT_DELIVERED",
        "Shipment delivered", $"Shipment {context.Message.ShipmentNumber} was delivered.", context.Message.ShipmentNumber, context.Message.DeliveredAt, context.CancellationToken);
}
