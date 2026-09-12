using DocumentOcr.Contracts.Events;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using ShipmentWorkflow.Domain.Enums;
using ShipmentWorkflow.Infrastructure.Persistences;

namespace ShipmentWorkflow.Application.Events;

public sealed class DocumentOcrIntegrationConsumer(
    ShipmentWorkflowDbContext dbContext,
    ILogger<DocumentOcrIntegrationConsumer> logger) :
    IConsumer<DocumentOcrCompletedEvent>,
    IConsumer<DocumentOcrFailedEvent>,
    IConsumer<DocumentOcrRequiresReviewEvent>
{
    public Task Consume(ConsumeContext<DocumentOcrCompletedEvent> context) =>
        HandleAsync(context.Message, context.CancellationToken);

    public Task Consume(ConsumeContext<DocumentOcrFailedEvent> context) =>
        HandleAsync(context.Message, context.CancellationToken);

    public Task Consume(ConsumeContext<DocumentOcrRequiresReviewEvent> context) =>
        HandleAsync(context.Message, context.CancellationToken);

    public Task HandleAsync(
        DocumentOcrCompletedEvent message,
        CancellationToken cancellationToken = default) =>
        ApplyAsync(message, OCRStatusForCompleted(message), cancellationToken);

    public Task HandleAsync(
        DocumentOcrFailedEvent message,
        CancellationToken cancellationToken = default) =>
        ApplyAsync(message, OCRStatus.Failed, cancellationToken);

    public Task HandleAsync(
        DocumentOcrRequiresReviewEvent message,
        CancellationToken cancellationToken = default) =>
        ApplyAsync(message, OCRStatus.NeedsReview, cancellationToken);

    private async Task ApplyAsync(
        DocumentOcrCompletedEvent message,
        OCRStatus status,
        CancellationToken cancellationToken)
    {
        DocumentOcrEventContract.ValidateVersion(nameof(DocumentOcrCompletedEvent), message.ContractVersion);
        if (message.Purpose != DocumentOcrPurpose.ShipmentDocument)
            return;

        await ApplyProjectionAsync(
            message.TenantId,
            message.ShipmentId,
            message.DocumentId ?? message.ExternalDocumentId,
            message.EventId,
            status,
            message.Confidence,
            message.NormalizedJson,
            cancellationToken);
    }

    private async Task ApplyAsync(
        DocumentOcrFailedEvent message,
        OCRStatus status,
        CancellationToken cancellationToken)
    {
        DocumentOcrEventContract.ValidateVersion(nameof(DocumentOcrFailedEvent), message.ContractVersion);
        if (message.Purpose != DocumentOcrPurpose.ShipmentDocument)
            return;

        await ApplyProjectionAsync(
            message.TenantId,
            message.ShipmentId,
            message.DocumentId ?? message.ExternalDocumentId,
            message.EventId,
            status,
            message.Confidence,
            null,
            cancellationToken);
    }

    private async Task ApplyAsync(
        DocumentOcrRequiresReviewEvent message,
        OCRStatus status,
        CancellationToken cancellationToken)
    {
        DocumentOcrEventContract.ValidateVersion(nameof(DocumentOcrRequiresReviewEvent), message.ContractVersion);
        if (message.Purpose != DocumentOcrPurpose.ShipmentDocument)
            return;

        await ApplyProjectionAsync(
            message.TenantId,
            message.ShipmentId,
            message.DocumentId ?? message.ExternalDocumentId,
            message.EventId,
            status,
            message.Confidence,
            null,
            cancellationToken);
    }

    private async Task ApplyProjectionAsync(
        Guid tenantId,
        Guid? shipmentId,
        Guid? documentId,
        Guid eventId,
        OCRStatus status,
        decimal confidence,
        string? extractedDataJson,
        CancellationToken cancellationToken)
    {
        if (tenantId == Guid.Empty ||
            !shipmentId.HasValue || shipmentId.Value == Guid.Empty ||
            !documentId.HasValue || documentId.Value == Guid.Empty)
        {
            throw new InvalidOperationException("Shipment OCR events require tenant, shipment, and document linkage.");
        }

        var shipment = await dbContext.Shipments
            .IgnoreQueryFilters()
            .Include(item => item.Documents)
            .SingleOrDefaultAsync(
                item => item.TenantId == tenantId && item.Id == shipmentId.Value,
                cancellationToken)
            ?? throw new InvalidOperationException("Shipment OCR event references an unknown shipment.");

        var document = shipment.Documents.SingleOrDefault(
            item => item.TenantId == tenantId && item.Id == documentId.Value)
            ?? throw new InvalidOperationException("Shipment OCR event references an unknown document.");

        document.ApplyOcrEvent(eventId, status, confidence, extractedDataJson);
        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation(
            "Applied OCR event {EventId} to shipment document {DocumentId} with status {Status}.",
            eventId,
            documentId,
            status);
    }

    private static OCRStatus OCRStatusForCompleted(DocumentOcrCompletedEvent message) =>
        message.NeedsReview ? OCRStatus.NeedsReview : OCRStatus.Completed;
}
