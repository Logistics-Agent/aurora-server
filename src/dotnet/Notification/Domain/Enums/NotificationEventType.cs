namespace Notification.Domain.Enums;

public enum NotificationEventType
{
    ShipmentCreated = 1,
    ShipmentSubmitted = 2,
    ShipmentStatusChanged = 3,
    ShipmentCancelled = 4,
    ShipmentPickedUp = 5,
    ShipmentDelivered = 6,
    ShipmentCompleted = 7,
    DocumentAttached = 8,
    GpsMonitoringAlertRaised = 9,
    DocumentOcrCompleted = 10,
    DocumentOcrFailed = 11,
    ComplianceEvaluationCompleted = 12,
    ComplianceEvaluationFailed = 13
}
