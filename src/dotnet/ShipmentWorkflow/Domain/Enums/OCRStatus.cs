namespace ShipmentWorkflow.Domain.Enums;

public enum OCRStatus
{
    NotRequired = 0,
    Pending = 1,
    Processing = 2,
    Completed = 3,
    Failed = 4,
    NeedsReview = 5
}
