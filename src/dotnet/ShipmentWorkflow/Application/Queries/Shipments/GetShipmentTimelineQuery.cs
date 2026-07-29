using MediatR;
using Microsoft.EntityFrameworkCore;
using Shared.Exceptions;
using Shared.Security;
using ShipmentWorkflow.Application.DTOs.Shipments;
using ShipmentWorkflow.Infrastructure.Persistences;

namespace ShipmentWorkflow.Application.Queries.Shipments;

public sealed record GetShipmentTimelineQuery(Guid ShipmentId) : IRequest<ShipmentTimelineDto>;

public sealed class GetShipmentTimelineQueryHandler(
    ShipmentWorkflowDbContext dbContext,
    ICurrentUserService currentUser) : IRequestHandler<GetShipmentTimelineQuery, ShipmentTimelineDto>
{
    public async Task<ShipmentTimelineDto> Handle(
        GetShipmentTimelineQuery request,
        CancellationToken cancellationToken)
    {
        if (!currentUser.TenantId.HasValue)
        {
            throw new DomainException("TenantId was not found in the authenticated user context.");
        }

        var exists = await dbContext.Shipments
            .AsNoTracking()
            .AnyAsync(shipment => shipment.Id == request.ShipmentId, cancellationToken);

        if (!exists)
        {
            throw new NotFoundException("Shipment was not found.");
        }

        var histories = await dbContext.ShipmentStatusHistories
            .AsNoTracking()
            .Where(history => history.ShipmentId == request.ShipmentId)
            .Select(history => new ShipmentTimelineItemDto(
                history.Status,
                history.Note,
                history.CreatedAt,
                "status-history"))
            .ToListAsync(cancellationToken);

        var milestones = await dbContext.ShipmentMilestones
            .AsNoTracking()
            .Where(milestone => milestone.ShipmentId == request.ShipmentId)
            .Select(milestone => new ShipmentTimelineItemDto(
                milestone.Status,
                milestone.Description,
                milestone.RecordedAt,
                milestone.Source.ToString()))
            .ToListAsync(cancellationToken);

        var items = histories
            .Concat(milestones)
            .OrderBy(item => item.CreatedAt)
            .ThenBy(item => item.Source, StringComparer.Ordinal)
            .ToArray();

        return new ShipmentTimelineDto(request.ShipmentId, items);
    }
}
