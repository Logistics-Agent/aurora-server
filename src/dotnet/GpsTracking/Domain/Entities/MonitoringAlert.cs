using GpsTracking.Domain.Enums;
using Shared.Entity;

namespace GpsTracking.Domain.Entities;

public sealed class MonitoringAlert : TenantAuditableEntity
{
    private MonitoringAlert() { }

    public MonitoringAlertType AlertType { get; private set; }
    public MonitoringAlertStatus Status { get; private set; }
    public string VehicleId { get; private set; } = string.Empty;
    public Guid? ShipmentId { get; private set; }
    public Guid? GeofenceId { get; private set; }
    public Guid? PositionId { get; private set; }
    public string DeduplicationKey { get; private set; } = string.Empty;
    public string Message { get; private set; } = string.Empty;
    public DateTimeOffset OccurredAt { get; private set; }
    public DateTimeOffset? ResolvedAt { get; private set; }

    public static MonitoringAlert Raise(
        Guid tenantId,
        MonitoringAlertType alertType,
        string vehicleId,
        Guid? shipmentId,
        Guid? geofenceId,
        Guid? positionId,
        string message,
        DateTimeOffset occurredAt,
        string? deduplicationKey = null)
    {
        GpsDomainValidation.RequiredId(tenantId, nameof(tenantId));
        if (!Enum.IsDefined(alertType))
            throw new ArgumentOutOfRangeException(nameof(alertType));
        if (shipmentId == Guid.Empty || geofenceId == Guid.Empty || positionId == Guid.Empty)
            throw new ArgumentException("Optional identifiers cannot be empty.");
        if (occurredAt == default)
            throw new ArgumentException("OccurredAt is required.", nameof(occurredAt));

        var normalizedVehicleId = GpsDomainValidation.RequiredText(vehicleId, nameof(vehicleId), 100);
        return new MonitoringAlert
        {
            TenantId = tenantId,
            AlertType = alertType,
            Status = MonitoringAlertStatus.Active,
            VehicleId = normalizedVehicleId,
            ShipmentId = shipmentId,
            GeofenceId = geofenceId,
            PositionId = positionId,
            DeduplicationKey = GpsDomainValidation.RequiredText(
                deduplicationKey ?? $"{alertType}:{normalizedVehicleId}:{shipmentId}:{geofenceId}",
                nameof(deduplicationKey), 300),
            Message = GpsDomainValidation.RequiredText(message, nameof(message), 1000),
            OccurredAt = occurredAt,
            CreatedAt = occurredAt
        };
    }

    public void Resolve(DateTimeOffset resolvedAt)
    {
        if (Status == MonitoringAlertStatus.Resolved)
            return;
        if (resolvedAt < OccurredAt)
            throw new ArgumentOutOfRangeException(nameof(resolvedAt), "ResolvedAt cannot precede OccurredAt.");
        Status = MonitoringAlertStatus.Resolved;
        ResolvedAt = resolvedAt;
        UpdatedAt = resolvedAt;
    }
}
