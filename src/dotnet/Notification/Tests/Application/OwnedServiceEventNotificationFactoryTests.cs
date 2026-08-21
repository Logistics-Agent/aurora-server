using DocumentOcr.Contracts.Events;
using GpsTracking.Contracts.Events;
using Notification.Application.Services;
using Notification.Domain.Enums;
using RegulatoryCompliance.Contracts.Events;

namespace Notification.Tests.Application;

public sealed class OwnedServiceEventNotificationFactoryTests
{
    [Fact]
    public void MapsGpsMonitoringAlert()
    {
        var shipmentId = Guid.CreateVersion7();
        var message = new GpsMonitoringAlertRaisedEvent
        {
            EventId = Guid.CreateVersion7(),
            TenantId = Guid.CreateVersion7(),
            AlertId = Guid.CreateVersion7(),
            AlertType = "GeofenceExit",
            VehicleId = "TRUCK-42",
            ShipmentId = shipmentId,
            Message = "Vehicle left the delivery geofence.",
            OccurredAt = DateTimeOffset.UtcNow
        };

        var result = GpsEventNotificationFactory.Create(message);

        Assert.Equal(NotificationEventType.GpsMonitoringAlertRaised, result.EventType);
        Assert.Equal(shipmentId, result.ShipmentId);
        Assert.Contains("TRUCK-42", result.Body);
        Assert.Contains("GeofenceExit", result.Body);
    }

    [Fact]
    public void MapsOcrCompletedWithoutEmbeddingExtractedJson()
    {
        var message = new DocumentOcrCompletedEvent
        {
            EventId = Guid.CreateVersion7(),
            TenantId = Guid.CreateVersion7(),
            JobId = Guid.CreateVersion7(),
            ExternalShipmentId = Guid.CreateVersion7(),
            DetectedDocumentType = "BillOfLading",
            NormalizedJson = """{"sensitive":"must not be copied"}""",
            Confidence = 0.94m,
            NeedsReview = false,
            OccurredAt = DateTimeOffset.UtcNow
        };

        var result = DocumentOcrEventNotificationFactory.Create(message);

        Assert.Equal(NotificationEventType.DocumentOcrCompleted, result.EventType);
        Assert.Equal(message.ExternalShipmentId, result.ShipmentId);
        Assert.Contains("BillOfLading", result.Body);
        Assert.DoesNotContain("sensitive", result.Body);
    }

    [Fact]
    public void MapsOcrFailureAndBoundsProviderError()
    {
        var message = new DocumentOcrFailedEvent
        {
            EventId = Guid.CreateVersion7(),
            TenantId = Guid.CreateVersion7(),
            JobId = Guid.CreateVersion7(),
            ErrorCode = "PROVIDER_FAILURE",
            ErrorMessage = new string((char)120, 3000),
            OccurredAt = DateTimeOffset.UtcNow
        };

        var result = DocumentOcrEventNotificationFactory.Create(message);

        Assert.Equal(NotificationEventType.DocumentOcrFailed, result.EventType);
        Assert.Contains("PROVIDER_FAILURE", result.Body);
        Assert.True(result.Body.Length <= 2000);
    }

    [Fact]
    public void MapsComplianceCompleted()
    {
        var message = new ComplianceEvaluationCompletedEvent
        {
            EventId = Guid.CreateVersion7(),
            TenantId = Guid.CreateVersion7(),
            EvaluationId = Guid.CreateVersion7(),
            ExternalShipmentId = Guid.CreateVersion7(),
            RiskLevel = "High",
            EvidenceSufficiency = "Sufficient",
            ComplianceConfidence = 0.88m,
            ViolationCount = 2,
            MissingDocuments = ["CommercialInvoice"],
            Summary = "Manual review is required.",
            OccurredAt = DateTimeOffset.UtcNow
        };

        var result = ComplianceEventNotificationFactory.Create(message);

        Assert.Equal(NotificationEventType.ComplianceEvaluationCompleted, result.EventType);
        Assert.Equal(message.ExternalShipmentId, result.ShipmentId);
        Assert.Contains("High", result.Body);
        Assert.Contains("2 violation", result.Body);
    }

    [Fact]
    public void MapsComplianceFailureAndBoundsError()
    {
        var message = new ComplianceEvaluationFailedEvent
        {
            EventId = Guid.CreateVersion7(),
            TenantId = Guid.CreateVersion7(),
            EvaluationId = Guid.CreateVersion7(),
            ExternalShipmentId = Guid.CreateVersion7(),
            ErrorCode = "EVALUATION_FAILED",
            ErrorMessage = new string((char)120, 3000),
            Summary = "Evaluation could not complete.",
            OccurredAt = DateTimeOffset.UtcNow
        };

        var result = ComplianceEventNotificationFactory.Create(message);

        Assert.Equal(NotificationEventType.ComplianceEvaluationFailed, result.EventType);
        Assert.Contains("EVALUATION_FAILED", result.Body);
        Assert.True(result.Body.Length <= 2000);
    }
}
