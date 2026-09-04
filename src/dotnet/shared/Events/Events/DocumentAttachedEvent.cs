namespace Shipment.Contracts.Events;

public sealed record DocumentAttachedEvent
{
    public Guid EventId { get; init; } = Guid.CreateVersion7();

    public int ContractVersion { get; init; } = 1;

    public Guid ShipmentId { get; init; }
    public Guid TenantId { get; init; }
    public Guid DocumentId { get; init; }
    public string DocumentType { get; init; } = string.Empty;
    public string FileName { get; init; } = string.Empty;
    public DateTimeOffset AttachedAt { get; init; }
}
