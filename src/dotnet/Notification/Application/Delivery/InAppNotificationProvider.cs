using Notification.Domain.Enums;

namespace Notification.Application.Delivery;

public sealed class InAppNotificationProvider : IInAppNotificationProvider
{
    public NotificationChannel Channel => NotificationChannel.InApp;

    public Task<NotificationDeliveryResult> DeliverAsync(
        NotificationDeliveryRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(NotificationDeliveryResult.Success(
            $"in-app:{request.NotificationId}"));
    }
}
