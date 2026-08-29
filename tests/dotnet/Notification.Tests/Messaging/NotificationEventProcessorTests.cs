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

    private static NotificationDbContext CreateDb(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<NotificationDbContext>()
            .UseSqlite(connection).Options;
        return new NotificationDbContext(options);
    }
}
