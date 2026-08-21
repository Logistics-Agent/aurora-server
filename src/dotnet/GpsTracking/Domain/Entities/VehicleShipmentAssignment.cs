using Shared.Entity;

namespace GpsTracking.Domain.Entities;

public sealed class VehicleShipmentAssignment : TenantAuditableEntity
{
    private VehicleShipmentAssignment() { }

    public Guid ShipmentId { get; private set; }
    public string RouteId { get; private set; } = string.Empty;
    public string VehicleId { get; private set; } = string.Empty;
    public DateTimeOffset AssignedAt { get; private set; }
    public DateTimeOffset? EndedAt { get; private set; }
    public bool IsActive => EndedAt is null;

    public static VehicleShipmentAssignment Create(
        Guid tenantId, Guid shipmentId, string routeId, string vehicleId, DateTimeOffset assignedAt)
    {
        GpsDomainValidation.RequiredId(tenantId, nameof(tenantId));
        GpsDomainValidation.RequiredId(shipmentId, nameof(shipmentId));
        if (assignedAt == default)
            throw new ArgumentException("AssignedAt is required.", nameof(assignedAt));

        return new VehicleShipmentAssignment
        {
            TenantId = tenantId,
            ShipmentId = shipmentId,
            RouteId = GpsDomainValidation.RequiredText(routeId, nameof(routeId), 100),
            VehicleId = GpsDomainValidation.RequiredText(vehicleId, nameof(vehicleId), 100),
            AssignedAt = assignedAt,
            CreatedAt = assignedAt
        };
    }

    public void End(DateTimeOffset endedAt)
    {
        if (EndedAt.HasValue)
            return;
        if (endedAt < AssignedAt)
            throw new ArgumentOutOfRangeException(nameof(endedAt), "EndedAt cannot precede AssignedAt.");
        EndedAt = endedAt;
        UpdatedAt = endedAt;
    }
}
