using DocumentOcr.Contracts.Events;

namespace Notification.Infrastructure.Messaging;

public static class DocumentOcrNotificationPolicy
{
    public static bool ShouldNotify(DocumentOcrPurpose purpose) =>
        purpose == DocumentOcrPurpose.ShipmentDocument;

    public static void ValidateCompletedVersion(int contractVersion) =>
        DocumentOcrEventContract.ValidateVersion(nameof(DocumentOcrCompletedEvent), contractVersion);

    public static void ValidateFailedVersion(int contractVersion) =>
        DocumentOcrEventContract.ValidateVersion(nameof(DocumentOcrFailedEvent), contractVersion);
}
