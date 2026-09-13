using Microsoft.EntityFrameworkCore;
using RegulatoryCompliance.Domain.Entities;
using RegulatoryCompliance.Domain.Enums;
using RegulatoryCompliance.Infrastructure.Persistences;

namespace RegulatoryCompliance.Application.Ingestion;

public sealed class CorpusCatalogService(
    RegulatoryComplianceDbContext dbContext) : ICorpusCatalogService
{
    public async Task<CorpusPage<RegulatorySourceCatalogItem>> ListRegulatorySourcesAsync(
        int page,
        int pageSize,
        RegulatoryIngestionStatus? status = null,
        string? jurisdictionCode = null,
        CancellationToken cancellationToken = default)
    {
        var (safePage, safePageSize) = NormalizePage(page, pageSize);
        var query = dbContext.RegulatoryDocuments
            .AsNoTracking()
            .Select(document => new
            {
                Document = document,
                LatestVersion = document.Versions
                    .OrderByDescending(version => version.CreatedAt)
                    .FirstOrDefault()
            });

        if (status.HasValue)
            query = query.Where(item => item.LatestVersion != null && item.LatestVersion.IngestionStatus == status.Value);

        if (!string.IsNullOrWhiteSpace(jurisdictionCode))
        {
            var normalizedJurisdiction = jurisdictionCode.Trim().ToUpperInvariant();
            query = query.Where(item => item.Document.JurisdictionCode == normalizedJurisdiction);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var rows = await query
            .OrderByDescending(item => item.Document.CreatedAt)
            .ThenBy(item => item.Document.Id)
            .Skip((safePage - 1) * safePageSize)
            .Take(safePageSize)
            .ToListAsync(cancellationToken);

        return new CorpusPage<RegulatorySourceCatalogItem>(
            rows.Select(item => MapRegulatory(item.Document, item.LatestVersion)).ToArray(),
            safePage,
            safePageSize,
            totalCount);
    }

    public async Task<RegulatorySourceCatalogDetails?> GetRegulatorySourceAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var document = await dbContext.RegulatoryDocuments
            .AsNoTracking()
            .Include(item => item.Versions)
            .ThenInclude(version => version.Chunks)
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);

        if (document is null)
            return null;

        var versions = document.Versions
            .OrderByDescending(version => version.CreatedAt)
            .Select(MapVersion)
            .ToArray();
        return new RegulatorySourceCatalogDetails(
            MapRegulatory(document, document.Versions.OrderByDescending(version => version.CreatedAt).FirstOrDefault()),
            versions);
    }

    public async Task<CorpusPage<KnowledgeDocumentCatalogItem>> ListKnowledgeDocumentsAsync(
        int page,
        int pageSize,
        RegulatoryIngestionStatus? status = null,
        KnowledgeCategory? category = null,
        CancellationToken cancellationToken = default)
    {
        var (safePage, safePageSize) = NormalizePage(page, pageSize);
        var query = dbContext.KnowledgeDocuments
            .AsNoTracking()
            .Select(document => new
            {
                Document = document,
                LatestVersion = document.Versions
                    .OrderByDescending(version => version.CreatedAt)
                    .FirstOrDefault()
            });

        if (status.HasValue)
            query = query.Where(item => item.LatestVersion != null && item.LatestVersion.IngestionStatus == status.Value);
        if (category.HasValue)
            query = query.Where(item => item.Document.Category == category.Value);

        var totalCount = await query.CountAsync(cancellationToken);
        var rows = await query
            .OrderByDescending(item => item.Document.CreatedAt)
            .ThenBy(item => item.Document.Id)
            .Skip((safePage - 1) * safePageSize)
            .Take(safePageSize)
            .ToListAsync(cancellationToken);

        return new CorpusPage<KnowledgeDocumentCatalogItem>(
            rows.Select(item => MapKnowledge(item.Document, item.LatestVersion)).ToArray(),
            safePage,
            safePageSize,
            totalCount);
    }

    public async Task<KnowledgeDocumentCatalogDetails?> GetKnowledgeDocumentAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var document = await dbContext.KnowledgeDocuments
            .AsNoTracking()
            .Include(item => item.Versions)
            .ThenInclude(version => version.Chunks)
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);

        if (document is null)
            return null;

        var versions = document.Versions
            .OrderByDescending(version => version.CreatedAt)
            .Select(MapVersion)
            .ToArray();
        return new KnowledgeDocumentCatalogDetails(
            MapKnowledge(document, document.Versions.OrderByDescending(version => version.CreatedAt).FirstOrDefault()),
            versions);
    }

    private static (int Page, int PageSize) NormalizePage(int page, int pageSize) =>
        (Math.Max(page, 1), Math.Clamp(pageSize <= 0 ? 20 : pageSize, 1, 100));

    private static RegulatorySourceCatalogItem MapRegulatory(
        RegulatoryDocument document,
        RegulatoryDocumentVersion? latestVersion) =>
        new(
            document.Id,
            document.Title,
            document.Authority,
            document.JurisdictionCode,
            document.RegulationType,
            document.LanguageCode,
            document.Visibility,
            document.CreatedAt,
            latestVersion is null ? null : MapVersion(latestVersion));

    private static KnowledgeDocumentCatalogItem MapKnowledge(
        KnowledgeDocument document,
        KnowledgeDocumentVersion? latestVersion) =>
        new(
            document.Id,
            document.Title,
            document.Category,
            document.SourceReference,
            document.LanguageCode,
            document.Visibility,
            document.CreatedAt,
            latestVersion is null ? null : MapVersion(latestVersion));

    private static CorpusVersionStatus MapVersion(RegulatoryDocumentVersion version) =>
        new(
            version.Id,
            version.VersionLabel,
            version.IngestionStatus,
            version.ChunkCount,
            version.Chunks.Count(chunk => chunk.EmbeddingStatus == ChunkEmbeddingStatus.Completed),
            version.FileName,
            version.MimeType,
            version.SizeBytes,
            version.ContentSha256,
            version.CreatedAt,
            version.UpdatedAt,
            version.CompletedAt,
            version.FailedAt,
            version.ErrorCode,
            version.ErrorMessage);

    private static CorpusVersionStatus MapVersion(KnowledgeDocumentVersion version) =>
        new(
            version.Id,
            version.VersionLabel,
            version.IngestionStatus,
            version.ChunkCount,
            version.Chunks.Count(chunk => chunk.EmbeddingStatus == ChunkEmbeddingStatus.Completed),
            version.FileName,
            version.MimeType,
            version.SizeBytes,
            version.ContentSha256,
            version.CreatedAt,
            version.UpdatedAt,
            version.CompletedAt,
            version.FailedAt,
            version.ErrorCode,
            version.ErrorMessage);
}
