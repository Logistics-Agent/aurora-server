using MediatR;
using Microsoft.EntityFrameworkCore;
using Shared.Exceptions;
using Shared.Security;
using ShipmentWorkflow.Application.DTOs.Shipments;
using ShipmentWorkflow.Infrastructure.Persistences;

namespace ShipmentWorkflow.Application.Queries.Shipments;

public sealed record GetShipmentTimelineQuery(Guid? ShipmentId = null, string? Identifier = null) : IRequest<ShipmentTimelineDto>
{
    public GetShipmentTimelineQuery(Guid shipmentId) : this(shipmentId, shipmentId.ToString()) { }
    public GetShipmentTimelineQuery(string identifier) : this(Guid.TryParse(identifier, out var g) ? g : null, identifier) { }
}

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

        global::ShipmentWorkflow.Domain.Entities.Shipment? shipment = null;

        if (request.ShipmentId.HasValue && request.ShipmentId.Value != Guid.Empty)
        {
            shipment = await dbContext.Shipments
                .AsNoTracking()
                .SingleOrDefaultAsync(s => s.Id == request.ShipmentId.Value, cancellationToken);
        }

        if (shipment is null && !string.IsNullOrWhiteSpace(request.Identifier))
        {
            shipment = await dbContext.Shipments
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    s => s.ShipmentNo == request.Identifier.Trim() || s.OrderId == request.Identifier.Trim(),
                    cancellationToken);
        }

        if (shipment is null)
        {
            throw new NotFoundException("Shipment was not found.");
        }

        var targetShipmentId = shipment.Id;

        var histories = await dbContext.ShipmentStatusHistories
            .AsNoTracking()
            .Where(history => history.ShipmentId == targetShipmentId)
            .Select(history => new ShipmentTimelineItemDto(
                history.Status,
                history.Note,
                history.CreatedAt,
                "status-history"))
            .ToListAsync(cancellationToken);

        var milestones = await dbContext.ShipmentMilestones
            .AsNoTracking()
            .Where(milestone => milestone.ShipmentId == targetShipmentId)
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

        return new ShipmentTimelineDto(targetShipmentId, items);
    }
}
