using MediatR;
using Microsoft.EntityFrameworkCore;
using Shared.Exceptions;
using Shared.Security;
using ShipmentWorkflow.Application.DTOs.Shipments;
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
    string? ExtractedDataJson) : IRequest<ShipmentDto>;

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
        var shipment = await ShipmentCommandHelpers.GetShipmentAsync(dbContext, request.ShipmentId, cancellationToken);
        ShipmentCommandHelpers.EnsureNonTerminalMutation(shipment);

        shipment.AddDocumentMetadata(
            request.FileName,
            request.DocumentType,
            request.StorageUrl,
            currentUser.UserId,
            DateTimeOffset.UtcNow,
            request.OCRStatus,
            request.OCRConfidence,
            request.ExtractedDataJson);

        var document = shipment.Documents.Last();
        dbContext.Entry(document).State = EntityState.Added;
        ShipmentCommandHelpers.AddDocumentAttachedOutbox(dbContext, shipment, document);
        ShipmentCommandHelpers.MarkAggregateRootUnchanged(dbContext, shipment);

        await dbContext.SaveChangesAsync(cancellationToken);
        return ShipmentDto.FromEntity(shipment);
    }
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
