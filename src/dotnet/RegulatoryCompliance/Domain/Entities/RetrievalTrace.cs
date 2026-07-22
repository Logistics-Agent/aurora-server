using RegulatoryCompliance.Domain.Enums;
using Shared.Entity;

namespace RegulatoryCompliance.Domain.Entities;

public sealed class RetrievalTrace : TenantAuditableEntity
{
    private RetrievalTrace() { }

    public Guid? ComplianceEvaluationId { get; private set; }
    public string QueryHash { get; private set; } = string.Empty;
    public string JurisdictionCode { get; private set; } = string.Empty;
    public DateTimeOffset EffectiveAt { get; private set; }
    public string LanguageCode { get; private set; } = string.Empty;
    public string RegulationTypesJson { get; private set; } = "[]";
    public string EmbeddingModel { get; private set; } = string.Empty;
    public int TopK { get; private set; }
    public decimal MinimumRelevanceScore { get; private set; }
    public string RetrievedChunkIdsJson { get; private set; } = "[]";
    public string ScoresJson { get; private set; } = "[]";
    public EvidenceSufficiency EvidenceSufficiency { get; private set; }

    internal static RetrievalTrace Create(
        Guid tenantId,
        Guid? complianceEvaluationId,
        string queryHash,
        string jurisdictionCode,
        DateTimeOffset effectiveAt,
        string languageCode,
        string regulationTypesJson,
        string embeddingModel,
        int topK,
        decimal minimumRelevanceScore,
        string retrievedChunkIdsJson,
        string scoresJson,
        EvidenceSufficiency evidenceSufficiency,
        DateTimeOffset createdAt)
    {
        ComplianceValidation.RequiredId(tenantId, nameof(tenantId));
        if (complianceEvaluationId.HasValue)
            ComplianceValidation.RequiredId(complianceEvaluationId.Value, nameof(complianceEvaluationId));
        if (topK is < 1 or > 100)
            throw new ArgumentOutOfRangeException(nameof(topK));
        if (!Enum.IsDefined(evidenceSufficiency))
            throw new ArgumentOutOfRangeException(nameof(evidenceSufficiency));
        ComplianceValidation.RequiredTimestamp(effectiveAt, nameof(effectiveAt));
        ComplianceValidation.RequiredTimestamp(createdAt, nameof(createdAt));

        return new RetrievalTrace
        {
            TenantId = tenantId,
            ComplianceEvaluationId = complianceEvaluationId,
            QueryHash = ComplianceValidation.Sha256(queryHash, nameof(queryHash)),
            JurisdictionCode = ComplianceValidation.RequiredText(
                jurisdictionCode, nameof(jurisdictionCode), 30).ToUpperInvariant(),
            EffectiveAt = effectiveAt,
            LanguageCode = ComplianceValidation.RequiredText(
                languageCode, nameof(languageCode), 15).ToLowerInvariant(),
            RegulationTypesJson = ComplianceValidation.Json(
                regulationTypesJson, nameof(regulationTypesJson), 5_000),
            EmbeddingModel = ComplianceValidation.RequiredText(
                embeddingModel, nameof(embeddingModel), 200),
            TopK = topK,
            MinimumRelevanceScore = ComplianceValidation.Confidence(
                minimumRelevanceScore, nameof(minimumRelevanceScore)),
            RetrievedChunkIdsJson = ComplianceValidation.Json(
                retrievedChunkIdsJson, nameof(retrievedChunkIdsJson), 50_000),
            ScoresJson = ComplianceValidation.Json(scoresJson, nameof(scoresJson), 50_000),
            EvidenceSufficiency = evidenceSufficiency,
            CreatedAt = createdAt
        };
    }
}
