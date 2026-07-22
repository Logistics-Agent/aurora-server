using DocumentOcr.Domain.Entities;
using DocumentOcr.Infrastructure.Persistences;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace DocumentOcr.Infrastructure.BackgroundJobs;

public interface IDocumentOcrOutboxBatch : IAsyncDisposable
{
    IReadOnlyList<OutboxMessage> Messages { get; }
    Task CommitAsync(CancellationToken cancellationToken);
}

public interface IDocumentOcrOutboxBatchStore
{
    Task<IDocumentOcrOutboxBatch> LockPendingBatchAsync(
        int batchSize,
        int maxRetries,
        CancellationToken cancellationToken);
}

public sealed class DocumentOcrOutboxBatchStore(DocumentOcrDbContext dbContext)
    : IDocumentOcrOutboxBatchStore
{
    public async Task<IDocumentOcrOutboxBatch> LockPendingBatchAsync(
        int batchSize,
        int maxRetries,
        CancellationToken cancellationToken)
    {
        var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var messages = await dbContext.OutboxMessages
                .FromSqlInterpolated($"""
                    SELECT *
                    FROM outbox_messages
                    WHERE "ProcessedAt" IS NULL
                      AND "RetryCount" < {maxRetries}
                    ORDER BY "OccurredAt", "Id"
                    LIMIT {batchSize}
                    FOR UPDATE SKIP LOCKED
                    """)
                .IgnoreQueryFilters()
                .ToListAsync(cancellationToken);
            return new EfOutboxBatch(dbContext, transaction, messages);
        }
        catch
        {
            await transaction.DisposeAsync();
            throw;
        }
    }

    private sealed class EfOutboxBatch(
        DocumentOcrDbContext dbContext,
        IDbContextTransaction transaction,
        IReadOnlyList<OutboxMessage> messages) : IDocumentOcrOutboxBatch
    {
        private bool _committed;

        public IReadOnlyList<OutboxMessage> Messages => messages;

        public async Task CommitAsync(CancellationToken cancellationToken)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            _committed = true;
        }

        public async ValueTask DisposeAsync()
        {
            if (!_committed)
                await transaction.RollbackAsync();
            await transaction.DisposeAsync();
        }
    }
}
