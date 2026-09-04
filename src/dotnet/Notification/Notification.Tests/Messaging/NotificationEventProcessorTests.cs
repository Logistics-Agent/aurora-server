using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Data.Sqlite;
using Notification.Application.Interfaces;
using Notification.Domain.Entities;
using Notification.Domain.Enums;
using Notification.Infrastructure.Firebase;
using Notification.Infrastructure.Messaging;
using Notification.Infrastructure.Persistences;
using Xunit;

namespace Notification.Tests.Messaging;

public sealed class NotificationEventProcessorTests
{
    [Fact]
    public async Task Event_is_idempotent_and_sends_to_subscribed_user()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var db = CreateDb(connection);
        await db.Database.EnsureCreatedAsync();
        var tenant = Guid.NewGuid(); var user = Guid.NewGuid(); var shipment = Guid.NewGuid();
        db.Subscriptions.Add(NotificationSubscription.Create(tenant, user, shipment));
        db.Devices.Add(NotificationDevice.Register(tenant, user, "token-1", DevicePlatform.Web));
        await db.SaveChangesAsync();
        var provider = new FakeFcmPushProvider();
        var processor = new NotificationEventProcessor(db, new SubscriptionRecipientResolver(db), provider, NullLogger<NotificationEventProcessor>.Instance);
        var eventId = Guid.NewGuid();

        await processor.ProcessAsync(eventId, tenant, shipment, "SHIPMENT_DELIVERED", "Delivered", "Done", "SHP-1", DateTimeOffset.UtcNow, default);
        await processor.ProcessAsync(eventId, tenant, shipment, "SHIPMENT_DELIVERED", "Delivered", "Done", "SHP-1", DateTimeOffset.UtcNow, default);

        Assert.Single(db.Notifications);
        Assert.Single(provider.SentMessages);
        Assert.Equal("/shipments/" + shipment, provider.SentMessages[0].Message.Data["actionUrl"]);
    }

    [Fact]
    public async Task Event_persists_one_receipt_and_complete_multi_recipient_audience_before_sending()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var db = CreateDb(connection);
        await db.Database.EnsureCreatedAsync();
        var tenantId = Guid.NewGuid();
        var shipmentId = Guid.NewGuid();
        var firstUserId = Guid.NewGuid();
        var secondUserId = Guid.NewGuid();
        db.Subscriptions.AddRange(
            NotificationSubscription.Create(tenantId, firstUserId, shipmentId),
            NotificationSubscription.Create(tenantId, secondUserId, shipmentId));
        db.Devices.AddRange(
            NotificationDevice.Register(tenantId, firstUserId, "token-1", DevicePlatform.Web),
            NotificationDevice.Register(tenantId, secondUserId, "token-2", DevicePlatform.Android));
        await db.SaveChangesAsync();
        var provider = new PersistedAudienceAssertingProvider(db, expectedRecipientCount: 2);
        var processor = new NotificationEventProcessor(
            db, new SubscriptionRecipientResolver(db), provider,
            NullLogger<NotificationEventProcessor>.Instance);
        var eventId = Guid.NewGuid();

        await processor.ProcessAsync(
            eventId, tenantId, shipmentId, "SHIPMENT_DELIVERED", "Delivered", "Done", "SHP-1",
            DateTimeOffset.UtcNow, default);
        await processor.ProcessAsync(
            eventId, tenantId, shipmentId, "SHIPMENT_DELIVERED", "Delivered", "Done", "SHP-1",
            DateTimeOffset.UtcNow, default);

        var receipt = Assert.Single(await db.ProcessedEvents.AsNoTracking().ToListAsync());
        Assert.Equal(ProcessedNotificationEventOutcome.AudienceResolved, receipt.Outcome);
        Assert.Equal(2, receipt.RecipientCount);
        Assert.Equal(2, await db.Notifications.CountAsync());
        Assert.Equal(2, provider.SendCount);
    }

    [Fact]
    public async Task Event_without_recipients_persists_one_no_recipient_receipt()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var db = CreateDb(connection);
        await db.Database.EnsureCreatedAsync();
        var provider = new FakeFcmPushProvider();
        var processor = new NotificationEventProcessor(
            db, new SubscriptionRecipientResolver(db), provider,
            NullLogger<NotificationEventProcessor>.Instance);
        var eventId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();

        await processor.ProcessAsync(
            eventId, tenantId, Guid.NewGuid(), "SHIPMENT_DELIVERED", "Delivered", "Done", "SHP-1",
            DateTimeOffset.UtcNow, default);
        await processor.ProcessAsync(
            eventId, tenantId, Guid.NewGuid(), "SHIPMENT_DELIVERED", "Delivered", "Done", "SHP-1",
            DateTimeOffset.UtcNow, default);

        var receipt = Assert.Single(await db.ProcessedEvents.AsNoTracking().ToListAsync());
        Assert.Equal(ProcessedNotificationEventOutcome.NoRecipient, receipt.Outcome);
        Assert.Equal(0, receipt.RecipientCount);
        Assert.Empty(db.Notifications);
        Assert.Empty(provider.SentMessages);
    }

    [Fact]
    public async Task Invalid_token_deactivates_only_the_rejected_device()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var db = CreateDb(connection);
        await db.Database.EnsureCreatedAsync();
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var shipmentId = Guid.NewGuid();
        var device = NotificationDevice.Register(tenantId, userId, "invalid-token", DevicePlatform.Web);
        db.Subscriptions.Add(NotificationSubscription.Create(tenantId, userId, shipmentId));
        db.Devices.Add(device);
        await db.SaveChangesAsync();
        var provider = new FakeFcmPushProvider { NextStatus = FcmSendStatus.InvalidToken };

        await new NotificationEventProcessor(
            db, new SubscriptionRecipientResolver(db), provider,
            NullLogger<NotificationEventProcessor>.Instance)
            .ProcessAsync(Guid.NewGuid(), tenantId, shipmentId, "SHIPMENT_DELIVERED", "Delivered", "Done", null, DateTimeOffset.UtcNow, default);

        var attempt = Assert.Single(await db.DeliveryAttempts.AsNoTracking().ToListAsync());
        Assert.Equal(DeliveryAttemptStatus.InvalidToken, attempt.Status);
        Assert.False(device.IsActive);
    }

    [Fact]
    public async Task Transient_provider_failure_is_persisted_as_a_scheduled_retry()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var db = CreateDb(connection);
        await db.Database.EnsureCreatedAsync();
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var shipmentId = Guid.NewGuid();
        db.Subscriptions.Add(NotificationSubscription.Create(tenantId, userId, shipmentId));
        db.Devices.Add(NotificationDevice.Register(tenantId, userId, "transient-token", DevicePlatform.Web));
        await db.SaveChangesAsync();
        var provider = new FakeFcmPushProvider { NextStatus = FcmSendStatus.TransientFailure };
        var before = DateTimeOffset.UtcNow;

        await new NotificationEventProcessor(
            db, new SubscriptionRecipientResolver(db), provider,
            NullLogger<NotificationEventProcessor>.Instance)
            .ProcessAsync(Guid.NewGuid(), tenantId, shipmentId, "SHIPMENT_DELIVERED", "Delivered", "Done", null, DateTimeOffset.UtcNow, default);

        var attempt = Assert.Single(await db.DeliveryAttempts.AsNoTracking().ToListAsync());
        Assert.Equal(DeliveryAttemptStatus.Retrying, attempt.Status);
        Assert.True(attempt.NextAttemptAt > before);
    }

    private static NotificationDbContext CreateDb(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<NotificationDbContext>()
            .UseSqlite(connection).Options;
        return new NotificationDbContext(options);
    }

    private sealed class PersistedAudienceAssertingProvider(
        NotificationDbContext db,
        int expectedRecipientCount) : IFcmPushProvider
    {
        public int SendCount { get; private set; }

        public async Task<FcmSendResult> SendAsync(
            NotificationDevice device,
            FcmMessage message,
            CancellationToken cancellationToken)
        {
            var receipt = Assert.Single(await db.ProcessedEvents.AsNoTracking().ToListAsync(cancellationToken));
            Assert.Equal(expectedRecipientCount, receipt.RecipientCount);
            Assert.Equal(expectedRecipientCount, await db.Notifications.CountAsync(cancellationToken));
            SendCount++;
            return new FcmSendResult(FcmSendStatus.Sent, $"message-{SendCount}");
        }
    }
}
