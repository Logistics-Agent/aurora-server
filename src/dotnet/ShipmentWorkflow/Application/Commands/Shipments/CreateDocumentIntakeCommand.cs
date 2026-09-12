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

public sealed record AttachDocumentIntakeCommand(
    Guid IntakeId,
    Guid UploadId,
    string StorageReference,
    string FileName) : IRequest<DocumentIntakeDto>;

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
            return await ReplayAsync(existing, requestHash, cancellationToken);

        var shipment = await ShipmentCommandHelpers.GetShipmentAsync(
            dbContext,
            input.ShipmentId,
            cancellationToken);
        ShipmentCommandHelpers.EnsureNonTerminalMutation(shipment);
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
            return await ReplayAsync(existing, requestHash, cancellationToken);
        }

        return DocumentIntakeDto.FromEntity(intake);
    }

    private async Task<DocumentIntakeDto> ReplayAsync(
        DocumentIntake intake,
        string requestHash,
        CancellationToken cancellationToken)
    {
        EnsureRequestMatches(intake, requestHash);
        if (intake.Status is not (DocumentIntakeStatus.PendingAttachment or DocumentIntakeStatus.FailedRetryable))
            await EnsureReferencedDocumentExistsAsync(intake, cancellationToken);
        return DocumentIntakeDto.FromEntity(intake);
    }

    private async Task<DocumentIntake?> FindIntakeAsync(
        Guid tenantId,
        Guid shipmentId,
        string idempotencyKey,
        CancellationToken cancellationToken) => await dbContext.DocumentIntakes.SingleOrDefaultAsync(
        item => item.TenantId == tenantId &&
            item.ShipmentId == shipmentId &&
            item.IdempotencyKey == idempotencyKey,
        cancellationToken);

    private async Task EnsureReferencedDocumentExistsAsync(
        DocumentIntake intake,
        CancellationToken cancellationToken)
    {
        if (!await dbContext.ShipmentDocuments.AnyAsync(
                document => document.Id == intake.DocumentId,
                cancellationToken))
            throw new ConflictException("The document intake references a missing document.");
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

public sealed class AttachDocumentIntakeCommandHandler(
    ShipmentWorkflowDbContext dbContext,
    ICurrentUserService currentUser) : IRequestHandler<AttachDocumentIntakeCommand, DocumentIntakeDto>
{
    public async Task<DocumentIntakeDto> Handle(
        AttachDocumentIntakeCommand request,
        CancellationToken cancellationToken)
    {
        ShipmentCommandHelpers.RequireTenantId(currentUser);
        Validate(request);
        var intake = await GetIntakeAsync(request.IntakeId, cancellationToken);
        if (request.UploadId == Guid.Empty || request.UploadId != intake.UploadId)
            throw new NotFoundException("Document intake was not found.");
        var document = await dbContext.ShipmentDocuments.SingleOrDefaultAsync(
            item => item.Id == intake.DocumentId,
            cancellationToken);

        if (intake.Status is DocumentIntakeStatus.PendingOcr or DocumentIntakeStatus.Submitted)
        {
            EnsureMetadataMatches(intake, request);
            if (document is null)
                throw new ConflictException("The document intake references a missing document.");
            return DocumentIntakeDto.FromEntity(intake);
        }

        var shipment = await ShipmentCommandHelpers.GetShipmentAsync(
            dbContext,
            intake.ShipmentId,
            cancellationToken);
        ShipmentCommandHelpers.EnsureNonTerminalMutation(shipment);
        if (intake.Status == DocumentIntakeStatus.FailedRetryable)
        {
            if (document is null)
                intake.ReplaceRetryableAttachmentMetadata(request.StorageReference, request.FileName);
            else
                EnsureMetadataMatches(intake, request);
            if (document is not null)
            {
                intake.AttachVerifiedDocument(request.StorageReference, request.FileName, DateTimeOffset.UtcNow);
                await SaveAsync(cancellationToken);
                return DocumentIntakeDto.FromEntity(intake);
            }
        }

        var duplicate = await dbContext.ShipmentDocuments.SingleOrDefaultAsync(
            item => item.ShipmentId == intake.ShipmentId &&
                item.StorageReference == request.StorageReference &&
                item.Id != intake.DocumentId,
            cancellationToken);
        if (duplicate is not null)
            throw new ConflictException("The storage reference is already attached to this shipment.");

        if (document is null)
        {
            document = shipment.AddDocumentMetadata(
                request.FileName,
                intake.DocumentType,
                request.StorageReference,
                currentUser.UserId,
                DateTimeOffset.UtcNow,
                OCRStatus.Pending,
                null,
                null,
                intake.IdempotencyKey,
                intake.UploadId,
                request.StorageReference,
                intake.RequestHash,
                intake.DocumentId);
            ShipmentCommandHelpers.MarkAggregateRootUnchanged(dbContext, shipment);
            dbContext.Entry(document).State = EntityState.Added;
            ShipmentCommandHelpers.AddDocumentAttachedOutbox(dbContext, shipment, document);
        }
        intake.AttachVerifiedDocument(request.StorageReference, request.FileName, DateTimeOffset.UtcNow);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            dbContext.ChangeTracker.Clear();
            var winner = await GetIntakeAsync(request.IntakeId, cancellationToken);
            if (winner.Status is DocumentIntakeStatus.PendingOcr or DocumentIntakeStatus.Submitted)
                return DocumentIntakeDto.FromEntity(winner);
            throw new ConflictException("The document intake was changed concurrently.");
        }
        catch (DbUpdateException exception) when (
            DocumentIntakePersistenceErrors.GetUniqueConstraintName(exception) is
            DocumentIntakePersistenceErrors.AttachmentStorageReferenceConstraint)
        {
            throw new ConflictException("The storage reference is already attached to this shipment.");
        }
        catch (DbUpdateException exception) when (
            DocumentIntakePersistenceErrors.GetUniqueConstraintName(exception) is
            DocumentIntakePersistenceErrors.IntakeDocumentIdConstraint or "PK_shipment_documents")
        {
            dbContext.ChangeTracker.Clear();
            var winner = await GetIntakeAsync(request.IntakeId, cancellationToken);
            if (winner.Status is DocumentIntakeStatus.PendingOcr or DocumentIntakeStatus.Submitted)
                return DocumentIntakeDto.FromEntity(winner);
            throw new ConflictException("The document intake could not be committed safely.");
        }

        return DocumentIntakeDto.FromEntity(intake);
    }

    private async Task<DocumentIntake> GetIntakeAsync(Guid intakeId, CancellationToken cancellationToken)
    {
        if (intakeId == Guid.Empty)
            throw new DomainException("IntakeId is required.");
        return await dbContext.DocumentIntakes.SingleOrDefaultAsync(
            item => item.Id == intakeId,
            cancellationToken) ?? throw new NotFoundException("Document intake was not found.");
    }

    private static void Validate(AttachDocumentIntakeCommand request)
    {
        if (string.IsNullOrWhiteSpace(request.StorageReference))
            throw new DomainException("StorageReference is required.");
        if (string.IsNullOrWhiteSpace(request.FileName))
            throw new DomainException("FileName is required.");
    }

    private static void EnsureMetadataMatches(DocumentIntake intake, AttachDocumentIntakeCommand request)
    {
        if (!string.Equals(intake.StorageReference, request.StorageReference.Trim(), StringComparison.Ordinal) ||
            !string.Equals(intake.FileName, request.FileName.Trim(), StringComparison.Ordinal))
            throw new ConflictException("The verified upload metadata does not match the document intake.");
    }

    private async Task SaveAsync(CancellationToken cancellationToken) =>
        await dbContext.SaveChangesAsync(cancellationToken);
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
