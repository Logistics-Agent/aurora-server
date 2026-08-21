using Microsoft.EntityFrameworkCore;
using Notification.Application.Services;
using Notification.Domain.Entities;
using Notification.Domain.Enums;
using Notification.Infrastructure.Persistences;
using Shared.Interceptors;
using Shared.Security;

namespace Notification.Tests.Application;

public sealed class IntegrationEventNotificationProjectorTests
{
    [Fact]
    public async Task CreatesNotificationsOnlyForEnabledPreferencesInEventTenant()
    {
        var tenantId = Guid.CreateVersion7();
        var otherTenantId = Guid.CreateVersion7();
        await using var context = CreateContext();
        context.NotificationPreferences.AddRange(
            NotificationPreference.Create(
                tenantId, Guid.CreateVersion7(),
                NotificationEventType.ShipmentCreated,
                NotificationChannel.InApp, true, null),
            NotificationPreference.Create(
                tenantId, Guid.CreateVersion7(),
                NotificationEventType.ShipmentCreated,
                NotificationChannel.Email, false, "disabled@example.com"),
            NotificationPreference.Create(
                otherTenantId, Guid.CreateVersion7(),
                NotificationEventType.ShipmentCreated,
                NotificationChannel.InApp, true, null));
        await context.SaveChangesAsync();

        var envelope = CreateEnvelope(tenantId);
        var projector = new IntegrationEventNotificationProjector(context, TimeProvider.System);

        await projector.ProjectAsync(envelope);

        var notifications = await context.Notifications
            .IgnoreQueryFilters()
            .ToListAsync();
        var receipt = await context.ConsumedIntegrationEvents
            .IgnoreQueryFilters()
            .SingleAsync();

        var notification = Assert.Single(notifications);
        Assert.Equal(tenantId, notification.TenantId);
        Assert.Equal(envelope.EventId, notification.SourceEventId);
        Assert.Equal(envelope.ShipmentId, notification.ShipmentId);
        Assert.Equal(tenantId, receipt.TenantId);
    }

    [Fact]
    public async Task DuplicateEventDoesNotCreateAnotherNotificationOrReceipt()
    {
        var tenantId = Guid.CreateVersion7();
        await using var context = CreateContext();
        context.NotificationPreferences.Add(NotificationPreference.Create(
            tenantId, Guid.CreateVersion7(),
            NotificationEventType.ShipmentCreated,
            NotificationChannel.InApp, true, null));
        await context.SaveChangesAsync();

        var envelope = CreateEnvelope(tenantId);
        var projector = new IntegrationEventNotificationProjector(context, TimeProvider.System);

        await projector.ProjectAsync(envelope);
        await projector.ProjectAsync(envelope);

        Assert.Equal(1, await context.Notifications.IgnoreQueryFilters().CountAsync());
        Assert.Equal(1, await context.ConsumedIntegrationEvents.IgnoreQueryFilters().CountAsync());
    }

    [Fact]
    public async Task EventWithoutPreferencesIsStillRecorded()
    {
        await using var context = CreateContext();
        var envelope = CreateEnvelope(Guid.CreateVersion7());
        var projector = new IntegrationEventNotificationProjector(context, TimeProvider.System);

        await projector.ProjectAsync(envelope);

        Assert.Empty(await context.Notifications.IgnoreQueryFilters().ToListAsync());
        Assert.Equal(1, await context.ConsumedIntegrationEvents.IgnoreQueryFilters().CountAsync());
    }

    private static NotificationDbContext CreateContext()
    {
        var currentUser = new CurrentUserService();
        var options = new DbContextOptionsBuilder<NotificationDbContext>()
            .UseInMemoryDatabase($"notification-{Guid.CreateVersion7()}")
            .Options;

        return new NotificationDbContext(
            options,
            currentUser,
            new AuditSaveChangesInterceptor(currentUser));
    }

    [Fact]
    public async Task CreatesNonShipmentNotificationWithoutShipmentReference()
    {
        var tenantId = Guid.CreateVersion7();
        await using var context = CreateContext();
        context.NotificationPreferences.Add(NotificationPreference.Create(
            tenantId, Guid.CreateVersion7(),
            NotificationEventType.DocumentOcrFailed,
            NotificationChannel.InApp, true, null));
        await context.SaveChangesAsync();

        var envelope = new IntegrationEventNotificationEnvelope(
            Guid.CreateVersion7(),
            1,
            tenantId,
            null,
            "DocumentOcrFailedEvent",
            NotificationEventType.DocumentOcrFailed,
            "Document OCR failed",
            "OCR processing failed.",
            DateTimeOffset.UtcNow);
        var projector = new IntegrationEventNotificationProjector(context, TimeProvider.System);

        await projector.ProjectAsync(envelope);

        var notification = await context.Notifications.IgnoreQueryFilters().SingleAsync();
        Assert.Null(notification.ShipmentId);
        Assert.Equal(NotificationEventType.DocumentOcrFailed, notification.EventType);
    }

    private static IntegrationEventNotificationEnvelope CreateEnvelope(Guid tenantId) =>
        new(
            Guid.CreateVersion7(),
            1,
            tenantId,
            Guid.CreateVersion7(),
            "ShipmentCreatedEvent",
            NotificationEventType.ShipmentCreated,
            "Shipment created",
            "Shipment SHP-1001 was created.",
            DateTimeOffset.UtcNow);
}
