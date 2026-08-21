using Microsoft.EntityFrameworkCore;
using Notification.Domain.Entities;
using Notification.Infrastructure.Persistences;

namespace Notification.Application.Services;

public interface IIntegrationEventNotificationProjector
{
    Task ProjectAsync(
        IntegrationEventNotificationEnvelope message,
        CancellationToken cancellationToken = default);
}

public sealed class IntegrationEventNotificationProjector(
    NotificationDbContext dbContext,
    TimeProvider timeProvider) : IIntegrationEventNotificationProjector
{
    public async Task ProjectAsync(
        IntegrationEventNotificationEnvelope message,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        var alreadyConsumed = await dbContext.ConsumedIntegrationEvents
            .IgnoreQueryFilters()
            .AnyAsync(
                item => item.SourceEventType == message.SourceEventType
                    && item.SourceEventId == message.EventId,
                cancellationToken);

        if (alreadyConsumed)
            return;

        var preferences = await dbContext.NotificationPreferences
            .IgnoreQueryFilters()
            .Where(item => item.TenantId == message.TenantId
                && item.EventType == message.EventType
                && item.IsEnabled)
            .ToListAsync(cancellationToken);

        foreach (var preference in preferences)
        {
            dbContext.Notifications.Add(NotificationMessage.Create(
                message.TenantId,
                preference.RecipientUserId,
                message.EventId,
                message.EventType,
                preference.Channel,
                message.Title,
                message.Body,
                preference.RecipientAddress,
                message.ShipmentId));
        }

        dbContext.ConsumedIntegrationEvents.Add(ConsumedIntegrationEvent.Create(
            message.TenantId,
            message.EventId,
            message.SourceEventType,
            message.ContractVersion,
            timeProvider.GetUtcNow()));

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
