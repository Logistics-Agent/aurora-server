using MassTransit;

namespace GpsTracking.Infrastructure.BackgroundJobs;

public interface IGpsIntegrationEventPublisher
{
    Task PublishAsync(object message, CancellationToken cancellationToken);
}

public sealed class GpsIntegrationEventPublisher(IPublishEndpoint publishEndpoint)
    : IGpsIntegrationEventPublisher
{
    public Task PublishAsync(object message, CancellationToken cancellationToken) =>
        publishEndpoint.Publish(message, message.GetType(), cancellationToken);
}
