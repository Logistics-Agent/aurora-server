using RegulatoryCompliance.Domain.Enums;

namespace RegulatoryCompliance.Application.Embeddings;

public sealed record EmbeddingModelDescriptor(string Name, string Version, int Dimension);

public sealed record EmbeddingInput
{
    public EmbeddingInput(Guid? tenantId, string text)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("TenantId cannot be empty.", nameof(tenantId));
        if (string.IsNullOrWhiteSpace(text))
            throw new ArgumentException("Embedding text is required.", nameof(text));

        TenantId = tenantId;
        Text = text;
    }

    public Guid? TenantId { get; }
    public string Text { get; }
}

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
    IReadOnlyCollection<Guid> CandidateChunkIds,
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
        IReadOnlyList<EmbeddingInput> inputs,
        CancellationToken cancellationToken = default);
}

public sealed record KnowledgeVectorSearchResult(
    Guid ChunkId,
    Guid KnowledgeDocumentVersionId,
    int Sequence,
    decimal Score);

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

public interface IKnowledgeVectorStore
{
    Task UpsertAsync(
        EmbeddingModelDescriptor model,
        IReadOnlyList<VectorUpsert> vectors,
        DateTimeOffset embeddedAt,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<KnowledgeVectorSearchResult>> SearchAsync(
        VectorSearchRequest request,
        CancellationToken cancellationToken = default);
}

public interface IEmbeddingBatchProcessor
{
    Task<int> ProcessPendingAsync(CancellationToken cancellationToken = default);
}
