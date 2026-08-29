using Notification.Domain.Enums;

namespace Notification.Domain.Entities;

public sealed class NotificationDevice
{
    private NotificationDevice() { }

    public Guid Id { get; private set; } = Guid.CreateVersion7();
    public Guid TenantId { get; private set; }
    public Guid UserId { get; private set; }
    public string FcmToken { get; private set; } = string.Empty;
    public DevicePlatform Platform { get; private set; }
    public bool IsActive { get; private set; }
    public DateTimeOffset LastSeenAt { get; private set; }

    public static NotificationDevice Register(Guid tenantId, Guid userId, string token, DevicePlatform platform)
    {
        if (tenantId == Guid.Empty || userId == Guid.Empty) throw new ArgumentException("Tenant and user are required.");
        if (string.IsNullOrWhiteSpace(token) || token.Length > 4096) throw new ArgumentException("Invalid FCM token.");
        return new NotificationDevice { TenantId = tenantId, UserId = userId, FcmToken = token.Trim(), Platform = platform, IsActive = true, LastSeenAt = DateTimeOffset.UtcNow };
    }

    public void Touch(string token, DevicePlatform platform) { FcmToken = token.Trim(); Platform = platform; IsActive = true; LastSeenAt = DateTimeOffset.UtcNow; }
    public void Deactivate() => IsActive = false;
}
