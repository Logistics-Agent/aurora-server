using Microsoft.EntityFrameworkCore;
using RegulatoryCompliance.Domain.Enums;
using RegulatoryCompliance.Infrastructure.Persistences;

namespace RegulatoryCompliance.Application.Embeddings;

public sealed class EfRegulationVectorStore(RegulatoryComplianceDbContext dbContext)
    : IRegulationVectorStore
{
    private const int MaximumSearchCandidates = 2_000;

    public async Task UpsertAsync(
        EmbeddingModelDescriptor model,
        IReadOnlyList<VectorUpsert> vectors,
        DateTimeOffset embeddedAt,
        CancellationToken cancellationToken = default)
    {
        ValidateModel(model);
        ArgumentNullException.ThrowIfNull(vectors);
        if (vectors.Count is < 1 or > EmbeddingBatchProcessor.MaximumBatchSize)
            throw new ArgumentOutOfRangeException(nameof(vectors));
        var ids = vectors.Select(item => item.ChunkId).Distinct().ToArray();
        if (ids.Length != vectors.Count)
            throw new ArgumentException("Duplicate chunk IDs are not allowed.", nameof(vectors));

        var chunks = await dbContext.RegulatoryChunks
            .IgnoreQueryFilters()
            .Where(chunk => ids.Contains(chunk.Id))
            .ToDictionaryAsync(chunk => chunk.Id, cancellationToken);
        foreach (var item in vectors)
        {
            ValidateVector(item.Vector, model.Dimension);
            if (!chunks.TryGetValue(item.ChunkId, out var chunk))
                throw new InvalidOperationException($"Regulatory chunk {item.ChunkId} was not found.");
            if (chunk.ScopeKey != item.ScopeKey || chunk.Visibility != item.Visibility ||
                chunk.ContentSha256 != item.ContentHash)
                throw new InvalidOperationException("Vector scope or content identity does not match the chunk.");
            if (!chunk.NeedsEmbedding(model.Name, model.Version))
                continue;
            chunk.MarkEmbedded(item.Vector, model.Name, model.Version, model.Dimension, embeddedAt);
        }
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<VectorSearchResult>> SearchAsync(
        VectorSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.TopK is < 1 or > 100)
            throw new ArgumentOutOfRangeException(nameof(request.TopK));
        if (request.MinimumScore is < 0m or > 1m)
            throw new ArgumentOutOfRangeException(nameof(request.MinimumScore));
        ValidateVector(request.QueryVector, request.Dimension);

        var candidates = await dbContext.RegulatoryChunks
            .AsNoTracking()
            .Where(chunk =>
                chunk.EmbeddingStatus == ChunkEmbeddingStatus.Completed &&
                chunk.EmbeddingModel == request.ModelName &&
                chunk.EmbeddingModelVersion == request.ModelVersion &&
                chunk.Embedding != null)
            .OrderBy(chunk => chunk.Id)
            .Take(MaximumSearchCandidates)
            .Select(chunk => new
            {
                chunk.Id,
                chunk.RegulatoryDocumentVersionId,
                chunk.Sequence,
                chunk.Embedding
            })
            .ToListAsync(cancellationToken);

        return candidates
            .Where(item => item.Embedding!.Length == request.QueryVector.Length)
            .Select(item => new VectorSearchResult(
                item.Id,
                item.RegulatoryDocumentVersionId,
                item.Sequence,
                Convert.ToDecimal(CosineSimilarity(request.QueryVector, item.Embedding!))))
            .Where(item => item.Score >= request.MinimumScore)
            .OrderByDescending(item => item.Score)
            .ThenBy(item => item.ChunkId)
            .Take(request.TopK)
            .ToArray();
    }

    private static void ValidateModel(EmbeddingModelDescriptor model)
    {
        ArgumentNullException.ThrowIfNull(model);
        if (string.IsNullOrWhiteSpace(model.Name) || string.IsNullOrWhiteSpace(model.Version))
            throw new ArgumentException("Embedding model name and version are required.", nameof(model));
        if (model.Dimension is < 1 or > 4_096)
            throw new ArgumentOutOfRangeException(nameof(model));
    }

    private static void ValidateVector(float[] vector, int expectedDimension)
    {
        ArgumentNullException.ThrowIfNull(vector);
        if (expectedDimension is < 1 or > 4_096 ||
            vector.Length != expectedDimension ||
            vector.Any(value => !float.IsFinite(value)))
            throw new ArgumentException("Vector dimension or values are invalid.", nameof(vector));
    }

    private static double CosineSimilarity(float[] left, float[] right)
    {
        double dot = 0;
        double leftMagnitude = 0;
        double rightMagnitude = 0;
        for (var index = 0; index < left.Length; index++)
        {
            dot += left[index] * right[index];
            leftMagnitude += left[index] * left[index];
            rightMagnitude += right[index] * right[index];
        }
        if (leftMagnitude == 0 || rightMagnitude == 0)
            return 0;
        return dot / (Math.Sqrt(leftMagnitude) * Math.Sqrt(rightMagnitude));
    }
}
