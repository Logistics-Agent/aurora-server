using MassTransit;
using Shipment.Contracts.Events;

namespace Notification.Infrastructure.Messaging.Consumers;

public sealed class ShipmentSubmittedConsumer(NotificationEventProcessor processor) : IConsumer<ShipmentSubmittedEvent>
{
    public Task Consume(ConsumeContext<ShipmentSubmittedEvent> context) => processor.ProcessAsync(
        context.Message.EventId, context.Message.TenantId, context.Message.ShipmentId,
        "SHIPMENT_SUBMITTED", "Shipment submitted",
        $"Shipment {context.Message.ShipmentNumber} was submitted for processing.",
        context.Message.ShipmentNumber, context.Message.SubmittedAt, context.CancellationToken);
}
