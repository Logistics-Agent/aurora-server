using MediatR;
using Microsoft.EntityFrameworkCore;
using Shared.Exceptions;
using Shared.Security;
using ShipmentWorkflow.Application.DTOs.Shipments;
using ShipmentWorkflow.Domain.Enums;
using ShipmentWorkflow.Infrastructure.Persistences;
using ShipmentEntity = global::ShipmentWorkflow.Domain.Entities.Shipment;

namespace ShipmentWorkflow.Application.Commands.Shipments;

public sealed record AddShipmentLocationCommand(
    Guid ShipmentId,
    LocationType Type,
    string Name,
    string Address,
    int Sequence,
    double? Latitude,
    double? Longitude,
    string? ContactName,
    string? ContactPhone) : IRequest<ShipmentDto>;

public sealed record UpdateShipmentLocationCommand(
    Guid ShipmentId,
    Guid LocationId,
    LocationType Type,
    string Name,
    string Address,
    int Sequence,
    double? Latitude,
    double? Longitude,
    string? ContactName,
    string? ContactPhone) : IRequest<ShipmentDto>;

public sealed record RemoveShipmentLocationCommand(Guid ShipmentId, Guid LocationId) : IRequest<ShipmentDto>;

public sealed class AddShipmentLocationCommandHandler(
    ShipmentWorkflowDbContext dbContext,
    ICurrentUserService currentUser) : IRequestHandler<AddShipmentLocationCommand, ShipmentDto>
{
    public async Task<ShipmentDto> Handle(AddShipmentLocationCommand request, CancellationToken cancellationToken)
    {
        ShipmentCommandHelpers.RequireTenantId(currentUser);
        var shipment = await ShipmentCommandHelpers.GetShipmentAsync(dbContext, request.ShipmentId, cancellationToken);
        ShipmentCommandHelpers.EnsurePreOperationalMutation(shipment);
        EnsureSequenceAvailable(shipment, request.Sequence);

        shipment.AddLocation(request.Type, request.Name, request.Address, request.Sequence, request.Latitude, request.Longitude, request.ContactName, request.ContactPhone);
        dbContext.Entry(shipment.Locations.Last()).State = EntityState.Added;

        ShipmentCommandHelpers.MarkAggregateRootUnchanged(dbContext, shipment);
        await dbContext.SaveChangesAsync(cancellationToken);
        return ShipmentDto.FromEntity(shipment);
    }

    private static void EnsureSequenceAvailable(ShipmentEntity shipment, int sequence)
    {
        if (shipment.Locations.Any(location => location.Sequence == sequence))
        {
            throw new DomainException("Location sequence already exists for this shipment.");
        }
    }
}

public sealed class UpdateShipmentLocationCommandHandler(
    ShipmentWorkflowDbContext dbContext,
    ICurrentUserService currentUser) : IRequestHandler<UpdateShipmentLocationCommand, ShipmentDto>
{
    public async Task<ShipmentDto> Handle(UpdateShipmentLocationCommand request, CancellationToken cancellationToken)
    {
        ShipmentCommandHelpers.RequireTenantId(currentUser);
        var shipment = await ShipmentCommandHelpers.GetShipmentAsync(dbContext, request.ShipmentId, cancellationToken);
        ShipmentCommandHelpers.EnsurePreOperationalMutation(shipment);
        if (shipment.Locations.Any(location => location.Id != request.LocationId && location.Sequence == request.Sequence))
        {
            throw new DomainException("Location sequence already exists for this shipment.");
        }

        var location = shipment.Locations.SingleOrDefault(location => location.Id == request.LocationId)
            ?? throw new NotFoundException("Shipment location was not found.");

        location.Update(request.Type, request.Name, request.Address, request.Sequence, request.Latitude, request.Longitude, request.ContactName, request.ContactPhone);

        ShipmentCommandHelpers.MarkAggregateRootUnchanged(dbContext, shipment);
        await dbContext.SaveChangesAsync(cancellationToken);
        return ShipmentDto.FromEntity(shipment);
    }
}

public sealed class RemoveShipmentLocationCommandHandler(
    ShipmentWorkflowDbContext dbContext,
    ICurrentUserService currentUser) : IRequestHandler<RemoveShipmentLocationCommand, ShipmentDto>
{
    public async Task<ShipmentDto> Handle(RemoveShipmentLocationCommand request, CancellationToken cancellationToken)
    {
        ShipmentCommandHelpers.RequireTenantId(currentUser);
        var shipment = await ShipmentCommandHelpers.GetShipmentAsync(dbContext, request.ShipmentId, cancellationToken);
        ShipmentCommandHelpers.EnsurePreOperationalMutation(shipment);

        var location = shipment.Locations.SingleOrDefault(location => location.Id == request.LocationId)
            ?? throw new NotFoundException("Shipment location was not found.");

        shipment.Locations.Remove(location);

        ShipmentCommandHelpers.MarkAggregateRootUnchanged(dbContext, shipment);
        await dbContext.SaveChangesAsync(cancellationToken);
        return ShipmentDto.FromEntity(shipment);
    }
}
