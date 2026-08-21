using Microsoft.EntityFrameworkCore;
using Shared.Interceptors;
using Shared.Security;
using ShipmentEntity = global::ShipmentWorkflow.Domain.Entities.Shipment;
using ShipmentWorkflow.Domain.Enums;
using ShipmentWorkflow.Infrastructure.Persistences;

namespace ShipmentWorkflow.Tests;

[Collection("ShipmentWorkflowDatabase")]
public sealed class ShipmentAggregateExpansionTests
{
    private const string ConnectionString =
        "Host=localhost;Port=5433;Database=aurora_shipment_workflow_tests;Username=postgres;Password=postgres";

    [Fact]
    public async Task Shipment_stores_valid_locations_documents_and_milestones()
    {
        var tenantId = Guid.NewGuid();
        var uploadedBy = Guid.NewGuid();
        var recordedBy = Guid.NewGuid();
        var currentUser = new TestCurrentUserService(tenantId, uploadedBy);
        await using var dbContext = await CreateDbContextAsync(currentUser);
        var shipment = CreateShipment(tenantId);

        shipment.AddLocation(
            LocationType.Pickup,
            "Factory",
            "1 Industrial Road",
            sequence: 1,
            latitude: 10.7769,
            longitude: 106.7009,
            contactName: "Ops",
            contactPhone: "+84900000000");
        shipment.AddDocumentMetadata(
            "invoice.pdf",
            DocumentType.Invoice,
            "s3://shipments/invoice.pdf",
            uploadedBy,
            DateTimeOffset.UtcNow,
            OCRStatus.Completed,
            0.9825m,
            "{\"invoiceNo\":\"INV-1\"}");
        shipment.AddMilestone(
            ShipmentStatus.InTransit,
            "Departed pickup location",
            DateTimeOffset.UtcNow,
            MilestoneSource.Staff,
            recordedBy,
            latitude: 10.7769,
            longitude: 106.7009);

        dbContext.Shipments.Add(shipment);
        await dbContext.SaveChangesAsync();

        var saved = await dbContext.Shipments
            .Include(s => s.Locations)
            .Include(s => s.Documents)
            .Include(s => s.Milestones)
            .SingleAsync();

        var location = Assert.Single(saved.Locations);
        Assert.Equal(tenantId, location.TenantId);
        Assert.Equal(LocationType.Pickup, location.Type);
        Assert.Equal(1, location.Sequence);

        var document = Assert.Single(saved.Documents);
        Assert.Equal(tenantId, document.TenantId);
        Assert.Equal(DocumentType.Invoice, document.DocumentType);
        Assert.Equal(OCRStatus.Completed, document.OCRStatus);
        Assert.Equal(0.9825m, document.OCRConfidence);
        Assert.Contains("INV-1", document.ExtractedDataJson);

        var milestone = Assert.Single(saved.Milestones);
        Assert.Equal(tenantId, milestone.TenantId);
        Assert.Equal(ShipmentStatus.InTransit, milestone.Status);
        Assert.Equal(MilestoneSource.Staff, milestone.Source);
        Assert.Equal(recordedBy, milestone.CreatedByUserId);
    }

    [Fact]
    public void AddLocation_rejects_invalid_sequence()
    {
        var shipment = CreateShipment(Guid.NewGuid());

        Assert.Throws<ArgumentException>(() => shipment.AddLocation(
            LocationType.Pickup,
            "Factory",
            "1 Industrial Road",
            sequence: 0));
    }

    [Theory]
    [InlineData(-90.1, 0)]
    [InlineData(90.1, 0)]
    [InlineData(0, -180.1)]
    [InlineData(0, 180.1)]
    public void AddLocation_rejects_invalid_coordinates(
        double latitude,
        double longitude)
    {
        var shipment = CreateShipment(Guid.NewGuid());

        Assert.Throws<ArgumentOutOfRangeException>(() => shipment.AddLocation(
            LocationType.Pickup,
            "Factory",
            "1 Industrial Road",
            sequence: 1,
            latitude: latitude,
            longitude: longitude));
    }

    [Fact]
    public void AddDocumentMetadata_rejects_invalid_ocr_confidence()
    {
        var shipment = CreateShipment(Guid.NewGuid());

        Assert.Throws<ArgumentOutOfRangeException>(() => shipment.AddDocumentMetadata(
            "invoice.pdf",
            DocumentType.Invoice,
            "s3://shipments/invoice.pdf",
            uploadedBy: Guid.NewGuid(),
            uploadedAt: DateTimeOffset.UtcNow,
            ocrStatus: OCRStatus.Completed,
            ocrConfidence: 1.01m));
    }

    [Theory]
    [InlineData(-90.1, 0)]
    [InlineData(90.1, 0)]
    [InlineData(0, -180.1)]
    [InlineData(0, 180.1)]
    public void AddMilestone_rejects_invalid_coordinates(
        double latitude,
        double longitude)
    {
        var shipment = CreateShipment(Guid.NewGuid());

        Assert.Throws<ArgumentOutOfRangeException>(() => shipment.AddMilestone(
            ShipmentStatus.InTransit,
            "Moved",
            DateTimeOffset.UtcNow,
            MilestoneSource.Gps,
            createdBy: null,
            latitude: latitude,
            longitude: longitude));
    }

    [Fact]
    public async Task Tenant_filter_prevents_other_tenant_from_reading_child_entities()
    {
        var tenantA = Guid.NewGuid();
        var currentUserA = new TestCurrentUserService(tenantA);
        await using var dbContextA = await CreateDbContextAsync(currentUserA);
        var shipment = CreateShipment(tenantA);

        shipment.AddLocation(
            LocationType.Pickup,
            "Factory",
            "1 Industrial Road",
            sequence: 1);
        shipment.AddDocumentMetadata(
            "invoice.pdf",
            DocumentType.Invoice,
            "s3://shipments/invoice.pdf",
            uploadedBy: Guid.NewGuid(),
            uploadedAt: DateTimeOffset.UtcNow);
        shipment.AddMilestone(
            ShipmentStatus.InTransit,
            "Departed",
            DateTimeOffset.UtcNow,
            MilestoneSource.Staff,
            createdBy: Guid.NewGuid());

        dbContextA.Shipments.Add(shipment);
        await dbContextA.SaveChangesAsync();

        var currentUserB = new TestCurrentUserService(Guid.NewGuid());
        await using var dbContextB = CreateDbContext(currentUserB);

        Assert.Empty(await dbContextB.ShipmentLocations.ToListAsync());
        Assert.Empty(await dbContextB.ShipmentDocuments.ToListAsync());
        Assert.Empty(await dbContextB.ShipmentMilestones.ToListAsync());
    }

    [Fact]
    public async Task Missing_tenant_context_does_not_expose_child_entities()
    {
        var tenantA = Guid.NewGuid();
        var currentUserA = new TestCurrentUserService(tenantA);
        await using var dbContextA = await CreateDbContextAsync(currentUserA);
        var shipment = CreateShipment(tenantA);

        shipment.AddLocation(
            LocationType.Pickup,
            "Factory",
            "1 Industrial Road",
            sequence: 1);
        shipment.AddDocumentMetadata(
            "invoice.pdf",
            DocumentType.Invoice,
            "s3://shipments/invoice.pdf",
            uploadedBy: Guid.NewGuid(),
            uploadedAt: DateTimeOffset.UtcNow);
        shipment.AddMilestone(
            ShipmentStatus.InTransit,
            "Departed",
            DateTimeOffset.UtcNow,
            MilestoneSource.Staff,
            createdBy: Guid.NewGuid());

        dbContextA.Shipments.Add(shipment);
        await dbContextA.SaveChangesAsync();

        var missingTenantUser = new TestCurrentUserService(null);
        await using var dbContextWithoutTenant = CreateDbContext(missingTenantUser);

        Assert.Empty(await dbContextWithoutTenant.ShipmentLocations.ToListAsync());
        Assert.Empty(await dbContextWithoutTenant.ShipmentDocuments.ToListAsync());
        Assert.Empty(await dbContextWithoutTenant.ShipmentMilestones.ToListAsync());
    }

    private static ShipmentEntity CreateShipment(Guid tenantId)
    {
        return ShipmentEntity.Create(
            tenantId,
            $"SHP-TEST-{Guid.CreateVersion7():N}",
            orderId: "ORD-1",
            customerName: "Acme",
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

    private sealed class TestCurrentUserService(
        Guid? tenantId,
        Guid? userId = null) : ICurrentUserService
    {
        public Guid? UserId { get; } = userId ?? Guid.NewGuid();
        public Guid? TenantId { get; } = tenantId;
        public string? TraceId { get; } = Guid.NewGuid().ToString();
        public int? PermissionVersion { get; } = 1;
        public IReadOnlyList<string> RoleIds { get; } = [];
        public IReadOnlyList<string> Permissions { get; } = [];
    }
}
