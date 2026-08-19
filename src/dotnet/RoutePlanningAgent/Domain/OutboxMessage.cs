using Shared.Entity;

namespace RoutePlanningAgent.Domain;

/// <summary>
/// Transactional Outbox: event được ghi cùng transaction với dữ liệu nghiệp vụ,
/// sau đó OutboxProcessorBackgroundService relay lên RabbitMQ.
/// </summary>
public class OutboxMessage : BaseEntity
{
    public string EventType { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ProcessedAt { get; set; }
    public int RetryCount { get; set; }
    public string? Error { get; set; }
}
