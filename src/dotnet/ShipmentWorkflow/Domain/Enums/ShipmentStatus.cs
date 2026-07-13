namespace ShipmentWorkflow.Domain.Enums;

public enum ShipmentStatus
{
    Created = 1,
    WaitingForDocuments = 2,
    DocumentsSubmitted = 3,
    CustomsChecking = 4,
    CustomsCleared = 5,
    InTransit = 6,
    Delivered = 7,
    Completed = 8,
    Cancelled = 9
}
