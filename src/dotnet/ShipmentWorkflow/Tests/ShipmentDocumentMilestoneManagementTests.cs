using Microsoft.EntityFrameworkCore;
using Shared.Exceptions;
using Shared.Interceptors;
using Shared.Security;
using Shipment.Contracts.Events;
using ShipmentWorkflow.Application.Commands.Shipments;
using ShipmentWorkflow.Application.Queries.Shipments;
using ShipmentWorkflow.Domain.Enums;
using ShipmentWorkflow.Infrastructure.Persistences;
using ShipmentEntity = global::ShipmentWorkflow.Domain.Entities.Shipment;

namespace ShipmentWorkflow.Tests;

[Collection("ShipmentWorkflowDatabase")]
public sealed class ShipmentDocumentMilestoneManagementTests
{
    private const string ConnectionString =
        "Host=localhost;Port=5433;Database=aurora_shipment_workflow_tests;Username=postgres;Password=postgres";

    [Fact]
    public async Task AttachShipmentDocument_stores_metadata_and_writes_outbox()
    {
        var tenantId = Guid.NewGuid();
        var currentUser = new TestCurrentUserService(tenantId);
        await using var dbContext = await CreateDbContextAsync(currentUser);
        var shipment = await AddShipmentAsync(dbContext, tenantId, "SHP-DOC-1");

        var result = await new AttachShipmentDocumentCommandHandler(dbContext, currentUser)
            .Handle(new AttachShipmentDocumentCommand(
                shipment.Id,
                "invoice.pdf",
                DocumentType.Invoice,
                "s3://shipment/invoice.pdf",
                OCRStatus.Pending,
                null,
                null), CancellationToken.None);

        Assert.Contains(result.Documents, document =>
            document.FileName == "invoice.pdf" && document.DocumentType == DocumentType.Invoice);
        Assert.Contains(await dbContext.OutboxMessages.ToListAsync(), message =>
            message.EventType == nameof(DocumentAttachedEvent));
    }

    [Fact]
    public async Task AttachShipmentDocument_rejects_invalid_ocr_confidence()
    {
        var tenantId = Guid.NewGuid();
        var currentUser = new TestCurrentUserService(tenantId);
        await using var dbContext = await CreateDbContextAsync(currentUser);
        var shipment = await AddShipmentAsync(dbContext, tenantId, "SHP-DOC-2");

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            new AttachShipmentDocumentCommandHandler(dbContext, currentUser)
                .Handle(new AttachShipmentDocumentCommand(
                    shipment.Id,
                    "invoice.pdf",
                    DocumentType.Invoice,
                    "s3://shipment/invoice.pdf",
                    OCRStatus.Completed,
                    1.1m,
                    null), CancellationToken.None));
    }

    [Fact]
    public async Task UpdateShipmentDocumentOcr_updates_controlled_metadata()
    {
        var tenantId = Guid.NewGuid();
        var currentUser = new TestCurrentUserService(tenantId);
        await using var dbContext = await CreateDbContextAsync(currentUser);
        var shipment = await AddShipmentWithDocumentAsync(dbContext, tenantId, "SHP-DOC-3");
        var documentId = shipment.Documents.Single().Id;

        var result = await new UpdateShipmentDocumentOcrCommandHandler(dbContext, currentUser)
            .Handle(new UpdateShipmentDocumentOcrCommand(
                shipment.Id,
                documentId,
                OCRStatus.Completed,
                0.96m,
                @"{""invoiceNo"":""INV-1""}"), CancellationToken.None);

        var document = result.Documents.Single(item => item.Id == documentId);
        Assert.Equal(OCRStatus.Completed, document.OCRStatus);
        Assert.Equal(0.96m, document.OCRConfidence);
        Assert.Contains("INV-1", document.ExtractedDataJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RemoveShipmentDocument_removes_metadata_only()
    {
        var tenantId = Guid.NewGuid();
        var currentUser = new TestCurrentUserService(tenantId);
        await using var dbContext = await CreateDbContextAsync(currentUser);
        var shipment = await AddShipmentWithDocumentAsync(dbContext, tenantId, "SHP-DOC-4");
        var documentId = shipment.Documents.Single().Id;

        var result = await new RemoveShipmentDocumentCommandHandler(dbContext, currentUser)
            .Handle(new RemoveShipmentDocumentCommand(shipment.Id, documentId), CancellationToken.None);

        Assert.DoesNotContain(result.Documents, document => document.Id == documentId);
    }

    [Fact]
    public async Task AddShipmentMilestone_stores_business_milestone()
    {
        var tenantId = Guid.NewGuid();
        var currentUser = new TestCurrentUserService(tenantId);
        await using var dbContext = await CreateDbContextAsync(currentUser);
        var shipment = await AddShipmentAsync(dbContext, tenantId, "SHP-MILE-1");
        var recordedAt = DateTimeOffset.UtcNow.AddMinutes(-5);

        var result = await new AddShipmentMilestoneCommandHandler(dbContext, currentUser)
            .Handle(new AddShipmentMilestoneCommand(
                shipment.Id,
                ShipmentStatus.InTransit,
                "Carrier checkpoint",
                recordedAt,
                MilestoneSource.ExternalProvider,
                10.75,
                106.66), CancellationToken.None);

        Assert.Contains(result.Milestones, milestone =>
            milestone.Status == ShipmentStatus.InTransit &&
            milestone.Source == MilestoneSource.ExternalProvider &&
            milestone.Latitude == 10.75);
    }

    [Fact]
    public async Task AddShipmentMilestone_rejects_invalid_coordinates()
    {
        var tenantId = Guid.NewGuid();
        var currentUser = new TestCurrentUserService(tenantId);
        await using var dbContext = await CreateDbContextAsync(currentUser);
        var shipment = await AddShipmentAsync(dbContext, tenantId, "SHP-MILE-2");

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            new AddShipmentMilestoneCommandHandler(dbContext, currentUser)
                .Handle(new AddShipmentMilestoneCommand(
                    shipment.Id,
                    ShipmentStatus.InTransit,
                    "Invalid coordinate",
                    DateTimeOffset.UtcNow,
                    MilestoneSource.Gps,
                    null,
                    181), CancellationToken.None));
    }

    [Fact]
    public async Task Timeline_combines_status_history_and_business_milestones()
    {
        var tenantId = Guid.NewGuid();
        var currentUser = new TestCurrentUserService(tenantId);
        await using var dbContext = await CreateDbContextAsync(currentUser);
        var shipment = await AddShipmentAsync(dbContext, tenantId, "SHP-MILE-3");
        await new SubmitShipmentCommandHandler(dbContext, currentUser)
            .Handle(new SubmitShipmentCommand(shipment.Id), CancellationToken.None);
        dbContext.ChangeTracker.Clear();

        await new AddShipmentMilestoneCommandHandler(dbContext, currentUser)
            .Handle(new AddShipmentMilestoneCommand(
                shipment.Id,
                ShipmentStatus.Submitted,
                "Staff reviewed documents",
                DateTimeOffset.UtcNow.AddMinutes(1),
                MilestoneSource.Staff,
                null,
                null), CancellationToken.None);

        var timeline = await new GetShipmentTimelineQueryHandler(dbContext, currentUser)
            .Handle(new GetShipmentTimelineQuery(shipment.Id), CancellationToken.None);

        Assert.Contains(timeline.Items, item => item.Source == "status-history");
        Assert.Contains(timeline.Items, item => item.Source == MilestoneSource.Staff.ToString());
    }

    [Fact]
    public async Task DocumentAndMilestoneCommands_preserve_tenant_isolation()
    {
        var tenantA = Guid.NewGuid();
        var currentUserA = new TestCurrentUserService(tenantA);
        await using var dbContextA = await CreateDbContextAsync(currentUserA);
        var shipment = await AddShipmentAsync(dbContextA, tenantA, "SHP-DOC-ISO-1");

        var currentUserB = new TestCurrentUserService(Guid.NewGuid());
        await using var dbContextB = CreateDbContext(currentUserB);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new AttachShipmentDocumentCommandHandler(dbContextB, currentUserB)
                .Handle(new AttachShipmentDocumentCommand(
                    shipment.Id,
                    "invoice.pdf",
                    DocumentType.Invoice,
                    "s3://shipment/invoice.pdf",
                    OCRStatus.Pending,
                    null,
                    null), CancellationToken.None));

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new AddShipmentMilestoneCommandHandler(dbContextB, currentUserB)
                .Handle(new AddShipmentMilestoneCommand(
                    shipment.Id,
                    ShipmentStatus.InTransit,
                    "Other tenant",
                    DateTimeOffset.UtcNow,
                    MilestoneSource.User,
                    null,
                    null), CancellationToken.None));
    }

    private static async Task<ShipmentEntity> AddShipmentWithDocumentAsync(
        ShipmentWorkflowDbContext dbContext,
        Guid tenantId,
        string shipmentNo)
    {
        var shipment = await AddShipmentAsync(dbContext, tenantId, shipmentNo, shipment =>
        {
            shipment.AddDocumentMetadata(
                "packing-list.pdf",
                DocumentType.PackingList,
                "s3://shipment/packing-list.pdf",
                null,
                DateTimeOffset.UtcNow);
        });

        return shipment;
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
