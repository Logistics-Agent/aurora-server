using GpsTracking.Application.Ingestion;
using GpsTracking.Application.Monitoring;
using GpsTracking.Domain.Entities;
using GpsTracking.Infrastructure.Persistences;
using Microsoft.EntityFrameworkCore;
using Shared.Exceptions;
using Shared.Interceptors;
using Shared.Security;

namespace GpsTracking.Tests.Application;

public sealed class PositionIngestionServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 21, 10, 0, 0, TimeSpan.Zero);
    private static readonly MonitoringOptions Options = new();

    [Fact]
    public async Task IngestDerivesShipmentAndPersistsHistorySnapshotAndOutbox()
    {
        var tenantId = Guid.CreateVersion7();
        var shipmentId = Guid.CreateVersion7();
        var currentUser = CurrentUser(tenantId);
        await using var context = CreateContext(currentUser);
        context.VehicleShipmentAssignments.Add(VehicleShipmentAssignment.Create(
            tenantId, shipmentId, "route-1", "vehicle-1", Now.AddHours(-1)));
        await context.SaveChangesAsync();
        var service = CreateService(context, currentUser);

        var result = await service.IngestAsync(Input("reading-1", Now.AddMinutes(-1)));

        Assert.Equal(shipmentId, result.ShipmentId);
        Assert.Equal(result.Id, (await context.CurrentLocations.SingleAsync()).PositionId);
        var outbox = await context.OutboxMessages.SingleAsync();
        Assert.Equal("GpsPositionUpdatedEvent", outbox.EventType);
        Assert.Contains(shipmentId.ToString(), outbox.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task IngestIsIdempotentForDeviceReadingIdentity()
    {
        var currentUser = CurrentUser(Guid.CreateVersion7());
        await using var context = CreateContext(currentUser);
        var service = CreateService(context, currentUser);

        var first = await service.IngestAsync(Input("reading-1", Now.AddMinutes(-1)));
        var replay = await service.IngestAsync(Input("reading-1", Now.AddMinutes(-1)));

        Assert.Equal(first.Id, replay.Id);
        Assert.Equal(1, await context.Positions.CountAsync());
        Assert.Equal(1, await context.CurrentLocations.CountAsync());
        Assert.Equal(1, await context.OutboxMessages.CountAsync());
    }

    [Fact]
    public async Task LateReadingRemainsInHistoryWithoutReplacingCurrentLocation()
    {
        var currentUser = CurrentUser(Guid.CreateVersion7());
        await using var context = CreateContext(currentUser);
        var monitor = new RecordingMonitoringService();
        var service = CreateService(context, currentUser, monitor);

        var latest = await service.IngestAsync(Input("latest", Now.AddMinutes(-1), 12));
        await service.IngestAsync(Input("late", Now.AddMinutes(-2), 11));

        Assert.Equal(2, await context.Positions.CountAsync());
        var current = await context.CurrentLocations.SingleAsync();
        Assert.Equal(latest.Id, current.PositionId);
        Assert.Equal(12, current.Latitude);
        Assert.Equal([latest.Id], monitor.PositionIds);
    }

    [Fact]
    public async Task MissingTenantIsRejectedBeforeWriting()
    {
        var currentUser = new CurrentUserService();
        await using var context = CreateContext(currentUser);
        var service = CreateService(context, currentUser);

        await Assert.ThrowsAsync<DomainException>(() =>
            service.IngestAsync(Input("reading-1", Now.AddMinutes(-1))));

        Assert.Empty(await context.Positions.IgnoreQueryFilters().ToListAsync());
    }

    [Fact]
    public async Task AssignmentFromAnotherTenantIsNeverApplied()
    {
        var tenantId = Guid.CreateVersion7();
        var otherTenantId = Guid.CreateVersion7();
        var currentUser = CurrentUser(tenantId);
        await using var context = CreateContext(currentUser);
        context.VehicleShipmentAssignments.Add(VehicleShipmentAssignment.Create(
            otherTenantId, Guid.CreateVersion7(), "route-2", "vehicle-1", Now.AddHours(-1)));
        await context.SaveChangesAsync();
        var service = CreateService(context, currentUser);

        var result = await service.IngestAsync(Input("reading-1", Now.AddMinutes(-1)));

        Assert.Null(result.ShipmentId);
    }

    private static IngestPositionInput Input(
        string readingId, DateTimeOffset recordedAt, decimal latitude = 10) =>
        new(readingId, "device-1", "vehicle-1", latitude, 106, 30, 90, 5, recordedAt);

    private static CurrentUserService CurrentUser(Guid tenantId)
    {
        var currentUser = new CurrentUserService();
        currentUser.Populate(Guid.CreateVersion7(), tenantId, null, null, [], []);
        currentUser.Populate(Guid.CreateVersion7(), tenantId, null, null, null, []);
        return currentUser;
    }

    private static GpsTrackingDbContext CreateContext(CurrentUserService currentUser)
    {
        var options = new DbContextOptionsBuilder<GpsTrackingDbContext>()
            .UseInMemoryDatabase(Guid.CreateVersion7().ToString())
            .Options;
        return new GpsTrackingDbContext(
            options, currentUser, new AuditSaveChangesInterceptor(currentUser));
    }

    private static PositionIngestionService CreateService(
        GpsTrackingDbContext context,
        CurrentUserService currentUser,
        IPositionMonitoringService? monitoringService = null) =>
        new(
            context,
            currentUser,
            new FixedTimeProvider(Now),
            Options,
            monitoringService ?? new RecordingMonitoringService());

    private sealed class RecordingMonitoringService : IPositionMonitoringService
    {
        public List<Guid> PositionIds { get; } = [];

        public Task EvaluateAsync(
            GpsPosition position,
            CurrentLocation current,
            CancellationToken cancellationToken = default)
        {
            PositionIds.Add(position.Id);
            return Task.CompletedTask;
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
