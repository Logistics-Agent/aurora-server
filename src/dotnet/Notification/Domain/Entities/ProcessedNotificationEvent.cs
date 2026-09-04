using Notification.Domain.Enums;

namespace Notification.Domain.Entities;

public sealed class ProcessedNotificationEvent
{
    private ProcessedNotificationEvent() { }
    public Guid Id { get; private set; } = Guid.CreateVersion7();
    public Guid EventId { get; private set; }
    public Guid TenantId { get; private set; }
    public string Rule { get; private set; } = string.Empty;
    public ProcessedNotificationEventOutcome Outcome { get; private set; }
    public int RecipientCount { get; private set; }
    public DateTimeOffset ProcessedAt { get; private set; }

    public static ProcessedNotificationEvent Create(
        Guid eventId,
        Guid tenantId,
        string rule,
        ProcessedNotificationEventOutcome outcome,
        int recipientCount)
    {
        if (eventId == Guid.Empty || tenantId == Guid.Empty)
            throw new ArgumentException("Event and tenant are required.");

        var normalizedRule = rule?.Trim();
        if (string.IsNullOrEmpty(normalizedRule) || normalizedRule.Length > 100)
            throw new ArgumentException("Rule must contain between 1 and 100 characters.", nameof(rule));

        if (outcome == ProcessedNotificationEventOutcome.AudienceResolved && recipientCount <= 0)
            throw new ArgumentException("A resolved audience requires at least one recipient.", nameof(recipientCount));
        if (outcome == ProcessedNotificationEventOutcome.NoRecipient && recipientCount != 0)
            throw new ArgumentException("A no-recipient outcome requires zero recipients.", nameof(recipientCount));
        if (!Enum.IsDefined(outcome))
            throw new ArgumentOutOfRangeException(nameof(outcome));

        return new()
        {
            EventId = eventId,
            TenantId = tenantId,
            Rule = normalizedRule,
            Outcome = outcome,
            RecipientCount = recipientCount,
            ProcessedAt = DateTimeOffset.UtcNow
        };
    }

}
