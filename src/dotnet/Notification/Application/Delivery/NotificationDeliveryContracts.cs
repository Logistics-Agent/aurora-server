using Notification.Domain.Enums;

namespace Notification.Application.Delivery;

public sealed record NotificationDeliveryRequest(
    Guid NotificationId,
    Guid TenantId,
    Guid RecipientUserId,
    NotificationChannel Channel,
    string Title,
    string Body,
    string? RecipientAddress);

public sealed record NotificationDeliveryResult(
    bool IsSuccess,
    string? ProviderMessageId,
    string? Error,
    bool IsTransient)
{
    public static NotificationDeliveryResult Success(string providerMessageId) =>
        new(true, providerMessageId, null, false);

    public static NotificationDeliveryResult Failure(string error, bool isTransient) =>
        new(false, null, error, isTransient);
}

public interface INotificationDeliveryProvider
{
    NotificationChannel Channel { get; }

    Task<NotificationDeliveryResult> DeliverAsync(
        NotificationDeliveryRequest request,
        CancellationToken cancellationToken = default);
}

public interface IEmailNotificationProvider : INotificationDeliveryProvider;

public interface IInAppNotificationProvider : INotificationDeliveryProvider;
