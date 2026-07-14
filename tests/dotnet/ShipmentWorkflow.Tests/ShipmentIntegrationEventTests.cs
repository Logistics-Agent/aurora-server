using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Shared.Interceptors;
using Shared.Security;
using Shipment.Contracts.Events;
using ShipmentWorkflow.Application.Commands.Shipments;
using ShipmentWorkflow.Domain.Enums;
using ShipmentWorkflow.Infrastructure.Persistences;
using ShipmentEntity = global::ShipmentWorkflow.Domain.Entities.Shipment;

namespace ShipmentWorkflow.Tests;

[Collection("ShipmentWorkflowDatabase")]
public sealed class ShipmentIntegrationEventTests
{
    private const string ConnectionString =
        "Host=localhost;Port=5433;Database=aurora_shipment_workflow_tests;Username=postgres;Password=postgres";

    [Fact]
    public void Event_contracts_have_version_and_unique_event_ids()
    {
        var first = new ShipmentCreatedEvent { ShipmentId = Guid.NewGuid(), TenantId = Guid.NewGuid() };
        var second = new ShipmentCreatedEvent { ShipmentId = first.ShipmentId, TenantId = first.TenantId };
        var cargo = new CargoUpdatedEvent { ShipmentId = first.ShipmentId, TenantId = first.TenantId };
        var document = new DocumentAttachedEvent { ShipmentId = first.ShipmentId, TenantId = first.TenantId };
        var route = new RouteAssignedEvent { ShipmentId = first.ShipmentId, TenantId = first.TenantId, RouteId = "R-1" };

        Assert.NotEqual(Guid.Empty, first.EventId);
        Assert.NotEqual(first.EventId, second.EventId);
        Assert.Equal(1, first.ContractVersion);
        Assert.Equal(1, cargo.ContractVersion);
        Assert.Equal(1, document.ContractVersion);
        Assert.Equal(1, route.ContractVersion);
    }

    [Fact]
    public void Event_serialization_is_consumer_safe()
    {
        var integrationEvent = new ShipmentDeliveredEvent
        {
            ShipmentId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            ShipmentNumber = "SHP-1",
            CurrentStatus = ShipmentStatus.Delivered.ToString(),
            DeliveredAt = DateTimeOffset.UtcNow
        };

        var json = JsonSerializer.Serialize(integrationEvent);
        var deserialized = JsonSerializer.Deserialize<ShipmentDeliveredEvent>(json);

        Assert.NotNull(deserialized);
        Assert.Equal(integrationEvent.EventId, deserialized.EventId);
        Assert.Equal(integrationEvent.TenantId, deserialized.TenantId);
        Assert.Equal("SHP-1", deserialized.ShipmentNumber);
    }

    [Fact]
    public async Task SubmitShipment_writes_submitted_and_status_changed_outbox_events()
    {
        var tenantId = Guid.NewGuid();
        var currentUser = new TestCurrentUserService(tenantId);
        await using var dbContext = await CreateDbContextAsync(currentUser);
        var shipment = await AddShipmentAsync(dbContext, tenantId, "SHP-EVT-1");

        await new SubmitShipmentCommandHandler(dbContext, currentUser)
            .Handle(new SubmitShipmentCommand(shipment.Id), CancellationToken.None);

        var messages = await dbContext.OutboxMessages.ToListAsync();
        Assert.Contains(messages, message => message.EventType == nameof(ShipmentSubmittedEvent));
        Assert.Contains(messages, message => message.EventType == nameof(ShipmentStatusChangedEvent));
        var submitted = JsonSerializer.Deserialize<ShipmentSubmittedEvent>(
            messages.Single(message => message.EventType == nameof(ShipmentSubmittedEvent)).Payload);
        Assert.Equal(tenantId, submitted?.TenantId);
    }

    [Fact]
    public async Task UpdateShipment_writes_updated_outbox_event()
    {
        var tenantId = Guid.NewGuid();
        var currentUser = new TestCurrentUserService(tenantId);
        await using var dbContext = await CreateDbContextAsync(currentUser);
        var shipment = await AddShipmentAsync(dbContext, tenantId, "SHP-EVT-2");

        await new UpdateShipmentCommandHandler(dbContext, currentUser)
            .Handle(new UpdateShipmentCommand(
                shipment.Id,
                "Updated Customer",
                "Updated Destination",
                ShipmentPriority.High,
                TransportMode.Road,
                "Updated"), CancellationToken.None);

        var message = await dbContext.OutboxMessages.SingleAsync(message =>
            message.EventType == nameof(ShipmentUpdatedEvent));
        var updated = JsonSerializer.Deserialize<ShipmentUpdatedEvent>(message.Payload);
        Assert.Equal(tenantId, updated?.TenantId);
        Assert.Contains(nameof(ShipmentEntity.CustomerName), updated?.ChangedFields ?? []);
    }

    [Fact]
    public async Task Lifecycle_status_transitions_write_specific_outbox_events()
    {
        var tenantId = Guid.NewGuid();
        var currentUser = new TestCurrentUserService(tenantId);
        await using var dbContext = await CreateDbContextAsync(currentUser);
        var shipment = await AddShipmentAsync(dbContext, tenantId, "SHP-EVT-3", shipment =>
        {
            shipment.Submit();
            shipment.StartPlanning();
            shipment.StartNegotiation();
            shipment.Confirm();
        });
        var handler = new UpdateShipmentStatusCommandHandler(dbContext, currentUser);

        await handler.Handle(new UpdateShipmentStatusCommand(shipment.Id, ShipmentStatus.PickedUp, null), CancellationToken.None);
        await handler.Handle(new UpdateShipmentStatusCommand(shipment.Id, ShipmentStatus.InTransit, null), CancellationToken.None);
        await handler.Handle(new UpdateShipmentStatusCommand(shipment.Id, ShipmentStatus.Delivered, null), CancellationToken.None);
        await handler.Handle(new UpdateShipmentStatusCommand(shipment.Id, ShipmentStatus.Completed, null), CancellationToken.None);

        var messages = await dbContext.OutboxMessages.ToListAsync();
        Assert.Contains(messages, message => message.EventType == nameof(ShipmentPickedUpEvent));
        Assert.Contains(messages, message => message.EventType == nameof(ShipmentDeliveredEvent));
        Assert.Contains(messages, message => message.EventType == nameof(ShipmentCompletedEvent));
    }

    private static async Task<ShipmentEntity> AddShipmentAsync(
        ShipmentWorkflowDbContext dbContext,
        Guid tenantId,
        string shipmentNo,
        Action<ShipmentEntity>? configure = null)
    {
        var shipment = ShipmentEntity.Create(
            tenantId,
            shipmentNo,
            orderId: shipmentNo.Replace("SHP", "ORD", StringComparison.Ordinal),
            customerName: "Acme",
            destinationAddress: "Warehouse 9");

        shipment.AddCargoItem("Laptop", 1, 2.5, "8471");
        shipment.AddLocation(LocationType.Pickup, "Factory", "Factory address", 1);
        shipment.AddLocation(LocationType.Delivery, "Warehouse", "Warehouse address", 2);
        configure?.Invoke(shipment);
        dbContext.Shipments.Add(shipment);
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();
        return shipment;
    }

    private static async Task<ShipmentWorkflowDbContext> CreateDbContextAsync(TestCurrentUserService currentUser)
    {
        var dbContext = CreateDbContext(currentUser);
        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();
        return dbContext;
    }

    private static ShipmentWorkflowDbContext CreateDbContext(TestCurrentUserService currentUser)
    {
        var options = new DbContextOptionsBuilder<ShipmentWorkflowDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;

        return new ShipmentWorkflowDbContext(
            options,
            currentUser,
            new AuditSaveChangesInterceptor(currentUser));
    }

    private sealed class TestCurrentUserService(Guid? tenantId) : ICurrentUserService
    {
        public Guid? UserId { get; } = Guid.NewGuid();
        public Guid? TenantId { get; } = tenantId;
        public string? TraceId { get; } = Guid.NewGuid().ToString();
        public int? PermissionVersion { get; } = 1;
        public IReadOnlyList<string> RoleIds { get; } = [];
        public IReadOnlyList<string> Permissions { get; } = [];
    }
}
