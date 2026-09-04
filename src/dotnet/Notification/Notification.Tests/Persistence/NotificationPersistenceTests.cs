using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Notification.Domain.Entities;
using Notification.Domain.Enums;
using Notification.Infrastructure.Persistences;
using NotificationEntity = Notification.Domain.Entities.Notification;
using Xunit;

namespace Notification.Tests.Persistence;

public sealed class NotificationPersistenceTests
{
    [Fact]
    public async Task Active_fcm_token_cannot_belong_to_two_owners()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var db = CreateDb(connection);
        await db.Database.EnsureCreatedAsync();

        db.Devices.Add(NotificationDevice.Register(Guid.NewGuid(), Guid.NewGuid(), "shared-token", DevicePlatform.Web));
        await db.SaveChangesAsync();

        db.Devices.Add(NotificationDevice.Register(Guid.NewGuid(), Guid.NewGuid(), "shared-token", DevicePlatform.Android));

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task Processed_event_rejects_duplicate_tenant_event_rule()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var db = CreateDb(connection);
        await db.Database.EnsureCreatedAsync();
        var tenantId = Guid.NewGuid();
        var eventId = Guid.NewGuid();

        db.ProcessedEvents.Add(ProcessedNotificationEvent.Create(
            eventId, tenantId, "shipment-status", ProcessedNotificationEventOutcome.NoRecipient, 0));
        await db.SaveChangesAsync();
        db.ProcessedEvents.Add(ProcessedNotificationEvent.Create(
            eventId, tenantId, "shipment-status", ProcessedNotificationEventOutcome.NoRecipient, 0));

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task Processed_event_allows_different_tenant_rule_or_event()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var db = CreateDb(connection);
        await db.Database.EnsureCreatedAsync();
        var tenantId = Guid.NewGuid();
        var eventId = Guid.NewGuid();

        db.ProcessedEvents.AddRange(
            ProcessedNotificationEvent.Create(
                eventId, tenantId, "shipment-status", ProcessedNotificationEventOutcome.NoRecipient, 0),
            ProcessedNotificationEvent.Create(
                eventId, Guid.NewGuid(), "shipment-status", ProcessedNotificationEventOutcome.NoRecipient, 0),
            ProcessedNotificationEvent.Create(
                eventId, tenantId, "shipment-created", ProcessedNotificationEventOutcome.NoRecipient, 0),
            ProcessedNotificationEvent.Create(
                Guid.NewGuid(), tenantId, "shipment-status", ProcessedNotificationEventOutcome.NoRecipient, 0));

        await db.SaveChangesAsync();

        Assert.Equal(4, await db.ProcessedEvents.CountAsync());
    }

    [Fact]
    public async Task Subscription_cannot_be_registered_twice_for_the_same_user_and_shipment()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var db = CreateDb(connection);
        await db.Database.EnsureCreatedAsync();
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var shipmentId = Guid.NewGuid();

        db.Subscriptions.Add(NotificationSubscription.Create(tenantId, userId, shipmentId));
        await db.SaveChangesAsync();
        db.Subscriptions.Add(NotificationSubscription.Create(tenantId, userId, shipmentId));

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task Tenant_user_query_helpers_do_not_return_another_tenants_records()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var db = CreateDb(connection);
        await db.Database.EnsureCreatedAsync();
        var tenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        db.Notifications.AddRange(
            NotificationEntity.Create(tenantId, userId, "SHIPMENT_DELIVERED", "Own", "Own", null, null, "/notifications", NotificationPriority.Info),
            NotificationEntity.Create(otherTenantId, userId, "SHIPMENT_DELIVERED", "Other", "Other", null, null, "/notifications", NotificationPriority.Info));
        db.Devices.AddRange(
            NotificationDevice.Register(tenantId, userId, "own-token", DevicePlatform.Web),
            NotificationDevice.Register(otherTenantId, userId, "other-token", DevicePlatform.Web));
        db.Subscriptions.AddRange(
            NotificationSubscription.Create(tenantId, userId, Guid.NewGuid()),
            NotificationSubscription.Create(otherTenantId, userId, Guid.NewGuid()));
        await db.SaveChangesAsync();

        Assert.Single(await db.NotificationsFor(tenantId, userId).ToListAsync());
        Assert.Equal("Own", (await db.NotificationsFor(tenantId, userId).SingleAsync()).Title);
        Assert.Single(await db.DevicesFor(tenantId, userId).ToListAsync());
        Assert.Single(await db.SubscriptionsFor(tenantId, userId).ToListAsync());
    }

    private static NotificationDbContext CreateDb(SqliteConnection connection) =>
        new(new DbContextOptionsBuilder<NotificationDbContext>().UseSqlite(connection).Options);
}
