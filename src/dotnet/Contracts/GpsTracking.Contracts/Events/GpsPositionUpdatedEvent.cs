namespace GpsTracking.Contracts.Events;

public sealed record GpsPositionUpdatedEvent
{
    public Guid EventId { get; init; } = Guid.CreateVersion7();
    public int ContractVersion { get; init; } = 1;
    public Guid TenantId { get; init; }
    public Guid PositionId { get; init; }
    public string DeviceId { get; init; } = string.Empty;
    public string VehicleId { get; init; } = string.Empty;
    public Guid? ShipmentId { get; init; }
    public decimal Latitude { get; init; }
    public decimal Longitude { get; init; }
    public decimal? SpeedKph { get; init; }
    public decimal? HeadingDegrees { get; init; }
    public DateTimeOffset RecordedAt { get; init; }
}
