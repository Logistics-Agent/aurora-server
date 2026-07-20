using Microsoft.EntityFrameworkCore;
using Shared.Exceptions;
using Shared.Interceptors;
using Shared.Security;
using Shipment.Contracts.Events;
using ShipmentWorkflow.Application.Commands.Shipments;
using ShipmentWorkflow.Domain.Enums;
using ShipmentWorkflow.Infrastructure.Persistences;
using ShipmentEntity = global::ShipmentWorkflow.Domain.Entities.Shipment;

namespace ShipmentWorkflow.Tests;

[Collection("ShipmentWorkflowDatabase")]
public sealed class ShipmentCargoLocationManagementTests
{
    private const string ConnectionString =
        "Host=localhost;Port=5433;Database=aurora_shipment_workflow_tests;Username=postgres;Password=postgres";

    [Fact]
    public async Task AddCargoItem_stores_cargo_and_writes_outbox()
    {
        var tenantId = Guid.NewGuid();
        var currentUser = new TestCurrentUserService(tenantId);
        await using var dbContext = await CreateDbContextAsync(currentUser);
        var shipment = await AddShipmentAsync(dbContext, tenantId, "SHP-CARGO-1");

        var result = await new AddCargoItemCommandHandler(dbContext, currentUser)
            .Handle(new AddCargoItemCommand(shipment.Id, "Server rack", 2, 125.5, "8471"), CancellationToken.None);

        Assert.Contains(result.CargoItems, cargo => cargo.Name == "Server rack" && cargo.Quantity == 2);
        Assert.Contains(await dbContext.OutboxMessages.ToListAsync(), message =>
            message.EventType == nameof(CargoUpdatedEvent));
    }

    [Fact]
    public async Task AddCargoItem_rejects_invalid_cargo_values()
    {
        var tenantId = Guid.NewGuid();
        var currentUser = new TestCurrentUserService(tenantId);
        await using var dbContext = await CreateDbContextAsync(currentUser);
        var shipment = await AddShipmentAsync(dbContext, tenantId, "SHP-CARGO-2");

        await Assert.ThrowsAsync<ArgumentException>(() =>
            new AddCargoItemCommandHandler(dbContext, currentUser)
                .Handle(new AddCargoItemCommand(shipment.Id, "", 1, 10, null), CancellationToken.None));

        await Assert.ThrowsAsync<ArgumentException>(() =>
            new AddCargoItemCommandHandler(dbContext, currentUser)
                .Handle(new AddCargoItemCommand(shipment.Id, "Box", 0, 10, null), CancellationToken.None));

        await Assert.ThrowsAsync<ArgumentException>(() =>
            new AddCargoItemCommandHandler(dbContext, currentUser)
                .Handle(new AddCargoItemCommand(shipment.Id, "Box", 1, -1, null), CancellationToken.None));
    }

    [Fact]
    public async Task UpdateAndRemoveCargoItem_mutates_existing_cargo_only()
    {
        var tenantId = Guid.NewGuid();
        var currentUser = new TestCurrentUserService(tenantId);
        await using var dbContext = await CreateDbContextAsync(currentUser);
        var shipment = await AddShipmentAsync(dbContext, tenantId, "SHP-CARGO-3");
        var cargoId = shipment.CargoItems.Single().Id;

        var updated = await new UpdateCargoItemCommandHandler(dbContext, currentUser)
            .Handle(new UpdateCargoItemCommand(shipment.Id, cargoId, "Updated cargo", 3, 42, "9403"), CancellationToken.None);

        Assert.Contains(updated.CargoItems, cargo => cargo.Id == cargoId && cargo.Name == "Updated cargo" && cargo.Quantity == 3);

        var removed = await new RemoveCargoItemCommandHandler(dbContext, currentUser)
            .Handle(new RemoveCargoItemCommand(shipment.Id, cargoId), CancellationToken.None);

        Assert.DoesNotContain(removed.CargoItems, cargo => cargo.Id == cargoId);
    }

    [Fact]
    public async Task CargoMutation_rejects_after_operational_processing_started()
    {
        var tenantId = Guid.NewGuid();
        var currentUser = new TestCurrentUserService(tenantId);
        await using var dbContext = await CreateDbContextAsync(currentUser);
        var shipment = await AddShipmentAsync(dbContext, tenantId, "SHP-CARGO-4", shipment =>
        {
            shipment.Submit();
            shipment.StartPlanning();
        });

        await Assert.ThrowsAsync<DomainException>(() =>
            new AddCargoItemCommandHandler(dbContext, currentUser)
                .Handle(new AddCargoItemCommand(shipment.Id, "Late cargo", 1, 5, null), CancellationToken.None));
    }

    [Fact]
    public async Task AddShipmentLocation_stores_valid_location()
    {
        var tenantId = Guid.NewGuid();
        var currentUser = new TestCurrentUserService(tenantId);
        await using var dbContext = await CreateDbContextAsync(currentUser);
        var shipment = await AddShipmentAsync(dbContext, tenantId, "SHP-LOC-1");

        var result = await new AddShipmentLocationCommandHandler(dbContext, currentUser)
            .Handle(new AddShipmentLocationCommand(
                shipment.Id,
                LocationType.Stop,
                "Cross dock",
                "12 Transit Road",
                3,
                10.5,
                106.7,
                "Ops",
                "+84999999999"), CancellationToken.None);

        Assert.Contains(result.Locations, location =>
            location.Type == LocationType.Stop && location.Sequence == 3 && location.Latitude == 10.5);
    }

    [Fact]
    public async Task LocationMutation_rejects_duplicate_sequence_and_invalid_coordinates()
    {
        var tenantId = Guid.NewGuid();
        var currentUser = new TestCurrentUserService(tenantId);
        await using var dbContext = await CreateDbContextAsync(currentUser);
        var shipment = await AddShipmentAsync(dbContext, tenantId, "SHP-LOC-2");

        await Assert.ThrowsAsync<DomainException>(() =>
            new AddShipmentLocationCommandHandler(dbContext, currentUser)
                .Handle(new AddShipmentLocationCommand(
                    shipment.Id,
                    LocationType.Stop,
                    "Duplicate",
                    "Duplicate address",
                    1,
                    null,
                    null,
                    null,
                    null), CancellationToken.None));

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            new AddShipmentLocationCommandHandler(dbContext, currentUser)
                .Handle(new AddShipmentLocationCommand(
                    shipment.Id,
                    LocationType.Stop,
                    "Invalid",
                    "Invalid address",
                    3,
                    91,
                    null,
                    null,
                    null), CancellationToken.None));
    }

    [Fact]
    public async Task UpdateAndRemoveShipmentLocation_mutates_existing_location()
    {
        var tenantId = Guid.NewGuid();
        var currentUser = new TestCurrentUserService(tenantId);
        await using var dbContext = await CreateDbContextAsync(currentUser);
        var shipment = await AddShipmentAsync(dbContext, tenantId, "SHP-LOC-3");
        var delivery = shipment.Locations.Single(location => location.Type == LocationType.Delivery);

        var updated = await new UpdateShipmentLocationCommandHandler(dbContext, currentUser)
            .Handle(new UpdateShipmentLocationCommand(
                shipment.Id,
                delivery.Id,
                LocationType.Warehouse,
                "Updated warehouse",
                "Updated address",
                2,
                null,
                null,
                null,
                null), CancellationToken.None);

        Assert.Contains(updated.Locations, location =>
            location.Id == delivery.Id && location.Type == LocationType.Warehouse && location.Name == "Updated warehouse");

        var removed = await new RemoveShipmentLocationCommandHandler(dbContext, currentUser)
            .Handle(new RemoveShipmentLocationCommand(shipment.Id, delivery.Id), CancellationToken.None);

        Assert.DoesNotContain(removed.Locations, location => location.Id == delivery.Id);
    }

    [Fact]
    public async Task CargoAndLocationCommands_preserve_tenant_isolation()
    {
        var tenantA = Guid.NewGuid();
        var currentUserA = new TestCurrentUserService(tenantA);
        await using var dbContextA = await CreateDbContextAsync(currentUserA);
        var shipment = await AddShipmentAsync(dbContextA, tenantA, "SHP-ISO-1");

        var currentUserB = new TestCurrentUserService(Guid.NewGuid());
        await using var dbContextB = CreateDbContext(currentUserB);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new AddCargoItemCommandHandler(dbContextB, currentUserB)
                .Handle(new AddCargoItemCommand(shipment.Id, "Other tenant", 1, 1, null), CancellationToken.None));

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new AddShipmentLocationCommandHandler(dbContextB, currentUserB)
                .Handle(new AddShipmentLocationCommand(
                    shipment.Id,
                    LocationType.Stop,
                    "Other tenant",
                    "Other tenant address",
                    3,
                    null,
                    null,
                    null,
                    null), CancellationToken.None));
    }

    [Fact]
    public async Task SubmitShipment_requires_cargo_pickup_and_delivery_locations()
    {
        var tenantId = Guid.NewGuid();
        var currentUser = new TestCurrentUserService(tenantId);
        await using var dbContext = await CreateDbContextAsync(currentUser);
        var shipment = ShipmentEntity.Create(
            tenantId,
            "SHP-REQ-1",
            orderId: "ORD-REQ-1",
            customerName: "Acme",
            destinationAddress: "Warehouse 9");
        dbContext.Shipments.Add(shipment);
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();

        await Assert.ThrowsAsync<DomainException>(() =>
            new SubmitShipmentCommandHandler(dbContext, currentUser)
                .Handle(new SubmitShipmentCommand(shipment.Id), CancellationToken.None));
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

    private static async Task<ShipmentWorkflowDbContext> CreateDbContextAsync(
        TestCurrentUserService currentUser)
    {
        var dbContext = CreateDbContext(currentUser);
        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();
        return dbContext;
    }

    private static ShipmentWorkflowDbContext CreateDbContext(
        TestCurrentUserService currentUser)
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
