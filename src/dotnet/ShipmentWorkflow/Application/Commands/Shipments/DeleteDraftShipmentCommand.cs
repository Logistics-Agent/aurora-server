using MediatR;
using Shared.Exceptions;
using Shared.Security;
using ShipmentWorkflow.Domain.Enums;
using ShipmentWorkflow.Infrastructure.Persistences;

namespace ShipmentWorkflow.Application.Commands.Shipments;

public sealed record DeleteDraftShipmentCommand(Guid ShipmentId) : IRequest;

public sealed class DeleteDraftShipmentCommandHandler(
    ShipmentWorkflowDbContext dbContext,
    ICurrentUserService currentUser) : IRequestHandler<DeleteDraftShipmentCommand>
{
    public async Task Handle(
        DeleteDraftShipmentCommand request,
        CancellationToken cancellationToken)
    {
        ShipmentCommandHelpers.RequireTenantId(currentUser);
        var shipment = await ShipmentCommandHelpers.GetShipmentAsync(dbContext, request.ShipmentId, cancellationToken);
        if (shipment.Status is not (ShipmentStatus.Draft or ShipmentStatus.Created))
        {
            throw new DomainException("Only draft shipments can be deleted.");
        }

        dbContext.Shipments.Remove(shipment);

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
