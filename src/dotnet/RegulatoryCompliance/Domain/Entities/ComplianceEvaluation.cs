using System.Text.Json;
using RegulatoryCompliance.Domain.Enums;
using Shared.Entity;

namespace RegulatoryCompliance.Domain.Entities;

public sealed class ComplianceEvaluation : TenantAuditableEntity
{
    private readonly List<ComplianceFinding> _findings = [];
    private readonly List<RetrievalTrace> _retrievalTraces = [];

    private ComplianceEvaluation() { }

    public string IdempotencyKey { get; private set; } = string.Empty;
    public Guid ExternalShipmentId { get; private set; }
    public string RequestHash { get; private set; } = string.Empty;
    public string RequestSnapshotJson { get; private set; } = string.Empty;
    public ComplianceEvaluationStatus Status { get; private set; }
    public ComplianceRiskLevel? RiskLevel { get; private set; }
    public EvidenceSufficiency? EvidenceSufficiency { get; private set; }
    public decimal? Confidence { get; private set; }
    public string AssumptionsJson { get; private set; } = "[]";
    public string MissingDocumentsJson { get; private set; } = "[]";
    public DateTimeOffset EffectiveAt { get; private set; }
    public DateTimeOffset RequestedAt { get; private set; }
    public DateTimeOffset? ProcessingStartedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public DateTimeOffset? FailedAt { get; private set; }
    public string? ErrorCode { get; private set; }
    public string? ErrorMessage { get; private set; }
    public IReadOnlyCollection<ComplianceFinding> Findings => _findings.AsReadOnly();
    public IReadOnlyCollection<RetrievalTrace> RetrievalTraces => _retrievalTraces.AsReadOnly();

    public static ComplianceEvaluation Create(
        Guid tenantId,
        string idempotencyKey,
        Guid externalShipmentId,
        string requestHash,
        string requestSnapshotJson,
        DateTimeOffset effectiveAt,
        DateTimeOffset requestedAt)
    {
        ComplianceValidation.RequiredId(tenantId, nameof(tenantId));
        ComplianceValidation.RequiredId(externalShipmentId, nameof(externalShipmentId));
        ComplianceValidation.RequiredTimestamp(effectiveAt, nameof(effectiveAt));
        ComplianceValidation.RequiredTimestamp(requestedAt, nameof(requestedAt));

        return new ComplianceEvaluation
        {
            TenantId = tenantId,
            IdempotencyKey = ComplianceValidation.RequiredText(idempotencyKey, nameof(idempotencyKey), 150),
            ExternalShipmentId = externalShipmentId,
            RequestHash = ComplianceValidation.Sha256(requestHash, nameof(requestHash)),
            RequestSnapshotJson = ComplianceValidation.Json(
                requestSnapshotJson, nameof(requestSnapshotJson), 500_000),
            Status = ComplianceEvaluationStatus.Pending,
            EffectiveAt = effectiveAt,
            RequestedAt = requestedAt,
            CreatedAt = requestedAt
        };
    }

    public void Start(DateTimeOffset startedAt)
    {
        EnsureStatus(ComplianceEvaluationStatus.Pending);
        ComplianceValidation.RequiredTimestamp(startedAt, nameof(startedAt));
        Status = ComplianceEvaluationStatus.Processing;
        ProcessingStartedAt = startedAt;
        UpdatedAt = startedAt;
    }

    public ComplianceFinding AddFinding(
        ComplianceFindingType type,
        string code,
        string category,
        string title,
        string description,
        ComplianceRiskLevel severity,
        DateTimeOffset createdAt)
    {
        EnsureStatus(ComplianceEvaluationStatus.Processing);
        var finding = ComplianceFinding.Create(
            TenantId, Id, type, code, category, title, description, severity, createdAt);
        _findings.Add(finding);
        return finding;
    }

    public RetrievalTrace RecordRetrieval(
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
        EnsureStatus(ComplianceEvaluationStatus.Processing);
        var trace = RetrievalTrace.Create(
            TenantId,
            Id,
            queryHash,
            jurisdictionCode,
            effectiveAt,
            languageCode,
            regulationTypesJson,
            embeddingModel,
            topK,
            minimumRelevanceScore,
            retrievedChunkIdsJson,
            scoresJson,
            evidenceSufficiency,
            createdAt);
        _retrievalTraces.Add(trace);
        return trace;
    }

    public void Complete(
        ComplianceRiskLevel riskLevel,
        EvidenceSufficiency evidenceSufficiency,
        decimal confidence,
        IReadOnlyCollection<string> assumptions,
        IReadOnlyCollection<string> missingDocuments,
        DateTimeOffset completedAt)
    {
        EnsureStatus(ComplianceEvaluationStatus.Processing);
        if (!Enum.IsDefined(riskLevel))
            throw new ArgumentOutOfRangeException(nameof(riskLevel));
        if (!Enum.IsDefined(evidenceSufficiency))
            throw new ArgumentOutOfRangeException(nameof(evidenceSufficiency));
        if (_findings.Any(finding => finding.Citations.Count == 0))
            throw new InvalidOperationException("Every compliance finding must include at least one citation.");
        ComplianceValidation.RequiredTimestamp(completedAt, nameof(completedAt));
        var validatedConfidence = ComplianceValidation.Confidence(confidence, nameof(confidence));
        var assumptionsJson = SerializeStrings(assumptions, nameof(assumptions));
        var missingDocumentsJson = SerializeStrings(missingDocuments, nameof(missingDocuments));

        RiskLevel = riskLevel;
        EvidenceSufficiency = evidenceSufficiency;
        Confidence = validatedConfidence;
        AssumptionsJson = assumptionsJson;
        MissingDocumentsJson = missingDocumentsJson;
        Status = ComplianceEvaluationStatus.Completed;
        CompletedAt = completedAt;
        ErrorCode = null;
        ErrorMessage = null;
        UpdatedAt = completedAt;
    }

    public void Fail(string errorCode, string errorMessage, DateTimeOffset failedAt)
    {
        EnsureStatus(ComplianceEvaluationStatus.Processing);
        ComplianceValidation.RequiredTimestamp(failedAt, nameof(failedAt));
        Status = ComplianceEvaluationStatus.Failed;
        RiskLevel = ComplianceRiskLevel.Unknown;
        EvidenceSufficiency = Enums.EvidenceSufficiency.Insufficient;
        Confidence = 0m;
        ErrorCode = ComplianceValidation.RequiredText(errorCode, nameof(errorCode), 100);
        ErrorMessage = ComplianceValidation.RequiredText(errorMessage, nameof(errorMessage), 2_000);
        FailedAt = failedAt;
        UpdatedAt = failedAt;
    }

    private static string SerializeStrings(IReadOnlyCollection<string> values, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(values, parameterName);
        var normalized = values
            .Select(value => ComplianceValidation.RequiredText(value, parameterName, 500))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return JsonSerializer.Serialize(normalized);
    }

    private void EnsureStatus(ComplianceEvaluationStatus required)
    {
        if (Status != required)
            throw new InvalidOperationException($"Evaluation must be {required} but is {Status}.");
    }
}
