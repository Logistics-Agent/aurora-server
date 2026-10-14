using MediatR;
using Microsoft.EntityFrameworkCore;
using Shared.Exceptions;
using Shared.Security;
using ShipmentWorkflow.Application.DTOs.Shipments;
using ShipmentWorkflow.Infrastructure.Persistences;

namespace ShipmentWorkflow.Application.Queries.Shipments;

public sealed record GetShipmentQuery(Guid? Id = null, string? Identifier = null) : IRequest<ShipmentDto>
{
    public GetShipmentQuery(Guid id) : this(id, id.ToString()) { }
    public GetShipmentQuery(string identifier) : this(Guid.TryParse(identifier, out var g) ? g : null, identifier) { }
}

public sealed class GetShipmentQueryHandler(
    ShipmentWorkflowDbContext dbContext,
    ICurrentUserService currentUser) : IRequestHandler<GetShipmentQuery, ShipmentDto>
{
    public async Task<ShipmentDto> Handle(
        GetShipmentQuery request,
        CancellationToken cancellationToken)
    {
        if (!currentUser.TenantId.HasValue)
        {
            throw new DomainException("TenantId was not found in the authenticated user context.");
        }

        var query = dbContext.Shipments
            .AsNoTracking()
            .Include(s => s.CargoItems)
            .Include(s => s.Locations)
            .Include(s => s.Documents)
            .Include(s => s.Milestones);

        global::ShipmentWorkflow.Domain.Entities.Shipment? shipment = null;

        if (request.Id.HasValue && request.Id.Value != Guid.Empty)
        {
            shipment = await query.SingleOrDefaultAsync(s => s.Id == request.Id.Value, cancellationToken);
        }

        if (shipment is null && !string.IsNullOrWhiteSpace(request.Identifier))
        {
            shipment = await query.SingleOrDefaultAsync(
                s => s.ShipmentNo == request.Identifier.Trim() || s.OrderId == request.Identifier.Trim(),
                cancellationToken);
        }

        return shipment is null
            ? throw new NotFoundException("Shipment was not found.")
            : ShipmentDto.FromEntity(shipment);
    }
}
