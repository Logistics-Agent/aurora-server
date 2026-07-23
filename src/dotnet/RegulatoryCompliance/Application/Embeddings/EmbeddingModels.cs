using RegulatoryCompliance.Domain.Enums;

namespace RegulatoryCompliance.Application.Embeddings;

public sealed record EmbeddingModelDescriptor(string Name, string Version, int Dimension);

public sealed record VectorUpsert(
    Guid ChunkId,
    Guid ScopeKey,
    SourceVisibility Visibility,
    string ContentHash,
    float[] Vector);

public sealed record VectorSearchRequest(
    float[] QueryVector,
    string ModelName,
    string ModelVersion,
    int Dimension,
    int TopK,
    decimal MinimumScore);

public sealed record VectorSearchResult(
    Guid ChunkId,
    Guid RegulatoryDocumentVersionId,
    int Sequence,
    decimal Score);

public interface IEmbeddingProvider
{
    EmbeddingModelDescriptor Model { get; }

    Task<IReadOnlyList<float[]>> GenerateAsync(
        IReadOnlyList<string> texts,
        CancellationToken cancellationToken = default);
}

public interface IRegulationVectorStore
{
    Task UpsertAsync(
        EmbeddingModelDescriptor model,
        IReadOnlyList<VectorUpsert> vectors,
        DateTimeOffset embeddedAt,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<VectorSearchResult>> SearchAsync(
        VectorSearchRequest request,
        CancellationToken cancellationToken = default);
}

public interface IEmbeddingBatchProcessor
{
    Task<int> ProcessPendingAsync(CancellationToken cancellationToken = default);
}
