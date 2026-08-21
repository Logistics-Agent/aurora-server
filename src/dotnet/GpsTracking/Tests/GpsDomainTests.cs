using GpsTracking.Contracts.Events;
using GpsTracking.Domain.Entities;
using GpsTracking.Domain.Enums;

namespace GpsTracking.Tests;

public sealed class GpsDomainTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 21, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void PositionValidatesCoordinatesAndMotion()
    {
        var tenantId = Guid.CreateVersion7();

        Assert.Throws<ArgumentOutOfRangeException>(() => CreatePosition(tenantId, latitude: 91));
        Assert.Throws<ArgumentOutOfRangeException>(() => CreatePosition(tenantId, longitude: -181));
        Assert.Throws<ArgumentOutOfRangeException>(() => CreatePosition(tenantId, speedKph: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => CreatePosition(tenantId, headingDegrees: 360));
        Assert.Throws<ArgumentOutOfRangeException>(() => CreatePosition(tenantId, accuracyMeters: -1));
    }

    [Fact]
    public void PositionValidatesRecordedTimeWindow()
    {
        var tenantId = Guid.CreateVersion7();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CreatePosition(tenantId, recordedAt: Now.AddMinutes(6)));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CreatePosition(tenantId, recordedAt: Now.AddDays(-31)));
    }

    [Fact]
    public void CurrentLocationOnlyAdvancesForNewerReadings()
    {
        var tenantId = Guid.CreateVersion7();
        var first = CreatePosition(tenantId, latitude: 10, recordedAt: Now.AddMinutes(-2));
        var late = CreatePosition(tenantId, latitude: 11, recordedAt: Now.AddMinutes(-3));
        var latest = CreatePosition(tenantId, latitude: 12, recordedAt: Now.AddMinutes(-1));
        var current = CurrentLocation.FromPosition(first);

        Assert.False(current.Apply(late));
        Assert.True(current.Apply(latest));
        Assert.Equal(12, current.Latitude);
        Assert.Equal(latest.Id, current.PositionId);
    }

    [Fact]
    public void AssignmentCanBeEndedOnlyOnce()
    {
        var assignment = VehicleShipmentAssignment.Create(
            Guid.CreateVersion7(), Guid.CreateVersion7(), "route-1", "vehicle-1", Now);

        Assert.True(assignment.IsActive);
        assignment.End(Now.AddHours(1));
        assignment.End(Now.AddHours(2));

        Assert.False(assignment.IsActive);
        Assert.Equal(Now.AddHours(1), assignment.EndedAt);
    }

    [Fact]
    public void GeofenceValidatesRadiusAndSupportsActivation()
    {
        var tenantId = Guid.CreateVersion7();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Geofence.Create(tenantId, "Port", 10, 106, 0, null, null));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Geofence.Create(tenantId, "Port", 10, 106, 100_001, null, null));

        var geofence = Geofence.Create(tenantId, "Port", 10, 106, 500, null, "vehicle-1");
        geofence.SetActive(false, Now);

        Assert.False(geofence.IsActive);
    }

    [Fact]
    public void AlertResolutionIsIdempotent()
    {
        var alert = MonitoringAlert.Raise(
            Guid.CreateVersion7(), MonitoringAlertType.SignalLost, "vehicle-1",
            Guid.CreateVersion7(), null, null, "Signal unavailable", Now);

        alert.Resolve(Now.AddMinutes(1));
        alert.Resolve(Now.AddMinutes(2));

        Assert.Equal(MonitoringAlertStatus.Resolved, alert.Status);
        Assert.Equal(Now.AddMinutes(1), alert.ResolvedAt);
    }

    [Fact]
    public void GpsEventsAreVersionedAndCarryTenantIdentity()
    {
        var tenantId = Guid.CreateVersion7();
        var positionEvent = new GpsPositionUpdatedEvent { TenantId = tenantId };
        var alertEvent = new GpsMonitoringAlertRaisedEvent { TenantId = tenantId };

        Assert.Equal(1, positionEvent.ContractVersion);
        Assert.Equal(1, alertEvent.ContractVersion);
        Assert.Equal(tenantId, positionEvent.TenantId);
        Assert.NotEqual(Guid.Empty, positionEvent.EventId);
        Assert.NotEqual(Guid.Empty, alertEvent.EventId);
    }

    private static GpsPosition CreatePosition(
        Guid tenantId,
        decimal latitude = 10,
        decimal longitude = 106,
        decimal? speedKph = 30,
        decimal? headingDegrees = 90,
        decimal? accuracyMeters = 5,
        DateTimeOffset? recordedAt = null) =>
        GpsPosition.Create(
            tenantId,
            "device-1",
            "vehicle-1",
            Guid.CreateVersion7(),
            Guid.CreateVersion7().ToString(),
            latitude,
            longitude,
            speedKph,
            headingDegrees,
            accuracyMeters,
            recordedAt ?? Now.AddMinutes(-1),
            Now);
}
