using MassTransit;
using RegulatoryCompliance.Contracts.Events;

namespace Notification.Infrastructure.Messaging.Consumers;

public sealed class ComplianceEvaluationFailedConsumer(NotificationEventProcessor processor) : IConsumer<ComplianceEvaluationFailedEvent>
{
    public Task Consume(ConsumeContext<ComplianceEvaluationFailedEvent> context) => processor.ProcessAsync(
        context.Message.EventId, context.Message.TenantId, context.Message.ExternalShipmentId,
        "COMPLIANCE_EVALUATION_FAILED", "Compliance evaluation failed",
        $"Compliance evaluation failed ({context.Message.ErrorCode}): {context.Message.ErrorMessage}. {context.Message.Summary}",
        null, context.Message.OccurredAt, context.CancellationToken);
}
