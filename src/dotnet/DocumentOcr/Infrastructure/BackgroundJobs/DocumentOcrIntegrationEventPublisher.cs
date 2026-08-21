using MassTransit;

namespace DocumentOcr.Infrastructure.BackgroundJobs;

public interface IDocumentOcrIntegrationEventPublisher
{
    Task PublishAsync(object message, CancellationToken cancellationToken);
}

public sealed class DocumentOcrIntegrationEventPublisher(IPublishEndpoint publishEndpoint)
    : IDocumentOcrIntegrationEventPublisher
{
    public Task PublishAsync(object message, CancellationToken cancellationToken) =>
        publishEndpoint.Publish(message, message.GetType(), cancellationToken);
}
