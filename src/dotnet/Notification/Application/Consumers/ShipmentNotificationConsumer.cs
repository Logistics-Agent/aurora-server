using MassTransit;
using Notification.Application.Services;
using Shipment.Contracts.Events;

namespace Notification.Application.Consumers;

public sealed class ShipmentNotificationConsumer(IShipmentNotificationProjector projector) :
    IConsumer<ShipmentCreatedEvent>,
    IConsumer<ShipmentStatusChangedEvent>,
    IConsumer<ShipmentCancelledEvent>
{
    public Task Consume(ConsumeContext<ShipmentCreatedEvent> context) =>
        projector.ProjectAsync(
            ShipmentEventNotificationFactory.Create(context.Message),
            context.CancellationToken);

    public Task Consume(ConsumeContext<ShipmentStatusChangedEvent> context) =>
        projector.ProjectAsync(
            ShipmentEventNotificationFactory.Create(context.Message),
            context.CancellationToken);

    public Task Consume(ConsumeContext<ShipmentCancelledEvent> context) =>
        projector.ProjectAsync(
            ShipmentEventNotificationFactory.Create(context.Message),
            context.CancellationToken);
}
