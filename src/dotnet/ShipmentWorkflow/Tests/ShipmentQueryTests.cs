using Microsoft.EntityFrameworkCore;
using Shared.Exceptions;
using Shared.Interceptors;
using Shared.Security;
using ShipmentEntity = global::ShipmentWorkflow.Domain.Entities.Shipment;
using ShipmentWorkflow.Application.Queries.Shipments;
using ShipmentWorkflow.Domain.Enums;
using ShipmentWorkflow.Infrastructure.Persistences;

namespace ShipmentWorkflow.Tests;

[Collection("ShipmentWorkflowDatabase")]
public sealed class ShipmentQueryTests
{
    private const string ConnectionString =
        "Host=localhost;Port=5433;Database=aurora_shipment_workflow_tests;Username=postgres;Password=postgres";

    [Fact]
    public async Task GetShipment_returns_tenant_owned_aggregate()
    {
        var tenantId = Guid.NewGuid();
        var currentUser = new TestCurrentUserService(tenantId);
        await using var dbContext = await CreateDbContextAsync(currentUser);
        var shipment = CreateShipment(tenantId, "SHP-GET-1", "Acme");
        shipment.AddCargoItem("Laptop", 1, 2.5, "8471");
        shipment.AddLocation(LocationType.Pickup, "Factory", "Factory address", 1);
        shipment.AddDocumentMetadata("invoice.pdf", DocumentType.Invoice, "s3://invoice.pdf", currentUser.UserId, DateTimeOffset.UtcNow);
        shipment.Submit(currentUser.UserId);
        dbContext.Shipments.Add(shipment);
        await dbContext.SaveChangesAsync();

        var result = await new GetShipmentQueryHandler(dbContext, currentUser)
            .Handle(new GetShipmentQuery(shipment.Id), CancellationToken.None);

        Assert.Equal(shipment.Id, result.Id);
        Assert.Single(result.CargoItems);
        Assert.Single(result.Locations);
        Assert.Single(result.Documents);
        Assert.Single(result.Milestones);
    }

    [Fact]
    public async Task GetShipment_returns_not_found_for_missing_or_other_tenant()
    {
        var tenantA = Guid.NewGuid();
        var currentUserA = new TestCurrentUserService(tenantA);
        await using var dbContextA = await CreateDbContextAsync(currentUserA);
        var shipment = CreateShipment(tenantA, "SHP-GET-2", "Acme");
        dbContextA.Shipments.Add(shipment);
        await dbContextA.SaveChangesAsync();

        var tenantB = new TestCurrentUserService(Guid.NewGuid());
        await using var dbContextB = CreateDbContext(tenantB);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new GetShipmentQueryHandler(dbContextB, tenantB)
                .Handle(new GetShipmentQuery(shipment.Id), CancellationToken.None));

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new GetShipmentQueryHandler(dbContextB, tenantB)
                .Handle(new GetShipmentQuery(Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task Queries_reject_missing_tenant_context()
    {
        var currentUser = new TestCurrentUserService(null);
        await using var dbContext = await CreateDbContextAsync(currentUser);

        await Assert.ThrowsAsync<DomainException>(() =>
            new GetShipmentQueryHandler(dbContext, currentUser)
                .Handle(new GetShipmentQuery(Guid.NewGuid()), CancellationToken.None));

        await Assert.ThrowsAsync<DomainException>(() =>
            new ListShipmentsQueryHandler(dbContext, currentUser)
                .Handle(new ListShipmentsQuery(1, 10, null, null, null, null, null), CancellationToken.None));

        await Assert.ThrowsAsync<DomainException>(() =>
            new GetShipmentTimelineQueryHandler(dbContext, currentUser)
                .Handle(new GetShipmentTimelineQuery(Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task ListShipments_paginates_filters_and_orders_stably()
    {
        var tenantId = Guid.NewGuid();
        var currentUser = new TestCurrentUserService(tenantId);
        await using var dbContext = await CreateDbContextAsync(currentUser);

        var draft = CreateShipment(tenantId, "SHP-LIST-1", "Acme");
        var submitted = CreateShipment(tenantId, "SHP-LIST-2", "Beta");
        submitted.Submit(currentUser.UserId);
        dbContext.Shipments.AddRange(draft, submitted);
        await dbContext.SaveChangesAsync();

        var handler = new ListShipmentsQueryHandler(dbContext, currentUser);

        var page = await handler.Handle(
            new ListShipmentsQuery(1, 1, null, "SHP-LIST", null, null, null),
            CancellationToken.None);
        var filtered = await handler.Handle(
            new ListShipmentsQuery(1, 10, "Submitted", null, "Beta", null, null),
            CancellationToken.None);

        Assert.Equal(2, page.TotalItems);
        Assert.Equal(2, page.TotalPages);
        Assert.Single(page.Shipments);
        Assert.Single(filtered.Shipments);
        Assert.Equal(submitted.Id, filtered.Shipments.Single().Id);
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(1, 0)]
    [InlineData(1, 101)]
    public async Task ListShipments_validates_paging(int page, int limit)
    {
        var currentUser = new TestCurrentUserService(Guid.NewGuid());
        await using var dbContext = await CreateDbContextAsync(currentUser);

        await Assert.ThrowsAsync<DomainException>(() =>
            new ListShipmentsQueryHandler(dbContext, currentUser)
                .Handle(new ListShipmentsQuery(page, limit, null, null, null, null, null), CancellationToken.None));
    }

    [Fact]
    public async Task Timeline_combines_status_history_and_business_milestones_in_order()
    {
        var tenantId = Guid.NewGuid();
        var currentUser = new TestCurrentUserService(tenantId);
        await using var dbContext = await CreateDbContextAsync(currentUser);
        var shipment = CreateShipment(tenantId, "SHP-TIME-1", "Acme");
        shipment.Submit(currentUser.UserId);
        shipment.StartPlanning(currentUser.UserId);
        dbContext.Shipments.Add(shipment);
        await dbContext.SaveChangesAsync();

        var result = await new GetShipmentTimelineQueryHandler(dbContext, currentUser)
            .Handle(new GetShipmentTimelineQuery(shipment.Id), CancellationToken.None);

        Assert.Equal(shipment.Id, result.ShipmentId);
        Assert.Equal(4, result.Items.Count);
        Assert.True(result.Items.SequenceEqual(result.Items.OrderBy(item => item.CreatedAt).ThenBy(item => item.Source, StringComparer.Ordinal)));
    }

    private static ShipmentEntity CreateShipment(Guid tenantId, string number, string customerName)
    {
        return ShipmentEntity.Create(
            tenantId,
            number,
            orderId: number.Replace("SHP", "ORD", StringComparison.Ordinal),
            customerName: customerName,
            destinationAddress: "Warehouse 9");
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
