namespace Shipment.Contracts.Events;

public sealed record ShipmentCancelledEvent
{
    public Guid ShipmentId { get; init; }

    public Guid TenantId { get; init; }

    public string? Reason { get; init; }

    public DateTimeOffset CancelledAt { get; init; }
}