using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MailService.Application.Interfaces.Messaging;
using MailService.Domain.Entities;
using MailService.Infrastructure.Persistence;

namespace MailService.Infrastructure.Messaging;

public class OutboxWriter(MailServiceDbContext dbContext) : IOutboxWriter
{
    public async Task WriteAsync<T>(T @event, CancellationToken cancellationToken = default) where T : class
    {
        var outboxMessage = new OutboxMessage
        {
            EventType = typeof(T).Name,
            Payload = JsonSerializer.Serialize(@event),
            CreatedAt = DateTimeOffset.UtcNow,
            RetryCount = 0
        };

        dbContext.OutboxMessages.Add(outboxMessage);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
