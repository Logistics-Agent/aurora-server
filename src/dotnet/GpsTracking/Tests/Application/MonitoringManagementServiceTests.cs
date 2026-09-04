using GpsTracking.Application.Monitoring;
using GpsTracking.Domain.Entities;
using GpsTracking.Domain.Enums;
using GpsTracking.Infrastructure.Persistences;
using Microsoft.EntityFrameworkCore;
using Shared.Exceptions;
using Shared.Interceptors;
using Shared.Security;

namespace GpsTracking.Tests.Application;

public sealed class MonitoringManagementServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 21, 18, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task GeofenceManagementIsTenantScopedAndClearsPresenceWhenDisabled()
    {
        var tenantId = Guid.CreateVersion7();
        var currentUser = CurrentUser(tenantId);
        var databaseName = Guid.CreateVersion7().ToString();
        await using var context = CreateContext(currentUser, databaseName);
        var service = new MonitoringManagementService(context, currentUser, new FixedTimeProvider(Now));
        var geofence = await service.CreateGeofenceAsync(
            new CreateGeofenceInput("Port", 10, 106, 500, null, "vehicle-1"));
        context.GeofencePresences.Add(GeofencePresence.Create(
            tenantId, geofence.Id, "vehicle-1", true, Now));
        await context.SaveChangesAsync();

        await service.SetGeofenceActiveAsync(geofence.Id, false);

        Assert.False(geofence.IsActive);
        Assert.Empty(await context.GeofencePresences.ToListAsync());
        var otherTenant = CurrentUser(Guid.CreateVersion7());
        await using var otherContext = CreateContext(otherTenant, databaseName);
        var otherService = new MonitoringManagementService(otherContext, otherTenant, new FixedTimeProvider(Now));
        await Assert.ThrowsAsync<NotFoundException>(() =>
            otherService.SetGeofenceActiveAsync(geofence.Id, true));
    }

    [Fact]
    public async Task AlertListFiltersAndResolveIsIdempotent()
    {
        var tenantId = Guid.CreateVersion7();
        var currentUser = CurrentUser(tenantId);
        await using var context = CreateContext(currentUser);
        var alert = MonitoringAlert.Raise(
            tenantId, MonitoringAlertType.SignalLost, "vehicle-1", null, null, null,
            "Lost", Now);
        context.MonitoringAlerts.Add(alert);
        await context.SaveChangesAsync();
        var service = new MonitoringManagementService(context, currentUser, new FixedTimeProvider(Now));

        var page = await service.ListAlertsAsync(
            MonitoringAlertType.SignalLost, MonitoringAlertStatus.Active, 1, 20);
        await service.ResolveAlertAsync(alert.Id);
        await service.ResolveAlertAsync(alert.Id);

        Assert.Single(page.Items);
        Assert.Equal(MonitoringAlertStatus.Resolved, alert.Status);
    }

    private static CurrentUserService CurrentUser(Guid tenantId)
    {
        var currentUser = new CurrentUserService();
        currentUser.Populate(Guid.CreateVersion7(), tenantId, null, null, [], []);
        currentUser.Populate(Guid.CreateVersion7(), tenantId, null, null, null, []);
        return currentUser;
    }

    private static GpsTrackingDbContext CreateContext(
        CurrentUserService currentUser,
        string? databaseName = null)
    {
        var options = new DbContextOptionsBuilder<GpsTrackingDbContext>()
            .UseInMemoryDatabase(databaseName ?? Guid.CreateVersion7().ToString())
            .Options;
        return new GpsTrackingDbContext(options, currentUser, new AuditSaveChangesInterceptor(currentUser));
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
