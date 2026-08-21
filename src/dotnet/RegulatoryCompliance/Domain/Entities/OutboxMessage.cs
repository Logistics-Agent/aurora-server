using Shared.Entity;

namespace RegulatoryCompliance.Domain.Entities;

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
        ComplianceValidation.RequiredId(tenantId, nameof(tenantId));
        ComplianceValidation.RequiredId(eventId, nameof(eventId));
        ComplianceValidation.RequiredTimestamp(occurredAt, nameof(occurredAt));

        return new OutboxMessage
        {
            TenantId = tenantId,
            EventId = eventId,
            EventType = ComplianceValidation.RequiredText(eventType, nameof(eventType), 256),
            Content = ComplianceValidation.Json(content, nameof(content), 500_000),
            OccurredAt = occurredAt,
            CreatedAt = occurredAt
        };
    }

    public void MarkProcessed(DateTimeOffset processedAt)
    {
        ComplianceValidation.RequiredTimestamp(processedAt, nameof(processedAt));
        ProcessedAt = processedAt;
        Error = null;
        UpdatedAt = processedAt;
    }

    public void RecordFailure(string error, DateTimeOffset failedAt)
    {
        ComplianceValidation.RequiredTimestamp(failedAt, nameof(failedAt));
        RetryCount++;
        Error = ComplianceValidation.RequiredText(error, nameof(error), 2_000);
        UpdatedAt = failedAt;
    }
}
