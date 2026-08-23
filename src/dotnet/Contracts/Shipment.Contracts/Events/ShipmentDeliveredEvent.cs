namespace Shipment.Contracts.Events;

public sealed record ShipmentDeliveredEvent
{
    public Guid EventId { get; init; } = Guid.CreateVersion7();
    public int ContractVersion { get; init; } = 1;
    public Guid ShipmentId { get; init; }
    public Guid TenantId { get; init; }
    public string ShipmentNumber { get; init; } = string.Empty;
    public string CurrentStatus { get; init; } = string.Empty;
    public DateTimeOffset DeliveredAt { get; init; }
}
