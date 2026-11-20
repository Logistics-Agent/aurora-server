using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using RegulatoryCompliance.Application.Embeddings;
using RegulatoryCompliance.Domain.Entities;
using RegulatoryCompliance.Domain.Enums;
using RegulatoryCompliance.Infrastructure.Persistences;
using Shared.Constants;
using Shared.Security;

namespace RegulatoryCompliance.Application.Retrieval;

public sealed record RegulationQueryInput(
    string Query,
    string JurisdictionCode,
    DateTimeOffset EffectiveAt,
    string LanguageCode,
    IReadOnlyCollection<RegulationType> RegulationTypes,
    int TopK,
    decimal MinimumRelevanceScore,
    bool PersistTrace = true);

public sealed record RegulationEvidenceResult(
    Guid RegulatoryDocumentId,
    Guid DocumentVersionId,
    Guid ChunkId,
    RegulationType RegulationType,
    string JurisdictionCode,
    string LanguageCode,
    string Authority,
    string Title,
    string CanonicalSourceUri,
    string VersionLabel,
    string? SectionLabel,
    string? PageLabel,
    DateTimeOffset EffectiveFrom,
    DateTimeOffset? EffectiveTo,
    string Excerpt,
    decimal RelevanceScore);

public sealed record RegulationQueryResult(
    Guid RetrievalTraceId,
    EvidenceSufficiency EvidenceSufficiency,
    IReadOnlyList<RegulationEvidenceResult> Evidence,
    string GeneratedExplanation);

public interface IRegulationRetrievalService
{
    Task<RegulationQueryResult> QueryAsync(
        RegulationQueryInput input,
        CancellationToken cancellationToken = default);
}

public sealed class RegulationRetrievalService(
    RegulatoryComplianceDbContext dbContext,
    IEmbeddingProvider embeddingProvider,
    IRegulationVectorStore vectorStore,
    ICurrentUserService currentUser,
    TimeProvider timeProvider,
    IDbContextFactory<RegulatoryComplianceDbContext>? dbContextFactory = null) : IRegulationRetrievalService
{
    public const int MaximumTopK = 100;
    private const int MaximumCandidateChunks = 2_000;
    private const int MaximumExcerptCharacters = 800;

    public async Task<RegulationQueryResult> QueryAsync(
        RegulationQueryInput input,
        CancellationToken cancellationToken = default)
    {
        if (dbContextFactory is null)
            return await QueryCoreAsync(dbContext, input, cancellationToken);

        await using var ownedDbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await QueryCoreAsync(ownedDbContext, input, cancellationToken);
    }

    private async Task<RegulationQueryResult> QueryCoreAsync(
        RegulatoryComplianceDbContext dbContext,
        RegulationQueryInput input,
        CancellationToken cancellationToken)
    {
        Validate(input);
        var tenantId = currentUser.TenantId ?? Guid.Empty;
        var types = input.RegulationTypes.Count == 0
            ? Enum.GetValues<RegulationType>()
            : input.RegulationTypes.Distinct().ToArray();
        var jurisdiction = string.IsNullOrWhiteSpace(input.JurisdictionCode)
            ? "GLOBAL"
            : input.JurisdictionCode.Trim().ToUpperInvariant();
        var language = (input.LanguageCode ?? string.Empty).Trim().ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(input.Query) || input.Query.Trim() == "*")
        {
            var docQuery = dbContext.RegulatoryDocuments
                .AsNoTracking()
                .Include(d => d.Versions)
                .ThenInclude(v => v.Chunks)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(jurisdiction))
            {
                docQuery = docQuery.Where(d => d.JurisdictionCode == jurisdiction || d.JurisdictionCode == "GLOBAL");
            }

            if (types.Length > 0)
            {
                docQuery = docQuery.Where(d => types.Contains(d.RegulationType));
            }

            var docList = !string.IsNullOrWhiteSpace(language)
                ? await docQuery
                    .Where(d => d.LanguageCode == language)
                    .OrderByDescending(d => d.CreatedAt)
                    .Take(input.TopK)
                    .ToListAsync(cancellationToken)
                : [];

            // Cross-lingual fallback when strict language filter yields 0 documents
            if (docList.Count == 0)
            {
                docList = await docQuery
                    .OrderByDescending(d => d.CreatedAt)
                    .Take(input.TopK)
                    .ToListAsync(cancellationToken);
            }

            var listResults = new List<RegulationEvidenceResult>();
            foreach (var doc in docList)
            {
                var latestVersion = doc.Versions.OrderByDescending(v => v.CreatedAt).FirstOrDefault();
                var fullText = latestVersion != null && latestVersion.Chunks.Count > 0
                    ? string.Join("\n\n", latestVersion.Chunks.OrderBy(c => c.Sequence).Select(c => c.NormalizedText))
                    : string.Empty;
                var firstChunk = latestVersion?.Chunks.OrderBy(c => c.Sequence).FirstOrDefault();
                listResults.Add(new RegulationEvidenceResult(
                    doc.Id,
                    latestVersion?.Id ?? Guid.Empty,
                    firstChunk?.Id ?? Guid.Empty,
                    doc.RegulationType,
                    doc.JurisdictionCode,
                    doc.LanguageCode,
                    doc.Authority,
                    doc.Title,
                    doc.CanonicalSourceUri,
                    latestVersion?.VersionLabel ?? "1.0",
                    firstChunk?.SectionLabel ?? "Overview",
                    firstChunk?.PageLabel ?? "1",
                    latestVersion?.EffectiveFrom ?? doc.CreatedAt,
                    latestVersion?.EffectiveTo,
                    fullText,
                    1.0m));
            }
            return await BuildResultAsync(dbContext, input, tenantId, jurisdiction, language, types, listResults, "Retrieved all regulatory sources.", cancellationToken);
        }

        var candidateIds = await QueryCandidateChunkIdsAsync(
            dbContext,
            input.EffectiveAt,
            jurisdiction,
            language,
            types,
            cancellationToken);

        // Cross-lingual fallback (Item 9): If strict language filter returns 0 chunks (e.g. 'en' query against 'vi' corpus or vice versa), retry across all languages
        if (candidateIds.Length == 0 && !string.IsNullOrWhiteSpace(language))
        {
            candidateIds = await QueryCandidateChunkIdsAsync(
                dbContext,
                input.EffectiveAt,
                jurisdiction,
                string.Empty,
                types,
                cancellationToken);
        }

        if (candidateIds.Length == 0)
            return await BuildResultAsync(dbContext, input, tenantId, jurisdiction, language, types, [], "Insufficient regulatory evidence was found for the supplied filters.", cancellationToken);

        var embeddings = await TryGenerateEmbeddingAsync(input.Query, cancellationToken);
        if (embeddings.Count == 0)
            return await QueryByKeywordAsync(dbContext, input, tenantId, jurisdiction, language, types, cancellationToken);

        int vectorTopK = Math.Min(MaximumTopK, Math.Max(1, input.TopK * 3));
        IReadOnlyList<VectorSearchResult> ranked;
        try
        {
            ranked = await vectorStore.SearchAsync(
                new VectorSearchRequest(
                    embeddings[0],
                    embeddingProvider.Model.Name,
                    embeddingProvider.Model.Version,
                    embeddingProvider.Model.Dimension,
                    candidateIds,
                    vectorTopK,
                    input.MinimumRelevanceScore),
                cancellationToken);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            return await QueryByKeywordAsync(dbContext, input, tenantId, jurisdiction, language, types, cancellationToken);
        }

        var vectorScoreMap = ranked
            .OrderByDescending(item => item.Score)
            .ThenBy(item => item.ChunkId)
            .Take(vectorTopK)
            .ToDictionary(item => item.ChunkId, item => item.Score);

        var lexicalCandidates = await (
                from chunk in dbContext.RegulatoryChunks.AsNoTracking()
                join version in dbContext.RegulatoryDocumentVersions.AsNoTracking()
                    on chunk.RegulatoryDocumentVersionId equals version.Id
                join document in dbContext.RegulatoryDocuments.AsNoTracking()
                    on version.RegulatoryDocumentId equals document.Id
                where candidateIds.Contains(chunk.Id)
                select new
                {
                    chunk.Id,
                    chunk.NormalizedText,
                    chunk.SectionLabel,
                    document.Title
                })
            .ToListAsync(cancellationToken);

        int lexicalLimit = Math.Min(MaximumTopK, Math.Max(1, input.TopK * 2));
        var lexicalTopIds = lexicalCandidates
            .Select(item => new
            {
                item.Id,
                LexScore = ComputeLexicalRelevanceScore(input.Query, item.NormalizedText, item.Title, item.SectionLabel)
            })
            .Where(item => item.LexScore > 0m)
            .OrderByDescending(item => item.LexScore)
            .ThenBy(item => item.Id)
            .Take(lexicalLimit)
            .Select(item => item.Id)
            .ToArray();

        var unionIds = vectorScoreMap.Keys
            .Union(lexicalTopIds)
            .Distinct()
            .ToArray();

        if (unionIds.Length == 0)
        {
            return await BuildResultAsync(
                dbContext,
                input,
                tenantId,
                jurisdiction,
                language,
                types,
                [],
                "Insufficient regulatory evidence was found for the supplied filters.",
                cancellationToken);
        }

        var metadata = await (
                from chunk in dbContext.RegulatoryChunks.AsNoTracking()
                join version in dbContext.RegulatoryDocumentVersions.AsNoTracking()
                    on chunk.RegulatoryDocumentVersionId equals version.Id
                join document in dbContext.RegulatoryDocuments.AsNoTracking()
                    on version.RegulatoryDocumentId equals document.Id
                where unionIds.Contains(chunk.Id)
                select new { Chunk = chunk, Version = version, Document = document })
            .ToDictionaryAsync(item => item.Chunk.Id, cancellationToken);

        // True Hybrid Fusion: Combine Dense Vector Similarity + Lexical/Code Relevance + Exact Match Boost
        var boostedResults = unionIds
            .Where(metadata.ContainsKey)
            .Select(chunkId =>
            {
                var source = metadata[chunkId];
                var vecScore = vectorScoreMap.GetValueOrDefault(chunkId, 0m);
                var lexScore = ComputeLexicalRelevanceScore(
                    input.Query,
                    source.Chunk.NormalizedText,
                    source.Document.Title,
                    source.Chunk.SectionLabel);
                var hybridBoost = ComputeHybridLexicalBoost(
                    input.Query,
                    source.Chunk.NormalizedText,
                    source.Document.Title);
                var finalScore = ComputeHybridFusionScore(vecScore, lexScore, hybridBoost);
                return new RegulationEvidenceResult(
                    source.Document.Id,
                    source.Version.Id,
                    source.Chunk.Id,
                    source.Document.RegulationType,
                    source.Document.JurisdictionCode,
                    source.Document.LanguageCode,
                    source.Document.Authority,
                    source.Document.Title,
                    source.Document.CanonicalSourceUri,
                    source.Version.VersionLabel,
                    source.Chunk.SectionLabel,
                    source.Chunk.PageLabel,
                    source.Version.EffectiveFrom,
                    source.Version.EffectiveTo,
                    source.Chunk.NormalizedText[..Math.Min(
                        source.Chunk.NormalizedText.Length, MaximumExcerptCharacters)],
                    finalScore);
            })
            .Where(item => item.RelevanceScore >= input.MinimumRelevanceScore)
            .OrderByDescending(item => item.RelevanceScore)
            .ThenBy(item => item.ChunkId);

        var evidence = Deduplicate(boostedResults).Take(input.TopK).ToArray();
        evidence = await ExpandNeighborContextAsync(dbContext, evidence, cancellationToken);
        var explanation = evidence.Length == 0
            ? "Insufficient regulatory evidence was found for the supplied filters."
            : $"Retrieved {evidence.Length} evidence passage(s). Conclusions must be limited to the cited source text.";
        return await BuildResultAsync(
            dbContext,
            input,
            tenantId,
            jurisdiction,
            language,
            types,
            evidence,
            explanation,
            cancellationToken);
    }

    private static readonly HashSet<string> Stopwords = new(StringComparer.OrdinalIgnoreCase)
    {
        "the", "and", "for", "of", "in", "on", "to", "is", "are", "with", "by", "from", "what", "how",
        "của", "và", "là", "cho", "trong", "có", "các", "với", "về", "tại", "theo", "khi", "này", "thế", "còn"
    };

    private static readonly char[] TokenSeparators =
        [' ', '\t', '\r', '\n', ',', ';', ':', '?', '!', '(', ')', '[', ']', '"', '\''];

    private static async Task<Guid[]> QueryCandidateChunkIdsAsync(
        RegulatoryComplianceDbContext dbContext,
        DateTimeOffset effectiveAt,
        string jurisdiction,
        string language,
        RegulationType[] types,
        CancellationToken cancellationToken)
    {
        return await (
                from chunk in dbContext.RegulatoryChunks.AsNoTracking()
                join version in dbContext.RegulatoryDocumentVersions.AsNoTracking()
                    on chunk.RegulatoryDocumentVersionId equals version.Id
                join document in dbContext.RegulatoryDocuments.AsNoTracking()
                    on version.RegulatoryDocumentId equals document.Id
                where version.IngestionStatus == RegulatoryIngestionStatus.Completed
                      && version.SupersededAt == null
                      && version.EffectiveFrom <= effectiveAt
                      && (version.EffectiveTo == null || effectiveAt < version.EffectiveTo)
                      && (string.IsNullOrWhiteSpace(jurisdiction) || document.JurisdictionCode == jurisdiction || document.JurisdictionCode == "GLOBAL")
                      && (string.IsNullOrWhiteSpace(language) || document.LanguageCode == language)
                      && types.Contains(document.RegulationType)
                orderby version.EffectiveFrom descending, chunk.Sequence ascending, chunk.Id ascending
                select chunk.Id)
            .Take(MaximumCandidateChunks)
            .ToArrayAsync(cancellationToken);
    }

    internal static decimal ComputeLexicalRelevanceScore(
        string query,
        string chunkText,
        string documentTitle,
        string? sectionLabel)
    {
        if (string.IsNullOrWhiteSpace(query) || string.IsNullOrWhiteSpace(chunkText))
            return 0m;

        var rawTokens = query.Split(TokenSeparators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(token => token.Length >= 2)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var terms = rawTokens.Where(token => !Stopwords.Contains(token)).ToArray();
        if (terms.Length == 0)
            terms = rawTokens;

        var headerTarget = $"{documentTitle} {sectionLabel}";
        var combinedTarget = $"{headerTarget} {chunkText}";

        decimal score = 0m;
        if (terms.Length > 0)
        {
            var bodyMatches = terms.Count(term => chunkText.Contains(term, StringComparison.OrdinalIgnoreCase));
            var headerMatches = terms.Count(term => headerTarget.Contains(term, StringComparison.OrdinalIgnoreCase));
            score += ((decimal)bodyMatches / terms.Length) * 0.65m;
            score += ((decimal)headerMatches / terms.Length) * 0.25m;
        }

        if (HasExactCodeOrAcronymMatch(query, combinedTarget))
            score += 0.25m;

        return Math.Clamp(decimal.Round(score, 4), 0m, 1m);
    }

    internal static decimal ComputeHybridLexicalBoost(string query, string chunkText, string documentTitle)
    {
        if (string.IsNullOrWhiteSpace(query) || string.IsNullOrWhiteSpace(chunkText))
            return 0m;

        var combinedTarget = $"{documentTitle} {chunkText}";
        return HasExactCodeOrAcronymMatch(query, combinedTarget) ? 0.12m : 0m;
    }

    internal static decimal ComputeHybridFusionScore(decimal vectorScore, decimal lexicalScore, decimal hybridBoost)
    {
        var raw = vectorScore > 0m
            ? Math.Max(vectorScore + hybridBoost, (vectorScore * 0.65m) + (lexicalScore * 0.35m) + hybridBoost)
            : (lexicalScore * 0.75m) + hybridBoost;
        return Math.Clamp(decimal.Round(raw, 4), 0m, 1m);
    }

    private static bool HasExactCodeOrAcronymMatch(string query, string combinedTarget)
    {
        var codeMatches = Regex.Matches(
            query,
            @"\b(?:UN\s?\d{2,4}(?:\.\d+)?|HS\s?\d{4,10}(?:\.\d{1,4})?|\d{4,12}|C/O|MSDS|FOB|CIF|EXW|DDP|DDU|INCOTERMS|IATA|IMDG|EUR\.?1)\b",
            RegexOptions.IgnoreCase);
        foreach (Match match in codeMatches)
        {
            if (combinedTarget.Contains(match.Value, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private async Task<RegulationQueryResult> QueryByKeywordAsync(
        RegulatoryComplianceDbContext dbContext,
        RegulationQueryInput input,
        Guid tenantId,
        string jurisdiction,
        string language,
        RegulationType[] types,
        CancellationToken cancellationToken)
    {
        var terms = input.Query.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(term => term.ToLowerInvariant())
            .Distinct()
            .ToArray();
        var rows = await (
            from chunk in dbContext.RegulatoryChunks.AsNoTracking()
            join version in dbContext.RegulatoryDocumentVersions.AsNoTracking()
                on chunk.RegulatoryDocumentVersionId equals version.Id
            join document in dbContext.RegulatoryDocuments.AsNoTracking()
                on version.RegulatoryDocumentId equals document.Id
            where version.IngestionStatus == RegulatoryIngestionStatus.Completed
                  && version.SupersededAt == null
                  && version.EffectiveFrom <= input.EffectiveAt
                  && (version.EffectiveTo == null || input.EffectiveAt < version.EffectiveTo)
                  && (string.IsNullOrWhiteSpace(jurisdiction) || document.JurisdictionCode == jurisdiction || document.JurisdictionCode == "GLOBAL")
                  && (string.IsNullOrWhiteSpace(language) || document.LanguageCode == language)
                  && types.Contains(document.RegulationType)
            select new { Chunk = chunk, Version = version, Document = document })
            .Take(MaximumCandidateChunks)
            .ToListAsync(cancellationToken);

        if (rows.Count == 0 && !string.IsNullOrWhiteSpace(language))
        {
            rows = await (
                from chunk in dbContext.RegulatoryChunks.AsNoTracking()
                join version in dbContext.RegulatoryDocumentVersions.AsNoTracking()
                    on chunk.RegulatoryDocumentVersionId equals version.Id
                join document in dbContext.RegulatoryDocuments.AsNoTracking()
                    on version.RegulatoryDocumentId equals document.Id
                where version.IngestionStatus == RegulatoryIngestionStatus.Completed
                      && version.SupersededAt == null
                      && version.EffectiveFrom <= input.EffectiveAt
                      && (version.EffectiveTo == null || input.EffectiveAt < version.EffectiveTo)
                      && (string.IsNullOrWhiteSpace(jurisdiction) || document.JurisdictionCode == jurisdiction || document.JurisdictionCode == "GLOBAL")
                      && types.Contains(document.RegulationType)
                select new { Chunk = chunk, Version = version, Document = document })
                .Take(MaximumCandidateChunks)
                .ToListAsync(cancellationToken);
        }

        var evidence = rows
            .Select(row =>
            {
                var normalized = row.Chunk.NormalizedText.ToLowerInvariant();
                var matches = terms.Count(term => normalized.Contains(term, StringComparison.Ordinal));
                var score = terms.Length == 0 ? 0m : (decimal)matches / terms.Length;
                return new RegulationEvidenceResult(
                    row.Document.Id,
                    row.Version.Id,
                    row.Chunk.Id,
                    row.Document.RegulationType,
                    row.Document.JurisdictionCode,
                    row.Document.LanguageCode,
                    row.Document.Authority,
                    row.Document.Title,
                    row.Document.CanonicalSourceUri,
                    row.Version.VersionLabel,
                    row.Chunk.SectionLabel,
                    row.Chunk.PageLabel,
                    row.Version.EffectiveFrom,
                    row.Version.EffectiveTo,
                    row.Chunk.NormalizedText[..Math.Min(row.Chunk.NormalizedText.Length, MaximumExcerptCharacters)],
                    score);
            })
            .Where(item => item.RelevanceScore >= input.MinimumRelevanceScore)
            .OrderByDescending(item => item.RelevanceScore)
            .ThenBy(item => item.ChunkId)
            .Take(input.TopK)
            .ToArray();

        return await BuildResultAsync(
            dbContext,
            input,
            tenantId,
            jurisdiction,
            language,
            types,
            evidence,
            evidence.Length == 0
                ? "Insufficient regulatory evidence was found for the supplied filters."
                : $"Retrieved {evidence.Length} evidence passage(s) using keyword fallback. Conclusions must be limited to the cited source text.",
            cancellationToken,
            "keyword-fallback:1");
    }

    private async Task<IReadOnlyList<float[]>> TryGenerateEmbeddingAsync(
        string query,
        CancellationToken cancellationToken)
    {
        try
        {
            return await embeddingProvider.GenerateAsync(
                [new EmbeddingInput(currentUser.TenantId, query.Trim())], cancellationToken);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            return [];
        }
    }

    private async Task<RegulationQueryResult> BuildResultAsync(
        RegulatoryComplianceDbContext dbContext,
        RegulationQueryInput input,
        Guid tenantId,
        string jurisdiction,
        string language,
        IReadOnlyCollection<RegulationType> types,
        IReadOnlyList<RegulationEvidenceResult> evidence,
        string explanation,
        CancellationToken cancellationToken,
        string? modelOverride = null)
    {
        var sufficiency = evidence.Count == 0
            ? EvidenceSufficiency.Insufficient
            : EvidenceSufficiency.Sufficient;
        if (tenantId == Guid.Empty)
            return new RegulationQueryResult(Guid.Empty, sufficiency, evidence, explanation);

        var traceLanguage = string.IsNullOrWhiteSpace(language) ? "any" : language;
        var now = timeProvider.GetUtcNow();
        var trace = RetrievalTrace.Create(
            tenantId,
            null,
            Sha256((input.Query ?? string.Empty).Trim()),
            jurisdiction,
            input.EffectiveAt,
            traceLanguage,
            JsonSerializer.Serialize(types.Select(type => type.ToString())),
            modelOverride ?? $"{embeddingProvider.Model.Name}:{embeddingProvider.Model.Version}",
            input.TopK,
            input.MinimumRelevanceScore,
            JsonSerializer.Serialize(evidence.Select(item => item.ChunkId)),
            JsonSerializer.Serialize(evidence.Select(item => item.RelevanceScore)),
            sufficiency,
            now);
        dbContext.RetrievalTraces.Add(trace);
        if (input.PersistTrace)
            await dbContext.SaveChangesAsync(cancellationToken);
        return new RegulationQueryResult(trace.Id, sufficiency, evidence, explanation);
    }

    private void Validate(RegulationQueryInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (!currentUser.IsSystemAdmin() && (!currentUser.TenantId.HasValue || currentUser.TenantId == Guid.Empty))
            throw new InvalidOperationException("Tenant context is required.");
        if (input.Query != null && input.Query.Trim().Length > 2_000)
            throw new ArgumentException("Query must contain 0-2,000 characters.", nameof(input.Query));
        if (input.JurisdictionCode != null && input.JurisdictionCode.Trim().Length > 30)
            throw new ArgumentException("JurisdictionCode must contain 0-30 characters.", nameof(input.JurisdictionCode));
        if (input.EffectiveAt == default)
            throw new ArgumentException("EffectiveAt is required.", nameof(input.EffectiveAt));
        if (input.LanguageCode != null && input.LanguageCode.Trim().Length > 15)
            throw new ArgumentException("LanguageCode must contain 0-15 characters.", nameof(input.LanguageCode));
        if (input.RegulationTypes.Any(type => !Enum.IsDefined(type)))
            throw new ArgumentException("RegulationTypes must contain valid values.", nameof(input.RegulationTypes));
        if (input.TopK is < 1 or > MaximumTopK)
            throw new ArgumentOutOfRangeException(nameof(input.TopK));
        if (input.MinimumRelevanceScore is < 0m or > 1m)
            throw new ArgumentOutOfRangeException(nameof(input.MinimumRelevanceScore));
    }

    private static IEnumerable<RegulationEvidenceResult> Deduplicate(
        IEnumerable<RegulationEvidenceResult> ranked)
    {
        var accepted = new List<RegulationEvidenceResult>();
        foreach (var candidate in ranked)
        {
            if (accepted.Any(existing =>
                    existing.DocumentVersionId == candidate.DocumentVersionId &&
                    HasHighOverlap(existing.Excerpt, candidate.Excerpt)))
                continue;
            accepted.Add(candidate);
        }
        return accepted;
    }

    internal const int NeighborExpansionLengthThreshold = 300;

    internal sealed record NeighborChunkSlice(
        Guid Id,
        Guid VersionId,
        int Sequence,
        string? SectionLabel,
        string NormalizedText);

    private static async Task<RegulationEvidenceResult[]> ExpandNeighborContextAsync(
        RegulatoryComplianceDbContext dbContext,
        RegulationEvidenceResult[] evidence,
        CancellationToken cancellationToken)
    {
        if (evidence.Length == 0)
            return evidence;

        var shortIds = evidence
            .Where(item => item.Excerpt.Length < NeighborExpansionLengthThreshold)
            .Select(item => item.ChunkId)
            .Distinct()
            .ToArray();

        if (shortIds.Length == 0)
            return evidence;

        var centerChunks = await dbContext.RegulatoryChunks
            .AsNoTracking()
            .Where(chunk => shortIds.Contains(chunk.Id))
            .Select(chunk => new NeighborChunkSlice(
                chunk.Id,
                chunk.RegulatoryDocumentVersionId,
                chunk.Sequence,
                chunk.SectionLabel,
                chunk.NormalizedText))
            .ToListAsync(cancellationToken);

        if (centerChunks.Count == 0)
            return evidence;

        var versionIds = centerChunks
            .Select(chunk => chunk.VersionId)
            .Distinct()
            .ToArray();

        var desiredSequences = centerChunks
            .SelectMany(chunk => new[] { chunk.Sequence - 1, chunk.Sequence + 1 })
            .Where(seq => seq >= 0)
            .Distinct()
            .ToArray();

        if (desiredSequences.Length == 0)
            return evidence;

        var neighborCandidates = await dbContext.RegulatoryChunks
            .AsNoTracking()
            .Where(chunk =>
                versionIds.Contains(chunk.RegulatoryDocumentVersionId) &&
                desiredSequences.Contains(chunk.Sequence))
            .Select(chunk => new NeighborChunkSlice(
                chunk.Id,
                chunk.RegulatoryDocumentVersionId,
                chunk.Sequence,
                chunk.SectionLabel,
                chunk.NormalizedText))
            .ToListAsync(cancellationToken);

        if (neighborCandidates.Count == 0)
            return evidence;

        var selectedChunkIds = evidence.Select(item => item.ChunkId).ToHashSet();
        var centersById = centerChunks.ToDictionary(chunk => chunk.Id);

        return evidence
            .Select(item =>
            {
                if (item.Excerpt.Length >= NeighborExpansionLengthThreshold)
                    return item;

                var merged = MergeNeighborContextForChunk(
                    item.ChunkId,
                    item.Excerpt,
                    selectedChunkIds,
                    centersById,
                    neighborCandidates);

                return ReferenceEquals(merged, item.Excerpt)
                    ? item
                    : item with { Excerpt = merged };
            })
            .ToArray();
    }

    internal static string MergeNeighborContextForChunk(
        Guid chunkId,
        string currentExcerpt,
        IReadOnlySet<Guid> selectedChunkIds,
        IReadOnlyDictionary<Guid, NeighborChunkSlice> centersById,
        IReadOnlyList<NeighborChunkSlice> neighborCandidates)
    {
        if (!centersById.TryGetValue(chunkId, out var center))
            return currentExcerpt;

        var previous = neighborCandidates.FirstOrDefault(candidate =>
            candidate.VersionId == center.VersionId &&
            candidate.Sequence == center.Sequence - 1 &&
            !selectedChunkIds.Contains(candidate.Id) &&
            string.Equals(candidate.SectionLabel, center.SectionLabel, StringComparison.OrdinalIgnoreCase));

        var next = neighborCandidates.FirstOrDefault(candidate =>
            candidate.VersionId == center.VersionId &&
            candidate.Sequence == center.Sequence + 1 &&
            !selectedChunkIds.Contains(candidate.Id) &&
            string.Equals(candidate.SectionLabel, center.SectionLabel, StringComparison.OrdinalIgnoreCase));

        if (previous is null && next is null)
            return currentExcerpt;

        return MergeNeighborContext(currentExcerpt, previous?.NormalizedText, next?.NormalizedText);
    }

    internal static string MergeNeighborContext(
        string currentExcerpt,
        string? previousText,
        string? nextText)
    {
        var center = currentExcerpt.Trim();
        if (center.Length >= MaximumExcerptCharacters)
            return center[..MaximumExcerptCharacters];

        var merged = center;
        if (!string.IsNullOrWhiteSpace(previousText))
        {
            var prev = previousText.Trim();
            if (!HasHighOverlap(merged, prev) && prev.Length + 1 + merged.Length <= MaximumExcerptCharacters)
                merged = $"{prev}\n{merged}";
        }

        if (!string.IsNullOrWhiteSpace(nextText))
        {
            var next = nextText.Trim();
            if (!HasHighOverlap(merged, next) && merged.Length + 1 < MaximumExcerptCharacters)
            {
                var remaining = MaximumExcerptCharacters - merged.Length - 1;
                var appended = next.Length <= remaining
                    ? next
                    : next[..remaining].TrimEnd();
                if (appended.Length > 0)
                    merged = $"{merged}\n{appended}";
            }
        }

        return merged.Length <= MaximumExcerptCharacters
            ? merged
            : merged[..MaximumExcerptCharacters];
    }

    internal static bool HasHighOverlap(string left, string right)
    {
        if (left.Contains(right, StringComparison.OrdinalIgnoreCase) ||
            right.Contains(left, StringComparison.OrdinalIgnoreCase))
            return true;
        var leftTokens = left.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var rightTokens = right.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (leftTokens.Count == 0 || rightTokens.Count == 0)
            return false;
        var intersection = leftTokens.Count(rightTokens.Contains);
        return intersection / (double)Math.Min(leftTokens.Count, rightTokens.Count) >= 0.8;
    }

    private static string Sha256(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
}
