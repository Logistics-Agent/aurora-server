using Microsoft.EntityFrameworkCore;
using Notification.Domain.Entities;
using Shared.Interceptors;
using Shared.Security;

namespace Notification.Infrastructure.Persistences;

public sealed class NotificationDbContext(
    DbContextOptions<NotificationDbContext> options,
    ICurrentUserService currentUser,
    AuditSaveChangesInterceptor auditInterceptor) : DbContext(options)
{
    private readonly ICurrentUserService _currentUser = currentUser;
    private readonly AuditSaveChangesInterceptor _auditInterceptor = auditInterceptor;

    public DbSet<NotificationMessage> Notifications => Set<NotificationMessage>();
    public DbSet<NotificationPreference> NotificationPreferences => Set<NotificationPreference>();
    public DbSet<NotificationDeliveryAttempt> DeliveryAttempts => Set<NotificationDeliveryAttempt>();
    public DbSet<ConsumedIntegrationEvent> ConsumedIntegrationEvents => Set<ConsumedIntegrationEvent>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder) =>
        optionsBuilder.AddInterceptors(_auditInterceptor);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ConfigureNotification(modelBuilder);
        ConfigurePreference(modelBuilder);
        ConfigureAttempt(modelBuilder);
        ConfigureConsumedEvent(modelBuilder);
    }

    private void ConfigureNotification(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<NotificationMessage>(entity =>
        {
            entity.ToTable("notifications");
            entity.HasKey(item => item.Id);
            entity.HasQueryFilter(item => item.TenantId == _currentUser.TenantId);
            entity.HasIndex(item => new { item.TenantId, item.RecipientUserId, item.CreatedAt });
            entity.HasIndex(item => new { item.TenantId, item.RecipientUserId, item.ReadAt });
            entity.HasIndex(item => new { item.TenantId, item.RecipientUserId, item.SourceEventId, item.Channel }).IsUnique();
            entity.HasIndex(item => new { item.Status, item.NextAttemptAt });
            entity.Property(item => item.EventType).HasConversion<string>().HasMaxLength(80).IsRequired();
            entity.Property(item => item.Channel).HasConversion<string>().HasMaxLength(20).IsRequired();
            entity.Property(item => item.Status).HasConversion<string>().HasMaxLength(30).IsRequired();
            entity.Property(item => item.Title).HasMaxLength(200).IsRequired();
            entity.Property(item => item.Body).HasMaxLength(2000).IsRequired();
            entity.Property(item => item.RecipientAddress).HasMaxLength(320);
            entity.HasMany(item => item.DeliveryAttempts)
                .WithOne()
                .HasForeignKey(attempt => attempt.NotificationId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    private void ConfigurePreference(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<NotificationPreference>(entity =>
        {
            entity.ToTable("notification_preferences");
            entity.HasKey(item => item.Id);
            entity.HasQueryFilter(item => item.TenantId == _currentUser.TenantId);
            entity.HasIndex(item => new { item.TenantId, item.RecipientUserId, item.EventType, item.Channel }).IsUnique();
            entity.Property(item => item.EventType).HasConversion<string>().HasMaxLength(80).IsRequired();
            entity.Property(item => item.Channel).HasConversion<string>().HasMaxLength(20).IsRequired();
            entity.Property(item => item.RecipientAddress).HasMaxLength(320);
        });
    }

    private void ConfigureAttempt(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<NotificationDeliveryAttempt>(entity =>
        {
            entity.ToTable("notification_delivery_attempts");
            entity.HasKey(item => item.Id);
            entity.HasQueryFilter(item => item.TenantId == _currentUser.TenantId);
            entity.HasIndex(item => new { item.NotificationId, item.AttemptNumber }).IsUnique();
            entity.Property(item => item.Status).HasConversion<string>().HasMaxLength(30).IsRequired();
            entity.Property(item => item.ProviderMessageId).HasMaxLength(255);
            entity.Property(item => item.Error).HasMaxLength(1000);
        });
    }

    private void ConfigureConsumedEvent(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ConsumedIntegrationEvent>(entity =>
        {
            entity.ToTable("consumed_integration_events");
            entity.HasKey(item => item.Id);
            entity.HasQueryFilter(item => item.TenantId == _currentUser.TenantId);
            entity.HasIndex(item => new { item.SourceEventType, item.SourceEventId }).IsUnique();
            entity.HasIndex(item => new { item.TenantId, item.ReceivedAt });
            entity.Property(item => item.SourceEventType).HasMaxLength(256).IsRequired();
        });
    }
}
