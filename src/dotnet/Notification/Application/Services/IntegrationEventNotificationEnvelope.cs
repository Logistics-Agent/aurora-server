using Notification.Domain.Enums;

namespace Notification.Application.Services;

public sealed record IntegrationEventNotificationEnvelope(
    Guid EventId,
    int ContractVersion,
    Guid TenantId,
    Guid? ShipmentId,
    string SourceEventType,
    NotificationEventType EventType,
    string Title,
    string Body,
    DateTimeOffset OccurredAt);

internal static class NotificationContent
{
    private const int MaximumBodyLength = 2000;

    public static string BoundBody(string body)
    {
        var value = body.Trim();
        return value.Length <= MaximumBodyLength
            ? value
            : value[..MaximumBodyLength];
    }
}
