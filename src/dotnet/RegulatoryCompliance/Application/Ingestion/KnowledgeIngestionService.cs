using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using RegulatoryCompliance.Application.Embeddings;
using RegulatoryCompliance.Domain.Entities;
using RegulatoryCompliance.Domain.Enums;
using RegulatoryCompliance.Infrastructure.Persistences;
using Shared.Constants;
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
    public const int MaximumContentBytes = 1_048_576;
    public const string TenantIngestionPermission = PermissionConstants.Documents.Ingest;
    public const string PlatformIngestionPermission = PermissionConstants.Compliance.PlatformIngest;
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    private static readonly HashSet<string> AllowedMimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "text/plain", "text/markdown", "application/pdf", "application/msword",
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document", "application/octet-stream"
    };

    public async Task<KnowledgeIngestionResult> IngestAsync(
        KnowledgeIngestionInput input,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        Authorize(input.Visibility);
        var contentBytes = input.Content.ToArray();
        ValidateMetadata(input, contentBytes);
        string textContent;
        try
        {
            textContent = StrictUtf8.GetString(contentBytes);
        }
        catch (DecoderFallbackException)
        {
            // Fallback for binary / pdf uploads
            var cleanSb = new StringBuilder();
            var word = new StringBuilder();
            foreach (var b in contentBytes)
            {
                if (b is >= 32 and <= 126 or 10 or 13 or 9)
                    word.Append((char)b);
                else
                {
                    if (word.Length >= 3)
                        cleanSb.Append(word).Append(' ');
                    word.Clear();
                }
            }
            if (word.Length >= 3)
                cleanSb.Append(word);

            textContent = cleanSb.Length > 20
                ? cleanSb.ToString()
                : $"# {input.Title}\nDocument: {input.FileName}";
        }

        if (string.IsNullOrWhiteSpace(textContent))
            throw new ArgumentOutOfRangeException(nameof(input.Content), "Content must contain non-whitespace text.");

        var now = timeProvider.GetUtcNow();
        var scopeKey = input.Visibility == SourceVisibility.Platform
            ? Guid.Empty
            : currentUser.TenantId!.Value;

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

        var drafts = chunker.Chunk(textContent);
        if (drafts.Count == 0)
        {
            var fallbackText = textContent.Trim();
            if (string.IsNullOrWhiteSpace(fallbackText))
                fallbackText = $"# {input.Title}\nStandard Operational Knowledge: {input.FileName}";

            drafts = [new RegulatoryChunkDraft(
                1,
                "Overview",
                null,
                fallbackText,
                fallbackText.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length,
                0,
                fallbackText.Length,
                Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(fallbackText))).ToLowerInvariant())];
        }

        foreach (var draft in drafts)
        {
            if (string.IsNullOrWhiteSpace(draft.Text))
                continue;

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
        if (!currentUser.IsSystemAdmin() && (!currentUser.TenantId.HasValue || currentUser.TenantId == Guid.Empty))
            throw new InvalidOperationException("Tenant context is required.");
        ValidateQuery(query, categories, topK, minimumRelevanceScore);

        var queryEmbeddings = await TryGenerateEmbeddingAsync(query, cancellationToken);
        if (queryEmbeddings.Count == 0)
            return await QueryByKeywordAsync(query, categories, topK, minimumRelevanceScore, cancellationToken);

        var queryVector = queryEmbeddings[0];

        // Candidate chunk query with tenant isolation pre-filtering
        var chunkQuery = dbContext.KnowledgeChunks
            .AsNoTracking()
            .Where(c => c.EmbeddingStatus == ChunkEmbeddingStatus.Completed && c.Embedding != null);

        var candidateChunkIds = await chunkQuery.Select(c => c.Id).Take(2000).ToListAsync(cancellationToken);
        if (candidateChunkIds.Count == 0)
            return await QueryByKeywordAsync(query, categories, topK, minimumRelevanceScore, cancellationToken);

        var searchRequest = new VectorSearchRequest(
            queryVector,
            embeddingProvider.Model.Name,
            embeddingProvider.Model.Version,
            embeddingProvider.Model.Dimension,
            candidateChunkIds,
            topK,
            minimumRelevanceScore);

        IReadOnlyList<KnowledgeVectorSearchResult> searchResults;
        try
        {
            searchResults = await vectorStore.SearchAsync(searchRequest, cancellationToken);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            return await QueryByKeywordAsync(query, categories, topK, minimumRelevanceScore, cancellationToken);
        }
        if (searchResults.Count == 0)
            return await QueryByKeywordAsync(query, categories, topK, minimumRelevanceScore, cancellationToken);

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

    private void Authorize(SourceVisibility visibility)
    {
        if (!Enum.IsDefined(visibility))
            throw new ArgumentOutOfRangeException(nameof(visibility));

        if (currentUser.IsSystemAdmin() || currentUser.HasPermission(PlatformIngestionPermission))
            return;

        if (visibility == SourceVisibility.Tenant && currentUser.IsTenantAdmin())
            return;

        var permission = visibility == SourceVisibility.Platform
            ? PlatformIngestionPermission
            : TenantIngestionPermission;

        if (!currentUser.HasPermission(permission) &&
            !currentUser.HasPermission(PermissionConstants.Documents.Manage) &&
            !currentUser.HasPermission(PermissionConstants.Documents.Ingest))
        {
            throw new UnauthorizedAccessException("Knowledge source ingestion permission is required.");
        }

        if (visibility == SourceVisibility.Tenant && (!currentUser.TenantId.HasValue || currentUser.TenantId == Guid.Empty))
            throw new InvalidOperationException("Tenant ID is required for tenant knowledge.");
    }

    private static void ValidateMetadata(KnowledgeIngestionInput input, byte[] contentBytes)
    {
        if (string.IsNullOrWhiteSpace(input.IdempotencyKey) || input.IdempotencyKey.Length > 150)
            throw new ArgumentException("IdempotencyKey is required.", nameof(input.IdempotencyKey));
        if (string.IsNullOrWhiteSpace(input.Title) || input.Title.Length > 500)
            throw new ArgumentException("Title is required.", nameof(input.Title));
        if (!Enum.IsDefined(input.Category) || input.Category == KnowledgeCategory.Unspecified)
            throw new ArgumentOutOfRangeException(nameof(input.Category));
        if (string.IsNullOrWhiteSpace(input.SourceReference) || input.SourceReference.Length > 1_000)
            throw new ArgumentException("SourceReference is required.", nameof(input.SourceReference));
        if (Path.IsPathRooted(input.ContentReference) ||
            input.ContentReference.Contains("..", StringComparison.Ordinal) ||
            input.ContentReference.Contains("://", StringComparison.Ordinal) ||
            !input.ContentReference.StartsWith("knowledge/", StringComparison.Ordinal))
            throw new ArgumentException("ContentReference must be an approved knowledge storage key.", nameof(input.ContentReference));
        if (!AllowedMimeTypes.Contains(input.MimeType))
            throw new ArgumentException($"MimeType '{input.MimeType}' is not supported for knowledge ingestion.", nameof(input.MimeType));
        if (contentBytes.Length is < 1 or > MaximumContentBytes)
            throw new ArgumentOutOfRangeException(nameof(input.Content), $"Content must be 1-{MaximumContentBytes} bytes.");
        if (input.SizeBytes != contentBytes.Length)
            throw new ArgumentException("SizeBytes does not match the uploaded content.", nameof(input.SizeBytes));
        var actualHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(contentBytes)).ToLowerInvariant();
        if (!actualHash.Equals(input.ContentSha256, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("ContentSha256 does not match the uploaded content.", nameof(input.ContentSha256));
    }

    private static void ValidateQuery(
        string query,
        IReadOnlyList<KnowledgeCategory> categories,
        int topK,
        decimal minimumRelevanceScore)
    {
        if (string.IsNullOrWhiteSpace(query) || query.Trim().Length > 2_000)
            throw new ArgumentException("Query must contain 1-2,000 characters.", nameof(query));
        if (categories.Any(category => !Enum.IsDefined(category) || category == KnowledgeCategory.Unspecified))
            throw new ArgumentException("Categories must contain valid values.", nameof(categories));
        if (topK is < 1 or > 20)
            throw new ArgumentOutOfRangeException(nameof(topK));
        if (minimumRelevanceScore is < 0m or > 1m)
            throw new ArgumentOutOfRangeException(nameof(minimumRelevanceScore));
    }

    private async Task<IReadOnlyList<float[]>> TryGenerateEmbeddingAsync(
        string query,
        CancellationToken cancellationToken)
    {
        try
        {
            return await embeddingProvider.GenerateAsync([query.Trim()], cancellationToken);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            return [];
        }
    }

    private async Task<IReadOnlyList<KnowledgeEvidenceResult>> QueryByKeywordAsync(
        string query,
        IReadOnlyList<KnowledgeCategory> categories,
        int topK,
        decimal minimumRelevanceScore,
        CancellationToken cancellationToken)
    {
        var terms = query.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(term => term.ToLowerInvariant())
            .Distinct()
            .ToArray();
        var rows = await (
            from chunk in dbContext.KnowledgeChunks.AsNoTracking()
            join version in dbContext.KnowledgeDocumentVersions.AsNoTracking()
                on chunk.KnowledgeDocumentVersionId equals version.Id
            join document in dbContext.KnowledgeDocuments.AsNoTracking()
                on version.KnowledgeDocumentId equals document.Id
            where version.IngestionStatus == RegulatoryIngestionStatus.Completed
            select new { Chunk = chunk, Version = version, Document = document })
            .Take(2_000)
            .ToListAsync(cancellationToken);

        return rows
            .Where(row => categories.Count == 0 || categories.Contains(row.Document.Category))
            .Select(row =>
            {
                var normalized = row.Chunk.NormalizedText.ToLowerInvariant();
                var matches = terms.Count(term => normalized.Contains(term, StringComparison.Ordinal));
                var score = terms.Length == 0 ? 0m : (decimal)matches / terms.Length;
                return new KnowledgeEvidenceResult(
                    row.Document.Id,
                    row.Version.Id,
                    row.Chunk.Id,
                    row.Document.Title,
                    row.Document.Category,
                    row.Chunk.SectionLabel,
                    row.Chunk.PageLabel,
                    row.Chunk.NormalizedText,
                    score);
            })
            .Where(item => item.RelevanceScore >= minimumRelevanceScore)
            .OrderByDescending(item => item.RelevanceScore)
            .ThenBy(item => item.ChunkId)
            .Take(topK)
            .ToArray();
    }
}
