using Microsoft.EntityFrameworkCore;
using Notification.Application.Delivery;
using Notification.Application.Services;
using Notification.Domain.Entities;
using Notification.Domain.Enums;
using Notification.Infrastructure.Persistences;
using Shared.Interceptors;
using Shared.Security;

namespace Notification.Tests.Integration;

public sealed class NotificationPostgresIntegrationTests
{
    private const string DefaultConnectionString =
        "Host=localhost;Port=5434;Database=aurora_notification;Username=postgres;Password=postgres";

    [Fact]
    public async Task EventProjectionDeliveryAndTenantFilterWorkAgainstMigratedPostgres()
    {
        var connectionString = Environment.GetEnvironmentVariable(
            "NOTIFICATION_TEST_CONNECTION_STRING") ?? DefaultConnectionString;
        var tenantId = Guid.CreateVersion7();
        var otherTenantId = Guid.CreateVersion7();
        var userId = Guid.CreateVersion7();
        var eventId = Guid.CreateVersion7();
        var currentUser = CreateCurrentUser(tenantId, userId);

        await using var context = CreateContext(connectionString, currentUser);
        var preference = NotificationPreference.Create(
            tenantId,
            userId,
            NotificationEventType.ShipmentCreated,
            NotificationChannel.InApp,
            true,
            null);
        context.NotificationPreferences.Add(preference);
        await context.SaveChangesAsync();

        try
        {
            var envelope = new ShipmentNotificationEnvelope(
                eventId,
                1,
                tenantId,
                Guid.CreateVersion7(),
                "ShipmentCreatedEvent",
                NotificationEventType.ShipmentCreated,
                "Shipment created",
                "Shipment SHP-INTEGRATION was created.",
                DateTimeOffset.UtcNow);
            var projector = new ShipmentNotificationProjector(
                context,
                TimeProvider.System);

            await projector.ProjectAsync(envelope);
            await projector.ProjectAsync(envelope);

            var notification = await context.Notifications
                .Include(item => item.DeliveryAttempts)
                .SingleAsync(item => item.SourceEventId == eventId);
            var delivery = new NotificationDeliveryService(
                context,
                [new InAppNotificationProvider()],
                new NotificationRetryPolicy(new NotificationRetryOptions()),
                TimeProvider.System);

            var result = await delivery.DeliverAsync(notification.Id);

            Assert.True(result.Delivered);
            Assert.Equal(NotificationStatus.Sent, notification.Status);
            Assert.Single(notification.DeliveryAttempts);
            Assert.Equal(
                1,
                await context.ConsumedIntegrationEvents.CountAsync(
                    item => item.SourceEventId == eventId));

            await using var otherTenantContext = CreateContext(
                connectionString,
                CreateCurrentUser(otherTenantId, userId));
            Assert.False(await otherTenantContext.Notifications
                .AnyAsync(item => item.SourceEventId == eventId));
            Assert.False(await otherTenantContext.ConsumedIntegrationEvents
                .AnyAsync(item => item.SourceEventId == eventId));
        }
        finally
        {
            await context.DeliveryAttempts
                .IgnoreQueryFilters()
                .Where(item => item.TenantId == tenantId)
                .ExecuteDeleteAsync();
            await context.Notifications
                .IgnoreQueryFilters()
                .Where(item => item.TenantId == tenantId)
                .ExecuteDeleteAsync();
            await context.NotificationPreferences
                .IgnoreQueryFilters()
                .Where(item => item.TenantId == tenantId)
                .ExecuteDeleteAsync();
            await context.ConsumedIntegrationEvents
                .IgnoreQueryFilters()
                .Where(item => item.TenantId == tenantId)
                .ExecuteDeleteAsync();
        }
    }

    private static NotificationDbContext CreateContext(
        string connectionString,
        CurrentUserService currentUser)
    {
        var options = new DbContextOptionsBuilder<NotificationDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new NotificationDbContext(
            options,
            currentUser,
            new AuditSaveChangesInterceptor(currentUser));
    }

    private static CurrentUserService CreateCurrentUser(
        Guid tenantId,
        Guid userId)
    {
        var currentUser = new CurrentUserService();
        currentUser.Populate(userId, tenantId, "integration-test", 1, [], []);
        return currentUser;
    }
}
