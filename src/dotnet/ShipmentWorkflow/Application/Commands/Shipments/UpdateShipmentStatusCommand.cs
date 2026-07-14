using MediatR;
using Shared.Exceptions;
using Shared.Security;
using ShipmentWorkflow.Application.DTOs.Shipments;
using ShipmentWorkflow.Domain.Enums;
using ShipmentWorkflow.Infrastructure.Persistences;

namespace ShipmentWorkflow.Application.Commands.Shipments;

public sealed record UpdateShipmentStatusCommand(
    Guid ShipmentId,
    ShipmentStatus Status,
    string? Note) : IRequest<ShipmentDto>;

public sealed class UpdateShipmentStatusCommandHandler(
    ShipmentWorkflowDbContext dbContext,
    ICurrentUserService currentUser) : IRequestHandler<UpdateShipmentStatusCommand, ShipmentDto>
{
    public async Task<ShipmentDto> Handle(
        UpdateShipmentStatusCommand request,
        CancellationToken cancellationToken)
    {
        ShipmentCommandHelpers.RequireTenantId(currentUser);
        var shipment = await ShipmentCommandHelpers.GetShipmentAsync(dbContext, request.ShipmentId, cancellationToken);
        var oldStatus = shipment.Status;

        try
        {
            ShipmentCommandHelpers.ApplyStatusTransition(shipment, request.Status, currentUser.UserId, request.Note);
        }
        catch (InvalidOperationException ex)
        {
            throw new DomainException(ex.Message);
        }

        ShipmentCommandHelpers.AddStatusChangedOutbox(dbContext, shipment, oldStatus, request.Note);
        ShipmentCommandHelpers.AddLifecycleOutbox(dbContext, shipment);
        await ShipmentCommandHelpers.PersistLifecycleStateAsync(dbContext, shipment, currentUser, cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);
        return ShipmentDto.FromEntity(shipment);
    }
}
