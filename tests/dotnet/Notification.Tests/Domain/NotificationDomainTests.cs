using Notification.Domain.Entities;
using Notification.Domain.Enums;
using NotificationEntity = Notification.Domain.Entities.Notification;
using Xunit;

namespace Notification.Tests.Domain;

public sealed class NotificationDomainTests
{
    [Fact]
    public void Create_rejects_external_action_url()
    {
        Assert.Throws<ArgumentException>(() => NotificationEntity.Create(Guid.NewGuid(), Guid.NewGuid(), "SHIPMENT_DELIVERED", "Title", "Body", null, null, "https://evil.example", NotificationPriority.Info));
    }

    [Fact]
    public void Device_registration_is_active_and_tenant_scoped()
    {
        var tenantId = Guid.NewGuid(); var userId = Guid.NewGuid();
        var device = NotificationDevice.Register(tenantId, userId, "token-1", DevicePlatform.Web);
        Assert.Equal(tenantId, device.TenantId); Assert.Equal(userId, device.UserId); Assert.True(device.IsActive);
    }
}
