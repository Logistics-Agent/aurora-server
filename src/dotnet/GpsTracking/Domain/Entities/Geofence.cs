using Shared.Entity;

namespace GpsTracking.Domain.Entities;

public sealed class Geofence : TenantAuditableEntity
{
    public const decimal MaximumRadiusMeters = 100_000;

    private Geofence() { }

    public string Name { get; private set; } = string.Empty;
    public decimal Latitude { get; private set; }
    public decimal Longitude { get; private set; }
    public decimal RadiusMeters { get; private set; }
    public Guid? ShipmentId { get; private set; }
    public string? VehicleId { get; private set; }
    public bool IsActive { get; private set; }

    public static Geofence Create(
        Guid tenantId, string name, decimal latitude, decimal longitude,
        decimal radiusMeters, Guid? shipmentId, string? vehicleId)
    {
        GpsDomainValidation.RequiredId(tenantId, nameof(tenantId));
        if (shipmentId == Guid.Empty)
            throw new ArgumentException("ShipmentId cannot be empty.", nameof(shipmentId));
        if (radiusMeters is <= 0 or > MaximumRadiusMeters)
            throw new ArgumentOutOfRangeException(nameof(radiusMeters), $"Radius must be greater than 0 and at most {MaximumRadiusMeters} metres.");

        return new Geofence
        {
            TenantId = tenantId,
            Name = GpsDomainValidation.RequiredText(name, nameof(name), 150),
            Latitude = GpsDomainValidation.Latitude(latitude),
            Longitude = GpsDomainValidation.Longitude(longitude),
            RadiusMeters = radiusMeters,
            ShipmentId = shipmentId,
            VehicleId = string.IsNullOrWhiteSpace(vehicleId)
                ? null
                : GpsDomainValidation.RequiredText(vehicleId, nameof(vehicleId), 100),
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    public void SetActive(bool isActive, DateTimeOffset changedAt)
    {
        if (changedAt == default)
            throw new ArgumentException("ChangedAt is required.", nameof(changedAt));
        IsActive = isActive;
        UpdatedAt = changedAt;
    }
}
