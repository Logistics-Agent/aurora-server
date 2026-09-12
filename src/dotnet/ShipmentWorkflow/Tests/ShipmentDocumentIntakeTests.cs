using Microsoft.EntityFrameworkCore;
using Shared.Exceptions;
using Shared.Interceptors;
using Shared.Security;
using ShipmentWorkflow.Application.Commands.Shipments;
using ShipmentWorkflow.Domain.Enums;
using ShipmentWorkflow.Infrastructure.Persistences;
using ShipmentEntity = global::ShipmentWorkflow.Domain.Entities.Shipment;

namespace ShipmentWorkflow.Tests;

[Collection("ShipmentWorkflowDatabase")]
public sealed class ShipmentDocumentIntakeTests
{
    private const string ConnectionString =
        "Host=localhost;Port=5433;Database=aurora_shipment_workflow_tests;Username=postgres;Password=postgres";

    [Fact]
    public async Task Same_key_replays_original_attachment_without_duplicate_event()
    {
        var tenantId = Guid.NewGuid();
        var currentUser = new TestCurrentUserService(tenantId);
        await using var dbContext = await CreateDbContextAsync(currentUser);
        var shipment = await AddShipmentAsync(dbContext, tenantId, "SHP-INTAKE-REPLAY");
        var command = CreateCommand(shipment.Id);

        var first = await new CreateDocumentIntakeCommandHandler(dbContext, currentUser)
            .Handle(command, CancellationToken.None);
        dbContext.ChangeTracker.Clear();
        var replay = await new CreateDocumentIntakeCommandHandler(dbContext, currentUser)
            .Handle(command, CancellationToken.None);

        Assert.Equal(first.IntakeId, replay.IntakeId);
        Assert.Equal(first.DocumentId, replay.DocumentId);
        Assert.Equal(DocumentIntakeStatus.PendingOcr, replay.Status);
        Assert.Equal(1, await dbContext.DocumentIntakes.CountAsync());
        Assert.Equal(1, await dbContext.ShipmentDocuments.CountAsync());
        Assert.Equal(1, await dbContext.OutboxMessages.CountAsync());
        var requestHash = await dbContext.DocumentIntakes
            .Select(intake => intake.RequestHash)
            .SingleAsync();
        Assert.Equal(64, requestHash.Length);
    }

    [Fact]
    public async Task Same_key_with_different_body_returns_stable_conflict()
    {
        var tenantId = Guid.NewGuid();
        var currentUser = new TestCurrentUserService(tenantId);
        await using var dbContext = await CreateDbContextAsync(currentUser);
        var shipment = await AddShipmentAsync(dbContext, tenantId, "SHP-INTAKE-CONFLICT");

        await new CreateDocumentIntakeCommandHandler(dbContext, currentUser)
            .Handle(CreateCommand(shipment.Id), CancellationToken.None);

        var exception = await Assert.ThrowsAsync<ConflictException>(() =>
            new CreateDocumentIntakeCommandHandler(dbContext, currentUser)
                .Handle(CreateCommand(shipment.Id, uploadId: Guid.NewGuid()), CancellationToken.None));

        Assert.Equal("The idempotency key was already used with a different request.", exception.Message);
        Assert.Equal(1, await dbContext.ShipmentDocuments.CountAsync());
        Assert.Equal(1, await dbContext.OutboxMessages.CountAsync());
    }

    [Fact]
    public async Task Concurrent_same_key_creates_one_document_and_one_event()
    {
        var tenantId = Guid.NewGuid();
        var setupUser = new TestCurrentUserService(tenantId);
        await using var setupContext = await CreateDbContextAsync(setupUser);
        var shipment = await AddShipmentAsync(setupContext, tenantId, "SHP-INTAKE-RACE");
        var command = CreateCommand(shipment.Id);

        var first = CreateDbContext(new TestCurrentUserService(tenantId));
        var second = CreateDbContext(new TestCurrentUserService(tenantId));
        try
        {
            var results = await Task.WhenAll(
                new CreateDocumentIntakeCommandHandler(first, new TestCurrentUserService(tenantId))
                    .Handle(command, CancellationToken.None),
                new CreateDocumentIntakeCommandHandler(second, new TestCurrentUserService(tenantId))
                    .Handle(command, CancellationToken.None));

            Assert.Equal(results[0].IntakeId, results[1].IntakeId);
            Assert.Equal(results[0].DocumentId, results[1].DocumentId);
        }
        finally
        {
            await first.DisposeAsync();
            await second.DisposeAsync();
        }

        await using var verificationContext = CreateDbContext(new TestCurrentUserService(tenantId));
        Assert.Equal(1, await verificationContext.DocumentIntakes.CountAsync());
        Assert.Equal(1, await verificationContext.ShipmentDocuments.CountAsync());
        Assert.Equal(1, await verificationContext.OutboxMessages.CountAsync());
    }

    [Fact]
    public async Task Cross_tenant_intake_does_not_reveal_shipment()
    {
        var ownerTenantId = Guid.NewGuid();
        await using var ownerContext = await CreateDbContextAsync(new TestCurrentUserService(ownerTenantId));
        var shipment = await AddShipmentAsync(ownerContext, ownerTenantId, "SHP-INTAKE-TENANT");

        var otherUser = new TestCurrentUserService(Guid.NewGuid());
        await using var otherContext = CreateDbContext(otherUser);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new CreateDocumentIntakeCommandHandler(otherContext, otherUser)
                .Handle(CreateCommand(shipment.Id), CancellationToken.None));
    }

    [Fact]
    public async Task Missing_shipment_upload_and_key_are_rejected_without_generating_ids()
    {
        var tenantId = Guid.NewGuid();
        var currentUser = new TestCurrentUserService(tenantId);
        await using var dbContext = await CreateDbContextAsync(currentUser);
        var handler = new CreateDocumentIntakeCommandHandler(dbContext, currentUser);

        await Assert.ThrowsAsync<DomainException>(() =>
            handler.Handle(CreateCommand(Guid.Empty), CancellationToken.None));
        await Assert.ThrowsAsync<DomainException>(() =>
            handler.Handle(CreateCommand(Guid.NewGuid(), uploadId: Guid.Empty), CancellationToken.None));
        await Assert.ThrowsAsync<DomainException>(() =>
            handler.Handle(CreateCommand(Guid.NewGuid(), idempotencyKey: " "), CancellationToken.None));

        Assert.Equal(0, await dbContext.DocumentIntakes.CountAsync());
        Assert.Equal(0, await dbContext.ShipmentDocuments.CountAsync());
    }

    [Fact]
    public async Task Retryable_intake_resumes_same_document()
    {
        var tenantId = Guid.NewGuid();
        var currentUser = new TestCurrentUserService(tenantId);
        await using var dbContext = await CreateDbContextAsync(currentUser);
        var shipment = await AddShipmentAsync(dbContext, tenantId, "SHP-INTAKE-RETRY");
        var command = CreateCommand(shipment.Id);
        var handler = new CreateDocumentIntakeCommandHandler(dbContext, currentUser);

        var created = await handler.Handle(command, CancellationToken.None);
        var retryable = await new MarkDocumentIntakeRetryableCommandHandler(dbContext, currentUser)
            .Handle(new MarkDocumentIntakeRetryableCommand(created.IntakeId, "OCR unavailable"), CancellationToken.None);
        dbContext.ChangeTracker.Clear();
        var resumed = await handler.Handle(command, CancellationToken.None);

        Assert.Equal(DocumentIntakeStatus.FailedRetryable, retryable.Status);
        Assert.Equal(DocumentIntakeStatus.PendingOcr, resumed.Status);
        Assert.Equal(created.DocumentId, resumed.DocumentId);
        Assert.Equal(1, await dbContext.ShipmentDocuments.CountAsync());
        Assert.Equal(1, await dbContext.OutboxMessages.CountAsync());
    }

    [Fact]
    public async Task Submitted_intake_replays_and_illegal_transition_is_rejected()
    {
        var tenantId = Guid.NewGuid();
        var currentUser = new TestCurrentUserService(tenantId);
        await using var dbContext = await CreateDbContextAsync(currentUser);
        var shipment = await AddShipmentAsync(dbContext, tenantId, "SHP-INTAKE-SUBMITTED");
        var created = await new CreateDocumentIntakeCommandHandler(dbContext, currentUser)
            .Handle(CreateCommand(shipment.Id), CancellationToken.None);
        var submittedHandler = new MarkDocumentIntakeSubmittedCommandHandler(dbContext, currentUser);

        var submitted = await submittedHandler.Handle(
            new MarkDocumentIntakeSubmittedCommand(created.IntakeId), CancellationToken.None);
        var replay = await submittedHandler.Handle(
            new MarkDocumentIntakeSubmittedCommand(created.IntakeId), CancellationToken.None);

        Assert.Equal(DocumentIntakeStatus.Submitted, submitted.Status);
        Assert.Equal(submitted.DocumentId, replay.DocumentId);
        await Assert.ThrowsAsync<DomainException>(() =>
            new MarkDocumentIntakeRetryableCommandHandler(dbContext, currentUser)
                .Handle(new MarkDocumentIntakeRetryableCommand(created.IntakeId, "late failure"), CancellationToken.None));
    }

    [Fact]
    public async Task Legacy_attach_without_intake_fields_remains_compatible()
    {
        var tenantId = Guid.NewGuid();
        var currentUser = new TestCurrentUserService(tenantId);
        await using var dbContext = await CreateDbContextAsync(currentUser);
        var shipment = await AddShipmentAsync(dbContext, tenantId, "SHP-INTAKE-LEGACY");

        var result = await new AttachShipmentDocumentCommandHandler(dbContext, currentUser)
            .Handle(new AttachShipmentDocumentCommand(
                shipment.Id,
                "legacy.pdf",
                DocumentType.Other,
                "s3://legacy/legacy.pdf",
                OCRStatus.Pending,
                null,
                null), CancellationToken.None);

        Assert.Single(result.Documents);
        Assert.Null(await dbContext.DocumentIntakes.SingleOrDefaultAsync());
    }

    [Fact]
    public async Task Idempotent_attach_replays_and_conflicts_on_different_body()
    {
        var tenantId = Guid.NewGuid();
        var currentUser = new TestCurrentUserService(tenantId);
        await using var dbContext = await CreateDbContextAsync(currentUser);
        var shipment = await AddShipmentAsync(dbContext, tenantId, "SHP-INTAKE-ATTACH");
        var uploadId = Guid.CreateVersion7();
        var command = new AttachShipmentDocumentCommand(
            shipment.Id,
            "invoice.pdf",
            DocumentType.Invoice,
            "s3://shipment/invoice.pdf",
            OCRStatus.Pending,
            null,
            null,
            "attach-key-1",
            uploadId,
            "objects/tenant/upload/invoice.pdf");

        var first = await new AttachShipmentDocumentCommandHandler(dbContext, currentUser)
            .Handle(command, CancellationToken.None);
        dbContext.ChangeTracker.Clear();
        var replay = await new AttachShipmentDocumentCommandHandler(dbContext, currentUser)
            .Handle(command, CancellationToken.None);

        Assert.Equal(first.Documents.Single().Id, replay.Documents.Single().Id);
        Assert.Equal(1, await dbContext.ShipmentDocuments.CountAsync());
        Assert.Equal(1, await dbContext.OutboxMessages.CountAsync());
        await Assert.ThrowsAsync<ConflictException>(() =>
            new AttachShipmentDocumentCommandHandler(dbContext, currentUser)
                .Handle(command with { UploadId = Guid.CreateVersion7() }, CancellationToken.None));
    }

    private static CreateDocumentIntakeCommand CreateCommand(
        Guid shipmentId,
        Guid? uploadId = null,
        string idempotencyKey = "intake-key-1")
    {
        return new CreateDocumentIntakeCommand(
            shipmentId,
            uploadId ?? Guid.Parse("01998a7e-4a0f-7b49-8d18-6f7c5c8d4ef1"),
            "objects/tenant/upload/invoice.pdf",
            "invoice.pdf",
            DocumentType.Invoice,
            idempotencyKey);
    }

    private static async Task<ShipmentEntity> AddShipmentAsync(
        ShipmentWorkflowDbContext dbContext,
        Guid tenantId,
        string shipmentNo)
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
}
