using Shared.Entity;

namespace GpsTracking.Domain.Entities;

public sealed class GpsPosition : TenantAuditableEntity
{
    private GpsPosition() { }

    public string ExternalReadingId { get; private set; } = string.Empty;
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

    public static GpsPosition Create(
        Guid tenantId,
        string deviceId,
        string vehicleId,
        Guid? shipmentId,
        string externalReadingId,
        decimal latitude,
        decimal longitude,
        decimal? speedKph,
        decimal? headingDegrees,
        decimal? accuracyMeters,
        DateTimeOffset recordedAt,
        DateTimeOffset receivedAt)
    {
        GpsDomainValidation.RequiredId(tenantId, nameof(tenantId));
        if (shipmentId == Guid.Empty)
            throw new ArgumentException("ShipmentId cannot be empty.", nameof(shipmentId));
        if (recordedAt == default)
            throw new ArgumentException("RecordedAt is required.", nameof(recordedAt));
        if (receivedAt == default)
            throw new ArgumentException("ReceivedAt is required.", nameof(receivedAt));
        if (recordedAt > receivedAt.AddMinutes(5))
            throw new ArgumentOutOfRangeException(nameof(recordedAt), "RecordedAt cannot be more than five minutes in the future.");
        if (recordedAt < receivedAt.AddDays(-30))
            throw new ArgumentOutOfRangeException(nameof(recordedAt), "RecordedAt cannot be older than thirty days.");
        if (headingDegrees is < 0 or >= 360)
            throw new ArgumentOutOfRangeException(nameof(headingDegrees), "Heading must be at least 0 and less than 360 degrees.");

        return new GpsPosition
        {
            TenantId = tenantId,
            DeviceId = GpsDomainValidation.RequiredText(deviceId, nameof(deviceId), 100),
            VehicleId = GpsDomainValidation.RequiredText(vehicleId, nameof(vehicleId), 100),
            ShipmentId = shipmentId,
            ExternalReadingId = GpsDomainValidation.RequiredText(externalReadingId, nameof(externalReadingId), 150),
            Latitude = GpsDomainValidation.Latitude(latitude),
            Longitude = GpsDomainValidation.Longitude(longitude),
            SpeedKph = GpsDomainValidation.NonNegative(speedKph, nameof(speedKph)),
            HeadingDegrees = headingDegrees,
            AccuracyMeters = GpsDomainValidation.NonNegative(accuracyMeters, nameof(accuracyMeters)),
            RecordedAt = recordedAt,
            ReceivedAt = receivedAt,
            CreatedAt = receivedAt
        };
    }
}
