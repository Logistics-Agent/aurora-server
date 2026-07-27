using System.Text.Json;
using RoutePlanningAgent.Application.Interfaces;
using RoutePlanningAgent.Domain;
using RoutePlanningAgent.Infrastructure.Persistences;

namespace RoutePlanningAgent.Infrastructure.Services;

public class OutboxWriter(RoutePlanningDbContext context) : IOutboxWriter
{
    public void Enqueue<TEvent>(TEvent evt) where TEvent : class
    {
        context.OutboxMessages.Add(new OutboxMessage
        {
            EventType = typeof(TEvent).Name,
            Payload = JsonSerializer.Serialize(evt),
            CreatedAt = DateTimeOffset.UtcNow
        });
        // Không SaveChanges ở đây — handler giữ transaction
    }
}
