using DocumentOcr.Application.Storage;
using DocumentOcr.Application.Uploads;
using DocumentOcr.Domain.Enums;
using DocumentOcr.Infrastructure.Persistences;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DocumentOcr.Infrastructure.BackgroundJobs;

public sealed class ExpiredUploadCleanupService(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    DocumentUploadOptions options,
    ILogger<ExpiredUploadCleanupService> logger) : BackgroundService
{
    public static async Task<int> CleanupExpiredAsync(
        DocumentOcrDbContext dbContext,
        IDocumentInputStorage inputStorage,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        var sessions = await dbContext.UploadSessions
            .IgnoreQueryFilters()
            .Where(session => session.Status != DocumentUploadStatus.Consumed &&
                              session.Status != DocumentUploadStatus.Expired &&
                              session.ExpiresAt <= now)
            .ToListAsync(cancellationToken);

        foreach (var session in sessions)
        {
            await inputStorage.DeleteAsync(session.TenantId, session.ObjectKey, cancellationToken);
            session.MarkExpired(now);
        }

        if (sessions.Count > 0)
            await dbContext.SaveChangesAsync(cancellationToken);
        return sessions.Count;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<DocumentOcrDbContext>();
                var inputStorage = scope.ServiceProvider.GetRequiredService<IDocumentInputStorage>();
                var deleted = await CleanupExpiredAsync(
                    dbContext, inputStorage, timeProvider.GetUtcNow(), stoppingToken);
                if (deleted > 0)
                    logger.LogInformation("Expired document upload sessions cleaned up: {Count}", deleted);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Expired document upload cleanup failed.");
            }

            await Task.Delay(options.CleanupInterval, stoppingToken);
        }
    }
}
