using Microsoft.EntityFrameworkCore;
using RegulatoryCompliance.Domain.Enums;
using RegulatoryCompliance.Infrastructure.Persistences;

namespace RegulatoryCompliance.Application.Embeddings;

public sealed class EmbeddingBatchProcessor(
    RegulatoryComplianceDbContext dbContext,
    IEmbeddingProvider provider,
    IRegulationVectorStore vectorStore,
    TimeProvider timeProvider) : IEmbeddingBatchProcessor
{
    public const int MaximumBatchSize = 64;
    private static readonly TimeSpan ProviderTimeout = TimeSpan.FromSeconds(30);

    public async Task<int> ProcessPendingAsync(CancellationToken cancellationToken = default)
    {
        var chunks = await dbContext.RegulatoryChunks
            .IgnoreQueryFilters()
            .Where(chunk =>
                chunk.EmbeddingStatus == ChunkEmbeddingStatus.Pending ||
                chunk.EmbeddingStatus == ChunkEmbeddingStatus.Failed)
            .OrderBy(chunk => chunk.CreatedAt)
            .ThenBy(chunk => chunk.Id)
            .Take(MaximumBatchSize)
            .ToListAsync(cancellationToken);
        chunks = chunks.Where(chunk => chunk.NeedsEmbedding(provider.Model.Name, provider.Model.Version)).ToList();
        if (chunks.Count == 0)
            return 0;

        var processedCount = 0;
        foreach (var chunk in chunks)
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(ProviderTimeout);

            try
            {
                var vectors = await provider.GenerateAsync(
                    [new EmbeddingInput(chunk.TenantId, chunk.NormalizedText)],
                    timeout.Token);
                if (vectors.Count != 1)
                    throw new InvalidOperationException("Embedding provider returned an unexpected vector count.");

                await vectorStore.UpsertAsync(
                    provider.Model,
                    [new VectorUpsert(
                        chunk.Id,
                        chunk.ScopeKey,
                        chunk.Visibility,
                        chunk.ContentSha256,
                        vectors[0])],
                    timeProvider.GetUtcNow(),
                    cancellationToken);
                processedCount++;
            }
            catch (Exception exception) when (
                exception is not OperationCanceledException && !cancellationToken.IsCancellationRequested)
            {
                chunk.MarkEmbeddingFailed(exception.Message, timeProvider.GetUtcNow());
                await dbContext.SaveChangesAsync(cancellationToken);
                throw;
            }
        }

        return processedCount;
    }
}
