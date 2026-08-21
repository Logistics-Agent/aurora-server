using GpsTracking.Application.Shipments;
using MassTransit;
using Shipment.Contracts.Events;

namespace GpsTracking.Application.Consumers;

public sealed class ShipmentTrackingConsumer(IShipmentAssignmentProjector projector) :
    IConsumer<RouteAssignedEvent>,
    IConsumer<ShipmentCancelledEvent>,
    IConsumer<ShipmentCompletedEvent>
{
    public Task Consume(ConsumeContext<RouteAssignedEvent> context) =>
        projector.ProjectAsync(context.Message, context.CancellationToken);

    public Task Consume(ConsumeContext<ShipmentCancelledEvent> context) =>
        projector.ProjectAsync(context.Message, context.CancellationToken);

    public Task Consume(ConsumeContext<ShipmentCompletedEvent> context) =>
        projector.ProjectAsync(context.Message, context.CancellationToken);
}
