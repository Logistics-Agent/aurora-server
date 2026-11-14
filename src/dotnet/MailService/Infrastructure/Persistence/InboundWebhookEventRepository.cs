using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MailService.Domain.Entities;

namespace MailService.Infrastructure.Persistence;

public class InboundWebhookEventRepository
{
    private readonly MailServiceDbContext _db;
    private readonly ILogger<InboundWebhookEventRepository> _logger;

    public InboundWebhookEventRepository(MailServiceDbContext db, ILogger<InboundWebhookEventRepository> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<(InboundWebhookEvent Event, bool ShouldProcess)> AcquireEventLockAsync(
        string stalwartEventId, 
        string eventType, 
        string? rawPayloadJson, 
        CancellationToken ct = default)
    {
        var existing = await _db.InboundWebhookEvents
            .FirstOrDefaultAsync(e => e.StalwartEventId == stalwartEventId, ct);

        if (existing != null)
        {
            if (existing.Status == WebhookEventStatus.Completed)
            {
                return (existing, false);
            }

            existing.Status = WebhookEventStatus.Processing;
            existing.AttemptCount++;
            existing.LastAttemptAt = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync(ct);
            return (existing, true);
        }

        var newEvent = new InboundWebhookEvent
        {
            StalwartEventId = stalwartEventId,
            EventType = eventType,
            Status = WebhookEventStatus.Processing,
            AttemptCount = 1,
            RawPayloadJson = rawPayloadJson,
            FirstReceivedAt = DateTimeOffset.UtcNow,
            LastAttemptAt = DateTimeOffset.UtcNow
        };

        try
        {
            _db.InboundWebhookEvents.Add(newEvent);
            await _db.SaveChangesAsync(ct);
            return (newEvent, true);
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            _logger.LogInformation("Concurrent insert detected for event {StalwartEventId}. Reloading existing record.", stalwartEventId);
            
            var reloaded = await _db.InboundWebhookEvents
                .AsNoTracking()
                .FirstAsync(e => e.StalwartEventId == stalwartEventId, ct);

            return (reloaded, reloaded.Status != WebhookEventStatus.Completed);
        }
    }

    public async Task MarkCompletedAsync(string stalwartEventId, CancellationToken ct = default)
    {
        var existing = await _db.InboundWebhookEvents
            .FirstOrDefaultAsync(e => e.StalwartEventId == stalwartEventId, ct);

        if (existing != null)
        {
            existing.Status = WebhookEventStatus.Completed;
            existing.CompletedAt = DateTimeOffset.UtcNow;
            existing.LastError = null;
            await _db.SaveChangesAsync(ct);
        }
    }

    public async Task MarkFailedAsync(string stalwartEventId, string error, CancellationToken ct = default)
    {
        var existing = await _db.InboundWebhookEvents
            .FirstOrDefaultAsync(e => e.StalwartEventId == stalwartEventId, ct);

        if (existing != null)
        {
            existing.Status = WebhookEventStatus.Failed;
            existing.LastError = error;
            await _db.SaveChangesAsync(ct);
        }
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException ex)
    {
        return ex.InnerException is Npgsql.PostgresException pgEx && pgEx.SqlState == "23505";
    }
}
