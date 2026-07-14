using ShipmentEntity =
    global::ShipmentWorkflow.Domain.Entities.Shipment;
using ShipmentWorkflow.Domain.Enums;

namespace ShipmentWorkflow.Application.DTOs.Shipments;

public sealed record ShipmentDto
{
    public Guid Id { get; init; }
    public Guid TenantId { get; init; }
    public string ShipmentNo { get; init; } = string.Empty;
    public string? OrderId { get; init; }
    public Guid? CustomerId { get; init; }
    public string CustomerName { get; init; } = string.Empty;
    public string DestinationAddress { get; init; } = string.Empty;
    public ShipmentStatus Status { get; init; }
    public ShipmentPriority Priority { get; init; }
    public TransportMode TransportMode { get; init; }
    public string? RouteId { get; init; }
    public string? VehicleId { get; init; }
    public DateTimeOffset? EstimatedPickupTime { get; init; }
    public DateTimeOffset? EstimatedDeliveryTime { get; init; }
    public DateTimeOffset? ActualPickupTime { get; init; }
    public DateTimeOffset? ActualDeliveryTime { get; init; }
    public string? Notes { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? UpdatedAt { get; init; }
    public IReadOnlyCollection<CargoItemDto> CargoItems { get; init; } = [];
    public IReadOnlyCollection<ShipmentLocationDto> Locations { get; init; } = [];
    public IReadOnlyCollection<ShipmentDocumentDto> Documents { get; init; } = [];
    public IReadOnlyCollection<ShipmentMilestoneDto> Milestones { get; init; } = [];

    public static ShipmentDto FromEntity(ShipmentEntity shipment)
    {
        return new ShipmentDto
        {
            Id = shipment.Id,
            TenantId = shipment.TenantId,
            ShipmentNo = shipment.ShipmentNo,
            OrderId = shipment.OrderId,
            CustomerId = shipment.CustomerId,
            CustomerName = shipment.CustomerName,
            DestinationAddress = shipment.DestinationAddress,
            Status = shipment.Status,
            Priority = shipment.Priority,
            TransportMode = shipment.TransportMode,
            RouteId = shipment.RouteId,
            VehicleId = shipment.VehicleId,
            EstimatedPickupTime = shipment.EstimatedPickupTime,
            EstimatedDeliveryTime = shipment.EstimatedDeliveryTime,
            ActualPickupTime = shipment.ActualPickupTime,
            ActualDeliveryTime = shipment.ActualDeliveryTime,
            Notes = shipment.Notes,
            CreatedAt = shipment.CreatedAt,
            UpdatedAt = shipment.UpdatedAt,
            CargoItems = shipment.CargoItems
                .Select(CargoItemDto.FromEntity)
                .ToArray(),
            Locations = shipment.Locations
                .OrderBy(location => location.Sequence)
                .Select(ShipmentLocationDto.FromEntity)
                .ToArray(),
            Documents = shipment.Documents
                .OrderBy(document => document.UploadedAt)
                .Select(ShipmentDocumentDto.FromEntity)
                .ToArray(),
            Milestones = shipment.Milestones
                .OrderBy(milestone => milestone.RecordedAt)
                .Select(ShipmentMilestoneDto.FromEntity)
                .ToArray()
        };
    }
}
