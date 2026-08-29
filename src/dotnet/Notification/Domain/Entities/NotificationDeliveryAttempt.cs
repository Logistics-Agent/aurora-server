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

    public static NotificationDeliveryAttempt Create(Guid notificationId, Guid deviceId) => new()
    { NotificationId = notificationId, DeviceId = deviceId, Status = DeliveryAttemptStatus.Pending, AttemptedAt = DateTimeOffset.UtcNow };

    public void Sent(string? providerMessageId) { Status = DeliveryAttemptStatus.Sent; ProviderMessageId = providerMessageId; AttemptCount++; AttemptedAt = DateTimeOffset.UtcNow; }
    public void Retry(string errorCode) { Status = DeliveryAttemptStatus.Retrying; ErrorCode = errorCode; AttemptCount++; AttemptedAt = DateTimeOffset.UtcNow; }
    public void Failed(string errorCode, bool invalidToken) { Status = invalidToken ? DeliveryAttemptStatus.InvalidToken : DeliveryAttemptStatus.Failed; ErrorCode = errorCode; AttemptCount++; AttemptedAt = DateTimeOffset.UtcNow; }
}
