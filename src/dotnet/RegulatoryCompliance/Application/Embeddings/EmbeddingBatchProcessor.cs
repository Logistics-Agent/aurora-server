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

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(ProviderTimeout);
        IReadOnlyList<float[]> vectors;
        try
        {
            vectors = await provider.GenerateAsync(
                chunks.Select(chunk => chunk.NormalizedText).ToArray(), timeout.Token);
        }
        catch (Exception exception) when (
            exception is not OperationCanceledException && !cancellationToken.IsCancellationRequested)
        {
            var failedAt = timeProvider.GetUtcNow();
            foreach (var chunk in chunks)
                chunk.MarkEmbeddingFailed(exception.Message, failedAt);
            await dbContext.SaveChangesAsync(cancellationToken);
            throw;
        }
        if (vectors.Count != chunks.Count)
            throw new InvalidOperationException("Embedding provider returned an unexpected vector count.");

        await vectorStore.UpsertAsync(
            provider.Model,
            chunks.Select((chunk, index) => new VectorUpsert(
                chunk.Id,
                chunk.ScopeKey,
                chunk.Visibility,
                chunk.ContentSha256,
                vectors[index])).ToArray(),
            timeProvider.GetUtcNow(),
            cancellationToken);
        return chunks.Count;
    }
}
