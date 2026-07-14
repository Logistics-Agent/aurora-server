namespace ShipmentWorkflow.Domain.Enums;

public enum ShipmentStatus
{
    Created = 1,
    Draft = 1,
    WaitingForDocuments = 2,
    DocumentsSubmitted = 3,
    CustomsProcessing = 4,
    CustomsChecking = 4,
    CustomsCleared = 5,
    InTransit = 6,
    Delivered = 7,
    Completed = 8,
    Cancelled = 9,
    Submitted = 10,
    Planning = 11,
    Negotiating = 12,
    Confirmed = 13,
    PickedUp = 14
}
