using Microsoft.EntityFrameworkCore;
using Shared.Exceptions;
using Shared.Interceptors;
using Shared.Security;
using Shipment.Contracts.Events;
using ShipmentWorkflow.Application.Commands.Shipments;
using ShipmentWorkflow.Application.Interfaces;
using ShipmentWorkflow.Infrastructure.Persistences;

namespace ShipmentWorkflow.Tests;

[Collection("ShipmentWorkflowDatabase")]
public sealed class ShipmentImportTests
{
    private const string ConnectionString =
        "Host=localhost;Port=5433;Database=aurora_shipment_workflow_tests;Username=postgres;Password=postgres";

    [Fact]
    public async Task ImportShipments_creates_valid_rows_and_outbox_messages()
    {
        var tenantId = Guid.NewGuid();
        var currentUser = new TestCurrentUserService(tenantId);
        await using var dbContext = await CreateDbContextAsync(currentUser);
        const string csv = """
orderId,customerName,destinationAddress,cargoName,quantity,weightKg,hsCode
ORD-1,Acme,Warehouse 1,Laptop,2,5.5,8471
ORD-2,Beta,Warehouse 2,Parts,1,3.2,8708
""";

        var result = await new ImportShipmentsCommandHandler(dbContext, currentUser, new TestShipmentNumberGenerator())
            .Handle(new ImportShipmentsCommand("shipments.csv", csv, "IMP-1"), CancellationToken.None);

        Assert.Equal(2, result.SuccessCount);
        Assert.Equal(0, result.ErrorCount);
        Assert.Equal(2, await dbContext.Shipments.CountAsync());
        Assert.Equal(2, await dbContext.OutboxMessages.CountAsync(message =>
            message.EventType == nameof(ShipmentCreatedEvent)));
    }

    [Fact]
    public async Task ImportShipments_reports_invalid_rows_without_blocking_valid_rows()
    {
        var tenantId = Guid.NewGuid();
        var currentUser = new TestCurrentUserService(tenantId);
        await using var dbContext = await CreateDbContextAsync(currentUser);
        const string csv = """
orderId,customerName,destinationAddress,cargoName,quantity,weightKg,hsCode
ORD-1,Acme,Warehouse 1,Laptop,2,5.5,8471
ORD-2,,Warehouse 2,Parts,0,3.2,8708
""";

        var result = await new ImportShipmentsCommandHandler(dbContext, currentUser, new TestShipmentNumberGenerator())
            .Handle(new ImportShipmentsCommand("shipments.csv", csv, null), CancellationToken.None);

        Assert.Equal(1, result.SuccessCount);
        Assert.Equal(1, result.ErrorCount);
        Assert.Contains(result.Rows, row => !row.Success && row.RowNumber == 3);
        Assert.Equal(1, await dbContext.Shipments.CountAsync());
    }

    [Fact]
    public async Task ImportShipments_rejects_missing_required_columns_and_tenant_id_column()
    {
        var tenantId = Guid.NewGuid();
        var currentUser = new TestCurrentUserService(tenantId);
        await using var dbContext = await CreateDbContextAsync(currentUser);
        var handler = new ImportShipmentsCommandHandler(dbContext, currentUser, new TestShipmentNumberGenerator());

        await Assert.ThrowsAsync<DomainException>(() => handler.Handle(
            new ImportShipmentsCommand(
                "shipments.csv",
                """
customerName,destinationAddress,cargoName,quantity
Acme,Warehouse,Laptop,1
""",
                null), CancellationToken.None));

        await Assert.ThrowsAsync<DomainException>(() => handler.Handle(
            new ImportShipmentsCommand(
                "shipments.csv",
                $$"""
tenantId,customerName,destinationAddress,cargoName,quantity,weightKg
{{tenantId}},Acme,Warehouse,Laptop,1,2
""",
                null), CancellationToken.None));
    }

    [Fact]
    public async Task ImportShipments_enforces_row_limit_and_file_type()
    {
        var tenantId = Guid.NewGuid();
        var currentUser = new TestCurrentUserService(tenantId);
        await using var dbContext = await CreateDbContextAsync(currentUser);
        var handler = new ImportShipmentsCommandHandler(dbContext, currentUser, new TestShipmentNumberGenerator());
        var rows = string.Join("\n", Enumerable.Range(1, 101).Select(i => $"ORD-{i},Acme,Warehouse,Laptop,1,2"));
        var csv = "orderId,customerName,destinationAddress,cargoName,quantity,weightKg\n" + rows;

        await Assert.ThrowsAsync<DomainException>(() => handler.Handle(
            new ImportShipmentsCommand("shipments.csv", csv, null), CancellationToken.None));

        await Assert.ThrowsAsync<DomainException>(() => handler.Handle(
            new ImportShipmentsCommand("shipments.xlsx", csv, null), CancellationToken.None));
    }

    [Fact]
    public async Task ImportShipments_rejects_missing_tenant_context()
    {
        var currentUser = new TestCurrentUserService(null);
        await using var dbContext = await CreateDbContextAsync(currentUser);
        const string csv = """
orderId,customerName,destinationAddress,cargoName,quantity,weightKg
ORD-1,Acme,Warehouse,Laptop,1,2
""";

        await Assert.ThrowsAsync<DomainException>(() =>
            new ImportShipmentsCommandHandler(dbContext, currentUser, new TestShipmentNumberGenerator())
                .Handle(new ImportShipmentsCommand("shipments.csv", csv, null), CancellationToken.None));
    }

    private static async Task<ShipmentWorkflowDbContext> CreateDbContextAsync(
        TestCurrentUserService currentUser)
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
        public string? Role { get; } = "STAFF";
        public IReadOnlyList<string> Permissions { get; } = [];
    }

    private sealed class TestShipmentNumberGenerator : IShipmentNumberGenerator
    {
        private int _value;

        public string Generate()
        {
            _value++;
            return $"SHP-IMPORT-{_value:000}";
        }
    }
}
