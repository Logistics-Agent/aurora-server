using Notification.Domain.Entities;
using Notification.Domain.Enums;

namespace Notification.Tests;

public sealed class NotificationDomainTests
{
    [Fact]
    public void CreateEmailNotificationRequiresRecipient()
    {
        Assert.Throws<ArgumentException>(() => NotificationMessage.Create(
            Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(),
            NotificationEventType.ShipmentCreated, NotificationChannel.Email,
            "Shipment created", "A shipment was created.", null, Guid.CreateVersion7()));
    }

    [Fact]
    public void CreateNotificationRejectsMissingTenant()
    {
        Assert.Throws<ArgumentException>(() => NotificationMessage.Create(
            Guid.Empty, Guid.CreateVersion7(), Guid.CreateVersion7(),
            NotificationEventType.ShipmentCreated, NotificationChannel.InApp,
            "Shipment created", "A shipment was created.", null, Guid.CreateVersion7()));
    }

    [Fact]
    public void PreferenceNormalizesEmailAddress()
    {
        var preference = NotificationPreference.Create(
            Guid.CreateVersion7(), Guid.CreateVersion7(),
            NotificationEventType.ShipmentDelivered, NotificationChannel.Email,
            true, "  USER@example.com ");

        Assert.Equal("USER@example.com", preference.RecipientAddress);
    }

    [Fact]
    public void DeliveryAttemptMustHavePositiveNumber()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => NotificationDeliveryAttempt.Create(
            Guid.CreateVersion7(), Guid.CreateVersion7(), 0, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void SuccessfulDeliveryMarksNotificationSent()
    {
        var notification = NotificationMessage.Create(
            Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(),
            NotificationEventType.ShipmentDelivered, NotificationChannel.Email,
            "Delivered", "Shipment delivered.", "user@example.com", Guid.CreateVersion7());

        var attempt = notification.StartDeliveryAttempt(DateTimeOffset.UtcNow);
        notification.CompleteDelivery(attempt, "provider-123", DateTimeOffset.UtcNow);

        Assert.Equal(NotificationStatus.Sent, notification.Status);
        Assert.Equal(DeliveryAttemptStatus.Succeeded, attempt.Status);
        Assert.Equal("provider-123", attempt.ProviderMessageId);
    }
}
