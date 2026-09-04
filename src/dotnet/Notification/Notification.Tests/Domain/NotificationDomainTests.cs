using Notification.Domain.Entities;
using Notification.Domain.Enums;
using NotificationEntity = Notification.Domain.Entities.Notification;
using Xunit;

namespace Notification.Tests.Domain;

public sealed class NotificationDomainTests
{
    [Fact]
    public void Create_rejects_external_action_url()
    {
        Assert.Throws<ArgumentException>(() => NotificationEntity.Create(Guid.NewGuid(), Guid.NewGuid(), "SHIPMENT_DELIVERED", "Title", "Body", null, null, "https://evil.example", NotificationPriority.Info));
    }

    [Fact]
    public void Create_rejects_titles_over_200_characters()
    {
        Assert.Throws<ArgumentException>(() => NotificationEntity.Create(
            Guid.NewGuid(), Guid.NewGuid(), "SHIPMENT_DELIVERED", new string('t', 201), "Body",
            null, null, "/shipments", NotificationPriority.Info));
    }

    [Fact]
    public void Create_rejects_bodies_over_2000_characters()
    {
        Assert.Throws<ArgumentException>(() => NotificationEntity.Create(
            Guid.NewGuid(), Guid.NewGuid(), "SHIPMENT_DELIVERED", "Title", new string('b', 2001),
            null, null, "/shipments", NotificationPriority.Info));
    }

    [Fact]
    public void Create_rejects_types_over_64_characters()
    {
        Assert.Throws<ArgumentException>(() => NotificationEntity.Create(
            Guid.NewGuid(), Guid.NewGuid(), new string('t', 65), "Title", "Body",
            null, null, "/notifications", NotificationPriority.Info));
    }

    [Fact]
    public void Create_accepts_type_at_64_characters()
    {
        var notification = NotificationEntity.Create(
            Guid.NewGuid(), Guid.NewGuid(), new string('t', 64), "Title", "Body",
            null, null, "/notifications", NotificationPriority.Info);

        Assert.Equal(64, notification.Type.Length);
    }

    [Fact]
    public void Create_rejects_action_urls_over_512_characters()
    {
        Assert.Throws<ArgumentException>(() => NotificationEntity.Create(
            Guid.NewGuid(), Guid.NewGuid(), "SHIPMENT_DELIVERED", "Title", "Body",
            null, null, "/" + new string('a', 512), NotificationPriority.Info));
    }

    [Fact]
    public void Create_accepts_internal_action_url_at_512_characters()
    {
        var notification = NotificationEntity.Create(
            Guid.NewGuid(), Guid.NewGuid(), "SHIPMENT_DELIVERED", "Title", "Body",
            null, null, "/" + new string('a', 511), NotificationPriority.Info);

        Assert.Equal(512, notification.ActionUrl!.Length);
    }

    [Fact]
    public void Device_registration_rejects_tokens_containing_whitespace()
    {
        Assert.Throws<ArgumentException>(() => NotificationDevice.Register(
            Guid.NewGuid(), Guid.NewGuid(), "token with whitespace", DevicePlatform.Web));
    }

    [Theory]
    [InlineData(" token")]
    [InlineData("token ")]
    public void Device_registration_rejects_leading_or_trailing_whitespace(string token)
    {
        Assert.Throws<ArgumentException>(() => NotificationDevice.Register(
            Guid.NewGuid(), Guid.NewGuid(), token, DevicePlatform.Web));
    }

    [Fact]
    public void Device_registration_is_active_and_tenant_scoped()
    {
        var tenantId = Guid.NewGuid(); var userId = Guid.NewGuid();
        var device = NotificationDevice.Register(tenantId, userId, "token-1", DevicePlatform.Web);
        Assert.Equal(tenantId, device.TenantId); Assert.Equal(userId, device.UserId); Assert.True(device.IsActive);
    }

    [Fact]
    public void Processed_event_records_resolved_audience_outcome()
    {
        var eventId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var before = DateTimeOffset.UtcNow;

        var receipt = ProcessedNotificationEvent.Create(
            eventId, tenantId, "shipment-status", ProcessedNotificationEventOutcome.AudienceResolved, 2);

        Assert.Equal(eventId, receipt.EventId);
        Assert.Equal(tenantId, receipt.TenantId);
        Assert.Equal("shipment-status", receipt.Rule);
        Assert.Equal(ProcessedNotificationEventOutcome.AudienceResolved, receipt.Outcome);
        Assert.Equal(2, receipt.RecipientCount);
        Assert.InRange(receipt.ProcessedAt, before, DateTimeOffset.UtcNow);
    }

    [Fact]
    public void Processed_event_records_no_recipient_outcome()
    {
        var receipt = ProcessedNotificationEvent.Create(
            Guid.NewGuid(), Guid.NewGuid(), "shipment-status", ProcessedNotificationEventOutcome.NoRecipient, 0);

        Assert.Equal(ProcessedNotificationEventOutcome.NoRecipient, receipt.Outcome);
        Assert.Equal(0, receipt.RecipientCount);
    }

    [Fact]
    public void Processed_event_rejects_empty_event_id()
    {
        Assert.Throws<ArgumentException>(() => ProcessedNotificationEvent.Create(
            Guid.Empty, Guid.NewGuid(), "shipment-status", ProcessedNotificationEventOutcome.NoRecipient, 0));
    }

    [Fact]
    public void Processed_event_rejects_empty_tenant_id()
    {
        Assert.Throws<ArgumentException>(() => ProcessedNotificationEvent.Create(
            Guid.NewGuid(), Guid.Empty, "shipment-status", ProcessedNotificationEventOutcome.NoRecipient, 0));
    }

    [Fact]
    public void Processed_event_rejects_blank_rule()
    {
        Assert.Throws<ArgumentException>(() => ProcessedNotificationEvent.Create(
            Guid.NewGuid(), Guid.NewGuid(), " ", ProcessedNotificationEventOutcome.NoRecipient, 0));
    }

    [Fact]
    public void Processed_event_rejects_rules_over_100_characters()
    {
        Assert.Throws<ArgumentException>(() => ProcessedNotificationEvent.Create(
            Guid.NewGuid(), Guid.NewGuid(), new string('r', 101),
            ProcessedNotificationEventOutcome.NoRecipient, 0));
    }

    [Fact]
    public void Processed_event_rejects_resolved_audience_without_recipients()
    {
        Assert.Throws<ArgumentException>(() => ProcessedNotificationEvent.Create(
            Guid.NewGuid(), Guid.NewGuid(), "shipment-status",
            ProcessedNotificationEventOutcome.AudienceResolved, 0));
    }

    [Fact]
    public void Processed_event_rejects_no_recipient_with_nonzero_count()
    {
        Assert.Throws<ArgumentException>(() => ProcessedNotificationEvent.Create(
            Guid.NewGuid(), Guid.NewGuid(), "shipment-status",
            ProcessedNotificationEventOutcome.NoRecipient, 1));
    }

    [Fact]
    public void Delivery_attempt_sent_is_terminal()
    {
        var attempt = NotificationDeliveryAttempt.Create(Guid.NewGuid(), Guid.NewGuid());

        attempt.Sent("provider-message");

        Assert.Equal(DeliveryAttemptStatus.Sent, attempt.Status);
        Assert.True(attempt.IsTerminal);
        Assert.Null(attempt.NextAttemptAt);
    }

    [Fact]
    public void Delivery_attempt_schedules_future_retry()
    {
        var attempt = NotificationDeliveryAttempt.Create(Guid.NewGuid(), Guid.NewGuid());
        var nextAttemptAt = DateTimeOffset.UtcNow.AddMinutes(5);

        attempt.Retry("transient", nextAttemptAt);

        Assert.Equal(DeliveryAttemptStatus.Retrying, attempt.Status);
        Assert.Equal(nextAttemptAt, attempt.NextAttemptAt);
        Assert.False(attempt.IsTerminal);
    }

    [Fact]
    public void Delivery_attempt_invalid_token_failure_is_terminal()
    {
        var attempt = NotificationDeliveryAttempt.Create(Guid.NewGuid(), Guid.NewGuid());

        attempt.Failed("invalid_token", invalidToken: true);

        Assert.Equal(DeliveryAttemptStatus.InvalidToken, attempt.Status);
        Assert.True(attempt.IsTerminal);
        Assert.Null(attempt.NextAttemptAt);
    }

    [Fact]
    public void Delivery_attempt_permanent_failure_is_terminal()
    {
        var attempt = NotificationDeliveryAttempt.Create(Guid.NewGuid(), Guid.NewGuid());

        attempt.Failed("provider_failure", invalidToken: false);

        Assert.Equal(DeliveryAttemptStatus.Failed, attempt.Status);
        Assert.True(attempt.IsTerminal);
        Assert.Null(attempt.NextAttemptAt);
    }

    [Fact]
    public void Delivery_attempt_bounds_provider_error()
    {
        var attempt = NotificationDeliveryAttempt.Create(Guid.NewGuid(), Guid.NewGuid());

        attempt.Failed(new string('e', 200), invalidToken: false);

        Assert.Equal(128, attempt.ErrorCode!.Length);
    }

    [Fact]
    public void Delivery_attempt_becomes_retry_eligible_at_next_attempt_time()
    {
        var attempt = NotificationDeliveryAttempt.Create(Guid.NewGuid(), Guid.NewGuid());
        var nextAttemptAt = DateTimeOffset.UtcNow.AddMinutes(5);
        attempt.Retry("transient", nextAttemptAt);

        Assert.False(attempt.CanRetry(nextAttemptAt.AddTicks(-1)));
        Assert.True(attempt.CanRetry(nextAttemptAt));
    }
}
