using Microsoft.EntityFrameworkCore;
using Notification.Application.Delivery;
using Notification.Domain.Entities;
using Notification.Domain.Enums;
using Notification.Infrastructure.Persistences;
using Shared.Interceptors;
using Shared.Security;

namespace Notification.Tests.Application;

public sealed class NotificationRetryTests
{
    [Fact]
    public async Task TransientFailureIsDeferredUntilRetryIsDue()
    {
        await using var context = CreateContext();
        var notification = CreateNotification();
        context.Notifications.Add(notification);
        await context.SaveChangesAsync();
        var provider = new SequenceEmailProvider(
            NotificationDeliveryResult.Failure("Temporary failure", true),
            NotificationDeliveryResult.Success("email-2"));
        var clock = new ManualTimeProvider(new DateTimeOffset(2026, 7, 19, 12, 0, 0, TimeSpan.Zero));
        var service = CreateService(context, provider, clock, maxAttempts: 3, delay: TimeSpan.FromSeconds(10));

        var first = await service.DeliverAsync(notification.Id);
        var deferred = await service.DeliverAsync(notification.Id);

        Assert.True(first.IsTransientFailure);
        Assert.Equal(clock.GetUtcNow().AddSeconds(10), first.NextAttemptAt);
        Assert.True(deferred.Deferred);
        Assert.Equal(1, provider.CallCount);
        Assert.Single(notification.DeliveryAttempts);

        clock.Advance(TimeSpan.FromSeconds(10));
        var retried = await service.DeliverAsync(notification.Id);

        Assert.True(retried.Delivered);
        Assert.Equal(NotificationStatus.Sent, notification.Status);
        Assert.Equal(2, provider.CallCount);
        Assert.Equal(2, notification.DeliveryAttempts.Count);
    }

    [Fact]
    public async Task TransientFailureStopsAtConfiguredAttemptLimit()
    {
        await using var context = CreateContext();
        var notification = CreateNotification();
        context.Notifications.Add(notification);
        await context.SaveChangesAsync();
        var provider = new SequenceEmailProvider(
            NotificationDeliveryResult.Failure("Temporary failure", true),
            NotificationDeliveryResult.Failure("Temporary failure", true),
            NotificationDeliveryResult.Failure("Temporary failure", true));
        var clock = new ManualTimeProvider(new DateTimeOffset(2026, 7, 19, 12, 0, 0, TimeSpan.Zero));
        var service = CreateService(context, provider, clock, maxAttempts: 3, delay: TimeSpan.Zero);

        await service.DeliverAsync(notification.Id);
        await service.DeliverAsync(notification.Id);
        var finalFailure = await service.DeliverAsync(notification.Id);
        var afterLimit = await service.DeliverAsync(notification.Id);

        Assert.True(finalFailure.RetryExhausted);
        Assert.Null(finalFailure.NextAttemptAt);
        Assert.True(afterLimit.RetryExhausted);
        Assert.Equal(3, provider.CallCount);
        Assert.Equal(3, notification.DeliveryAttempts.Count);
    }

    [Fact]
    public void RetryPolicyUsesBoundedExponentialBackoff()
    {
        var failedAt = new DateTimeOffset(2026, 7, 19, 12, 0, 0, TimeSpan.Zero);
        var policy = new NotificationRetryPolicy(new NotificationRetryOptions
        {
            MaxAttempts = 5,
            InitialDelay = TimeSpan.FromSeconds(10),
            MaximumDelay = TimeSpan.FromSeconds(25)
        });

        var first = policy.Decide(1, true, failedAt);
        var second = policy.Decide(2, true, failedAt);
        var third = policy.Decide(3, true, failedAt);
        var permanent = policy.Decide(1, false, failedAt);

        Assert.Equal(failedAt.AddSeconds(10), first.NextAttemptAt);
        Assert.Equal(failedAt.AddSeconds(20), second.NextAttemptAt);
        Assert.Equal(failedAt.AddSeconds(25), third.NextAttemptAt);
        Assert.False(permanent.ShouldRetry);
        Assert.Null(permanent.NextAttemptAt);
    }

    private static NotificationDeliveryService CreateService(
        NotificationDbContext context,
        INotificationDeliveryProvider provider,
        TimeProvider clock,
        int maxAttempts,
        TimeSpan delay)
    {
        var policy = new NotificationRetryPolicy(new NotificationRetryOptions
        {
            MaxAttempts = maxAttempts,
            InitialDelay = delay,
            MaximumDelay = delay
        });

        return new NotificationDeliveryService(context, [provider], policy, clock);
    }

    private static NotificationMessage CreateNotification() =>
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
            .UseInMemoryDatabase($"notification-retry-{Guid.CreateVersion7()}")
            .Options;

        return new NotificationDbContext(
            options,
            currentUser,
            new AuditSaveChangesInterceptor(currentUser));
    }

    private sealed class SequenceEmailProvider(
        params NotificationDeliveryResult[] results) : IEmailNotificationProvider
    {
        private readonly Queue<NotificationDeliveryResult> _results = new(results);

        public NotificationChannel Channel => NotificationChannel.Email;
        public int CallCount { get; private set; }

        public Task<NotificationDeliveryResult> DeliverAsync(
            NotificationDeliveryRequest request,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(_results.Dequeue());
        }
    }

    private sealed class ManualTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        private DateTimeOffset _utcNow = utcNow;

        public override DateTimeOffset GetUtcNow() => _utcNow;

        public void Advance(TimeSpan duration) => _utcNow = _utcNow.Add(duration);
    }
}
