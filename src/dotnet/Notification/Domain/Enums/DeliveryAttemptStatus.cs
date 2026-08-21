namespace Notification.Domain.Enums;

public enum DeliveryAttemptStatus
{
    InProgress = 1,
    Succeeded = 2,
    TransientFailure = 3,
    PermanentFailure = 4
}
