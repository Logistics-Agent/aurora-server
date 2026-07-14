using MediatR;
using Shared.Security;
using ShipmentWorkflow.Application.DTOs.Shipments;
using ShipmentWorkflow.Infrastructure.Persistences;

namespace ShipmentWorkflow.Application.Commands.Shipments;

public sealed record SubmitShipmentCommand(Guid ShipmentId) : IRequest<ShipmentDto>;

public sealed class SubmitShipmentCommandHandler(
    ShipmentWorkflowDbContext dbContext,
    ICurrentUserService currentUser) : IRequestHandler<SubmitShipmentCommand, ShipmentDto>
{
    public async Task<ShipmentDto> Handle(
        SubmitShipmentCommand request,
        CancellationToken cancellationToken)
    {
        ShipmentCommandHelpers.RequireTenantId(currentUser);
        var shipment = await ShipmentCommandHelpers.GetShipmentAsync(dbContext, request.ShipmentId, cancellationToken);
        var oldStatus = shipment.Status;
        shipment.Submit(currentUser.UserId);
        ShipmentCommandHelpers.AddStatusChangedOutbox(dbContext, shipment, oldStatus, "Shipment submitted.");
        await ShipmentCommandHelpers.PersistLifecycleStateAsync(dbContext, shipment, currentUser, cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);
        return ShipmentDto.FromEntity(shipment);
    }
}
