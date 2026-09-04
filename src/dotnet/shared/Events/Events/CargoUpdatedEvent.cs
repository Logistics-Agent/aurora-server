namespace Shipment.Contracts.Events;

public sealed record CargoUpdatedEvent
{
    public Guid EventId { get; init; } = Guid.CreateVersion7();

    public int ContractVersion { get; init; } = 1;

    public Guid ShipmentId { get; init; }
    public Guid TenantId { get; init; }
    public Guid CargoItemId { get; init; }
    public string Action { get; init; } = string.Empty;
    public DateTimeOffset UpdatedAt { get; init; }
}
