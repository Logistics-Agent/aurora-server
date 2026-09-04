using Notification.Domain.Enums;

namespace Notification.Domain.Entities;

public sealed class NotificationDeliveryAttempt
{
    private NotificationDeliveryAttempt() { }
    public Guid Id { get; private set; } = Guid.CreateVersion7();
    public Guid NotificationId { get; private set; }
    public Guid DeviceId { get; private set; }
    public DeliveryAttemptStatus Status { get; private set; }
    public int AttemptCount { get; private set; }
    public string? ProviderMessageId { get; private set; }
    public string? ErrorCode { get; private set; }
    public DateTimeOffset AttemptedAt { get; private set; }
    public DateTimeOffset? NextAttemptAt { get; private set; }
    public bool IsTerminal => Status is DeliveryAttemptStatus.Sent
        or DeliveryAttemptStatus.Failed
        or DeliveryAttemptStatus.InvalidToken;

    public static NotificationDeliveryAttempt Create(Guid notificationId, Guid deviceId)
    {
        if (notificationId == Guid.Empty || deviceId == Guid.Empty)
            throw new ArgumentException("Notification and device are required.");

        return new()
        {
            NotificationId = notificationId,
            DeviceId = deviceId,
            Status = DeliveryAttemptStatus.Pending,
            AttemptedAt = DateTimeOffset.UtcNow
        };
    }

    public void Sent(string? providerMessageId)
    {
        EnsureMutable();
        Status = DeliveryAttemptStatus.Sent;
        ProviderMessageId = providerMessageId;
        ErrorCode = null;
        NextAttemptAt = null;
        AttemptCount++;
        AttemptedAt = DateTimeOffset.UtcNow;
    }

    public void Retry(string errorCode) => Retry(errorCode, DateTimeOffset.UtcNow);

    public void Retry(string errorCode, DateTimeOffset nextAttemptAt)
    {
        EnsureMutable();
        Status = DeliveryAttemptStatus.Retrying;
        ErrorCode = NormalizeError(errorCode);
        NextAttemptAt = nextAttemptAt;
        AttemptCount++;
        AttemptedAt = DateTimeOffset.UtcNow;
    }

    public bool CanRetry(DateTimeOffset now) =>
        Status == DeliveryAttemptStatus.Retrying &&
        NextAttemptAt.HasValue && NextAttemptAt.Value <= now;

    public void Failed(string errorCode, bool invalidToken)
    {
        EnsureMutable();
        Status = invalidToken ? DeliveryAttemptStatus.InvalidToken : DeliveryAttemptStatus.Failed;
        ErrorCode = NormalizeError(errorCode);
        NextAttemptAt = null;
        AttemptCount++;
        AttemptedAt = DateTimeOffset.UtcNow;
    }

    private void EnsureMutable()
    {
        if (IsTerminal) throw new InvalidOperationException("A terminal delivery attempt cannot change state.");
    }

    private static string NormalizeError(string errorCode)
    {
        if (string.IsNullOrWhiteSpace(errorCode)) return "provider_failure";
        return errorCode.Length <= 128 ? errorCode : errorCode[..128];
    }
}
