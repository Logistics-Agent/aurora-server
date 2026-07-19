using MediatR;
using Microsoft.EntityFrameworkCore;
using Shared.Security;
using ShipmentWorkflow.Application.DTOs.Shipments;
using ShipmentWorkflow.Domain.Enums;
using ShipmentWorkflow.Infrastructure.Persistences;

namespace ShipmentWorkflow.Application.Commands.Shipments;

public sealed record AddShipmentMilestoneCommand(
    Guid ShipmentId,
    ShipmentStatus Status,
    string? Description,
    DateTimeOffset RecordedAt,
    MilestoneSource Source,
    double? Latitude,
    double? Longitude) : IRequest<ShipmentDto>;

public sealed class AddShipmentMilestoneCommandHandler(
    ShipmentWorkflowDbContext dbContext,
    ICurrentUserService currentUser) : IRequestHandler<AddShipmentMilestoneCommand, ShipmentDto>
{
    public async Task<ShipmentDto> Handle(AddShipmentMilestoneCommand request, CancellationToken cancellationToken)
    {
        ShipmentCommandHelpers.RequireTenantId(currentUser);
        var shipment = await ShipmentCommandHelpers.GetShipmentAsync(dbContext, request.ShipmentId, cancellationToken);
        ShipmentCommandHelpers.EnsureNonTerminalMutation(shipment);

        shipment.AddMilestone(
            request.Status,
            request.Description,
            request.RecordedAt,
            request.Source,
            currentUser.UserId,
            request.Latitude,
            request.Longitude);

        dbContext.Entry(shipment.Milestones.Last()).State = EntityState.Added;
        ShipmentCommandHelpers.MarkAggregateRootUnchanged(dbContext, shipment);

        await dbContext.SaveChangesAsync(cancellationToken);
        return ShipmentDto.FromEntity(shipment);
    }
}
