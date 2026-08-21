namespace GpsTracking.Contracts.Events;

public sealed record GpsMonitoringAlertRaisedEvent
{
    public Guid EventId { get; init; } = Guid.CreateVersion7();
    public int ContractVersion { get; init; } = 1;
    public Guid TenantId { get; init; }
    public Guid AlertId { get; init; }
    public string AlertType { get; init; } = string.Empty;
    public string VehicleId { get; init; } = string.Empty;
    public Guid? ShipmentId { get; init; }
    public Guid? GeofenceId { get; init; }
    public Guid? PositionId { get; init; }
    public string Message { get; init; } = string.Empty;
    public DateTimeOffset OccurredAt { get; init; }
}
