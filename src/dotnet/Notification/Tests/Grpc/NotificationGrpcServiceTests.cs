using Grpc.Core;
using Microsoft.EntityFrameworkCore;
using Notification.Domain.Entities;
using Notification.Domain.Enums;
using Notification.Grpc;
using Notification.GrpcServices;
using Notification.Infrastructure.Persistences;
using Shared.Interceptors;
using Shared.Security;

namespace Notification.Tests.Grpc;

public sealed class NotificationGrpcServiceTests
{
    [Fact]
    public async Task ListNotificationsReturnsOnlyCurrentTenantAndUserRecords()
    {
        var tenantId = Guid.CreateVersion7();
        var userId = Guid.CreateVersion7();
        var currentUser = CreateCurrentUser(tenantId, userId);
        await using var context = CreateContext(currentUser);
        var visible = CreateNotification(tenantId, userId);
        context.Notifications.AddRange(
            visible,
            CreateNotification(tenantId, Guid.CreateVersion7()),
            CreateNotification(Guid.CreateVersion7(), userId));
        await context.SaveChangesAsync();
        var service = new NotificationGrpcService(context, currentUser, TimeProvider.System);

        var response = await service.ListNotifications(
            new ListNotificationsRequest { Page = 1, PageSize = 20 },
            TestServerCallContext.Create());

        var item = Assert.Single(response.Notifications);
        Assert.Equal(visible.Id.ToString(), item.Id);
        Assert.Equal(1, response.TotalItems);
    }

    [Fact]
    public async Task MissingIdentityIsRejected()
    {
        var currentUser = new CurrentUserService();
        await using var context = CreateContext(currentUser);
        var service = new NotificationGrpcService(context, currentUser, TimeProvider.System);

        var exception = await Assert.ThrowsAsync<RpcException>(() =>
            service.ListNotifications(
                new ListNotificationsRequest(),
                TestServerCallContext.Create()));

        Assert.Equal(StatusCode.Unauthenticated, exception.StatusCode);
    }

    [Fact]
    public async Task MarkNotificationReadDoesNotLeakCrossTenantRecord()
    {
        var currentUser = CreateCurrentUser(
            Guid.CreateVersion7(),
            Guid.CreateVersion7());
        await using var context = CreateContext(currentUser);
        var otherTenantNotification = CreateNotification(
            Guid.CreateVersion7(),
            currentUser.UserId!.Value);
        context.Notifications.Add(otherTenantNotification);
        await context.SaveChangesAsync();
        var service = new NotificationGrpcService(context, currentUser, TimeProvider.System);

        var exception = await Assert.ThrowsAsync<RpcException>(() =>
            service.MarkNotificationRead(
                new MarkNotificationReadRequest
                {
                    Id = otherTenantNotification.Id.ToString()
                },
                TestServerCallContext.Create()));

        Assert.Equal(StatusCode.NotFound, exception.StatusCode);
        Assert.Null(otherTenantNotification.ReadAt);
    }

    [Fact]
    public async Task MarkNotificationReadUpdatesOwnedInAppNotification()
    {
        var tenantId = Guid.CreateVersion7();
        var userId = Guid.CreateVersion7();
        var currentUser = CreateCurrentUser(tenantId, userId);
        await using var context = CreateContext(currentUser);
        var notification = CreateNotification(tenantId, userId);
        context.Notifications.Add(notification);
        await context.SaveChangesAsync();
        var service = new NotificationGrpcService(context, currentUser, TimeProvider.System);

        var response = await service.MarkNotificationRead(
            new MarkNotificationReadRequest { Id = notification.Id.ToString() },
            TestServerCallContext.Create());

        Assert.True(response.IsRead);
        Assert.NotNull(notification.ReadAt);
    }

    [Fact]
    public async Task ListNotificationsAppliesUnreadFilterAndPagination()
    {
        var tenantId = Guid.CreateVersion7();
        var userId = Guid.CreateVersion7();
        var currentUser = CreateCurrentUser(tenantId, userId);
        await using var context = CreateContext(currentUser);
        var read = CreateNotification(tenantId, userId);
        read.MarkRead(DateTimeOffset.UtcNow.AddSeconds(1));
        context.Notifications.AddRange(
            read,
            CreateNotification(tenantId, userId),
            CreateNotification(tenantId, userId));
        await context.SaveChangesAsync();
        var service = new NotificationGrpcService(context, currentUser, TimeProvider.System);

        var response = await service.ListNotifications(
            new ListNotificationsRequest
            {
                Page = 2,
                PageSize = 1,
                UnreadOnly = true
            },
            TestServerCallContext.Create());

        Assert.Equal(2, response.TotalItems);
        Assert.Equal(2, response.TotalPages);
        Assert.Single(response.Notifications);
        Assert.False(response.Notifications[0].IsRead);
    }

    [Fact]
    public async Task UpsertPreferenceCreatesThenUpdatesCurrentUserPreference()
    {
        var tenantId = Guid.CreateVersion7();
        var userId = Guid.CreateVersion7();
        var currentUser = CreateCurrentUser(tenantId, userId);
        await using var context = CreateContext(currentUser);
        var service = new NotificationGrpcService(context, currentUser, TimeProvider.System);
        var request = new UpsertNotificationPreferenceRequest
        {
            EventType = nameof(NotificationEventType.ShipmentCreated),
            Channel = nameof(NotificationChannel.Email),
            IsEnabled = true,
            RecipientAddress = "first@example.com"
        };

        var created = await service.UpsertNotificationPreference(
            request,
            TestServerCallContext.Create());
        request.IsEnabled = false;
        request.RecipientAddress = "second@example.com";
        var updated = await service.UpsertNotificationPreference(
            request,
            TestServerCallContext.Create());

        Assert.Equal(created.Id, updated.Id);
        Assert.False(updated.IsEnabled);
        Assert.Equal("second@example.com", updated.RecipientAddress);
        var preference = await context.NotificationPreferences.SingleAsync();
        Assert.Equal(tenantId, preference.TenantId);
        Assert.Equal(userId, preference.RecipientUserId);
    }

    private static NotificationMessage CreateNotification(
        Guid tenantId,
        Guid userId) =>
        NotificationMessage.Create(
            tenantId,
            userId,
            Guid.CreateVersion7(),
            NotificationEventType.ShipmentCreated,
            NotificationChannel.InApp,
            "Shipment created",
            "Shipment was created.",
            null,
            Guid.CreateVersion7());

    private static NotificationDbContext CreateContext(CurrentUserService currentUser)
    {
        var options = new DbContextOptionsBuilder<NotificationDbContext>()
            .UseInMemoryDatabase($"notification-grpc-{Guid.CreateVersion7()}")
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
        currentUser.Populate(userId, tenantId, "test", 1, [], []);
        return currentUser;
    }
}
