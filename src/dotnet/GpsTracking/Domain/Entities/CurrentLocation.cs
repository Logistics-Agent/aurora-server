using Shared.Entity;

namespace GpsTracking.Domain.Entities;

public sealed class CurrentLocation : TenantAuditableEntity
{
    private const decimal StationarySpeedKph = 1;

    private CurrentLocation() { }

    public Guid PositionId { get; private set; }
    public string DeviceId { get; private set; } = string.Empty;
    public string VehicleId { get; private set; } = string.Empty;
    public Guid? ShipmentId { get; private set; }
    public decimal Latitude { get; private set; }
    public decimal Longitude { get; private set; }
    public decimal? SpeedKph { get; private set; }
    public decimal? HeadingDegrees { get; private set; }
    public decimal? AccuracyMeters { get; private set; }
    public DateTimeOffset RecordedAt { get; private set; }
    public DateTimeOffset ReceivedAt { get; private set; }
    public DateTimeOffset? StationarySince { get; private set; }

    public static CurrentLocation FromPosition(GpsPosition position)
    {
        ArgumentNullException.ThrowIfNull(position);
        var current = new CurrentLocation
        {
            TenantId = position.TenantId,
            VehicleId = position.VehicleId,
            CreatedAt = position.ReceivedAt
        };
        current.Copy(position);
        return current;
    }

    public bool Apply(GpsPosition position)
    {
        ArgumentNullException.ThrowIfNull(position);
        if (position.TenantId != TenantId || position.VehicleId != VehicleId)
            throw new InvalidOperationException("Position does not belong to this current-location snapshot.");
        if (position.RecordedAt < RecordedAt ||
            (position.RecordedAt == RecordedAt && position.Id.CompareTo(PositionId) <= 0))
            return false;

        Copy(position);
        UpdatedAt = position.ReceivedAt;
        return true;
    }

    private void Copy(GpsPosition position)
    {
        var wasStationary = SpeedKph is <= StationarySpeedKph;
        PositionId = position.Id;
        DeviceId = position.DeviceId;
        ShipmentId = position.ShipmentId;
        Latitude = position.Latitude;
        Longitude = position.Longitude;
        SpeedKph = position.SpeedKph;
        HeadingDegrees = position.HeadingDegrees;
        AccuracyMeters = position.AccuracyMeters;
        RecordedAt = position.RecordedAt;
        ReceivedAt = position.ReceivedAt;
        StationarySince = position.SpeedKph is <= StationarySpeedKph
            ? wasStationary && StationarySince.HasValue ? StationarySince : position.RecordedAt
            : null;
    }
}
