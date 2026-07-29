using MediatR;
using Microsoft.EntityFrameworkCore;
using Shared.Exceptions;
using Shared.Security;
using ShipmentWorkflow.Application.DTOs.Shipments;
using ShipmentWorkflow.Infrastructure.Persistences;

namespace ShipmentWorkflow.Application.Commands.Shipments;

public sealed record AddCargoItemCommand(
    Guid ShipmentId,
    string Name,
    int Quantity,
    double WeightKg,
    string? HsCode) : IRequest<ShipmentDto>;

public sealed record UpdateCargoItemCommand(
    Guid ShipmentId,
    Guid CargoItemId,
    string Name,
    int Quantity,
    double WeightKg,
    string? HsCode) : IRequest<ShipmentDto>;

public sealed record RemoveCargoItemCommand(Guid ShipmentId, Guid CargoItemId) : IRequest<ShipmentDto>;

public sealed class AddCargoItemCommandHandler(
    ShipmentWorkflowDbContext dbContext,
    ICurrentUserService currentUser) : IRequestHandler<AddCargoItemCommand, ShipmentDto>
{
    public async Task<ShipmentDto> Handle(AddCargoItemCommand request, CancellationToken cancellationToken)
    {
        ShipmentCommandHelpers.RequireTenantId(currentUser);
        var shipment = await ShipmentCommandHelpers.GetShipmentAsync(dbContext, request.ShipmentId, cancellationToken);
        ShipmentCommandHelpers.EnsurePreOperationalMutation(shipment);

        shipment.AddCargoItem(request.Name, request.Quantity, request.WeightKg, request.HsCode);
        var cargoItem = shipment.CargoItems.Last();
        dbContext.Entry(cargoItem).State = EntityState.Added;
        ShipmentCommandHelpers.AddCargoUpdatedOutbox(dbContext, shipment, cargoItem.Id, "Added");

        ShipmentCommandHelpers.MarkAggregateRootUnchanged(dbContext, shipment);
        await dbContext.SaveChangesAsync(cancellationToken);
        return ShipmentDto.FromEntity(shipment);
    }
}

public sealed class UpdateCargoItemCommandHandler(
    ShipmentWorkflowDbContext dbContext,
    ICurrentUserService currentUser) : IRequestHandler<UpdateCargoItemCommand, ShipmentDto>
{
    public async Task<ShipmentDto> Handle(UpdateCargoItemCommand request, CancellationToken cancellationToken)
    {
        ShipmentCommandHelpers.RequireTenantId(currentUser);
        var shipment = await ShipmentCommandHelpers.GetShipmentAsync(dbContext, request.ShipmentId, cancellationToken);
        ShipmentCommandHelpers.EnsurePreOperationalMutation(shipment);

        try
        {
            shipment.UpdateCargoItem(request.CargoItemId, request.Name, request.Quantity, request.WeightKg, request.HsCode);
        }
        catch (InvalidOperationException ex)
        {
            throw new NotFoundException(ex.Message);
        }

        ShipmentCommandHelpers.AddCargoUpdatedOutbox(dbContext, shipment, request.CargoItemId, "Updated");

        ShipmentCommandHelpers.MarkAggregateRootUnchanged(dbContext, shipment);
        await dbContext.SaveChangesAsync(cancellationToken);
        return ShipmentDto.FromEntity(shipment);
    }
}

public sealed class RemoveCargoItemCommandHandler(
    ShipmentWorkflowDbContext dbContext,
    ICurrentUserService currentUser) : IRequestHandler<RemoveCargoItemCommand, ShipmentDto>
{
    public async Task<ShipmentDto> Handle(RemoveCargoItemCommand request, CancellationToken cancellationToken)
    {
        ShipmentCommandHelpers.RequireTenantId(currentUser);
        var shipment = await ShipmentCommandHelpers.GetShipmentAsync(dbContext, request.ShipmentId, cancellationToken);
        ShipmentCommandHelpers.EnsurePreOperationalMutation(shipment);

        try
        {
            shipment.RemoveCargoItem(request.CargoItemId);
        }
        catch (InvalidOperationException ex)
        {
            throw new NotFoundException(ex.Message);
        }

        ShipmentCommandHelpers.AddCargoUpdatedOutbox(dbContext, shipment, request.CargoItemId, "Removed");

        ShipmentCommandHelpers.MarkAggregateRootUnchanged(dbContext, shipment);
        await dbContext.SaveChangesAsync(cancellationToken);
        return ShipmentDto.FromEntity(shipment);
    }
}
