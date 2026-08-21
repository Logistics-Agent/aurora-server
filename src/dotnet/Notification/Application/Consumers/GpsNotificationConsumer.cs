using GpsTracking.Contracts.Events;
using MassTransit;
using Notification.Application.Services;

namespace Notification.Application.Consumers;

public sealed class GpsNotificationConsumer(IIntegrationEventNotificationProjector projector) :
    IConsumer<GpsMonitoringAlertRaisedEvent>
{
    public Task Consume(ConsumeContext<GpsMonitoringAlertRaisedEvent> context) =>
        projector.ProjectAsync(
            GpsEventNotificationFactory.Create(context.Message),
            context.CancellationToken);
}
