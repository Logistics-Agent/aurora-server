using Microsoft.EntityFrameworkCore;
using RegulatoryCompliance.Application.Embeddings;
using RegulatoryCompliance.Domain.Entities;
using RegulatoryCompliance.Domain.Enums;
using RegulatoryCompliance.Infrastructure.Persistences;
using Shared.Security;

namespace RegulatoryCompliance.Application.Ingestion;

public sealed class KnowledgeIngestionService(
    RegulatoryComplianceDbContext dbContext,
    IRegulatoryChunker chunker,
    IEmbeddingProvider embeddingProvider,
    IKnowledgeVectorStore vectorStore,
    ICurrentUserService currentUser,
    TimeProvider timeProvider) : IKnowledgeIngestionService
{
    public async Task<KnowledgeIngestionResult> IngestAsync(
        KnowledgeIngestionInput input,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        var now = timeProvider.GetUtcNow();
        var scopeKey = currentUser.TenantId ?? Guid.Empty;

        // Check idempotency replay
        var existingVersion = await dbContext.KnowledgeDocumentVersions
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(v => v.ScopeKey == scopeKey && v.IngestionKey == input.IdempotencyKey, cancellationToken);

        if (existingVersion != null)
        {
            return new KnowledgeIngestionResult(
                existingVersion.KnowledgeDocumentId,
                existingVersion.Id,
                existingVersion.IngestionStatus,
                existingVersion.ChunkCount,
                true,
                existingVersion.CreatedAt);
        }

        // Find or create KnowledgeDocument
        var document = await dbContext.KnowledgeDocuments
            .FirstOrDefaultAsync(d =>
                d.ScopeKey == scopeKey &&
                d.Title == input.Title &&
                d.Category == input.Category &&
                d.LanguageCode == input.LanguageCode, cancellationToken);

        if (document == null)
        {
            document = input.Visibility == SourceVisibility.Platform
                ? KnowledgeDocument.CreatePlatform(input.Category, input.Title, input.SourceReference, input.LanguageCode, now)
                : KnowledgeDocument.CreateTenant(currentUser.TenantId ?? throw new InvalidOperationException("Tenant ID is required for tenant knowledge."),
                    input.Category, input.Title, input.SourceReference, input.LanguageCode, now);

            dbContext.KnowledgeDocuments.Add(document);
        }

        var version = document.AddVersion(
            input.IdempotencyKey,
            input.VersionLabel,
            input.ContentSha256,
            input.ContentReference,
            input.FileName,
            input.MimeType,
            input.SizeBytes,
            now);

        var textContent = System.Text.Encoding.UTF8.GetString(input.Content.Span);
        if (string.IsNullOrWhiteSpace(textContent))
        {
            version.MarkPendingOcr(now);
            await dbContext.SaveChangesAsync(cancellationToken);
            return new KnowledgeIngestionResult(
                document.Id,
                version.Id,
                version.IngestionStatus,
                0,
                false,
                now);
        }

        var drafts = chunker.Chunk(textContent);
        foreach (var draft in drafts)
        {
            var chunk = version.AddChunk(
                draft.Sequence,
                draft.SectionLabel,
                draft.PageLabel,
                draft.Text,
                draft.TokenCount,
                draft.StartOffset,
                draft.EndOffset,
                draft.ContentSha256,
                now);

            var embeddings = await embeddingProvider.GenerateAsync([chunk.NormalizedText], cancellationToken);
            if (embeddings.Count > 0)
            {
                chunk.MarkEmbedded(embeddings[0], embeddingProvider.Model.Name, embeddingProvider.Model.Version, embeddingProvider.Model.Dimension, now);
            }
        }

        version.MarkCompleted(now);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new KnowledgeIngestionResult(
            document.Id,
            version.Id,
            version.IngestionStatus,
            version.ChunkCount,
            false,
            now);
    }

    public async Task<IReadOnlyList<KnowledgeEvidenceResult>> QueryAsync(
        string query,
        IReadOnlyList<KnowledgeCategory> categories,
        int topK,
        decimal minimumRelevanceScore,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
            return [];

        var queryEmbeddings = await embeddingProvider.GenerateAsync([query], cancellationToken);
        if (queryEmbeddings.Count == 0)
            return [];

        var queryVector = queryEmbeddings[0];

        // Candidate chunk query with tenant isolation pre-filtering
        var chunkQuery = dbContext.KnowledgeChunks
            .AsNoTracking()
            .Include(c => c.KnowledgeDocumentVersionId)
            .Where(c => c.EmbeddingStatus == ChunkEmbeddingStatus.Completed && c.Embedding != null);

        var candidateChunkIds = await chunkQuery.Select(c => c.Id).Take(2000).ToListAsync(cancellationToken);
        if (candidateChunkIds.Count == 0)
            return [];

        var searchRequest = new VectorSearchRequest(
            queryVector,
            embeddingProvider.Model.Name,
            embeddingProvider.Model.Version,
            embeddingProvider.Model.Dimension,
            candidateChunkIds,
            topK,
            minimumRelevanceScore);

        var searchResults = await vectorStore.SearchAsync(searchRequest, cancellationToken);
        if (searchResults.Count == 0)
            return [];

        var resultChunkIds = searchResults.Select(r => r.ChunkId).ToList();
        var chunks = await dbContext.KnowledgeChunks
            .AsNoTracking()
            .Where(c => resultChunkIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, cancellationToken);

        var versionIds = chunks.Values.Select(c => c.KnowledgeDocumentVersionId).Distinct().ToList();
        var versions = await dbContext.KnowledgeDocumentVersions
            .AsNoTracking()
            .Where(v => versionIds.Contains(v.Id))
            .ToDictionaryAsync(v => v.Id, cancellationToken);

        var docIds = versions.Values.Select(v => v.KnowledgeDocumentId).Distinct().ToList();
        var docs = await dbContext.KnowledgeDocuments
            .AsNoTracking()
            .Where(d => docIds.Contains(d.Id))
            .ToDictionaryAsync(d => d.Id, cancellationToken);

        var evidence = new List<KnowledgeEvidenceResult>();
        foreach (var result in searchResults)
        {
            if (!chunks.TryGetValue(result.ChunkId, out var chunk)) continue;
            if (!versions.TryGetValue(chunk.KnowledgeDocumentVersionId, out var ver)) continue;
            if (!docs.TryGetValue(ver.KnowledgeDocumentId, out var doc)) continue;

            if (categories.Count > 0 && !categories.Contains(doc.Category))
                continue;

            evidence.Add(new KnowledgeEvidenceResult(
                doc.Id,
                ver.Id,
                chunk.Id,
                doc.Title,
                doc.Category,
                chunk.SectionLabel,
                chunk.PageLabel,
                chunk.NormalizedText,
                result.Score));
        }

        return evidence;
    }
}
