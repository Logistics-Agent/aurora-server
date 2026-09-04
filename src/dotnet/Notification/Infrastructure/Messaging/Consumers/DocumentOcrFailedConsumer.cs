using DocumentOcr.Contracts.Events;
using MassTransit;

namespace Notification.Infrastructure.Messaging.Consumers;

public sealed class DocumentOcrFailedConsumer(NotificationEventProcessor processor) : IConsumer<DocumentOcrFailedEvent>
{
    public Task Consume(ConsumeContext<DocumentOcrFailedEvent> context) => processor.ProcessAsync(
        context.Message.EventId, context.Message.TenantId, context.Message.ExternalShipmentId,
        "DOCUMENT_OCR_FAILED", "Document OCR failed",
        $"OCR job {context.Message.JobId} failed ({context.Message.ErrorCode}): {context.Message.ErrorMessage}",
        null, context.Message.OccurredAt, context.CancellationToken);
}
