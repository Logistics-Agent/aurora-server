using Microsoft.EntityFrameworkCore;
using Shared.Exceptions;
using Shared.Interceptors;
using Shared.Security;
using ShipmentWorkflow.Application.Commands.Shipments;
using ShipmentWorkflow.Application.DTOs.Shipments;
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
    public async Task Create_intake_only_persists_pending_attachment_without_document_or_outbox()
    {
        var tenantId = Guid.NewGuid();
        var currentUser = new TestCurrentUserService(tenantId);
        await using var dbContext = await CreateDbContextAsync(currentUser);
        var shipment = await AddShipmentAsync(dbContext, tenantId, "SHP-INTAKE-LEDGER");

        var created = await new CreateDocumentIntakeCommandHandler(dbContext, currentUser)
            .Handle(CreateCommand(shipment.Id), CancellationToken.None);

        Assert.Equal(DocumentIntakeStatus.PendingAttachment, created.Status);
        Assert.Equal(1, await dbContext.DocumentIntakes.CountAsync());
        Assert.Equal(0, await dbContext.ShipmentDocuments.CountAsync());
        Assert.Equal(0, await dbContext.OutboxMessages.CountAsync());
    }

    [Fact]
    public async Task Attach_intake_after_verification_is_idempotent_and_emits_one_attachment()
    {
        var tenantId = Guid.NewGuid();
        var currentUser = new TestCurrentUserService(tenantId);
        await using var dbContext = await CreateDbContextAsync(currentUser);
        var shipment = await AddShipmentAsync(dbContext, tenantId, "SHP-INTAKE-ATTACH");
        var command = CreateCommand(shipment.Id);
        var created = await new CreateDocumentIntakeCommandHandler(dbContext, currentUser)
            .Handle(command, CancellationToken.None);
        var attach = new AttachDocumentIntakeCommandHandler(dbContext, currentUser);
        var attachCommand = new AttachDocumentIntakeCommand(
            created.IntakeId,
            created.UploadId,
            "objects/tenant/upload/invoice.pdf",
            "invoice.pdf");

        var first = await attach.Handle(attachCommand, CancellationToken.None);
        dbContext.ChangeTracker.Clear();
        var replay = await new AttachDocumentIntakeCommandHandler(dbContext, currentUser)
            .Handle(attachCommand, CancellationToken.None);

        Assert.Equal(DocumentIntakeStatus.PendingOcr, first.Status);
        Assert.Equal(first.DocumentId, replay.DocumentId);
        Assert.Equal(DocumentIntakeStatus.PendingOcr, replay.Status);
        Assert.Equal(1, await dbContext.ShipmentDocuments.CountAsync());
        Assert.Equal(1, await dbContext.OutboxMessages.CountAsync());
    }

    [Fact]
    public async Task Attach_intake_is_tenant_scoped()
    {
        var ownerTenantId = Guid.NewGuid();
        await using var ownerContext = await CreateDbContextAsync(new TestCurrentUserService(ownerTenantId));
        var shipment = await AddShipmentAsync(ownerContext, ownerTenantId, "SHP-INTAKE-ATTACH-TENANT");
        var created = await new CreateDocumentIntakeCommandHandler(ownerContext, new TestCurrentUserService(ownerTenantId))
            .Handle(CreateCommand(shipment.Id), CancellationToken.None);

        var otherUser = new TestCurrentUserService(Guid.NewGuid());
        await using var otherContext = CreateDbContext(otherUser);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new AttachDocumentIntakeCommandHandler(otherContext, otherUser)
                .Handle(new AttachDocumentIntakeCommand(
                    created.IntakeId,
                    created.UploadId,
                    "objects/other/upload/invoice.pdf",
                    "invoice.pdf"), CancellationToken.None));
    }

    [Fact]
    public async Task Same_key_replays_original_ledger_without_attachment()
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
        Assert.Equal(DocumentIntakeStatus.PendingAttachment, replay.Status);
        Assert.Equal(1, await dbContext.DocumentIntakes.CountAsync());
        Assert.Equal(0, await dbContext.ShipmentDocuments.CountAsync());
        Assert.Equal(0, await dbContext.OutboxMessages.CountAsync());
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
        Assert.Equal(0, await dbContext.ShipmentDocuments.CountAsync());
        Assert.Equal(0, await dbContext.OutboxMessages.CountAsync());
    }

    [Fact]
    public async Task Different_keys_with_same_storage_reference_conflict_at_attach_without_duplicate_document()
    {
        var tenantId = Guid.NewGuid();
        var currentUser = new TestCurrentUserService(tenantId);
        await using var dbContext = await CreateDbContextAsync(currentUser);
        var shipment = await AddShipmentAsync(dbContext, tenantId, "SHP-INTAKE-STORAGE-COLLISION");

        var first = await new CreateDocumentIntakeCommandHandler(dbContext, currentUser)
            .Handle(CreateCommand(shipment.Id), CancellationToken.None);
        await new AttachDocumentIntakeCommandHandler(dbContext, currentUser)
            .Handle(new AttachDocumentIntakeCommand(
                first.IntakeId,
                first.UploadId,
                "objects/tenant/upload/invoice.pdf",
                "invoice.pdf"), CancellationToken.None);

        var second = await new CreateDocumentIntakeCommandHandler(dbContext, currentUser)
            .Handle(CreateCommand(shipment.Id, idempotencyKey: "intake-key-2"), CancellationToken.None);
        var exception = await Assert.ThrowsAsync<ConflictException>(() =>
            new AttachDocumentIntakeCommandHandler(dbContext, currentUser)
                .Handle(new AttachDocumentIntakeCommand(
                    second.IntakeId,
                    second.UploadId,
                    "objects/tenant/upload/invoice.pdf",
                    "invoice.pdf"), CancellationToken.None));

        Assert.Equal("The storage reference is already attached to this shipment.", exception.Message);
        Assert.Equal(2, await dbContext.DocumentIntakes.CountAsync());
        Assert.Equal(1, await dbContext.ShipmentDocuments.CountAsync());
    }

    [Fact]
    public async Task Existing_intake_with_missing_document_does_not_replay_phantom_document()
    {
        var tenantId = Guid.NewGuid();
        var currentUser = new TestCurrentUserService(tenantId);
        await using var dbContext = await CreateDbContextAsync(currentUser);
        var shipment = await AddShipmentAsync(dbContext, tenantId, "SHP-INTAKE-MISSING-DOCUMENT");
        var command = CreateCommand(shipment.Id);
        await new CreateDocumentIntakeCommandHandler(dbContext, currentUser)
            .Handle(command, CancellationToken.None);
        var created = await new CreateDocumentIntakeCommandHandler(dbContext, currentUser)
            .Handle(command, CancellationToken.None);
        await new AttachDocumentIntakeCommandHandler(dbContext, currentUser)
            .Handle(new AttachDocumentIntakeCommand(
                created.IntakeId,
                created.UploadId,
                "objects/tenant/upload/invoice.pdf",
                "invoice.pdf"), CancellationToken.None);
        var document = await dbContext.ShipmentDocuments.SingleAsync();
        dbContext.ShipmentDocuments.Remove(document);
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();

        var exception = await Assert.ThrowsAsync<ConflictException>(() =>
            new CreateDocumentIntakeCommandHandler(dbContext, currentUser)
                .Handle(command, CancellationToken.None));

        Assert.Equal("The document intake references a missing document.", exception.Message);
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
        DocumentIntakeDto[] results;
        try
        {
            results = await Task.WhenAll(
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
        var attachResult = await new AttachDocumentIntakeCommandHandler(
                verificationContext,
                new TestCurrentUserService(tenantId))
            .Handle(new AttachDocumentIntakeCommand(
                results[0].IntakeId,
                results[0].UploadId,
                "objects/tenant/upload/invoice.pdf",
                "invoice.pdf"), CancellationToken.None);
        Assert.Equal(results[0].DocumentId, attachResult.DocumentId);
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
        await new AttachDocumentIntakeCommandHandler(dbContext, currentUser)
            .Handle(new AttachDocumentIntakeCommand(
                created.IntakeId,
                created.UploadId,
                "objects/tenant/upload/invoice.pdf",
                "invoice.pdf"), CancellationToken.None);
        var retryable = await new MarkDocumentIntakeRetryableCommandHandler(dbContext, currentUser)
            .Handle(new MarkDocumentIntakeRetryableCommand(created.IntakeId, "OCR unavailable"), CancellationToken.None);
        dbContext.ChangeTracker.Clear();
        var resumed = await new AttachDocumentIntakeCommandHandler(dbContext, currentUser)
            .Handle(new AttachDocumentIntakeCommand(
                created.IntakeId,
                created.UploadId,
                "objects/tenant/upload/invoice.pdf",
                "invoice.pdf"), CancellationToken.None);

        Assert.Equal(DocumentIntakeStatus.FailedRetryable, retryable.Status);
        Assert.Equal(DocumentIntakeStatus.PendingOcr, resumed.Status);
        Assert.Equal(created.DocumentId, resumed.DocumentId);
        Assert.Equal(1, await dbContext.ShipmentDocuments.CountAsync());
        Assert.Equal(1, await dbContext.OutboxMessages.CountAsync());
    }

    [Fact]
    public async Task Stale_transition_contexts_conflict_but_same_transition_is_idempotent()
    {
        var tenantId = Guid.NewGuid();
        var currentUser = new TestCurrentUserService(tenantId);
        await using var setupContext = await CreateDbContextAsync(currentUser);
        var shipment = await AddShipmentAsync(setupContext, tenantId, "SHP-INTAKE-CONCURRENCY");
        var created = await new CreateDocumentIntakeCommandHandler(setupContext, currentUser)
            .Handle(CreateCommand(shipment.Id), CancellationToken.None);
        await new AttachDocumentIntakeCommandHandler(setupContext, currentUser)
            .Handle(new AttachDocumentIntakeCommand(
                created.IntakeId,
                created.UploadId,
                "objects/tenant/upload/invoice.pdf",
                "invoice.pdf"), CancellationToken.None);

        await using var staleRetryableContext = CreateDbContext(new TestCurrentUserService(tenantId));
        _ = await staleRetryableContext.DocumentIntakes
            .SingleAsync(intake => intake.Id == created.IntakeId);
        await using var submittedContext = CreateDbContext(new TestCurrentUserService(tenantId));
        var submitted = await new MarkDocumentIntakeSubmittedCommandHandler(
                submittedContext,
                new TestCurrentUserService(tenantId))
            .Handle(new MarkDocumentIntakeSubmittedCommand(created.IntakeId), CancellationToken.None);
        Assert.Equal(DocumentIntakeStatus.Submitted, submitted.Status);

        await Assert.ThrowsAsync<ConflictException>(() =>
            new MarkDocumentIntakeRetryableCommandHandler(
                    staleRetryableContext,
                    new TestCurrentUserService(tenantId))
                .Handle(
                    new MarkDocumentIntakeRetryableCommand(created.IntakeId, "OCR unavailable"),
                    CancellationToken.None));

        var sameTransitionContext = CreateDbContext(new TestCurrentUserService(tenantId));
        _ = await sameTransitionContext.DocumentIntakes
            .SingleAsync(intake => intake.Id == created.IntakeId);
        var sameTransition = await new MarkDocumentIntakeSubmittedCommandHandler(
                sameTransitionContext,
                new TestCurrentUserService(tenantId))
            .Handle(new MarkDocumentIntakeSubmittedCommand(created.IntakeId), CancellationToken.None);
        Assert.Equal(DocumentIntakeStatus.Submitted, sameTransition.Status);
        await sameTransitionContext.DisposeAsync();
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
        await new AttachDocumentIntakeCommandHandler(dbContext, currentUser)
            .Handle(new AttachDocumentIntakeCommand(
                created.IntakeId,
                created.UploadId,
                "objects/tenant/upload/invoice.pdf",
                "invoice.pdf"), CancellationToken.None);
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
        Assert.Null(await dbContext.ShipmentDocuments.Select(document => document.StorageReference).SingleAsync());
    }

    [Fact]
    public async Task Legacy_duplicate_and_idempotent_mixed_attachments_remain_compatible()
    {
        var tenantId = Guid.NewGuid();
        var currentUser = new TestCurrentUserService(tenantId);
        await using var dbContext = await CreateDbContextAsync(currentUser);
        var shipment = await AddShipmentAsync(dbContext, tenantId, "SHP-INTAKE-LEGACY-MIXED");
        var handler = new AttachShipmentDocumentCommandHandler(dbContext, currentUser);
        var legacy = new AttachShipmentDocumentCommand(
            shipment.Id,
            "legacy.pdf",
            DocumentType.Other,
            "s3://legacy/repeatable.pdf",
            OCRStatus.Pending,
            null,
            null);

        await handler.Handle(legacy, CancellationToken.None);
        await handler.Handle(legacy, CancellationToken.None);
        await handler.Handle(legacy with
        {
            IdempotencyKey = "mixed-key",
            UploadId = Guid.CreateVersion7(),
            StorageReference = "s3://legacy/repeatable.pdf"
        }, CancellationToken.None);

        Assert.Equal(3, await dbContext.ShipmentDocuments.CountAsync());
        Assert.Equal(2, await dbContext.ShipmentDocuments.CountAsync(document => document.StorageReference == null));
        Assert.Equal(1, await dbContext.ShipmentDocuments.CountAsync(document => document.IdempotencyKey == "mixed-key"));
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

    [Fact]
    public async Task Idempotent_attach_hash_uses_normalized_persisted_semantics()
    {
        var tenantId = Guid.NewGuid();
        var currentUser = new TestCurrentUserService(tenantId);
        await using var dbContext = await CreateDbContextAsync(currentUser);
        var shipment = await AddShipmentAsync(dbContext, tenantId, "SHP-INTAKE-HASH");
        var uploadId = Guid.CreateVersion7();
        var first = new AttachShipmentDocumentCommand(
            shipment.Id,
            " invoice.pdf ",
            DocumentType.Invoice,
            " s3://shipment/invoice.pdf ",
            OCRStatus.Pending,
            0.87501m,
            " {\"b\":2, \"a\":1} ",
            " hash-key ",
            uploadId,
            " objects/tenant/upload/invoice.pdf ");
        var equivalent = first with
        {
            FileName = "invoice.pdf",
            StorageUrl = "s3://shipment/invoice.pdf",
            OCRConfidence = 0.8750m,
            ExtractedDataJson = "{\"a\":1,\"b\":2}",
            IdempotencyKey = "hash-key",
            StorageReference = "objects/tenant/upload/invoice.pdf"
        };

        var firstResult = await new AttachShipmentDocumentCommandHandler(dbContext, currentUser)
            .Handle(first, CancellationToken.None);
        dbContext.ChangeTracker.Clear();
        var replay = await new AttachShipmentDocumentCommandHandler(dbContext, currentUser)
            .Handle(equivalent, CancellationToken.None);

        Assert.Equal(firstResult.Documents.Single().Id, replay.Documents.Single().Id);
        Assert.Equal(1, await dbContext.ShipmentDocuments.CountAsync());

        await Assert.ThrowsAsync<ConflictException>(() =>
            new AttachShipmentDocumentCommandHandler(dbContext, currentUser)
                .Handle(equivalent with { OCRStatus = OCRStatus.Completed }, CancellationToken.None));
    }

    [Fact]
    public async Task Different_idempotent_attachment_keys_with_same_storage_reference_conflict()
    {
        var tenantId = Guid.NewGuid();
        var currentUser = new TestCurrentUserService(tenantId);
        await using var dbContext = await CreateDbContextAsync(currentUser);
        var shipment = await AddShipmentAsync(dbContext, tenantId, "SHP-INTAKE-ATTACH-COLLISION");
        var first = new AttachShipmentDocumentCommand(
            shipment.Id,
            "invoice.pdf",
            DocumentType.Invoice,
            "s3://shipment/invoice.pdf",
            OCRStatus.Pending,
            null,
            null,
            "attach-key-1",
            Guid.CreateVersion7(),
            "objects/tenant/upload/invoice.pdf");

        await new AttachShipmentDocumentCommandHandler(dbContext, currentUser)
            .Handle(first, CancellationToken.None);

        var exception = await Assert.ThrowsAsync<ConflictException>(() =>
            new AttachShipmentDocumentCommandHandler(dbContext, currentUser)
                .Handle(first with
                {
                    IdempotencyKey = "attach-key-2",
                    UploadId = Guid.CreateVersion7()
                }, CancellationToken.None));

        Assert.Equal("The storage reference is already attached to this shipment.", exception.Message);
        Assert.Equal(1, await dbContext.ShipmentDocuments.CountAsync());
        Assert.Equal(1, await dbContext.OutboxMessages.CountAsync());
    }

    [Fact]
    public async Task Migration_preserves_existing_shipments_documents_and_outbox_rows()
    {
        var tenantId = Guid.NewGuid();
        var currentUser = new TestCurrentUserService(tenantId);
        await using var dbContext = await CreateDbContextAsync(currentUser);
        var shipment = await AddShipmentAsync(dbContext, tenantId, "SHP-INTAKE-MIGRATION");
        await new AttachShipmentDocumentCommandHandler(dbContext, currentUser)
            .Handle(new AttachShipmentDocumentCommand(
                shipment.Id,
                "legacy.pdf",
                DocumentType.Other,
                "s3://legacy/migration.pdf",
                OCRStatus.Pending,
                null,
                null), CancellationToken.None);
        var existingOutboxCount = await dbContext.OutboxMessages.CountAsync();

        await dbContext.Database.ExecuteSqlRawAsync("DROP TABLE IF EXISTS document_intakes;");
        await dbContext.Database.ExecuteSqlRawAsync("DROP INDEX IF EXISTS \"IX_shipment_documents_TenantId_ShipmentId_IdempotencyKey\";");
        await dbContext.Database.ExecuteSqlRawAsync("DROP INDEX IF EXISTS \"IX_shipment_documents_TenantId_ShipmentId_StorageReference\";");
        await dbContext.Database.ExecuteSqlRawAsync("ALTER TABLE shipment_documents DROP COLUMN IF EXISTS \"StorageReference\", DROP COLUMN IF EXISTS \"IdempotencyKey\", DROP COLUMN IF EXISTS \"UploadId\", DROP COLUMN IF EXISTS \"RequestHash\";");
        dbContext.ChangeTracker.Clear();

        await dbContext.Database.MigrateAsync();

        await using var migratedContext = CreateDbContext(currentUser);
        Assert.Equal(1, await migratedContext.Shipments.CountAsync());
        Assert.Equal(1, await migratedContext.ShipmentDocuments.CountAsync());
        Assert.Equal(existingOutboxCount, await migratedContext.OutboxMessages.CountAsync());
        Assert.Equal(0, await migratedContext.DocumentIntakes.CountAsync());
        Assert.Null(await migratedContext.ShipmentDocuments.Select(document => document.StorageReference).SingleAsync());
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
