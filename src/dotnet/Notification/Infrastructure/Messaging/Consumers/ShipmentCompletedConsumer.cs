using MassTransit;
using Shipment.Contracts.Events;

namespace Notification.Infrastructure.Messaging.Consumers;

public sealed class ShipmentCompletedConsumer(NotificationEventProcessor processor) : IConsumer<ShipmentCompletedEvent>
{
    public Task Consume(ConsumeContext<ShipmentCompletedEvent> context) => processor.ProcessAsync(
        context.Message.EventId, context.Message.TenantId, context.Message.ShipmentId,
        "SHIPMENT_COMPLETED", "Shipment completed",
        $"Shipment {context.Message.ShipmentNumber} was completed.",
        context.Message.ShipmentNumber, context.Message.CompletedAt, context.CancellationToken);
}
