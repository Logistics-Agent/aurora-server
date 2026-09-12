using Microsoft.EntityFrameworkCore;
using MediatR;
using Shared.Exceptions;
using Shared.Security;
using ShipmentWorkflow.Application.DTOs.Shipments;
using ShipmentWorkflow.Domain.Entities;
using ShipmentWorkflow.Domain.Enums;
using ShipmentWorkflow.Infrastructure.Persistences;
using ShipmentEntity = global::ShipmentWorkflow.Domain.Entities.Shipment;

namespace ShipmentWorkflow.Application.Commands.Shipments;

public sealed record CreateDocumentIntakeCommand(
    Guid ShipmentId,
    Guid UploadId,
    string StorageReference,
    string FileName,
    DocumentType DocumentType,
    string IdempotencyKey) : IRequest<DocumentIntakeDto>;

public sealed record MarkDocumentIntakeSubmittedCommand(Guid IntakeId) : IRequest<DocumentIntakeDto>;

public sealed record MarkDocumentIntakeRetryableCommand(
    Guid IntakeId,
    string? FailureReason) : IRequest<DocumentIntakeDto>;

public sealed class CreateDocumentIntakeCommandHandler(
    ShipmentWorkflowDbContext dbContext,
    ICurrentUserService currentUser) : IRequestHandler<CreateDocumentIntakeCommand, DocumentIntakeDto>
{
    public async Task<DocumentIntakeDto> Handle(
        CreateDocumentIntakeCommand request,
        CancellationToken cancellationToken)
    {
        var tenantId = ShipmentCommandHelpers.RequireTenantId(currentUser);
        var input = Normalize(request);
        var requestHash = DocumentIntakeRequestHasher.ForIntake(
            input.ShipmentId,
            input.UploadId,
            input.StorageReference,
            input.FileName,
            input.DocumentType,
            input.IdempotencyKey);
        var existing = await FindIntakeAsync(tenantId, input.ShipmentId, input.IdempotencyKey, cancellationToken);
        if (existing is not null)
            return await ReplayOrResumeAsync(existing, requestHash, cancellationToken);

        var shipment = await ShipmentCommandHelpers.GetShipmentAsync(
            dbContext,
            input.ShipmentId,
            cancellationToken);
        ShipmentCommandHelpers.EnsureNonTerminalMutation(shipment);
        if (await dbContext.ShipmentDocuments.AnyAsync(
                document => document.ShipmentId == input.ShipmentId &&
                    document.StorageReference == input.StorageReference,
                cancellationToken))
        {
            var committedIntake = await FindIntakeAsync(
                tenantId,
                input.ShipmentId,
                input.IdempotencyKey,
                cancellationToken);
            if (committedIntake is not null)
                return await ReplayOrResumeAsync(committedIntake, requestHash, cancellationToken);

            throw new ConflictException("The storage reference is already attached to this shipment.");
        }

        var intake = DocumentIntake.Create(
            tenantId,
            input.ShipmentId,
            Guid.CreateVersion7(),
            input.UploadId,
            input.StorageReference,
            input.FileName,
            input.DocumentType,
            input.IdempotencyKey,
            requestHash,
            DateTimeOffset.UtcNow);
        dbContext.DocumentIntakes.Add(intake);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
        {
            var constraintName = DocumentIntakePersistenceErrors.GetUniqueConstraintName(exception);
            if (constraintName != DocumentIntakePersistenceErrors.IntakeIdempotencyKeyConstraint)
                throw;

            dbContext.ChangeTracker.Clear();
            existing = await FindIntakeAsync(tenantId, input.ShipmentId, input.IdempotencyKey, cancellationToken)
                ?? throw new ConflictException("The document intake could not be committed safely.");
            return await ReplayOrResumeAsync(existing, requestHash, cancellationToken);
        }

        return await AttachPendingAsync(intake, shipment, cancellationToken);
    }

    private async Task<DocumentIntakeDto> ReplayOrResumeAsync(
        DocumentIntake intake,
        string requestHash,
        CancellationToken cancellationToken)
    {
        EnsureRequestMatches(intake, requestHash);
        if (intake.Status == DocumentIntakeStatus.Submitted ||
            intake.Status == DocumentIntakeStatus.PendingOcr)
        {
            await EnsureReferencedDocumentExistsAsync(intake, cancellationToken);
            return DocumentIntakeDto.FromEntity(intake);
        }

        if (intake.Status == DocumentIntakeStatus.FailedRetryable)
        {
            await EnsureReferencedDocumentExistsAsync(intake, cancellationToken);
            intake.ResumeOcr(DateTimeOffset.UtcNow);
            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);
                return DocumentIntakeDto.FromEntity(intake);
            }
            catch (DbUpdateConcurrencyException)
            {
                dbContext.ChangeTracker.Clear();
                var current = await FindIntakeAsync(
                    intake.TenantId,
                    intake.ShipmentId,
                    intake.IdempotencyKey,
                    cancellationToken) ?? throw new ConflictException("The document intake could not be committed safely.");
                EnsureRequestMatches(current, requestHash);
                await EnsureReferencedDocumentExistsAsync(current, cancellationToken);
                if (current.Status is DocumentIntakeStatus.PendingOcr or DocumentIntakeStatus.Submitted)
                    return DocumentIntakeDto.FromEntity(current);
                throw new ConflictException("The document intake was changed concurrently.");
            }
        }

        var shipment = await ShipmentCommandHelpers.GetShipmentAsync(
            dbContext,
            intake.ShipmentId,
            cancellationToken);
        ShipmentCommandHelpers.EnsureNonTerminalMutation(shipment);
        return await AttachPendingAsync(intake, shipment, cancellationToken);
    }

    private async Task<DocumentIntakeDto> AttachPendingAsync(
        DocumentIntake intake,
        ShipmentEntity shipment,
        CancellationToken cancellationToken)
    {
        var document = await dbContext.ShipmentDocuments.SingleOrDefaultAsync(
            item => item.Id == intake.DocumentId,
            cancellationToken);
        if (document is null)
        {
            document = shipment.AddDocumentMetadata(
                intake.FileName,
                intake.DocumentType,
                intake.StorageReference,
                currentUser.UserId,
                DateTimeOffset.UtcNow,
                OCRStatus.Pending,
                null,
                null,
                intake.IdempotencyKey,
                intake.UploadId,
                intake.StorageReference,
                intake.RequestHash,
                intake.DocumentId);
            ShipmentCommandHelpers.MarkAggregateRootUnchanged(dbContext, shipment);
            dbContext.Entry(document).State = EntityState.Added;
            ShipmentCommandHelpers.AddDocumentAttachedOutbox(dbContext, shipment, document);
        }

        intake.MarkAttachmentCreated(DateTimeOffset.UtcNow);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            dbContext.ChangeTracker.Clear();
            var winner = await FindIntakeAsync(
                intake.TenantId,
                intake.ShipmentId,
                intake.IdempotencyKey,
                cancellationToken) ?? throw new ConflictException("The document intake could not be committed safely.");
            EnsureRequestMatches(winner, intake.RequestHash);
            await EnsureReferencedDocumentExistsAsync(winner, cancellationToken);
            return DocumentIntakeDto.FromEntity(winner);
        }
        catch (DbUpdateException exception)
        {
            var constraintName = DocumentIntakePersistenceErrors.GetUniqueConstraintName(exception);
            if (constraintName == DocumentIntakePersistenceErrors.AttachmentStorageReferenceConstraint)
            {
                await RemovePendingIntakeAsync(intake.Id, cancellationToken);
                throw new ConflictException("The storage reference is already attached to this shipment.");
            }

            if (constraintName is not (DocumentIntakePersistenceErrors.IntakeDocumentIdConstraint or
                "PK_shipment_documents"))
                throw;

            dbContext.ChangeTracker.Clear();
            var winner = await FindIntakeAsync(
                intake.TenantId,
                intake.ShipmentId,
                intake.IdempotencyKey,
                cancellationToken) ?? throw new ConflictException("The document intake could not be committed safely.");
            EnsureRequestMatches(winner, intake.RequestHash);
            await EnsureReferencedDocumentExistsAsync(winner, cancellationToken);
            return DocumentIntakeDto.FromEntity(winner);
        }

        return DocumentIntakeDto.FromEntity(intake);
    }

    private async Task<DocumentIntake?> FindIntakeAsync(
        Guid tenantId,
        Guid shipmentId,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        return await dbContext.DocumentIntakes.SingleOrDefaultAsync(
            item => item.TenantId == tenantId &&
                item.ShipmentId == shipmentId &&
                item.IdempotencyKey == idempotencyKey,
            cancellationToken);
    }

    private async Task EnsureReferencedDocumentExistsAsync(
        DocumentIntake intake,
        CancellationToken cancellationToken)
    {
        if (!await dbContext.ShipmentDocuments.AnyAsync(
                document => document.Id == intake.DocumentId,
                cancellationToken))
        {
            throw new ConflictException("The document intake references a missing document.");
        }
    }

    private async Task RemovePendingIntakeAsync(
        Guid intakeId,
        CancellationToken cancellationToken)
    {
        dbContext.ChangeTracker.Clear();
        var pending = await dbContext.DocumentIntakes.SingleOrDefaultAsync(
            intake => intake.Id == intakeId,
            cancellationToken);
        if (pending is null)
            return;

        dbContext.DocumentIntakes.Remove(pending);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static NormalizedIntake Normalize(CreateDocumentIntakeCommand request)
    {
        if (request.ShipmentId == Guid.Empty)
            throw new DomainException("ShipmentId is required.");
        if (request.UploadId == Guid.Empty)
            throw new DomainException("UploadId is required.");
        if (request.DocumentType == DocumentType.Unknown)
            throw new DomainException("DocumentType is required.");

        return new NormalizedIntake(
            request.ShipmentId,
            request.UploadId,
            Required(request.StorageReference, "StorageReference", DocumentIntake.StorageReferenceMaxLength),
            Required(request.FileName, "FileName", DocumentIntake.FileNameMaxLength),
            request.DocumentType,
            Required(request.IdempotencyKey, "IdempotencyKey", DocumentIntake.IdempotencyKeyMaxLength));
    }

    private static string Required(string? value, string name, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new DomainException($"{name} is required.");
        var normalized = value.Trim();
        if (normalized.Length > maxLength)
            throw new DomainException($"{name} must be {maxLength} characters or fewer.");
        return normalized;
    }

    private static void EnsureRequestMatches(DocumentIntake intake, string requestHash)
    {
        if (!string.Equals(intake.RequestHash, requestHash, StringComparison.Ordinal))
            throw new ConflictException("The idempotency key was already used with a different request.");
    }

    private sealed record NormalizedIntake(
        Guid ShipmentId,
        Guid UploadId,
        string StorageReference,
        string FileName,
        DocumentType DocumentType,
        string IdempotencyKey);
}

public sealed class MarkDocumentIntakeSubmittedCommandHandler(
    ShipmentWorkflowDbContext dbContext,
    ICurrentUserService currentUser) : IRequestHandler<MarkDocumentIntakeSubmittedCommand, DocumentIntakeDto>
{
    public async Task<DocumentIntakeDto> Handle(
        MarkDocumentIntakeSubmittedCommand request,
        CancellationToken cancellationToken)
    {
        var intake = await GetIntakeAsync(request.IntakeId, cancellationToken);
        intake.MarkSubmitted(DateTimeOffset.UtcNow);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return DocumentIntakeDto.FromEntity(intake);
        }
        catch (DbUpdateConcurrencyException)
        {
            return await DocumentIntakeTransitionResolver.ResolveAsync(
                dbContext,
                request.IntakeId,
                DocumentIntakeStatus.Submitted,
                cancellationToken);
        }
    }

    private async Task<DocumentIntake> GetIntakeAsync(Guid intakeId, CancellationToken cancellationToken)
    {
        ShipmentCommandHelpers.RequireTenantId(currentUser);
        if (intakeId == Guid.Empty)
            throw new DomainException("IntakeId is required.");

        return await dbContext.DocumentIntakes.SingleOrDefaultAsync(
            intake => intake.Id == intakeId,
            cancellationToken) ?? throw new NotFoundException("Document intake was not found.");
    }
}

public sealed class MarkDocumentIntakeRetryableCommandHandler(
    ShipmentWorkflowDbContext dbContext,
    ICurrentUserService currentUser) : IRequestHandler<MarkDocumentIntakeRetryableCommand, DocumentIntakeDto>
{
    public async Task<DocumentIntakeDto> Handle(
        MarkDocumentIntakeRetryableCommand request,
        CancellationToken cancellationToken)
    {
        ShipmentCommandHelpers.RequireTenantId(currentUser);
        if (request.IntakeId == Guid.Empty)
            throw new DomainException("IntakeId is required.");

        var intake = await dbContext.DocumentIntakes.SingleOrDefaultAsync(
            item => item.Id == request.IntakeId,
            cancellationToken) ?? throw new NotFoundException("Document intake was not found.");
        intake.MarkRetryable(request.FailureReason, DateTimeOffset.UtcNow);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return DocumentIntakeDto.FromEntity(intake);
        }
        catch (DbUpdateConcurrencyException)
        {
            return await DocumentIntakeTransitionResolver.ResolveAsync(
                dbContext,
                request.IntakeId,
                DocumentIntakeStatus.FailedRetryable,
                cancellationToken);
        }
    }
}
