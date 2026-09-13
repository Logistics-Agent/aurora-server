using RegulatoryCompliance.Domain.Enums;

namespace RegulatoryCompliance.Application.Ingestion;

public sealed record CorpusPage<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalCount);

public sealed record CorpusVersionStatus(
    Guid Id,
    string VersionLabel,
    RegulatoryIngestionStatus Status,
    int ChunkCount,
    int EmbeddedChunkCount,
    string FileName,
    string MimeType,
    long SizeBytes,
    string ContentSha256,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    DateTimeOffset? CompletedAt,
    DateTimeOffset? FailedAt,
    string? ErrorCode,
    string? ErrorMessage);

public sealed record RegulatorySourceCatalogItem(
    Guid Id,
    string Title,
    string Authority,
    string JurisdictionCode,
    RegulationType RegulationType,
    string LanguageCode,
    SourceVisibility Visibility,
    DateTimeOffset CreatedAt,
    CorpusVersionStatus? LatestVersion);

public sealed record RegulatorySourceCatalogDetails(
    RegulatorySourceCatalogItem Summary,
    IReadOnlyList<CorpusVersionStatus> Versions);

public sealed record KnowledgeDocumentCatalogItem(
    Guid Id,
    string Title,
    KnowledgeCategory Category,
    string SourceReference,
    string LanguageCode,
    SourceVisibility Visibility,
    DateTimeOffset CreatedAt,
    CorpusVersionStatus? LatestVersion);

public sealed record KnowledgeDocumentCatalogDetails(
    KnowledgeDocumentCatalogItem Summary,
    IReadOnlyList<CorpusVersionStatus> Versions);

public interface ICorpusCatalogService
{
    Task<CorpusPage<RegulatorySourceCatalogItem>> ListRegulatorySourcesAsync(
        int page,
        int pageSize,
        RegulatoryIngestionStatus? status = null,
        string? jurisdictionCode = null,
        CancellationToken cancellationToken = default);

    Task<RegulatorySourceCatalogDetails?> GetRegulatorySourceAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<CorpusPage<KnowledgeDocumentCatalogItem>> ListKnowledgeDocumentsAsync(
        int page,
        int pageSize,
        RegulatoryIngestionStatus? status = null,
        KnowledgeCategory? category = null,
        CancellationToken cancellationToken = default);

    Task<KnowledgeDocumentCatalogDetails?> GetKnowledgeDocumentAsync(
        Guid id,
        CancellationToken cancellationToken = default);
}
