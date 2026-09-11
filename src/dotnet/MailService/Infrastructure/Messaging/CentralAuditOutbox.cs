using System.Text.Json;
using MailService.Domain.Entities;
using MailService.Infrastructure.Persistence;

namespace MailService.Infrastructure.Messaging;

public sealed record CentralAuditEvent(
    Guid EventId, string ServiceName, string EventType, Guid TenantId, Guid UserId,
    string UserRole, Guid ResourceId, string PayloadJson, string? IpAddress);

public static class CentralAuditOutbox
{
    public static void Enqueue(MailServiceDbContext dbContext, AuditRecord audit)
    {
        var payload = new CentralAuditEvent(audit.Id, "MailService", audit.Action, audit.TenantId,
            audit.ActorId, audit.ActorType.ToString(), audit.ResourceId, audit.DetailJson ?? "{}", audit.ClientIp);
        dbContext.OutboxMessages.Add(new OutboxMessage
        {
            EventType = nameof(CentralAuditEvent),
            Payload = JsonSerializer.Serialize(payload),
            CreatedAt = audit.Timestamp
        });
    }
}
