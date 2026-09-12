using MediatR;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Shared.Exceptions;
using Shared.Security;
using ShipmentWorkflow.Application.DTOs.Shipments;
using ShipmentWorkflow.Domain.Entities;
using ShipmentWorkflow.Domain.Enums;
using ShipmentWorkflow.Infrastructure.Persistences;

namespace ShipmentWorkflow.Application.Commands.Shipments;

public sealed record AttachShipmentDocumentCommand(
    Guid ShipmentId,
    string FileName,
    DocumentType DocumentType,
    string StorageUrl,
    OCRStatus OCRStatus,
    decimal? OCRConfidence,
    string? ExtractedDataJson,
    string? IdempotencyKey = null,
    Guid? UploadId = null,
    string? StorageReference = null) : IRequest<ShipmentDto>;

public sealed record UpdateShipmentDocumentOcrCommand(
    Guid ShipmentId,
    Guid DocumentId,
    OCRStatus OCRStatus,
    decimal? OCRConfidence,
    string? ExtractedDataJson) : IRequest<ShipmentDto>;

public sealed record RemoveShipmentDocumentCommand(Guid ShipmentId, Guid DocumentId) : IRequest<ShipmentDto>;

public sealed class AttachShipmentDocumentCommandHandler(
    ShipmentWorkflowDbContext dbContext,
    ICurrentUserService currentUser) : IRequestHandler<AttachShipmentDocumentCommand, ShipmentDto>
{
    public async Task<ShipmentDto> Handle(AttachShipmentDocumentCommand request, CancellationToken cancellationToken)
    {
        ShipmentCommandHelpers.RequireTenantId(currentUser);
        var storageReference = NormalizeStorageReference(request);
        var idempotencyKey = NormalizeOptional(request.IdempotencyKey);
        ValidateIdempotentFields(request, idempotencyKey, storageReference);
        var shipment = await ShipmentCommandHelpers.GetShipmentAsync(dbContext, request.ShipmentId, cancellationToken);
        ShipmentCommandHelpers.EnsureNonTerminalMutation(shipment);

        var requestHash = idempotencyKey is null
            ? null
            : DocumentIntakeRequestHasher.ForAttachment(request, storageReference);
        var existing = idempotencyKey is null
            ? null
            : shipment.Documents.SingleOrDefault(document =>
                document.IdempotencyKey == idempotencyKey);
        if (existing is not null)
        {
            if (!string.Equals(existing.RequestHash, requestHash, StringComparison.Ordinal))
                throw new ConflictException("The idempotency key was already used with a different request.");
            return ShipmentDto.FromEntity(shipment);
        }

        var document = shipment.AddDocumentMetadata(
            request.FileName,
            request.DocumentType,
            string.IsNullOrWhiteSpace(request.StorageUrl) ? storageReference : request.StorageUrl,
            currentUser.UserId,
            DateTimeOffset.UtcNow,
            request.OCRStatus,
            request.OCRConfidence,
            request.ExtractedDataJson,
            idempotencyKey,
            request.UploadId,
            idempotencyKey is null ? null : storageReference,
            requestHash);
        ShipmentCommandHelpers.MarkAggregateRootUnchanged(dbContext, shipment);
        dbContext.Entry(document).State = EntityState.Added;
        ShipmentCommandHelpers.AddDocumentAttachedOutbox(dbContext, shipment, document);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception) && idempotencyKey is not null)
        {
            dbContext.ChangeTracker.Clear();
            shipment = await ShipmentCommandHelpers.GetShipmentAsync(dbContext, request.ShipmentId, cancellationToken);
            existing = shipment.Documents.SingleOrDefault(document => document.IdempotencyKey == idempotencyKey)
                ?? throw new ConflictException("The document attachment could not be committed safely.");
            if (!string.Equals(existing.RequestHash, requestHash, StringComparison.Ordinal))
                throw new ConflictException("The idempotency key was already used with a different request.");
        }

        return ShipmentDto.FromEntity(shipment);
    }

    private static string NormalizeStorageReference(AttachShipmentDocumentCommand request)
    {
        var storageReference = string.IsNullOrWhiteSpace(request.StorageReference)
            ? request.StorageUrl
            : request.StorageReference;
        return string.IsNullOrWhiteSpace(storageReference)
            ? throw new DomainException("StorageReference is required.")
            : storageReference.Trim();
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static void ValidateIdempotentFields(
        AttachShipmentDocumentCommand request,
        string? idempotencyKey,
        string storageReference)
    {
        if (idempotencyKey is null &&
            (request.UploadId.HasValue || !string.IsNullOrWhiteSpace(request.StorageReference)))
        {
            throw new DomainException("IdempotencyKey is required for intake metadata.");
        }

        if (idempotencyKey is null)
            return;
        if (!request.UploadId.HasValue || request.UploadId.Value == Guid.Empty)
            throw new DomainException("UploadId is required for idempotent attachment.");
        if (storageReference.Length > ShipmentDocument.StorageUrlMaxLength)
            throw new DomainException("StorageReference must be 1000 characters or fewer.");
    }

    private static bool IsUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
}

public sealed class UpdateShipmentDocumentOcrCommandHandler(
    ShipmentWorkflowDbContext dbContext,
    ICurrentUserService currentUser) : IRequestHandler<UpdateShipmentDocumentOcrCommand, ShipmentDto>
{
    public async Task<ShipmentDto> Handle(UpdateShipmentDocumentOcrCommand request, CancellationToken cancellationToken)
    {
        ShipmentCommandHelpers.RequireTenantId(currentUser);
        var shipment = await ShipmentCommandHelpers.GetShipmentAsync(dbContext, request.ShipmentId, cancellationToken);
        ShipmentCommandHelpers.EnsureNonTerminalMutation(shipment);

        try
        {
            shipment.UpdateDocumentOcrMetadata(request.DocumentId, request.OCRStatus, request.OCRConfidence, request.ExtractedDataJson);
        }
        catch (InvalidOperationException ex)
        {
            throw new NotFoundException(ex.Message);
        }

        ShipmentCommandHelpers.MarkAggregateRootUnchanged(dbContext, shipment);
        await dbContext.SaveChangesAsync(cancellationToken);
        return ShipmentDto.FromEntity(shipment);
    }
}

public sealed class RemoveShipmentDocumentCommandHandler(
    ShipmentWorkflowDbContext dbContext,
    ICurrentUserService currentUser) : IRequestHandler<RemoveShipmentDocumentCommand, ShipmentDto>
{
    public async Task<ShipmentDto> Handle(RemoveShipmentDocumentCommand request, CancellationToken cancellationToken)
    {
        ShipmentCommandHelpers.RequireTenantId(currentUser);
        var shipment = await ShipmentCommandHelpers.GetShipmentAsync(dbContext, request.ShipmentId, cancellationToken);
        ShipmentCommandHelpers.EnsureNonTerminalMutation(shipment);

        try
        {
            shipment.RemoveDocumentMetadata(request.DocumentId);
        }
        catch (InvalidOperationException ex)
        {
            throw new NotFoundException(ex.Message);
        }

        ShipmentCommandHelpers.MarkAggregateRootUnchanged(dbContext, shipment);
        await dbContext.SaveChangesAsync(cancellationToken);
        return ShipmentDto.FromEntity(shipment);
    }
}
