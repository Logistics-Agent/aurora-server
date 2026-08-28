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
public sealed class ShipmentCommandTests
{
    private const string ConnectionString =
        "Host=localhost;Port=5433;Database=aurora_shipment_workflow_tests;Username=postgres;Password=postgres";

    [Fact]
    public async Task SubmitShipment_transitions_and_writes_outbox()
    {
        var tenantId = Guid.NewGuid();
        var currentUser = new TestCurrentUserService(tenantId);
        await using var dbContext = await CreateDbContextAsync(currentUser);
        var shipment = await AddShipmentAsync(dbContext, tenantId, "SHP-CMD-1");

        var result = await new SubmitShipmentCommandHandler(dbContext, currentUser)
            .Handle(new SubmitShipmentCommand(shipment.Id), CancellationToken.None);

        Assert.Equal(ShipmentStatus.Submitted, result.Status);
        Assert.Contains(await dbContext.OutboxMessages.ToListAsync(), message =>
            message.EventType == nameof(ShipmentStatusChangedEvent));
    }

    [Fact]
    public async Task UpdateShipment_changes_editable_fields_before_processing()
    {
        var tenantId = Guid.NewGuid();
        var currentUser = new TestCurrentUserService(tenantId);
        await using var dbContext = await CreateDbContextAsync(currentUser);
        var shipment = await AddShipmentAsync(dbContext, tenantId, "SHP-CMD-2");

        var result = await new UpdateShipmentCommandHandler(dbContext, currentUser)
            .Handle(new UpdateShipmentCommand(
                shipment.Id,
                "Beta",
                "New Warehouse",
                ShipmentPriority.High,
                TransportMode.Road,
                "Handle carefully"), CancellationToken.None);

        Assert.Equal("Beta", result.CustomerName);
        Assert.Equal("New Warehouse", result.DestinationAddress);
        Assert.Equal(ShipmentPriority.High, result.Priority);
        Assert.Equal(TransportMode.Road, result.TransportMode);
    }

    [Fact]
    public async Task UpdateShipment_rejects_after_processing_started()
    {
        var tenantId = Guid.NewGuid();
        var currentUser = new TestCurrentUserService(tenantId);
        await using var dbContext = await CreateDbContextAsync(currentUser);
        var shipment = await AddShipmentAsync(dbContext, tenantId, "SHP-CMD-3", shipment =>
        {
            shipment.Submit();
            shipment.StartPlanning();
        });

        await Assert.ThrowsAsync<DomainException>(() =>
            new UpdateShipmentCommandHandler(dbContext, currentUser)
                .Handle(new UpdateShipmentCommand(
                    shipment.Id,
                    "Beta",
                    "New Warehouse",
                    ShipmentPriority.High,
                    TransportMode.Road,
                    null), CancellationToken.None));
    }

    [Fact]
    public async Task UpdateShipmentStatus_uses_state_machine_and_rejects_invalid_transition()
    {
        var tenantId = Guid.NewGuid();
        var currentUser = new TestCurrentUserService(tenantId);
        await using var dbContext = await CreateDbContextAsync(currentUser);
        var shipment = await AddShipmentAsync(dbContext, tenantId, "SHP-CMD-4");
        var handler = new UpdateShipmentStatusCommandHandler(dbContext, currentUser);

        var submitted = await handler.Handle(
            new UpdateShipmentStatusCommand(shipment.Id, ShipmentStatus.Submitted, "submit"),
            CancellationToken.None);

        Assert.Equal(ShipmentStatus.Submitted, submitted.Status);
        await Assert.ThrowsAsync<DomainException>(() => handler.Handle(
            new UpdateShipmentStatusCommand(shipment.Id, ShipmentStatus.Delivered, "skip"),
            CancellationToken.None));
    }

    [Fact]
    public async Task CancelShipment_transitions_and_writes_cancel_outbox()
    {
        var tenantId = Guid.NewGuid();
        var currentUser = new TestCurrentUserService(tenantId);
        await using var dbContext = await CreateDbContextAsync(currentUser);
        var shipment = await AddShipmentAsync(dbContext, tenantId, "SHP-CMD-5");

        var result = await new CancelShipmentCommandHandler(dbContext, currentUser)
            .Handle(new CancelShipmentCommand(shipment.Id, "customer request"), CancellationToken.None);

        Assert.Equal(ShipmentStatus.Cancelled, result.Status);
        var messages = await dbContext.OutboxMessages.ToListAsync();
        Assert.Contains(messages, message => message.EventType == nameof(ShipmentCancelledEvent));
        Assert.Contains(messages, message => message.EventType == nameof(ShipmentStatusChangedEvent));
    }

    [Fact]
    public async Task DeleteDraftShipment_removes_only_draft_shipments()
    {
        var tenantId = Guid.NewGuid();
        var currentUser = new TestCurrentUserService(tenantId);
        await using var dbContext = await CreateDbContextAsync(currentUser);
        var draft = await AddShipmentAsync(dbContext, tenantId, "SHP-CMD-6");
        var submitted = await AddShipmentAsync(dbContext, tenantId, "SHP-CMD-7", shipment => shipment.Submit());
        var handler = new DeleteDraftShipmentCommandHandler(dbContext, currentUser);

        await handler.Handle(new DeleteDraftShipmentCommand(draft.Id), CancellationToken.None);

        Assert.False(await dbContext.Shipments.AnyAsync(shipment => shipment.Id == draft.Id));
        await Assert.ThrowsAsync<DomainException>(() =>
            handler.Handle(new DeleteDraftShipmentCommand(submitted.Id), CancellationToken.None));
    }

    [Fact]
    public async Task Commands_reject_missing_tenant_and_cross_tenant_mutation()
    {
        var tenantA = Guid.NewGuid();
        var currentUserA = new TestCurrentUserService(tenantA);
        await using var dbContextA = await CreateDbContextAsync(currentUserA);
        var shipment = await AddShipmentAsync(dbContextA, tenantA, "SHP-CMD-8");

        var noTenant = new TestCurrentUserService(null);
        await using var dbContextNoTenant = CreateDbContext(noTenant);
        await Assert.ThrowsAsync<DomainException>(() =>
            new SubmitShipmentCommandHandler(dbContextNoTenant, noTenant)
                .Handle(new SubmitShipmentCommand(shipment.Id), CancellationToken.None));

        var currentUserB = new TestCurrentUserService(Guid.NewGuid());
        await using var dbContextB = CreateDbContext(currentUserB);
        await Assert.ThrowsAsync<NotFoundException>(() =>
            new SubmitShipmentCommandHandler(dbContextB, currentUserB)
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
        public string? Role { get; } = "STAFF";
        public IReadOnlyList<string> Permissions { get; } = [];
    }
}
