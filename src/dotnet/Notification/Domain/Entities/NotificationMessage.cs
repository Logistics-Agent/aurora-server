using Notification.Domain.Enums;
using Shared.Entity;

namespace Notification.Domain.Entities;

public sealed class NotificationMessage : TenantAuditableEntity
{
    private readonly List<NotificationDeliveryAttempt> _deliveryAttempts = [];

    private NotificationMessage() { }

    public Guid RecipientUserId { get; private set; }
    public Guid SourceEventId { get; private set; }
    public Guid? ShipmentId { get; private set; }
    public NotificationEventType EventType { get; private set; }
    public NotificationChannel Channel { get; private set; }
    public NotificationStatus Status { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Body { get; private set; } = string.Empty;
    public string? RecipientAddress { get; private set; }
    public DateTimeOffset? SentAt { get; private set; }
    public DateTimeOffset? ReadAt { get; private set; }
    public IReadOnlyCollection<NotificationDeliveryAttempt> DeliveryAttempts => _deliveryAttempts;

    public static NotificationMessage Create(
        Guid tenantId, Guid recipientUserId, Guid sourceEventId,
        NotificationEventType eventType, NotificationChannel channel,
        string title, string body, string? recipientAddress, Guid? shipmentId = null)
    {
        DomainValidation.RequiredId(tenantId, nameof(tenantId));
        DomainValidation.RequiredId(recipientUserId, nameof(recipientUserId));
        DomainValidation.RequiredId(sourceEventId, nameof(sourceEventId));
        if (shipmentId == Guid.Empty)
            throw new ArgumentException("ShipmentId cannot be empty.", nameof(shipmentId));

        return new NotificationMessage
        {
            TenantId = tenantId,
            RecipientUserId = recipientUserId,
            SourceEventId = sourceEventId,
            ShipmentId = shipmentId,
            EventType = eventType,
            Channel = channel,
            Status = NotificationStatus.Pending,
            Title = DomainValidation.RequiredText(title, nameof(title), 200),
            Body = DomainValidation.RequiredText(body, nameof(body), 2000),
            RecipientAddress = DomainValidation.Recipient(channel, recipientAddress),
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    public NotificationDeliveryAttempt StartDeliveryAttempt(DateTimeOffset startedAt)
    {
        if (Status == NotificationStatus.Sent)
            throw new InvalidOperationException("Notification is already sent.");

        var attempt = NotificationDeliveryAttempt.Create(TenantId, Id, _deliveryAttempts.Count + 1, startedAt);
        _deliveryAttempts.Add(attempt);
        return attempt;
    }

    public void CompleteDelivery(NotificationDeliveryAttempt attempt, string providerMessageId, DateTimeOffset completedAt)
    {
        EnsureOwned(attempt);
        attempt.Succeed(providerMessageId, completedAt);
        Status = NotificationStatus.Sent;
        SentAt = completedAt;
        UpdatedAt = completedAt;
    }

    public void FailDelivery(NotificationDeliveryAttempt attempt, string error, bool transient, DateTimeOffset completedAt)
    {
        EnsureOwned(attempt);
        attempt.Fail(error, transient, completedAt);
        Status = NotificationStatus.Failed;
        UpdatedAt = completedAt;
    }

    public void MarkRead(DateTimeOffset readAt)
    {
        if (Channel != NotificationChannel.InApp)
            throw new InvalidOperationException("Only in-app notifications can be read.");
        if (readAt < CreatedAt)
            throw new ArgumentException("ReadAt cannot precede CreatedAt.", nameof(readAt));
        ReadAt ??= readAt;
        UpdatedAt = readAt;
    }

    private void EnsureOwned(NotificationDeliveryAttempt attempt)
    {
        ArgumentNullException.ThrowIfNull(attempt);
        if (!_deliveryAttempts.Contains(attempt))
            throw new InvalidOperationException("Attempt does not belong to this notification.");
    }
}
