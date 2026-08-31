namespace Notification.Infrastructure.Security;

public sealed class NotificationServiceAuthOptions
{
    public const string SectionName = "ServiceAuth";

    public string AllowedServiceId { get; init; } = string.Empty;
    public string ApiKey { get; init; } = string.Empty;
}
