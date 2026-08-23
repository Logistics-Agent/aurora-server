using RegulatoryCompliance.Domain.Enums;

namespace RegulatoryCompliance.Application.Ingestion;

public sealed record KnowledgeIngestionInput(
    string IdempotencyKey,
    string Title,
    KnowledgeCategory Category,
    string SourceReference,
    string LanguageCode,
    string VersionLabel,
    string ContentReference,
    string FileName,
    string MimeType,
    long SizeBytes,
    string ContentSha256,
    ReadOnlyMemory<byte> Content,
    SourceVisibility Visibility);

public sealed record KnowledgeIngestionResult(
    Guid KnowledgeDocumentId,
    Guid DocumentVersionId,
    RegulatoryIngestionStatus Status,
    int ChunkCount,
    bool Replayed,
    DateTimeOffset ReceivedAt);

public sealed record KnowledgeEvidenceResult(
    Guid KnowledgeDocumentId,
    Guid DocumentVersionId,
    Guid ChunkId,
    string Title,
    KnowledgeCategory Category,
    string? SectionLabel,
    string? PageLabel,
    string Excerpt,
    decimal RelevanceScore);

public interface IKnowledgeIngestionService
{
    Task<KnowledgeIngestionResult> IngestAsync(
        KnowledgeIngestionInput input,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<KnowledgeEvidenceResult>> QueryAsync(
        string query,
        IReadOnlyList<KnowledgeCategory> categories,
        int topK,
        decimal minimumRelevanceScore,
        CancellationToken cancellationToken = default);
}
