using Shared.Entity;

namespace DocumentOcr.Domain.Entities;

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
        Guid tenantId,
        Guid eventId,
        string eventType,
        string content,
        DateTimeOffset occurredAt)
    {
        DocumentOcrValidation.RequiredId(tenantId, nameof(tenantId));
        DocumentOcrValidation.RequiredId(eventId, nameof(eventId));
        if (occurredAt == default)
            throw new ArgumentException("OccurredAt is required.", nameof(occurredAt));

        return new OutboxMessage
        {
            TenantId = tenantId,
            EventId = eventId,
            EventType = DocumentOcrValidation.RequiredText(eventType, nameof(eventType), 256),
            Content = DocumentOcrValidation.Json(content, nameof(content), 100_000),
            OccurredAt = occurredAt,
            CreatedAt = occurredAt
        };
    }

    public void MarkProcessed(DateTimeOffset processedAt)
    {
        if (processedAt == default)
            throw new ArgumentException("ProcessedAt is required.", nameof(processedAt));
        ProcessedAt = processedAt;
        Error = null;
        UpdatedAt = processedAt;
    }

    public void RecordFailure(string error, DateTimeOffset failedAt)
    {
        if (failedAt == default)
            throw new ArgumentException("FailedAt is required.", nameof(failedAt));
        RetryCount++;
        Error = DocumentOcrValidation.RequiredText(error, nameof(error), 2_000);
        UpdatedAt = failedAt;
    }
}
