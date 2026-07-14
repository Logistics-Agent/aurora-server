using ShipmentWorkflow.Domain.Entities;
using ShipmentWorkflow.Domain.Enums;

namespace ShipmentWorkflow.Application.DTOs.Shipments;

public sealed record ShipmentMilestoneDto
{
    public Guid Id { get; init; }
    public ShipmentStatus Status { get; init; }
    public string? Description { get; init; }
    public double? Latitude { get; init; }
    public double? Longitude { get; init; }
    public DateTimeOffset RecordedAt { get; init; }
    public MilestoneSource Source { get; init; }
    public Guid? CreatedByUserId { get; init; }

    public static ShipmentMilestoneDto FromEntity(ShipmentMilestone milestone)
    {
        return new ShipmentMilestoneDto
        {
            Id = milestone.Id,
            Status = milestone.Status,
            Description = milestone.Description,
            Latitude = milestone.Latitude,
            Longitude = milestone.Longitude,
            RecordedAt = milestone.RecordedAt,
            Source = milestone.Source,
            CreatedByUserId = milestone.CreatedByUserId
        };
    }
}
