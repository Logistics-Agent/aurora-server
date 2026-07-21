using Shared.Entity;

namespace GpsTracking.Domain.Entities;

public sealed class GeofencePresence : TenantAuditableEntity
{
    private GeofencePresence() { }

    public Guid GeofenceId { get; private set; }
    public string VehicleId { get; private set; } = string.Empty;
    public bool IsInside { get; private set; }
    public DateTimeOffset ObservedAt { get; private set; }

    public static GeofencePresence Create(
        Guid tenantId, Guid geofenceId, string vehicleId, bool isInside, DateTimeOffset observedAt)
    {
        GpsDomainValidation.RequiredId(tenantId, nameof(tenantId));
        GpsDomainValidation.RequiredId(geofenceId, nameof(geofenceId));
        if (observedAt == default)
            throw new ArgumentException("ObservedAt is required.", nameof(observedAt));
        return new GeofencePresence
        {
            TenantId = tenantId,
            GeofenceId = geofenceId,
            VehicleId = GpsDomainValidation.RequiredText(vehicleId, nameof(vehicleId), 100),
            IsInside = isInside,
            ObservedAt = observedAt,
            CreatedAt = observedAt
        };
    }

    public bool Observe(bool isInside, DateTimeOffset observedAt)
    {
        if (observedAt < ObservedAt)
            return false;
        var changed = IsInside != isInside;
        IsInside = isInside;
        ObservedAt = observedAt;
        UpdatedAt = observedAt;
        return changed;
    }
}
