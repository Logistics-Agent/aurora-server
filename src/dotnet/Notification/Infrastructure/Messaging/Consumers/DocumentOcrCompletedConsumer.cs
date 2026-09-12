using DocumentOcr.Contracts.Events;
using MassTransit;

namespace Notification.Infrastructure.Messaging.Consumers;

public sealed class DocumentOcrCompletedConsumer(NotificationEventProcessor processor) : IConsumer<DocumentOcrCompletedEvent>
{
    public Task Consume(ConsumeContext<DocumentOcrCompletedEvent> context) => processor.ProcessAsync(
        context.Message.EventId, context.Message.TenantId, context.Message.ExternalShipmentId,
        "DOCUMENT_OCR_COMPLETED", "Document OCR completed",
        $"OCR job {context.Message.JobId} detected {context.Message.DetectedDocumentType} with {context.Message.Confidence:P0} confidence.{(context.Message.NeedsReview ? " Manual review is required." : string.Empty)}",
        null, context.Message.OccurredAt, context.CancellationToken);
}
