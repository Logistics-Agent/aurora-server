using Microsoft.EntityFrameworkCore;
using Notification.Application.Delivery;
using Notification.Domain.Entities;
using Notification.Domain.Enums;
using Notification.Infrastructure.Persistences;
using Shared.Interceptors;
using Shared.Security;

namespace Notification.Tests.Application;

public sealed class NotificationDeliveryServiceTests
{
    [Fact]
    public async Task SuccessfulEmailDeliveryRecordsAttemptAndMarksSent()
    {
        await using var context = CreateContext();
        var notification = CreateEmailNotification();
        context.Notifications.Add(notification);
        await context.SaveChangesAsync();
        var provider = new FakeEmailProvider(
            NotificationDeliveryResult.Success("email-123"));
        var service = CreateService(context, provider);

        var result = await service.DeliverAsync(notification.Id);

        Assert.True(result.Delivered);
        Assert.Equal(NotificationStatus.Sent, notification.Status);
        var attempt = Assert.Single(notification.DeliveryAttempts);
        Assert.Equal(DeliveryAttemptStatus.Succeeded, attempt.Status);
        Assert.Equal("email-123", attempt.ProviderMessageId);
        Assert.Equal(1, provider.CallCount);
    }

    [Fact]
    public async Task TransientFailureRecordsErrorAndMarksFailed()
    {
        await using var context = CreateContext();
        var notification = CreateEmailNotification();
        context.Notifications.Add(notification);
        await context.SaveChangesAsync();
        var provider = new FakeEmailProvider(
            NotificationDeliveryResult.Failure("Provider unavailable", true));
        var service = CreateService(context, provider);

        var result = await service.DeliverAsync(notification.Id);

        Assert.False(result.Delivered);
        Assert.True(result.IsTransientFailure);
        Assert.Equal(NotificationStatus.Failed, notification.Status);
        var attempt = Assert.Single(notification.DeliveryAttempts);
        Assert.Equal(DeliveryAttemptStatus.TransientFailure, attempt.Status);
        Assert.Equal("Provider unavailable", attempt.Error);
    }

    [Fact]
    public async Task SentNotificationIsNotDeliveredTwice()
    {
        await using var context = CreateContext();
        var notification = CreateEmailNotification();
        context.Notifications.Add(notification);
        await context.SaveChangesAsync();
        var provider = new FakeEmailProvider(
            NotificationDeliveryResult.Success("email-123"));
        var service = CreateService(context, provider);

        await service.DeliverAsync(notification.Id);
        var secondResult = await service.DeliverAsync(notification.Id);

        Assert.True(secondResult.AlreadyDelivered);
        Assert.Single(notification.DeliveryAttempts);
        Assert.Equal(1, provider.CallCount);
    }

    [Fact]
    public async Task InAppProviderReturnsDeterministicMessageId()
    {
        var notificationId = Guid.CreateVersion7();
        var provider = new InAppNotificationProvider();

        var result = await provider.DeliverAsync(new NotificationDeliveryRequest(
            notificationId,
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            NotificationChannel.InApp,
            "Title",
            "Body",
            null));

        Assert.True(result.IsSuccess);
        Assert.Equal($"in-app:{notificationId}", result.ProviderMessageId);
    }

    private static NotificationDeliveryService CreateService(
        NotificationDbContext context,
        params INotificationDeliveryProvider[] providers) =>
        new(context, providers, new NotificationRetryPolicy(new NotificationRetryOptions()), TimeProvider.System);

    private static NotificationMessage CreateEmailNotification() =>
        NotificationMessage.Create(
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            NotificationEventType.ShipmentCreated,
            NotificationChannel.Email,
            "Shipment created",
            "Shipment SHP-1001 was created.",
            "recipient@example.com",
            Guid.CreateVersion7());

    private static NotificationDbContext CreateContext()
    {
        var currentUser = new CurrentUserService();
        var options = new DbContextOptionsBuilder<NotificationDbContext>()
            .UseInMemoryDatabase($"notification-delivery-{Guid.CreateVersion7()}")
            .Options;

        return new NotificationDbContext(
            options,
            currentUser,
            new AuditSaveChangesInterceptor(currentUser));
    }

    private sealed class FakeEmailProvider(NotificationDeliveryResult result)
        : IEmailNotificationProvider
    {
        public NotificationChannel Channel => NotificationChannel.Email;
        public int CallCount { get; private set; }

        public Task<NotificationDeliveryResult> DeliverAsync(
            NotificationDeliveryRequest request,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(result);
        }
    }
}
