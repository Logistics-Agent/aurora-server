using Shared.Entity;

namespace GpsTracking.Domain.Entities;

public sealed class OutboxMessage : TenantAuditableEntity
{
    private OutboxMessage() { }

    public Guid EventId { get; private set; }
    public string EventType { get; private set; } = string.Empty;
    public string Content { get; private set; } = string.Empty;
    public DateTimeOffset OccurredAt { get; private set; }
    public DateTimeOffset? ProcessedAt { get; private set; }
    public int RetryCount { get; private set; }
    public string? Error { get; private set; }

    public static OutboxMessage Create(
        Guid tenantId, Guid eventId, string eventType, string content, DateTimeOffset occurredAt)
    {
        GpsDomainValidation.RequiredId(tenantId, nameof(tenantId));
        GpsDomainValidation.RequiredId(eventId, nameof(eventId));
        if (occurredAt == default)
            throw new ArgumentException("OccurredAt is required.", nameof(occurredAt));
        return new OutboxMessage
        {
            TenantId = tenantId,
            EventId = eventId,
            EventType = GpsDomainValidation.RequiredText(eventType, nameof(eventType), 256),
            Content = GpsDomainValidation.RequiredText(content, nameof(content), 100_000),
            OccurredAt = occurredAt,
            CreatedAt = occurredAt
        };
    }

    public void MarkProcessed(DateTimeOffset processedAt)
    {
        ProcessedAt = processedAt;
        Error = null;
        UpdatedAt = processedAt;
    }

    public void RecordFailure(string error, DateTimeOffset failedAt)
    {
        RetryCount++;
        Error = GpsDomainValidation.RequiredText(error, nameof(error), 2000);
        UpdatedAt = failedAt;
    }
}
