using RegulatoryCompliance.Domain.Enums;
using Shared.Entity;

namespace RegulatoryCompliance.Domain.Entities;

public sealed class ComplianceFinding : TenantAuditableEntity
{
    private readonly List<ComplianceCitation> _citations = [];

    private ComplianceFinding() { }

    public Guid ComplianceEvaluationId { get; private set; }
    public ComplianceFindingType Type { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string Category { get; private set; } = string.Empty;
    public string Title { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public ComplianceRiskLevel Severity { get; private set; }
    public IReadOnlyCollection<ComplianceCitation> Citations => _citations.AsReadOnly();

    internal static ComplianceFinding Create(
        Guid tenantId,
        Guid complianceEvaluationId,
        ComplianceFindingType type,
        string code,
        string category,
        string title,
        string description,
        ComplianceRiskLevel severity,
        DateTimeOffset createdAt)
    {
        ComplianceValidation.RequiredId(tenantId, nameof(tenantId));
        ComplianceValidation.RequiredId(complianceEvaluationId, nameof(complianceEvaluationId));
        if (!Enum.IsDefined(type))
            throw new ArgumentOutOfRangeException(nameof(type));
        if (!Enum.IsDefined(severity))
            throw new ArgumentOutOfRangeException(nameof(severity));
        ComplianceValidation.RequiredTimestamp(createdAt, nameof(createdAt));

        return new ComplianceFinding
        {
            TenantId = tenantId,
            ComplianceEvaluationId = complianceEvaluationId,
            Type = type,
            Code = ComplianceValidation.RequiredText(code, nameof(code), 100),
            Category = ComplianceValidation.RequiredText(category, nameof(category), 100),
            Title = ComplianceValidation.RequiredText(title, nameof(title), 300),
            Description = ComplianceValidation.RequiredText(description, nameof(description), 4_000),
            Severity = severity,
            CreatedAt = createdAt
        };
    }

    public ComplianceCitation AddCitation(
        Guid regulatoryDocumentId,
        Guid regulatoryDocumentVersionId,
        Guid regulatoryChunkId,
        string authority,
        string title,
        string canonicalSourceUri,
        string versionLabel,
        string? sectionLabel,
        string? pageLabel,
        DateTimeOffset effectiveFrom,
        DateTimeOffset? effectiveTo,
        string excerpt,
        decimal relevanceScore,
        DateTimeOffset createdAt)
    {
        var citation = ComplianceCitation.Create(
            TenantId,
            Id,
            regulatoryDocumentId,
            regulatoryDocumentVersionId,
            regulatoryChunkId,
            authority,
            title,
            canonicalSourceUri,
            versionLabel,
            sectionLabel,
            pageLabel,
            effectiveFrom,
            effectiveTo,
            excerpt,
            relevanceScore,
            createdAt);
        if (_citations.Any(existing => existing.RegulatoryChunkId == regulatoryChunkId))
            throw new InvalidOperationException("The finding already cites this regulatory chunk.");
        _citations.Add(citation);
        return citation;
    }
}
