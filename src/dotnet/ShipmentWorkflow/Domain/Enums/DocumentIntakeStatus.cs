namespace ShipmentWorkflow.Domain.Enums;

public enum DocumentIntakeStatus
{
    PendingAttachment = 1,
    PendingOcr = 2,
    Submitted = 3,
    FailedRetryable = 4
}
