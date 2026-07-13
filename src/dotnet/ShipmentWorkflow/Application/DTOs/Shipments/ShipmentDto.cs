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
    public string CustomerName { get; init; } = string.Empty;
    public string DestinationAddress { get; init; } = string.Empty;
    public ShipmentStatus Status { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? UpdatedAt { get; init; }

    public static ShipmentDto FromEntity(ShipmentEntity shipment)
    {
        return new ShipmentDto
        {
            Id = shipment.Id,
            TenantId = shipment.TenantId,
            ShipmentNo = shipment.ShipmentNo,
            OrderId = shipment.OrderId,
            CustomerName = shipment.CustomerName,
            DestinationAddress = shipment.DestinationAddress,
            Status = shipment.Status,
            CreatedAt = shipment.CreatedAt,
            UpdatedAt = shipment.UpdatedAt
        };
    }
}