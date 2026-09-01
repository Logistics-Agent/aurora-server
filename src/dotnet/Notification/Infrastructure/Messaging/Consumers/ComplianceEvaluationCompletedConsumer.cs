using MassTransit;
using RegulatoryCompliance.Contracts.Events;

namespace Notification.Infrastructure.Messaging.Consumers;

public sealed class ComplianceEvaluationCompletedConsumer(NotificationEventProcessor processor) : IConsumer<ComplianceEvaluationCompletedEvent>
{
    public Task Consume(ConsumeContext<ComplianceEvaluationCompletedEvent> context) => processor.ProcessAsync(
        context.Message.EventId, context.Message.TenantId, context.Message.ExternalShipmentId,
        "COMPLIANCE_EVALUATION_COMPLETED", "Compliance evaluation completed",
        $"Compliance evaluation completed with {context.Message.RiskLevel} risk and {context.Message.ViolationCount} violation(s). {context.Message.Summary}",
        null, context.Message.OccurredAt, context.CancellationToken);
}
