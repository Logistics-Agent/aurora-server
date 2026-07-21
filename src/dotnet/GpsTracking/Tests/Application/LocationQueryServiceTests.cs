using GpsTracking.Application.Queries;
using GpsTracking.Domain.Entities;
using GpsTracking.Infrastructure.Persistences;
using Microsoft.EntityFrameworkCore;
using Shared.Exceptions;
using Shared.Interceptors;
using Shared.Security;

namespace GpsTracking.Tests.Application;

public sealed class LocationQueryServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 21, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task CurrentLocationUsesLatestTenantSnapshot()
    {
        var tenantId = Guid.CreateVersion7();
        var shipmentId = Guid.CreateVersion7();
        var currentUser = CurrentUser(tenantId);
        await using var context = CreateContext(currentUser);
        var older = Position(tenantId, shipmentId, "vehicle-1", "old", Now.AddMinutes(-2));
        var latest = Position(tenantId, shipmentId, "vehicle-2", "new", Now.AddMinutes(-1));
        context.Positions.AddRange(older, latest);
        context.CurrentLocations.AddRange(CurrentLocation.FromPosition(older), CurrentLocation.FromPosition(latest));
        await context.SaveChangesAsync();
        var service = new LocationQueryService(context, currentUser);

        var result = await service.GetCurrentAsync(new LocationSelector(null, shipmentId));

        Assert.Equal(latest.Id, result.PositionId);
    }

    [Fact]
    public async Task CurrentLocationDoesNotLeakAnotherTenant()
    {
        var tenantId = Guid.CreateVersion7();
        var currentUser = CurrentUser(tenantId);
        await using var context = CreateContext(currentUser);
        var other = Position(Guid.CreateVersion7(), null, "vehicle-1", "other", Now.AddMinutes(-1));
        context.Positions.Add(other);
        context.CurrentLocations.Add(CurrentLocation.FromPosition(other));
        await context.SaveChangesAsync();
        var service = new LocationQueryService(context, currentUser);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            service.GetCurrentAsync(new LocationSelector("vehicle-1", null)));
    }

    [Fact]
    public async Task HistoryFiltersPagesAndOrdersDeterministically()
    {
        var tenantId = Guid.CreateVersion7();
        var currentUser = CurrentUser(tenantId);
        await using var context = CreateContext(currentUser);
        context.Positions.AddRange(
            Position(tenantId, null, "vehicle-1", "one", Now.AddMinutes(-3)),
            Position(tenantId, null, "vehicle-1", "two", Now.AddMinutes(-2)),
            Position(tenantId, null, "vehicle-1", "three", Now.AddMinutes(-1)),
            Position(tenantId, null, "vehicle-2", "ignored", Now.AddMinutes(-1)));
        await context.SaveChangesAsync();
        var service = new LocationQueryService(context, currentUser);

        var result = await service.ListHistoryAsync(
            new LocationSelector("vehicle-1", null), Now.AddHours(-1), Now, 1, 2);

        Assert.Equal(3, result.TotalItems);
        Assert.Equal(2, result.TotalPages);
        Assert.Equal(["three", "two"], result.Items.Select(item => item.ExternalReadingId));
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(1, 0)]
    [InlineData(1, 501)]
    public async Task HistoryValidatesPaging(int page, int pageSize)
    {
        var currentUser = CurrentUser(Guid.CreateVersion7());
        await using var context = CreateContext(currentUser);
        var service = new LocationQueryService(context, currentUser);

        await Assert.ThrowsAsync<DomainException>(() => service.ListHistoryAsync(
            new LocationSelector("vehicle", null), Now.AddHours(-1), Now, page, pageSize));
    }

    [Fact]
    public async Task QueriesValidateSelectorRangeAndTenant()
    {
        var missingTenant = new CurrentUserService();
        await using var context = CreateContext(missingTenant);
        var service = new LocationQueryService(context, missingTenant);

        await Assert.ThrowsAsync<DomainException>(() =>
            service.GetCurrentAsync(new LocationSelector("vehicle", null)));

        var currentUser = CurrentUser(Guid.CreateVersion7());
        await using var tenantContext = CreateContext(currentUser);
        var tenantService = new LocationQueryService(tenantContext, currentUser);
        await Assert.ThrowsAsync<DomainException>(() =>
            tenantService.GetCurrentAsync(new LocationSelector("vehicle", Guid.CreateVersion7())));
        await Assert.ThrowsAsync<DomainException>(() => tenantService.ListHistoryAsync(
            new LocationSelector("vehicle", null), Now.AddDays(-8), Now, 1, 10));
    }

    private static GpsPosition Position(
        Guid tenantId, Guid? shipmentId, string vehicleId, string readingId, DateTimeOffset recordedAt) =>
        GpsPosition.Create(
            tenantId, "device", vehicleId, shipmentId, readingId, 10, 106,
            20, 90, 5, recordedAt, Now);

    private static CurrentUserService CurrentUser(Guid tenantId)
    {
        var currentUser = new CurrentUserService();
        currentUser.Populate(Guid.CreateVersion7(), tenantId, null, null, [], []);
        return currentUser;
    }

    private static GpsTrackingDbContext CreateContext(CurrentUserService currentUser)
    {
        var options = new DbContextOptionsBuilder<GpsTrackingDbContext>()
            .UseInMemoryDatabase(Guid.CreateVersion7().ToString())
            .Options;
        return new GpsTrackingDbContext(options, currentUser, new AuditSaveChangesInterceptor(currentUser));
    }
}
