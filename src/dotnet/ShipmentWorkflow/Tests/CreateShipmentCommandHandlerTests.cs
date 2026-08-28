using Microsoft.EntityFrameworkCore;
using Shared.Exceptions;
using Shared.Interceptors;
using Shared.Security;
using Shipment.Contracts.Events;
using ShipmentWorkflow.Application.Commands.Shipments;
using ShipmentWorkflow.Application.Interfaces;
using ShipmentWorkflow.Domain.Enums;
using ShipmentWorkflow.Infrastructure.Persistences;

namespace ShipmentWorkflow.Tests;

[Collection("ShipmentWorkflowDatabase")]
public sealed class CreateShipmentCommandHandlerTests
{
    private const string ConnectionString =
        "Host=localhost;Port=5433;Database=aurora_shipment_workflow_tests;Username=postgres;Password=postgres";

    [Fact]
    public async Task Handle_creates_shipment_with_cargo_history_and_outbox()
    {
        var tenantId = Guid.NewGuid();
        var currentUser = new TestCurrentUserService(tenantId);
        await using var dbContext = await CreateDbContextAsync(currentUser);
        var handler = CreateHandler(dbContext, currentUser);

        var result = await handler.Handle(
            ValidCommand(),
            CancellationToken.None);

        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.Equal(tenantId, result.TenantId);
        Assert.Equal("Acme", result.CustomerName);
        Assert.Equal("Warehouse 9", result.DestinationAddress);
        Assert.Equal(ShipmentStatus.Created, result.Status);
        Assert.StartsWith("SHP-", result.ShipmentNo, StringComparison.Ordinal);
        Assert.Single(result.CargoItems);

        var shipment = await dbContext.Shipments
            .Include(s => s.CargoItems)
            .Include(s => s.StatusHistories)
            .SingleAsync();

        Assert.Equal(tenantId, shipment.TenantId);
        Assert.Single(shipment.CargoItems);
        Assert.Equal("Laptop", shipment.CargoItems.Single().Name);
        Assert.Single(shipment.StatusHistories);
        Assert.Equal(ShipmentStatus.Created, shipment.StatusHistories.Single().Status);

        var outboxMessage = await dbContext.OutboxMessages.SingleAsync();
        Assert.Equal(nameof(ShipmentCreatedEvent), outboxMessage.EventType);
        Assert.Contains(shipment.Id.ToString(), outboxMessage.Payload);
        Assert.Contains(shipment.ShipmentNo, outboxMessage.Payload);
    }

    [Fact]
    public async Task Handle_rejects_missing_tenant_context()
    {
        var currentUser = new TestCurrentUserService(null);
        await using var dbContext = await CreateDbContextAsync(currentUser);
        var handler = CreateHandler(dbContext, currentUser);

        await Assert.ThrowsAsync<DomainException>(() =>
            handler.Handle(ValidCommand(), CancellationToken.None));

        Assert.Empty(await dbContext.Shipments.ToListAsync());
    }

    [Theory]
    [InlineData("", "Warehouse 9")]
    [InlineData("   ", "Warehouse 9")]
    [InlineData("Acme", "")]
    [InlineData("Acme", "   ")]
    public async Task Handle_rejects_required_shipment_fields(
        string customerName,
        string destinationAddress)
    {
        var currentUser = new TestCurrentUserService(Guid.NewGuid());
        await using var dbContext = await CreateDbContextAsync(currentUser);
        var handler = CreateHandler(dbContext, currentUser);

        var command = ValidCommand(
            customerName: customerName,
            destinationAddress: destinationAddress);

        await Assert.ThrowsAsync<DomainException>(() =>
            handler.Handle(command, CancellationToken.None));

        Assert.Empty(await dbContext.Shipments.ToListAsync());
    }

    [Theory]
    [InlineData("", 1, 1)]
    [InlineData("Laptop", 0, 1)]
    [InlineData("Laptop", -1, 1)]
    [InlineData("Laptop", 1, -0.1)]
    public async Task Handle_rejects_invalid_cargo_fields(
        string name,
        int quantity,
        double weightKg)
    {
        var currentUser = new TestCurrentUserService(Guid.NewGuid());
        await using var dbContext = await CreateDbContextAsync(currentUser);
        var handler = CreateHandler(dbContext, currentUser);

        var command = ValidCommand(cargoItems:
        [
            new CreateShipmentCargoItem(name, quantity, weightKg, "8471")
        ]);

        await Assert.ThrowsAsync<DomainException>(() =>
            handler.Handle(command, CancellationToken.None));

        Assert.Empty(await dbContext.Shipments.ToListAsync());
    }

    [Fact]
    public async Task Tenant_filter_prevents_other_tenant_from_reading_shipment()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var currentUserA = new TestCurrentUserService(tenantA);
        await using var dbContextA = await CreateDbContextAsync(currentUserA);
        var handler = CreateHandler(dbContextA, currentUserA);

        await handler.Handle(ValidCommand(), CancellationToken.None);

        var currentUserB = new TestCurrentUserService(tenantB);
        await using var dbContextB = CreateDbContext(currentUserB);

        Assert.Empty(await dbContextB.Shipments.ToListAsync());
        Assert.Empty(await dbContextB.CargoItems.ToListAsync());
        Assert.Empty(await dbContextB.ShipmentStatusHistories.ToListAsync());
    }

    private static CreateShipmentCommand ValidCommand(
        string customerName = "Acme",
        string destinationAddress = "Warehouse 9",
        IReadOnlyCollection<CreateShipmentCargoItem>? cargoItems = null)
    {
        return new CreateShipmentCommand(
            OrderId: "ORD-1",
            CustomerName: customerName,
            DestinationAddress: destinationAddress,
            CargoItems: cargoItems ??
            [
                new CreateShipmentCargoItem("Laptop", 2, 3.5, "8471")
            ]);
    }

    private static CreateShipmentCommandHandler CreateHandler(
        ShipmentWorkflowDbContext dbContext,
        ICurrentUserService currentUser)
    {
        return new CreateShipmentCommandHandler(
            dbContext,
            currentUser,
            new TestShipmentNumberGenerator());
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

    private sealed class TestShipmentNumberGenerator : IShipmentNumberGenerator
    {
        public string Generate()
        {
            return $"SHP-TEST-{Guid.CreateVersion7():N}";
        }
    }
}
