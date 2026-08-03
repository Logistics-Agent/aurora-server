using MediatR;
using Shared.Exceptions;
using Shared.Security;
using ShipmentWorkflow.Application.DTOs.Shipments;
using ShipmentWorkflow.Domain.Enums;
using ShipmentWorkflow.Infrastructure.Persistences;

namespace ShipmentWorkflow.Application.Commands.Shipments;

public sealed record UpdateShipmentCommand(
    Guid ShipmentId,
    string CustomerName,
    string DestinationAddress,
    ShipmentPriority Priority,
    TransportMode TransportMode,
    string? Notes) : IRequest<ShipmentDto>;

public sealed class UpdateShipmentCommandHandler(
    ShipmentWorkflowDbContext dbContext,
    ICurrentUserService currentUser) : IRequestHandler<UpdateShipmentCommand, ShipmentDto>
{
    public async Task<ShipmentDto> Handle(
        UpdateShipmentCommand request,
        CancellationToken cancellationToken)
    {
        ShipmentCommandHelpers.RequireTenantId(currentUser);
        ValidateRequest(request);
        var shipment = await ShipmentCommandHelpers.GetShipmentAsync(dbContext, request.ShipmentId, cancellationToken);
        if (shipment.Status is not (ShipmentStatus.Draft or ShipmentStatus.Created or ShipmentStatus.Submitted))
        {
            throw new DomainException("Shipment can only be updated before operational processing starts.");
        }

        shipment.CustomerName = request.CustomerName.Trim();
        shipment.DestinationAddress = request.DestinationAddress.Trim();
        shipment.Priority = request.Priority;
        shipment.TransportMode = request.TransportMode;
        shipment.Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();

        ShipmentCommandHelpers.AddShipmentUpdatedOutbox(
            dbContext,
            shipment,
            [nameof(shipment.CustomerName), nameof(shipment.DestinationAddress), nameof(shipment.Priority), nameof(shipment.TransportMode), nameof(shipment.Notes)]);

        await dbContext.SaveChangesAsync(cancellationToken);
        return ShipmentDto.FromEntity(shipment);
    }

    private static void ValidateRequest(UpdateShipmentCommand request)
    {
        if (string.IsNullOrWhiteSpace(request.CustomerName))
        {
            throw new DomainException("Customer name is required.");
        }

        if (string.IsNullOrWhiteSpace(request.DestinationAddress))
        {
            throw new DomainException("Destination address is required.");
        }
    }
}
