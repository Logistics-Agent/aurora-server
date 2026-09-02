using MassTransit;
using Notification.Infrastructure.Messaging;
using Shipment.Contracts.Events;

namespace Notification.Infrastructure.Messaging.Consumers;

public sealed class ShipmentCreatedConsumer(NotificationEventProcessor processor) : IConsumer<ShipmentCreatedEvent>
{
    public Task Consume(ConsumeContext<ShipmentCreatedEvent> context) => processor.ProcessAsync(
        context.Message.EventId, context.Message.TenantId, context.Message.ShipmentId, "SHIPMENT_CREATED",
        "Shipment created", $"Shipment {context.Message.ShipmentNumber} was created.", context.Message.ShipmentNumber, context.Message.CreatedAt, context.CancellationToken);
}
