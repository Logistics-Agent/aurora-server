using Microsoft.EntityFrameworkCore;
using Notification.Domain.Entities;
using NotificationEntity = Notification.Domain.Entities.Notification;

namespace Notification.Infrastructure.Persistences;

public sealed class NotificationDbContext(DbContextOptions<NotificationDbContext> options) : DbContext(options)
{
    public DbSet<NotificationEntity> Notifications => Set<NotificationEntity>();
    public DbSet<NotificationDevice> Devices => Set<NotificationDevice>();
    public DbSet<NotificationSubscription> Subscriptions => Set<NotificationSubscription>();
    public DbSet<NotificationDeliveryAttempt> DeliveryAttempts => Set<NotificationDeliveryAttempt>();
    public DbSet<ProcessedNotificationEvent> ProcessedEvents => Set<ProcessedNotificationEvent>();

    public IQueryable<NotificationEntity> NotificationsFor(Guid tenantId, Guid userId) =>
        Notifications.Where(x => x.TenantId == tenantId && x.UserId == userId);

    public IQueryable<NotificationDevice> DevicesFor(Guid tenantId, Guid userId) =>
        Devices.Where(x => x.TenantId == tenantId && x.UserId == userId);

    public IQueryable<NotificationSubscription> SubscriptionsFor(Guid tenantId, Guid userId) =>
        Subscriptions.Where(x => x.TenantId == tenantId && x.UserId == userId);

    public IQueryable<NotificationSubscription> SubscriptionsForShipment(Guid tenantId, Guid shipmentId) =>
        Subscriptions.Where(x => x.TenantId == tenantId && x.ShipmentId == shipmentId);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<NotificationEntity>(entity =>
        {
            entity.ToTable("notifications");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Type).HasMaxLength(64).IsRequired();
            entity.Property(x => x.Title).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Body).HasMaxLength(2000).IsRequired();
            entity.Property(x => x.ActionUrl).HasMaxLength(512);
            entity.HasIndex(x => new { x.TenantId, x.UserId, x.CreatedAt, x.Id });
        });
        modelBuilder.Entity<NotificationDevice>(entity =>
        {
            entity.ToTable("notification_devices");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.FcmToken).HasMaxLength(4096).IsRequired();
            entity.HasIndex(x => x.FcmToken).IsUnique().HasFilter("\"IsActive\" = true");
            entity.HasIndex(x => new { x.TenantId, x.UserId, x.IsActive });
        });
        modelBuilder.Entity<NotificationSubscription>(entity =>
        {
            entity.ToTable("notification_subscriptions");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.TenantId, x.UserId, x.ShipmentId }).IsUnique();
            entity.HasIndex(x => new { x.TenantId, x.ShipmentId });
        });
        modelBuilder.Entity<NotificationDeliveryAttempt>(entity =>
        {
            entity.ToTable("notification_delivery_attempts");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ErrorCode).HasMaxLength(128);
            entity.Property(x => x.NextAttemptAt);
            entity.HasIndex(x => new { x.NotificationId, x.DeviceId }).IsUnique();
            entity.HasIndex(x => new { x.Status, x.NextAttemptAt });
        });
        modelBuilder.Entity<ProcessedNotificationEvent>(entity =>
        {
            entity.ToTable("processed_notification_events");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Rule).HasMaxLength(100).IsRequired();
            entity.HasIndex(x => new { x.TenantId, x.EventId, x.Rule }).IsUnique();
        });
    }
}
