namespace Notification.Domain.Entities;

public sealed class NotificationSubscription
{
    private NotificationSubscription() { }
    public Guid Id { get; private set; } = Guid.CreateVersion7();
    public Guid TenantId { get; private set; }
    public Guid UserId { get; private set; }
    public Guid ShipmentId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public static NotificationSubscription Create(Guid tenantId, Guid userId, Guid shipmentId) =>
        tenantId == Guid.Empty || userId == Guid.Empty || shipmentId == Guid.Empty
            ? throw new ArgumentException("Tenant, user, and shipment are required.")
            : new() { TenantId = tenantId, UserId = userId, ShipmentId = shipmentId, CreatedAt = DateTimeOffset.UtcNow };
}
