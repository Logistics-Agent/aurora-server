using Microsoft.EntityFrameworkCore;
using Notification.Domain.Enums;
using Notification.Infrastructure.Persistences;

namespace Notification.Application.Delivery;

public sealed record NotificationDeliveryExecution(
    bool Found,
    bool AlreadyDelivered,
    bool Delivered,
    bool Deferred,
    bool RetryExhausted,
    bool IsTransientFailure,
    DateTimeOffset? NextAttemptAt,
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
    INotificationRetryPolicy retryPolicy,
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
            return new NotificationDeliveryExecution(false, false, false, false, false, false, null, null);

        if (notification.Status == NotificationStatus.Sent)
            return new NotificationDeliveryExecution(true, true, true, false, false, false, null, null);

        var startedAt = timeProvider.GetUtcNow();
        if (notification.Status == NotificationStatus.Failed && notification.NextAttemptAt is null)
        {
            return new NotificationDeliveryExecution(
                true, false, false, false, true, false, null,
                "Delivery failed permanently or exhausted its retry limit.");
        }

        if (notification.NextAttemptAt is not null && notification.NextAttemptAt > startedAt)
        {
            return new NotificationDeliveryExecution(
                true, false, false, true, false, true, notification.NextAttemptAt, null);
        }

        var attempt = notification.StartDeliveryAttempt(startedAt);
        dbContext.DeliveryAttempts.Add(attempt);
        await dbContext.SaveChangesAsync(cancellationToken);

        if (!_providers.TryGetValue(notification.Channel, out var provider))
        {
            const string error = "No delivery provider is configured for the notification channel.";
            notification.FailDelivery(attempt, error, false, timeProvider.GetUtcNow());
            await dbContext.SaveChangesAsync(cancellationToken);
            return new NotificationDeliveryExecution(true, false, false, false, true, false, null, error);
        }

        var request = new NotificationDeliveryRequest(
            notification.Id,
            notification.TenantId,
            notification.RecipientUserId,
            notification.Channel,
            notification.Title,
            notification.Body,
            notification.RecipientAddress);

        NotificationDeliveryResult result;
        try
        {
            result = await provider.DeliverAsync(request, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            result = NotificationDeliveryResult.Failure(NormalizeError(exception.Message), true);
        }

        var completedAt = timeProvider.GetUtcNow();
        NotificationRetryDecision? retryDecision = null;

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
            var error = NormalizeError(
                result.Error ?? "Delivery provider returned an unspecified error.");
            retryDecision = retryPolicy.Decide(
                attempt.AttemptNumber,
                result.IsTransient,
                completedAt);
            notification.FailDelivery(
                attempt,
                error,
                result.IsTransient,
                completedAt,
                retryDecision.NextAttemptAt);
            result = result with { Error = error };
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return new NotificationDeliveryExecution(
            true,
            false,
            result.IsSuccess,
            false,
            retryDecision is { ShouldRetry: false },
            !result.IsSuccess && result.IsTransient,
            retryDecision?.NextAttemptAt,
            result.Error);
    }

    private static string NormalizeError(string error) =>
        error.Length <= 1000 ? error : error[..1000];
}
