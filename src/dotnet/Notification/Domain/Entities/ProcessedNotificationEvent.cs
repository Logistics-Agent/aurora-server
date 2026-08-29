namespace Notification.Domain.Entities;

public sealed class ProcessedNotificationEvent
{
    private ProcessedNotificationEvent() { }
    public Guid Id { get; private set; } = Guid.CreateVersion7();
    public Guid EventId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid UserId { get; private set; }
    public string Rule { get; private set; } = string.Empty;
    public DateTimeOffset ProcessedAt { get; private set; }

    public static ProcessedNotificationEvent Create(Guid eventId, Guid tenantId, Guid userId, string rule) => new()
    { EventId = eventId, TenantId = tenantId, UserId = userId, Rule = rule, ProcessedAt = DateTimeOffset.UtcNow };
}
