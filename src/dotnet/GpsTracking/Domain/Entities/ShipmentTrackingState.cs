using Shared.Entity;

namespace GpsTracking.Domain.Entities;

public sealed class ShipmentTrackingState : TenantAuditableEntity
{
    private ShipmentTrackingState() { }

    public Guid ShipmentId { get; private set; }
    public bool IsClosed { get; private set; }
    public DateTimeOffset LastEventAt { get; private set; }

    public static ShipmentTrackingState Create(
        Guid tenantId, Guid shipmentId, DateTimeOffset eventAt, bool isClosed)
    {
        GpsDomainValidation.RequiredId(tenantId, nameof(tenantId));
        GpsDomainValidation.RequiredId(shipmentId, nameof(shipmentId));
        if (eventAt == default)
            throw new ArgumentException("EventAt is required.", nameof(eventAt));
        return new ShipmentTrackingState
        {
            TenantId = tenantId,
            ShipmentId = shipmentId,
            IsClosed = isClosed,
            LastEventAt = eventAt,
            CreatedAt = eventAt
        };
    }

    public bool ObserveAssignment(DateTimeOffset assignedAt)
    {
        if (IsClosed || assignedAt <= LastEventAt)
            return false;
        LastEventAt = assignedAt;
        UpdatedAt = assignedAt;
        return true;
    }

    public void Close(DateTimeOffset closedAt)
    {
        IsClosed = true;
        if (closedAt > LastEventAt)
            LastEventAt = closedAt;
        UpdatedAt = closedAt;
    }
}
