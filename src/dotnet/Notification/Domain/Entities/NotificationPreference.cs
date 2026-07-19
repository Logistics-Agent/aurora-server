using Notification.Domain.Enums;
using Shared.Entity;

namespace Notification.Domain.Entities;

public sealed class NotificationPreference : TenantAuditableEntity
{
    private NotificationPreference() { }

    public Guid RecipientUserId { get; private set; }
    public NotificationEventType EventType { get; private set; }
    public NotificationChannel Channel { get; private set; }
    public bool IsEnabled { get; private set; }
    public string? RecipientAddress { get; private set; }

    public static NotificationPreference Create(
        Guid tenantId,
        Guid recipientUserId,
        NotificationEventType eventType,
        NotificationChannel channel,
        bool isEnabled,
        string? recipientAddress)
    {
        DomainValidation.RequiredId(tenantId, nameof(tenantId));
        DomainValidation.RequiredId(recipientUserId, nameof(recipientUserId));

        return new NotificationPreference
        {
            TenantId = tenantId,
            RecipientUserId = recipientUserId,
            EventType = eventType,
            Channel = channel,
            IsEnabled = isEnabled,
            RecipientAddress = DomainValidation.Recipient(channel, recipientAddress),
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    public void Update(bool isEnabled, string? recipientAddress, DateTimeOffset updatedAt)
    {
        if (updatedAt == default)
            throw new ArgumentException("UpdatedAt is required.", nameof(updatedAt));
        IsEnabled = isEnabled;
        RecipientAddress = DomainValidation.Recipient(Channel, recipientAddress);
        UpdatedAt = updatedAt;
    }
}
