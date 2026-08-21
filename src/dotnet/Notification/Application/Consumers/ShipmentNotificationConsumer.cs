using MassTransit;
using Notification.Application.Services;
using Shipment.Contracts.Events;

namespace Notification.Application.Consumers;

public sealed class ShipmentNotificationConsumer(IIntegrationEventNotificationProjector projector) :
    IConsumer<ShipmentCreatedEvent>,
    IConsumer<ShipmentSubmittedEvent>,
    IConsumer<ShipmentStatusChangedEvent>,
    IConsumer<ShipmentCancelledEvent>,
    IConsumer<ShipmentPickedUpEvent>,
    IConsumer<ShipmentDeliveredEvent>,
    IConsumer<ShipmentCompletedEvent>,
    IConsumer<DocumentAttachedEvent>
{
    public Task Consume(ConsumeContext<ShipmentCreatedEvent> context) =>
        projector.ProjectAsync(
            ShipmentEventNotificationFactory.Create(context.Message),
            context.CancellationToken);

    public Task Consume(ConsumeContext<ShipmentSubmittedEvent> context) =>
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

    public Task Consume(ConsumeContext<ShipmentPickedUpEvent> context) =>
        projector.ProjectAsync(
            ShipmentEventNotificationFactory.Create(context.Message),
            context.CancellationToken);

    public Task Consume(ConsumeContext<ShipmentDeliveredEvent> context) =>
        projector.ProjectAsync(
            ShipmentEventNotificationFactory.Create(context.Message),
            context.CancellationToken);

    public Task Consume(ConsumeContext<ShipmentCompletedEvent> context) =>
        projector.ProjectAsync(
            ShipmentEventNotificationFactory.Create(context.Message),
            context.CancellationToken);

    public Task Consume(ConsumeContext<DocumentAttachedEvent> context) =>
        projector.ProjectAsync(
            ShipmentEventNotificationFactory.Create(context.Message),
            context.CancellationToken);
}
