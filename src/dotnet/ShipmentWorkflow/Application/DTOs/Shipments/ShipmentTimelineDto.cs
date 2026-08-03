using ShipmentWorkflow.Domain.Enums;

namespace ShipmentWorkflow.Application.DTOs.Shipments;

public sealed record ShipmentTimelineDto(
    Guid ShipmentId,
    IReadOnlyCollection<ShipmentTimelineItemDto> Items);

public sealed record ShipmentTimelineItemDto(
    ShipmentStatus Status,
    string? Note,
    DateTimeOffset CreatedAt,
    string Source);
