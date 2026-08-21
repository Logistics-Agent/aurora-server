using RegulatoryCompliance.Domain.Enums;

namespace RegulatoryCompliance.Application.Ingestion;

public sealed record RegulatoryIngestionInput(
    string IdempotencyKey,
    string Authority,
    string Title,
    string CanonicalSourceUri,
    string JurisdictionCode,
    RegulationType RegulationType,
    string LanguageCode,
    string VersionLabel,
    DateTimeOffset PublishedAt,
    DateTimeOffset EffectiveFrom,
    DateTimeOffset? EffectiveTo,
    string ContentReference,
    string FileName,
    string MimeType,
    long SizeBytes,
    string ContentSha256,
    ReadOnlyMemory<byte> Content,
    SourceVisibility Visibility);

public sealed record RegulatoryIngestionResult(
    Guid RegulatoryDocumentId,
    Guid DocumentVersionId,
    RegulatoryIngestionStatus Status,
    int ChunkCount,
    bool Replayed,
    DateTimeOffset ReceivedAt);

public sealed record RegulatoryChunkDraft(
    int Sequence,
    string? SectionLabel,
    string? PageLabel,
    string Text,
    int TokenCount,
    int StartOffset,
    int EndOffset,
    string ContentSha256);

public interface IRegulatoryIngestionService
{
    Task<RegulatoryIngestionResult> IngestAsync(
        RegulatoryIngestionInput input,
        CancellationToken cancellationToken = default);
}

public interface IRegulatoryChunker
{
    IReadOnlyList<RegulatoryChunkDraft> Chunk(string content);
}
