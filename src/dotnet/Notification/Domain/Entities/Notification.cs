using Notification.Domain.Enums;

namespace Notification.Domain.Entities;

public sealed class Notification
{
    private const int MaxTitleLength = 200;
    private const int MaxBodyLength = 2000;

    private Notification() { }

    public Guid Id { get; private set; } = Guid.CreateVersion7();
    public Guid TenantId { get; private set; }
    public Guid UserId { get; private set; }
    public string Type { get; private set; } = string.Empty;
    public NotificationChannel Channel { get; private set; }
    public NotificationPriority Priority { get; private set; }
    public NotificationStatus Status { get; private set; }
    public Guid? ShipmentId { get; private set; }
    public string? ShipmentNumber { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Body { get; private set; } = string.Empty;
    public string? ActionUrl { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? ReadAt { get; private set; }

    public static Notification Create(Guid tenantId, Guid userId, string type, string title, string body,
        Guid? shipmentId, string? shipmentNumber, string? actionUrl, NotificationPriority priority)
    {
        if (tenantId == Guid.Empty || userId == Guid.Empty) throw new ArgumentException("Tenant and user are required.");
        if (string.IsNullOrWhiteSpace(type) || string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(body))
            throw new ArgumentException("Notification type, title, and body are required.");
        if (type.Trim().Length > 64) throw new ArgumentException("Notification type exceeds its maximum length.");
        if (title.Trim().Length > MaxTitleLength) throw new ArgumentException("Notification title exceeds its maximum length.");
        if (body.Trim().Length > MaxBodyLength) throw new ArgumentException("Notification body exceeds its maximum length.");
        if (actionUrl is not null && (!actionUrl.StartsWith("/", StringComparison.Ordinal) || actionUrl.StartsWith("//", StringComparison.Ordinal)))
            throw new ArgumentException("ActionUrl must be an internal path.");
        if (actionUrl is not null && actionUrl.Length > 512)
            throw new ArgumentException("ActionUrl exceeds its maximum length.");

        return new Notification
        {
            TenantId = tenantId, UserId = userId, Type = type.Trim(), Channel = NotificationChannel.Push,
            Priority = priority, Status = NotificationStatus.Pending, ShipmentId = shipmentId,
            ShipmentNumber = shipmentNumber, Title = title.Trim(), Body = body.Trim(), ActionUrl = actionUrl,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    public void MarkSent() => Status = NotificationStatus.Sent;
    public void MarkRead() { Status = NotificationStatus.Read; ReadAt = DateTimeOffset.UtcNow; }
}
