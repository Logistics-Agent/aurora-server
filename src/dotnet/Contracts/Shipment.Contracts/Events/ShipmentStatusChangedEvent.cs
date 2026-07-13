namespace Shipment.Contracts.Events;

public sealed record ShipmentStatusChangedEvent
{
    public Guid ShipmentId { get; init; }

    public Guid TenantId { get; init; }

    public string OldStatus { get; init; } = string.Empty;

    public string NewStatus { get; init; } = string.Empty;

    public string? Note { get; init; }

    public DateTimeOffset ChangedAt { get; init; }
}