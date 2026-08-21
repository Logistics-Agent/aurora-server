using GpsTracking.Application.Shipments;
using GpsTracking.Infrastructure.Persistences;
using Microsoft.EntityFrameworkCore;
using Shared.Interceptors;
using Shared.Security;
using Shipment.Contracts.Events;

namespace GpsTracking.Tests.Application;

public sealed class ShipmentAssignmentProjectorTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 21, 14, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task RouteAssignmentCreatesLocalProjectionAndInboxReceiptOnce()
    {
        await using var context = CreateContext();
        var projector = new ShipmentAssignmentProjector(context, new FixedTimeProvider(Now));
        var message = RouteAssigned(Guid.CreateVersion7(), Guid.CreateVersion7(), "vehicle-1", Now);

        await projector.ProjectAsync(message);
        await projector.ProjectAsync(message);

        var assignment = await context.VehicleShipmentAssignments.IgnoreQueryFilters().SingleAsync();
        Assert.Equal(message.TenantId, assignment.TenantId);
        Assert.Equal(message.ShipmentId, assignment.ShipmentId);
        Assert.Equal(1, await context.ConsumedIntegrationEvents.IgnoreQueryFilters().CountAsync());
    }

    [Fact]
    public async Task NewAssignmentClosesPreviousVehicleAndShipmentAssignments()
    {
        await using var context = CreateContext();
        var projector = new ShipmentAssignmentProjector(context, new FixedTimeProvider(Now));
        var tenantId = Guid.CreateVersion7();
        var shipmentId = Guid.CreateVersion7();

        await projector.ProjectAsync(RouteAssigned(tenantId, shipmentId, "vehicle-1", Now.AddMinutes(-2)));
        await projector.ProjectAsync(RouteAssigned(tenantId, shipmentId, "vehicle-2", Now.AddMinutes(-1)));

        var assignments = await context.VehicleShipmentAssignments
            .IgnoreQueryFilters().OrderBy(item => item.AssignedAt).ToListAsync();
        Assert.NotNull(assignments[0].EndedAt);
        Assert.True(assignments[1].IsActive);
        Assert.Equal("vehicle-2", assignments[1].VehicleId);
    }

    [Fact]
    public async Task BusinessDuplicateAtSameTimestampDoesNotReplaceAssignment()
    {
        await using var context = CreateContext();
        var projector = new ShipmentAssignmentProjector(context, new FixedTimeProvider(Now));
        var tenantId = Guid.CreateVersion7();
        var shipmentId = Guid.CreateVersion7();

        await projector.ProjectAsync(RouteAssigned(tenantId, shipmentId, "vehicle-1", Now));
        await projector.ProjectAsync(RouteAssigned(tenantId, shipmentId, "vehicle-1", Now));

        var assignments = await context.VehicleShipmentAssignments.IgnoreQueryFilters().ToListAsync();
        Assert.Single(assignments);
        Assert.True(assignments[0].IsActive);
        Assert.Equal(2, await context.ConsumedIntegrationEvents.IgnoreQueryFilters().CountAsync());
    }

    [Fact]
    public async Task TerminalEventClosesAssignmentAndPreventsOutOfOrderReopen()
    {
        await using var context = CreateContext();
        var projector = new ShipmentAssignmentProjector(context, new FixedTimeProvider(Now));
        var tenantId = Guid.CreateVersion7();
        var shipmentId = Guid.CreateVersion7();
        await projector.ProjectAsync(new ShipmentCancelledEvent
        {
            EventId = Guid.CreateVersion7(),
            TenantId = tenantId,
            ShipmentId = shipmentId,
            CancelledAt = Now
        });

        await projector.ProjectAsync(RouteAssigned(tenantId, shipmentId, "vehicle-1", Now.AddMinutes(-1)));

        Assert.Empty(await context.VehicleShipmentAssignments.IgnoreQueryFilters().ToListAsync());
        var state = await context.ShipmentTrackingStates.IgnoreQueryFilters().SingleAsync();
        Assert.True(state.IsClosed);
        Assert.Equal(2, await context.ConsumedIntegrationEvents.IgnoreQueryFilters().CountAsync());
    }

    [Fact]
    public async Task CompletionEndsOnlyMatchingTenantShipment()
    {
        await using var context = CreateContext();
        var projector = new ShipmentAssignmentProjector(context, new FixedTimeProvider(Now));
        var tenantId = Guid.CreateVersion7();
        var otherTenantId = Guid.CreateVersion7();
        var shipmentId = Guid.CreateVersion7();
        await projector.ProjectAsync(RouteAssigned(tenantId, shipmentId, "vehicle-1", Now.AddHours(-1)));
        await projector.ProjectAsync(RouteAssigned(otherTenantId, shipmentId, "vehicle-2", Now.AddHours(-1)));

        await projector.ProjectAsync(new ShipmentCompletedEvent
        {
            EventId = Guid.CreateVersion7(),
            TenantId = tenantId,
            ShipmentId = shipmentId,
            CompletedAt = Now
        });

        var assignments = await context.VehicleShipmentAssignments.IgnoreQueryFilters().ToListAsync();
        Assert.False(assignments.Single(item => item.TenantId == tenantId).IsActive);
        Assert.True(assignments.Single(item => item.TenantId == otherTenantId).IsActive);
    }

    private static RouteAssignedEvent RouteAssigned(
        Guid tenantId, Guid shipmentId, string vehicleId, DateTimeOffset assignedAt) =>
        new()
        {
            EventId = Guid.CreateVersion7(),
            TenantId = tenantId,
            ShipmentId = shipmentId,
            ShipmentNumber = "SHP-1",
            RouteId = Guid.CreateVersion7().ToString(),
            VehicleId = vehicleId,
            AssignedAt = assignedAt
        };

    private static GpsTrackingDbContext CreateContext()
    {
        var currentUser = new CurrentUserService();
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
