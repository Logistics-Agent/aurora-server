using GpsTracking.Application.Monitoring;
using GpsTracking.Domain.Entities;
using GpsTracking.Domain.Enums;
using GpsTracking.Infrastructure.Persistences;
using Microsoft.EntityFrameworkCore;
using Shared.Interceptors;
using Shared.Security;

namespace GpsTracking.Tests.Application;

public sealed class MonitoringServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 21, 16, 0, 0, TimeSpan.Zero);
    private static readonly MonitoringOptions Options = new()
    {
        StationarySpeedKph = 1,
        AbnormalStopDuration = TimeSpan.FromMinutes(15),
        SignalLossThreshold = TimeSpan.FromMinutes(5),
        SignalLossBatchSize = 100
    };

    [Fact]
    public void HaversineDistanceIsDeterministicAtBoundary()
    {
        var same = GeofenceDistanceCalculator.DistanceMeters(10, 106, 10, 106);
        var approximatelyOneKilometre = GeofenceDistanceCalculator.DistanceMeters(10, 106, 10.009m, 106);

        Assert.Equal(0, same);
        Assert.InRange(approximatelyOneKilometre, 990, 1_020);
    }

    [Fact]
    public async Task GeofenceEntryAndExitAreStatefulAndDeduplicated()
    {
        var tenantId = Guid.CreateVersion7();
        var currentUser = CurrentUser(tenantId);
        await using var context = CreateContext(currentUser);
        var geofence = Geofence.Create(tenantId, "Port", 10, 106, 500, null, "vehicle-1");
        context.Geofences.Add(geofence);
        await context.SaveChangesAsync();
        var service = new PositionMonitoringService(context, Options);
        var inside = Position(tenantId, "inside", 10, 106, 20, Now.AddMinutes(-2));
        var current = CurrentLocation.FromPosition(inside);

        await service.EvaluateAsync(inside, current);
        await service.EvaluateAsync(inside, current);
        var outside = Position(tenantId, "outside", 10.02m, 106, 20, Now.AddMinutes(-1));
        current.Apply(outside);
        await service.EvaluateAsync(outside, current);
        await context.SaveChangesAsync();

        var alerts = await context.MonitoringAlerts.ToListAsync();
        Assert.Equal(2, alerts.Count);
        Assert.Contains(alerts, item => item.AlertType == MonitoringAlertType.GeofenceEntered
            && item.Status == MonitoringAlertStatus.Resolved);
        Assert.Contains(alerts, item => item.AlertType == MonitoringAlertType.GeofenceExited
            && item.Status == MonitoringAlertStatus.Active);
        Assert.Equal(2, await context.OutboxMessages.CountAsync());
    }

    [Fact]
    public async Task AbnormalStopRaisesAfterThresholdAndResolvesOnMovement()
    {
        var tenantId = Guid.CreateVersion7();
        var currentUser = CurrentUser(tenantId);
        await using var context = CreateContext(currentUser);
        var service = new PositionMonitoringService(context, Options);
        var stopped = Position(tenantId, "stopped", 10, 106, 0, Now.AddMinutes(-20));
        var current = CurrentLocation.FromPosition(stopped);

        await service.EvaluateAsync(stopped, current);
        var moving = Position(tenantId, "moving", 10.01m, 106, 30, Now.AddMinutes(-1));
        current.Apply(moving);
        await service.EvaluateAsync(moving, current);
        await context.SaveChangesAsync();

        var alert = await context.MonitoringAlerts.SingleAsync();
        Assert.Equal(MonitoringAlertType.AbnormalStop, alert.AlertType);
        Assert.Equal(MonitoringAlertStatus.Resolved, alert.Status);
    }

    [Fact]
    public async Task SignalLossScanIsTenantAwareAndDeduplicated()
    {
        var tenantId = Guid.CreateVersion7();
        var otherTenantId = Guid.CreateVersion7();
        var currentUser = new CurrentUserService();
        await using var context = CreateContext(currentUser);
        context.VehicleShipmentAssignments.AddRange(
            VehicleShipmentAssignment.Create(tenantId, Guid.CreateVersion7(), "route-1", "vehicle-1", Now.AddHours(-1)),
            VehicleShipmentAssignment.Create(otherTenantId, Guid.CreateVersion7(), "route-2", "vehicle-2", Now.AddMinutes(-1)));
        await context.SaveChangesAsync();
        var monitor = new SignalLossMonitor(context, Options, new FixedTimeProvider(Now));

        await monitor.ScanAsync();
        await monitor.ScanAsync();

        var alerts = await context.MonitoringAlerts.IgnoreQueryFilters().ToListAsync();
        Assert.Single(alerts);
        Assert.Equal(tenantId, alerts[0].TenantId);
        Assert.Equal(MonitoringAlertType.SignalLost, alerts[0].AlertType);
    }

    [Fact]
    public async Task SignalLossScanAdvancesPastActiveAlertsAcrossBatches()
    {
        var tenantId = Guid.CreateVersion7();
        var currentUser = new CurrentUserService();
        await using var context = CreateContext(currentUser);
        context.VehicleShipmentAssignments.AddRange(
            VehicleShipmentAssignment.Create(
                tenantId, Guid.CreateVersion7(), "route-1", "vehicle-1", Now.AddHours(-2)),
            VehicleShipmentAssignment.Create(
                tenantId, Guid.CreateVersion7(), "route-2", "vehicle-2", Now.AddHours(-1)));
        await context.SaveChangesAsync();
        var options = new MonitoringOptions
        {
            SignalLossBatchSize = 1,
            SignalLossThreshold = TimeSpan.FromMinutes(5)
        };
        var monitor = new SignalLossMonitor(context, options, new FixedTimeProvider(Now));

        Assert.Equal(1, await monitor.ScanAsync());
        Assert.Equal(1, await monitor.ScanAsync());

        Assert.Equal(2, await context.MonitoringAlerts.IgnoreQueryFilters().CountAsync());
    }

    private static GpsPosition Position(
        Guid tenantId,
        string readingId,
        decimal latitude,
        decimal longitude,
        decimal speed,
        DateTimeOffset recordedAt) =>
        GpsPosition.Create(
            tenantId, "device", "vehicle-1", Guid.CreateVersion7(), readingId,
            latitude, longitude, speed, 90, 5, recordedAt, Now);

    private static CurrentUserService CurrentUser(Guid tenantId)
    {
        var currentUser = new CurrentUserService();
        currentUser.Populate(Guid.CreateVersion7(), tenantId, null, null, null, []);
        return currentUser;
    }

    private static GpsTrackingDbContext CreateContext(CurrentUserService currentUser)
    {
        var options = new DbContextOptionsBuilder<GpsTrackingDbContext>()
            .UseInMemoryDatabase(Guid.CreateVersion7().ToString())
            .Options;
        return new GpsTrackingDbContext(options, currentUser, new AuditSaveChangesInterceptor(currentUser));
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
