using MediatR;
using Shared.Exceptions;
using Shared.Security;
using ShipmentWorkflow.Application.DTOs.Shipments;
using ShipmentWorkflow.Infrastructure.Persistences;

namespace ShipmentWorkflow.Application.Commands.Shipments;

public sealed record CancelShipmentCommand(Guid ShipmentId, string Reason) : IRequest<ShipmentDto>;

public sealed class CancelShipmentCommandHandler(
    ShipmentWorkflowDbContext dbContext,
    ICurrentUserService currentUser) : IRequestHandler<CancelShipmentCommand, ShipmentDto>
{
    public async Task<ShipmentDto> Handle(
        CancelShipmentCommand request,
        CancellationToken cancellationToken)
    {
        ShipmentCommandHelpers.RequireTenantId(currentUser);
        var shipment = await ShipmentCommandHelpers.GetShipmentAsync(dbContext, request.ShipmentId, cancellationToken);
        var oldStatus = shipment.Status;

        try
        {
            shipment.Cancel(request.Reason, currentUser.UserId);
        }
        catch (ArgumentException ex)
        {
            throw new DomainException(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            throw new DomainException(ex.Message);
        }

        ShipmentCommandHelpers.AddStatusChangedOutbox(dbContext, shipment, oldStatus, request.Reason);
        ShipmentCommandHelpers.AddCancelledOutbox(dbContext, shipment, request.Reason.Trim());
        await ShipmentCommandHelpers.PersistLifecycleStateAsync(dbContext, shipment, currentUser, cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);
        return ShipmentDto.FromEntity(shipment);
    }
}
