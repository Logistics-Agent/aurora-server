using MassTransit;
using Notification.Infrastructure.Messaging;
using Shipment.Contracts.Events;

namespace Notification.Infrastructure.Messaging.Consumers;

public sealed class ShipmentCancelledConsumer(NotificationEventProcessor processor) : IConsumer<ShipmentCancelledEvent>
{
    public Task Consume(ConsumeContext<ShipmentCancelledEvent> context) => processor.ProcessAsync(
        context.Message.EventId, context.Message.TenantId, context.Message.ShipmentId, "SHIPMENT_CANCELLED",
        "Shipment cancelled", context.Message.Reason ?? "The shipment was cancelled.", null, context.Message.CancelledAt, context.CancellationToken);
}
