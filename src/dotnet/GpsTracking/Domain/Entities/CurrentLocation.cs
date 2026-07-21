using Shared.Entity;

namespace GpsTracking.Domain.Entities;

public sealed class CurrentLocation : TenantAuditableEntity
{
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

    public static CurrentLocation FromPosition(
        GpsPosition position,
        decimal stationarySpeedKph = 1)
    {
        ArgumentNullException.ThrowIfNull(position);
        var current = new CurrentLocation
        {
            TenantId = position.TenantId,
            VehicleId = position.VehicleId,
            CreatedAt = position.ReceivedAt
        };
        current.Copy(position, stationarySpeedKph);
        return current;
    }

    public bool Apply(GpsPosition position, decimal stationarySpeedKph = 1)
    {
        ArgumentNullException.ThrowIfNull(position);
        if (position.TenantId != TenantId || position.VehicleId != VehicleId)
            throw new InvalidOperationException("Position does not belong to this current-location snapshot.");
        if (position.RecordedAt < RecordedAt ||
            (position.RecordedAt == RecordedAt && position.Id.CompareTo(PositionId) <= 0))
            return false;

        Copy(position, stationarySpeedKph);
        UpdatedAt = position.ReceivedAt;
        return true;
    }

    private void Copy(GpsPosition position, decimal stationarySpeedKph)
    {
        if (stationarySpeedKph < 0)
            throw new ArgumentOutOfRangeException(nameof(stationarySpeedKph));
        var wasStationary = SpeedKph is not null && SpeedKph <= stationarySpeedKph;
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
        StationarySince = position.SpeedKph is not null && position.SpeedKph <= stationarySpeedKph
            ? wasStationary && StationarySince.HasValue ? StationarySince : position.RecordedAt
            : null;
    }
}
