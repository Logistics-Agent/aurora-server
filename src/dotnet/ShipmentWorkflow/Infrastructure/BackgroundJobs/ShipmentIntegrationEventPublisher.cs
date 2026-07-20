using MassTransit;

namespace ShipmentWorkflow.Infrastructure.BackgroundJobs;

public interface IShipmentIntegrationEventPublisher
{
    Task PublishAsync(object message, CancellationToken cancellationToken);
}

public sealed class ShipmentIntegrationEventPublisher(IPublishEndpoint publishEndpoint) :
    IShipmentIntegrationEventPublisher
{
    public Task PublishAsync(object message, CancellationToken cancellationToken) =>
        publishEndpoint.Publish(message, message.GetType(), cancellationToken);
}
