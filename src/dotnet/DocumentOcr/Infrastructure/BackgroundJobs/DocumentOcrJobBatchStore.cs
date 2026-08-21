using DocumentOcr.Application.Jobs;
using DocumentOcr.Application.Providers;
using DocumentOcr.Domain.Entities;
using DocumentOcr.Domain.Enums;
using DocumentOcr.Infrastructure.Persistences;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace DocumentOcr.Infrastructure.BackgroundJobs;

public sealed record ClaimedDocumentOcrJob(
    Guid TenantId,
    Guid JobId,
    Guid AttemptId,
    string ProviderName);

public interface IDocumentOcrJobBatchStore
{
    Task<IReadOnlyList<ClaimedDocumentOcrJob>> ClaimPendingAsync(
        CancellationToken cancellationToken = default);
    Task<bool> RenewLeaseAsync(
        Guid tenantId,
        Guid jobId,
        CancellationToken cancellationToken = default);
    Task<int> RecoverExpiredAsync(CancellationToken cancellationToken = default);
}

public sealed class DocumentOcrJobBatchStore(
    DocumentOcrDbContext dbContext,
    IOcrProvider provider,
    TimeProvider timeProvider,
    DocumentOcrWorkerOptions options) : IDocumentOcrJobBatchStore
{
    public async Task<IReadOnlyList<ClaimedDocumentOcrJob>> ClaimPendingAsync(
        CancellationToken cancellationToken = default)
    {
        options.Validate();
        var now = timeProvider.GetUtcNow();
        await using var transaction = await BeginTransactionAsync(cancellationToken);

        try
        {
            var jobs = dbContext.Database.IsRelational()
                ? await dbContext.Jobs
                    .FromSqlInterpolated($"""
                        SELECT *
                        FROM document_ocr_jobs
                        WHERE "Status" = 'Queued'
                          AND ("NextAttemptAt" IS NULL OR "NextAttemptAt" <= {now})
                        ORDER BY "CreatedAt", "Id"
                        LIMIT {options.BatchSize}
                        FOR UPDATE SKIP LOCKED
                        """)
                    .IgnoreQueryFilters()
                    .ToListAsync(cancellationToken)
                : await dbContext.Jobs
                    .IgnoreQueryFilters()
                    .Where(job => job.Status == DocumentOcrJobStatus.Queued &&
                                  (job.NextAttemptAt == null || job.NextAttemptAt <= now))
                    .OrderBy(job => job.CreatedAt)
                    .ThenBy(job => job.Id)
                    .Take(options.BatchSize)
                    .ToListAsync(cancellationToken);

            var claimed = new List<ClaimedDocumentOcrJob>(jobs.Count);
            foreach (var job in jobs)
            {
                var attempt = job.Start(provider.Name, now, now.Add(options.LeaseDuration));
                dbContext.ProviderAttempts.Add(attempt);
                claimed.Add(new ClaimedDocumentOcrJob(job.TenantId, job.Id, attempt.Id, provider.Name));
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            if (transaction is not null)
                await transaction.CommitAsync(cancellationToken);
            return claimed;
        }
        catch (DbUpdateConcurrencyException)
        {
            if (transaction is not null)
                await transaction.RollbackAsync(cancellationToken);
            dbContext.ChangeTracker.Clear();
            return [];
        }
    }

    public async Task<bool> RenewLeaseAsync(
        Guid tenantId,
        Guid jobId,
        CancellationToken cancellationToken = default)
    {
        var job = await dbContext.Jobs
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(
                item => item.TenantId == tenantId && item.Id == jobId,
                cancellationToken);
        if (job is null || job.Status != DocumentOcrJobStatus.Processing)
            return false;

        var now = timeProvider.GetUtcNow();
        job.RenewLease(now, now.Add(options.LeaseDuration));
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            dbContext.ChangeTracker.Clear();
            return false;
        }
    }

    public async Task<int> RecoverExpiredAsync(CancellationToken cancellationToken = default)
    {
        options.Validate();
        var now = timeProvider.GetUtcNow();
        await using var transaction = await BeginTransactionAsync(cancellationToken);

        var jobs = dbContext.Database.IsRelational()
            ? await dbContext.Jobs
                .FromSqlInterpolated($"""
                    SELECT *
                    FROM document_ocr_jobs
                    WHERE "Status" = 'Processing'
                      AND "LeaseExpiresAt" <= {now}
                    ORDER BY "LeaseExpiresAt", "Id"
                    LIMIT {options.BatchSize}
                    FOR UPDATE SKIP LOCKED
                    """)
                .IgnoreQueryFilters()
                .ToListAsync(cancellationToken)
            : await dbContext.Jobs
                .IgnoreQueryFilters()
                .Include(job => job.Attempts)
                .Where(job => job.Status == DocumentOcrJobStatus.Processing &&
                              job.LeaseExpiresAt <= now)
                .OrderBy(job => job.LeaseExpiresAt)
                .ThenBy(job => job.Id)
                .Take(options.BatchSize)
                .ToListAsync(cancellationToken);

        if (dbContext.Database.IsRelational() && jobs.Count > 0)
        {
            var jobIds = jobs.Select(job => job.Id).ToArray();
            await dbContext.ProviderAttempts
                .IgnoreQueryFilters()
                .Where(attempt => jobIds.Contains(attempt.JobId) &&
                                  attempt.Outcome == OcrAttemptOutcome.Processing)
                .LoadAsync(cancellationToken);
        }

        foreach (var job in jobs)
        {
            var attempt = job.Attempts.Single(item => item.Outcome == OcrAttemptOutcome.Processing);
            job.RecordFailure(
                attempt.Id,
                OcrAttemptOutcome.TransientFailure,
                "processing_lease_expired",
                "The OCR processing lease expired before completion.",
                now);
            if (job.AttemptCount < options.MaxAttempts)
            {
                var delay = DocumentOcrRetryPolicy.GetDelay(job.Id, job.AttemptCount, options);
                job.ScheduleRetry(now.Add(delay), now);
            }
            else
            {
                dbContext.OutboxMessages.Add(DocumentOcrOutboxFactory.CreateFailed(job, now));
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        if (transaction is not null)
            await transaction.CommitAsync(cancellationToken);
        return jobs.Count;
    }

    private async Task<IDbContextTransaction?> BeginTransactionAsync(CancellationToken cancellationToken) =>
        dbContext.Database.IsRelational()
            ? await dbContext.Database.BeginTransactionAsync(cancellationToken)
            : null;
}
