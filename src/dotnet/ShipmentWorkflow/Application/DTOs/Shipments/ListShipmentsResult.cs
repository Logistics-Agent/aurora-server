namespace ShipmentWorkflow.Application.DTOs.Shipments;

public sealed record ListShipmentsResult(
    IReadOnlyCollection<ShipmentDto> Shipments,
    int Page,
    int Limit,
    int TotalItems,
    int TotalPages);
