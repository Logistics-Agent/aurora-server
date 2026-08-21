using System.Text.Json;
using GpsTracking.Application.Ingestion;
using GpsTracking.Application.Monitoring;
using GpsTracking.Application.Queries;
using GpsTracking.Application.Shipments;
using GpsTracking.Contracts.Events;
using GpsTracking.Domain.Entities;
using GpsTracking.Domain.Enums;
using GpsTracking.Infrastructure.BackgroundJobs;
using GpsTracking.Infrastructure.Persistences;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Shared.Exceptions;
using Shared.Security;
using Shipment.Contracts.Events;

namespace GpsTracking.Tests.Integration;

[Collection(GpsPostgresCollection.Name)]
public sealed class GpsPostgresIntegrationTests(GpsPostgresFixture database)
{
    private static readonly DateTimeOffset Now = new(2026, 7, 21, 22, 0, 0, TimeSpan.Zero);
    private static readonly MonitoringOptions MonitoringOptions = new();

    [Fact]
    public async Task MigrationBackedIngestionQueryIdempotencyAndTenantIsolationWorkTogether()
    {
        await database.ResetAsync();
        var tenantId = Guid.CreateVersion7();
        var currentUser = CurrentUser(tenantId);
        await using var context = database.CreateContext(currentUser);
        var monitoring = new PositionMonitoringService(context, MonitoringOptions);
        var ingestion = new PositionIngestionService(
            context, currentUser, new FixedTimeProvider(Now), MonitoringOptions, monitoring);

        var first = await ingestion.IngestAsync(Input("reading-1"));
        var replay = await ingestion.IngestAsync(Input("reading-1"));
        var queries = new LocationQueryService(context, currentUser);
        var current = await queries.GetCurrentAsync(new LocationSelector("vehicle-1", null));
        var history = await queries.ListHistoryAsync(
            new LocationSelector("vehicle-1", null), Now.AddHours(-1), Now, 1, 20);

        Assert.Equal(first.Id, replay.Id);
        Assert.Equal(first.Id, current.PositionId);
        Assert.Single(history.Items);
        Assert.Equal(1, await context.OutboxMessages.CountAsync());

        var otherUser = CurrentUser(Guid.CreateVersion7());
        await using var otherContext = database.CreateContext(otherUser);
        await Assert.ThrowsAsync<NotFoundException>(() =>
            new LocationQueryService(otherContext, otherUser)
                .GetCurrentAsync(new LocationSelector("vehicle-1", null)));

        var missingTenant = new CurrentUserService();
        await using var missingContext = database.CreateContext(missingTenant);
        Assert.Empty(await missingContext.Positions.ToListAsync());
    }

    [Fact]
    public async Task ConcurrentDeviceReplayProducesOnePositionAndOneOutboxMessage()
    {
        await database.ResetAsync();
        var tenantId = Guid.CreateVersion7();
        var firstUser = CurrentUser(tenantId);
        var secondUser = CurrentUser(tenantId);
        await using var firstContext = database.CreateContext(firstUser);
        await using var secondContext = database.CreateContext(secondUser);
        var firstService = Ingestion(firstContext, firstUser);
        var secondService = Ingestion(secondContext, secondUser);

        var results = await Task.WhenAll(
            firstService.IngestAsync(Input("concurrent-reading")),
            secondService.IngestAsync(Input("concurrent-reading")));

        Assert.Equal(results[0].Id, results[1].Id);
        await using var verification = database.CreateContext(CurrentUser(tenantId));
        Assert.Equal(1, await verification.Positions.CountAsync());
        Assert.Equal(1, await verification.CurrentLocations.CountAsync());
        Assert.Equal(1, await verification.OutboxMessages.CountAsync());
    }

    [Fact]
    public async Task ShipmentProjectionUsesInboxIdempotencyOnPostgres()
    {
        await database.ResetAsync();
        var tenantId = Guid.CreateVersion7();
        var shipmentId = Guid.CreateVersion7();
        await using var context = database.CreateContext(new CurrentUserService());
        var projector = new ShipmentAssignmentProjector(context, new FixedTimeProvider(Now));
        var message = new RouteAssignedEvent
        {
            TenantId = tenantId,
            ShipmentId = shipmentId,
            ShipmentNumber = "SHP-GPS-1",
            RouteId = "route-1",
            VehicleId = "vehicle-1",
            AssignedAt = Now.AddMinutes(-10)
        };

        await projector.ProjectAsync(message);
        await projector.ProjectAsync(message);

        Assert.Equal(1, await context.VehicleShipmentAssignments.IgnoreQueryFilters().CountAsync());
        Assert.Equal(1, await context.ConsumedIntegrationEvents.IgnoreQueryFilters().CountAsync());
        var assignment = await context.VehicleShipmentAssignments.IgnoreQueryFilters().SingleAsync();
        Assert.Equal(tenantId, assignment.TenantId);
        Assert.Equal(shipmentId, assignment.ShipmentId);
    }

    [Fact]
    public async Task GeofenceDeleteCascadesPresenceAndPreservesAlertMetadata()
    {
        await database.ResetAsync();
        var tenantId = Guid.CreateVersion7();
        var currentUser = CurrentUser(tenantId);
        await using (var context = database.CreateContext(currentUser))
        {
            var geofence = Geofence.Create(tenantId, "Port", 10, 106, 500, null, "vehicle-1");
            context.Geofences.Add(geofence);
            context.GeofencePresences.Add(GeofencePresence.Create(
                tenantId, geofence.Id, "vehicle-1", true, Now));
            context.MonitoringAlerts.Add(MonitoringAlert.Raise(
                tenantId,
                MonitoringAlertType.GeofenceEntered,
                "vehicle-1",
                null,
                geofence.Id,
                null,
                "Entered port.",
                Now));
            await context.SaveChangesAsync();
            context.Geofences.Remove(geofence);
            await context.SaveChangesAsync();
        }

        await using var verification = database.CreateContext(currentUser);
        Assert.Empty(await verification.GeofencePresences.ToListAsync());
        var alert = await verification.MonitoringAlerts.SingleAsync();
        Assert.Null(alert.GeofenceId);
        Assert.Equal("Entered port.", alert.Message);
    }

    [Fact]
    public async Task OutboxPublishesThroughRabbitMqAndMarksPostgresMessageProcessed()
    {
        await database.ResetAsync();
        var received = new TaskCompletionSource<GpsPositionUpdatedEvent>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var queueName = $"gps-runtime-proof-{Guid.NewGuid():N}";
        var bus = Bus.Factory.CreateUsingRabbitMq(configuration =>
        {
            configuration.Host("localhost", "/", host =>
            {
                host.Username("aurora");
                host.Password("aurora_dev");
            });
            configuration.UseRawJsonSerializer();
            configuration.ReceiveEndpoint(queueName, endpoint =>
            {
                endpoint.Durable = false;
                endpoint.AutoDelete = true;
                endpoint.Handler<GpsPositionUpdatedEvent>(context =>
                {
                    received.TrySetResult(context.Message);
                    return Task.CompletedTask;
                });
            });
        });

        await bus.StartAsync();
        try
        {
            var integrationEvent = new GpsPositionUpdatedEvent
            {
                TenantId = Guid.CreateVersion7(),
                PositionId = Guid.CreateVersion7(),
                DeviceId = "device-runtime",
                VehicleId = "vehicle-runtime",
                Latitude = 10,
                Longitude = 106,
                RecordedAt = Now
            };
            var message = OutboxMessage.Create(
                integrationEvent.TenantId,
                integrationEvent.EventId,
                nameof(GpsPositionUpdatedEvent),
                JsonSerializer.Serialize(integrationEvent),
                Now);
            var currentUser = new CurrentUserService();
            await using var context = database.CreateContext(currentUser);
            context.OutboxMessages.Add(message);
            await context.SaveChangesAsync();
            var processor = new GpsOutboxProcessor(
                new GpsOutboxBatchStore(context),
                new GpsIntegrationEventPublisher(bus),
                new FixedTimeProvider(Now),
                Options.Create(new GpsOutboxPublisherOptions()),
                NullLogger<GpsOutboxProcessor>.Instance);

            Assert.Equal(1, await processor.ProcessBatchAsync());
            var delivered = await received.Task.WaitAsync(TimeSpan.FromSeconds(15));
            await context.Entry(message).ReloadAsync();

            Assert.Equal(integrationEvent.EventId, delivered.EventId);
            Assert.Equal(integrationEvent.TenantId, delivered.TenantId);
            Assert.NotNull(message.ProcessedAt);
        }
        finally
        {
            await bus.StopAsync();
        }
    }

    private static IngestPositionInput Input(string readingId) =>
        new(readingId, "device-1", "vehicle-1", 10, 106, 30, 90, 5, Now.AddMinutes(-1));

    private static PositionIngestionService Ingestion(
        GpsTrackingDbContext context,
        CurrentUserService currentUser) =>
        new(
            context,
            currentUser,
            new FixedTimeProvider(Now),
            MonitoringOptions,
            new PositionMonitoringService(context, MonitoringOptions));

    private static CurrentUserService CurrentUser(Guid tenantId)
    {
        var currentUser = new CurrentUserService();
        currentUser.Populate(Guid.CreateVersion7(), tenantId, null, null, [], []);
        return currentUser;
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
