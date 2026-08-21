using Notification.Domain.Enums;
using RegulatoryCompliance.Contracts.Events;

namespace Notification.Application.Services;

public static class ComplianceEventNotificationFactory
{
    public static IntegrationEventNotificationEnvelope Create(ComplianceEvaluationCompletedEvent message)
    {
        ArgumentNullException.ThrowIfNull(message);

        var missingDocuments = message.MissingDocuments.Count == 0
            ? "none"
            : string.Join(", ", message.MissingDocuments);
        var body = $"Compliance evaluation for shipment {message.ExternalShipmentId} completed with "
            + $"{message.RiskLevel} risk and {message.ViolationCount} violation(s). "
            + $"Missing documents: {missingDocuments}. {message.Summary}";

        return new IntegrationEventNotificationEnvelope(
            message.EventId,
            message.ContractVersion,
            message.TenantId,
            message.ExternalShipmentId,
            nameof(ComplianceEvaluationCompletedEvent),
            NotificationEventType.ComplianceEvaluationCompleted,
            "Compliance evaluation completed",
            NotificationContent.BoundBody(body),
            message.OccurredAt);
    }

    public static IntegrationEventNotificationEnvelope Create(ComplianceEvaluationFailedEvent message)
    {
        ArgumentNullException.ThrowIfNull(message);

        return new IntegrationEventNotificationEnvelope(
            message.EventId,
            message.ContractVersion,
            message.TenantId,
            message.ExternalShipmentId,
            nameof(ComplianceEvaluationFailedEvent),
            NotificationEventType.ComplianceEvaluationFailed,
            "Compliance evaluation failed",
            NotificationContent.BoundBody(
                $"Compliance evaluation for shipment {message.ExternalShipmentId} failed "
                + $"({message.ErrorCode}): {message.ErrorMessage}. {message.Summary}"),
            message.OccurredAt);
    }
}
