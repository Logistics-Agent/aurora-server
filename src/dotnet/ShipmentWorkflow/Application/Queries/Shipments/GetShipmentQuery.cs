using MediatR;
using Microsoft.EntityFrameworkCore;
using Shared.Exceptions;
using Shared.Security;
using ShipmentWorkflow.Application.DTOs.Shipments;
using ShipmentWorkflow.Infrastructure.Persistences;

namespace ShipmentWorkflow.Application.Queries.Shipments;

public sealed record GetShipmentQuery(Guid Id) : IRequest<ShipmentDto>;

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

        var shipment = await dbContext.Shipments
            .AsNoTracking()
            .Include(s => s.CargoItems)
            .Include(s => s.Locations)
            .Include(s => s.Documents)
            .Include(s => s.Milestones)
            .SingleOrDefaultAsync(s => s.Id == request.Id, cancellationToken);

        return shipment is null
            ? throw new NotFoundException("Shipment was not found.")
            : ShipmentDto.FromEntity(shipment);
    }
}
