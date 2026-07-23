using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RegulatoryCompliance.Application.Embeddings;
using RegulatoryCompliance.Domain.Entities;
using RegulatoryCompliance.Domain.Enums;
using RegulatoryCompliance.Infrastructure.Persistences;
using Shared.Security;

namespace RegulatoryCompliance.Application.Retrieval;

public sealed record RegulationQueryInput(
    string Query,
    string JurisdictionCode,
    DateTimeOffset EffectiveAt,
    string LanguageCode,
    IReadOnlyCollection<RegulationType> RegulationTypes,
    int TopK,
    decimal MinimumRelevanceScore);

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
    TimeProvider timeProvider) : IRegulationRetrievalService
{
    public const int MaximumTopK = 20;
    private const int MaximumCandidateChunks = 2_000;
    private const int MaximumExcerptCharacters = 800;

    public async Task<RegulationQueryResult> QueryAsync(
        RegulationQueryInput input,
        CancellationToken cancellationToken = default)
    {
        Validate(input);
        var tenantId = currentUser.TenantId!.Value;
        var types = input.RegulationTypes.Distinct().ToArray();
        var jurisdiction = input.JurisdictionCode.Trim().ToUpperInvariant();
        var language = input.LanguageCode.Trim().ToLowerInvariant();

        var candidateIds = await (
                from chunk in dbContext.RegulatoryChunks.AsNoTracking()
                join version in dbContext.RegulatoryDocumentVersions.AsNoTracking()
                    on chunk.RegulatoryDocumentVersionId equals version.Id
                join document in dbContext.RegulatoryDocuments.AsNoTracking()
                    on version.RegulatoryDocumentId equals document.Id
                where version.IngestionStatus == RegulatoryIngestionStatus.Completed
                      && version.SupersededAt == null
                      && version.EffectiveFrom <= input.EffectiveAt
                      && (version.EffectiveTo == null || input.EffectiveAt < version.EffectiveTo)
                      && (document.JurisdictionCode == jurisdiction ||
                          document.JurisdictionCode == "GLOBAL")
                      && document.LanguageCode == language
                      && types.Contains(document.RegulationType)
                orderby chunk.Id
                select chunk.Id)
            .Take(MaximumCandidateChunks)
            .ToArrayAsync(cancellationToken);

        var queryVector = (await embeddingProvider.GenerateAsync(
            [input.Query.Trim()], cancellationToken))[0];
        var ranked = await vectorStore.SearchAsync(
            new VectorSearchRequest(
                queryVector,
                embeddingProvider.Model.Name,
                embeddingProvider.Model.Version,
                embeddingProvider.Model.Dimension,
                candidateIds,
                input.TopK * 2,
                input.MinimumRelevanceScore),
            cancellationToken);
        var selected = ranked
            .OrderByDescending(item => item.Score)
            .ThenBy(item => item.ChunkId)
            .Take(input.TopK * 2)
            .ToArray();
        var selectedIds = selected.Select(item => item.ChunkId).ToArray();

        var metadata = await (
                from chunk in dbContext.RegulatoryChunks.AsNoTracking()
                join version in dbContext.RegulatoryDocumentVersions.AsNoTracking()
                    on chunk.RegulatoryDocumentVersionId equals version.Id
                join document in dbContext.RegulatoryDocuments.AsNoTracking()
                    on version.RegulatoryDocumentId equals document.Id
                where selectedIds.Contains(chunk.Id)
                select new { Chunk = chunk, Version = version, Document = document })
            .ToDictionaryAsync(item => item.Chunk.Id, cancellationToken);
        var evidence = Deduplicate(selected.Select(item =>
        {
            var source = metadata[item.ChunkId];
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
                item.Score);
        })).Take(input.TopK).ToArray();
        var sufficiency = evidence.Length == 0
            ? EvidenceSufficiency.Insufficient
            : EvidenceSufficiency.Sufficient;
        var now = timeProvider.GetUtcNow();
        var trace = RetrievalTrace.Create(
            tenantId,
            null,
            Sha256(input.Query.Trim()),
            jurisdiction,
            input.EffectiveAt,
            language,
            JsonSerializer.Serialize(types.Select(type => type.ToString())),
            $"{embeddingProvider.Model.Name}:{embeddingProvider.Model.Version}",
            input.TopK,
            input.MinimumRelevanceScore,
            JsonSerializer.Serialize(evidence.Select(item => item.ChunkId)),
            JsonSerializer.Serialize(evidence.Select(item => item.RelevanceScore)),
            sufficiency,
            now);
        dbContext.RetrievalTraces.Add(trace);
        await dbContext.SaveChangesAsync(cancellationToken);

        var explanation = evidence.Length == 0
            ? "Insufficient regulatory evidence was found for the supplied filters."
            : $"Retrieved {evidence.Length} evidence passage(s). Conclusions must be limited to the cited source text.";
        return new RegulationQueryResult(trace.Id, sufficiency, evidence, explanation);
    }

    private void Validate(RegulationQueryInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (!currentUser.TenantId.HasValue || currentUser.TenantId == Guid.Empty)
            throw new InvalidOperationException("Tenant context is required.");
        if (string.IsNullOrWhiteSpace(input.Query) || input.Query.Trim().Length > 2_000)
            throw new ArgumentException("Query must contain 1-2,000 characters.", nameof(input.Query));
        if (string.IsNullOrWhiteSpace(input.JurisdictionCode) ||
            input.JurisdictionCode.Trim().Length > 30)
            throw new ArgumentException("JurisdictionCode is required.", nameof(input.JurisdictionCode));
        if (input.EffectiveAt == default)
            throw new ArgumentException("EffectiveAt is required.", nameof(input.EffectiveAt));
        if (string.IsNullOrWhiteSpace(input.LanguageCode) || input.LanguageCode.Trim().Length > 15)
            throw new ArgumentException("LanguageCode is required.", nameof(input.LanguageCode));
        if (input.RegulationTypes.Count == 0 ||
            input.RegulationTypes.Any(type => !Enum.IsDefined(type)))
            throw new ArgumentException("At least one valid RegulationType is required.", nameof(input.RegulationTypes));
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

    private static bool HasHighOverlap(string left, string right)
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
