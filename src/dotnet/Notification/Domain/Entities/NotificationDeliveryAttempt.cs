using Notification.Domain.Enums;
using Shared.Entity;

namespace Notification.Domain.Entities;

public sealed class NotificationDeliveryAttempt : TenantAuditableEntity
{
    private NotificationDeliveryAttempt() { }

    public Guid NotificationId { get; private set; }
    public int AttemptNumber { get; private set; }
    public DeliveryAttemptStatus Status { get; private set; }
    public DateTimeOffset StartedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public string? ProviderMessageId { get; private set; }
    public string? Error { get; private set; }

    public static NotificationDeliveryAttempt Create(
        Guid tenantId, Guid notificationId, int attemptNumber, DateTimeOffset startedAt)
    {
        DomainValidation.RequiredId(tenantId, nameof(tenantId));
        DomainValidation.RequiredId(notificationId, nameof(notificationId));
        if (attemptNumber <= 0)
            throw new ArgumentOutOfRangeException(nameof(attemptNumber));
        if (startedAt == default)
            throw new ArgumentException("StartedAt is required.", nameof(startedAt));

        return new NotificationDeliveryAttempt
        {
            TenantId = tenantId,
            NotificationId = notificationId,
            AttemptNumber = attemptNumber,
            Status = DeliveryAttemptStatus.InProgress,
            StartedAt = startedAt,
            CreatedAt = startedAt
        };
    }

    internal void Succeed(string providerMessageId, DateTimeOffset completedAt)
    {
        if (Status != DeliveryAttemptStatus.InProgress)
            throw new InvalidOperationException("Attempt is already completed.");
        if (completedAt < StartedAt)
            throw new ArgumentException("CompletedAt cannot precede StartedAt.", nameof(completedAt));

        ProviderMessageId = DomainValidation.RequiredText(providerMessageId, nameof(providerMessageId), 255);
        Status = DeliveryAttemptStatus.Succeeded;
        CompletedAt = completedAt;
        UpdatedAt = completedAt;
    }

    internal void Fail(string error, bool transient, DateTimeOffset completedAt)
    {
        if (Status != DeliveryAttemptStatus.InProgress)
            throw new InvalidOperationException("Attempt is already completed.");
        if (completedAt < StartedAt)
            throw new ArgumentException("CompletedAt cannot precede StartedAt.", nameof(completedAt));

        Error = DomainValidation.RequiredText(error, nameof(error), 1000);
        Status = transient ? DeliveryAttemptStatus.TransientFailure : DeliveryAttemptStatus.PermanentFailure;
        CompletedAt = completedAt;
        UpdatedAt = completedAt;
    }
}
