using DocumentOcr.Contracts.Events;
using MassTransit;
using Notification.Application.Services;

namespace Notification.Application.Consumers;

public sealed class DocumentOcrNotificationConsumer(IIntegrationEventNotificationProjector projector) :
    IConsumer<DocumentOcrCompletedEvent>,
    IConsumer<DocumentOcrFailedEvent>
{
    public Task Consume(ConsumeContext<DocumentOcrCompletedEvent> context) =>
        projector.ProjectAsync(
            DocumentOcrEventNotificationFactory.Create(context.Message),
            context.CancellationToken);

    public Task Consume(ConsumeContext<DocumentOcrFailedEvent> context) =>
        projector.ProjectAsync(
            DocumentOcrEventNotificationFactory.Create(context.Message),
            context.CancellationToken);
}
