using DocumentOcr.Contracts.Events;
using Notification.Domain.Enums;

namespace Notification.Application.Services;

public static class DocumentOcrEventNotificationFactory
{
    public static IntegrationEventNotificationEnvelope Create(DocumentOcrCompletedEvent message)
    {
        ArgumentNullException.ThrowIfNull(message);

        var review = message.NeedsReview ? " Manual review is required." : string.Empty;
        return new IntegrationEventNotificationEnvelope(
            message.EventId,
            message.ContractVersion,
            message.TenantId,
            message.ExternalShipmentId,
            nameof(DocumentOcrCompletedEvent),
            NotificationEventType.DocumentOcrCompleted,
            "Document OCR completed",
            NotificationContent.BoundBody(
                $"OCR job {message.JobId} detected {message.DetectedDocumentType} with {message.Confidence:P0} confidence.{review}"),
            message.OccurredAt);
    }

    public static IntegrationEventNotificationEnvelope Create(DocumentOcrFailedEvent message)
    {
        ArgumentNullException.ThrowIfNull(message);

        return new IntegrationEventNotificationEnvelope(
            message.EventId,
            message.ContractVersion,
            message.TenantId,
            message.ExternalShipmentId,
            nameof(DocumentOcrFailedEvent),
            NotificationEventType.DocumentOcrFailed,
            "Document OCR failed",
            NotificationContent.BoundBody(
                $"OCR job {message.JobId} failed ({message.ErrorCode}): {message.ErrorMessage}"),
            message.OccurredAt);
    }
}
