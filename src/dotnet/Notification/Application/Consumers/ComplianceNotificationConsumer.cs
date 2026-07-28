using MassTransit;
using Notification.Application.Services;
using RegulatoryCompliance.Contracts.Events;

namespace Notification.Application.Consumers;

public sealed class ComplianceNotificationConsumer(IIntegrationEventNotificationProjector projector) :
    IConsumer<ComplianceEvaluationCompletedEvent>,
    IConsumer<ComplianceEvaluationFailedEvent>
{
    public Task Consume(ConsumeContext<ComplianceEvaluationCompletedEvent> context) =>
        projector.ProjectAsync(
            ComplianceEventNotificationFactory.Create(context.Message),
            context.CancellationToken);

    public Task Consume(ConsumeContext<ComplianceEvaluationFailedEvent> context) =>
        projector.ProjectAsync(
            ComplianceEventNotificationFactory.Create(context.Message),
            context.CancellationToken);
}
