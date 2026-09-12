using DocumentOcr.Contracts.Events;
using Notification.Infrastructure.Messaging;
using Xunit;

namespace Notification.Tests.Messaging;

public sealed class DocumentOcrNotificationPolicyTests
{
    [Fact]
    public void Shipment_purpose_is_the_only_purpose_that_creates_notifications()
    {
        Assert.True(DocumentOcrNotificationPolicy.ShouldNotify(DocumentOcrPurpose.ShipmentDocument));
        Assert.False(DocumentOcrNotificationPolicy.ShouldNotify(DocumentOcrPurpose.RegulatoryCorpus));
        Assert.False(DocumentOcrNotificationPolicy.ShouldNotify(DocumentOcrPurpose.KnowledgeCorpus));
        Assert.False(DocumentOcrNotificationPolicy.ShouldNotify(DocumentOcrPurpose.GeneralDocument));
    }

    [Fact]
    public void Unsupported_completed_version_is_rejected_for_retry()
    {
        Assert.Throws<NotSupportedException>(() =>
            DocumentOcrNotificationPolicy.ValidateCompletedVersion(1));
    }

    [Fact]
    public void Unsupported_failed_version_is_rejected_for_retry()
    {
        Assert.Throws<NotSupportedException>(() =>
            DocumentOcrNotificationPolicy.ValidateFailedVersion(1));
    }
}
