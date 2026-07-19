using Microsoft.EntityFrameworkCore;
using Notification.Domain.Enums;
using Notification.Infrastructure.Persistences;

namespace Notification.Application.Delivery;

public sealed record NotificationDeliveryExecution(
    bool Found,
    bool AlreadyDelivered,
    bool Delivered,
    bool IsTransientFailure,
    string? Error);

public interface INotificationDeliveryService
{
    Task<NotificationDeliveryExecution> DeliverAsync(
        Guid notificationId,
        CancellationToken cancellationToken = default);
}

public sealed class NotificationDeliveryService(
    NotificationDbContext dbContext,
    IEnumerable<INotificationDeliveryProvider> providers,
    TimeProvider timeProvider) : INotificationDeliveryService
{
    private readonly IReadOnlyDictionary<NotificationChannel, INotificationDeliveryProvider> _providers =
        providers.ToDictionary(provider => provider.Channel);

    public async Task<NotificationDeliveryExecution> DeliverAsync(
        Guid notificationId,
        CancellationToken cancellationToken = default)
    {
        if (notificationId == Guid.Empty)
            throw new ArgumentException("NotificationId is required.", nameof(notificationId));

        var notification = await dbContext.Notifications
            .IgnoreQueryFilters()
            .Include(item => item.DeliveryAttempts)
            .SingleOrDefaultAsync(item => item.Id == notificationId, cancellationToken);

        if (notification is null)
            return new NotificationDeliveryExecution(false, false, false, false, null);

        if (notification.Status == NotificationStatus.Sent)
            return new NotificationDeliveryExecution(true, true, true, false, null);

        var startedAt = timeProvider.GetUtcNow();
        var attempt = notification.StartDeliveryAttempt(startedAt);
        dbContext.DeliveryAttempts.Add(attempt);
        await dbContext.SaveChangesAsync(cancellationToken);

        if (!_providers.TryGetValue(notification.Channel, out var provider))
        {
            const string error = "No delivery provider is configured for the notification channel.";
            notification.FailDelivery(attempt, error, false, timeProvider.GetUtcNow());
            await dbContext.SaveChangesAsync(cancellationToken);
            return new NotificationDeliveryExecution(true, false, false, false, error);
        }

        var request = new NotificationDeliveryRequest(
            notification.Id,
            notification.TenantId,
            notification.RecipientUserId,
            notification.Channel,
            notification.Title,
            notification.Body,
            notification.RecipientAddress);

        try
        {
            var result = await provider.DeliverAsync(request, cancellationToken);
            var completedAt = timeProvider.GetUtcNow();

            if (result.IsSuccess)
            {
                notification.CompleteDelivery(
                    attempt,
                    result.ProviderMessageId
                        ?? throw new InvalidOperationException("A successful provider result requires a message ID."),
                    completedAt);
            }
            else
            {
                notification.FailDelivery(
                    attempt,
                    result.Error ?? "Delivery provider returned an unspecified error.",
                    result.IsTransient,
                    completedAt);
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            return new NotificationDeliveryExecution(
                true,
                false,
                result.IsSuccess,
                !result.IsSuccess && result.IsTransient,
                result.Error);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            notification.FailDelivery(attempt, exception.Message, true, timeProvider.GetUtcNow());
            await dbContext.SaveChangesAsync(CancellationToken.None);
            return new NotificationDeliveryExecution(true, false, false, true, exception.Message);
        }
    }
}
