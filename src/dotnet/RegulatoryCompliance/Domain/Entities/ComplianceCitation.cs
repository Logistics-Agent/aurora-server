using Shared.Entity;

namespace RegulatoryCompliance.Domain.Entities;

public sealed class ComplianceCitation : TenantAuditableEntity
{
    private ComplianceCitation() { }

    public Guid ComplianceFindingId { get; private set; }
    public Guid RegulatoryDocumentId { get; private set; }
    public Guid RegulatoryDocumentVersionId { get; private set; }
    public Guid RegulatoryChunkId { get; private set; }
    public string Authority { get; private set; } = string.Empty;
    public string Title { get; private set; } = string.Empty;
    public string CanonicalSourceUri { get; private set; } = string.Empty;
    public string VersionLabel { get; private set; } = string.Empty;
    public string? SectionLabel { get; private set; }
    public string? PageLabel { get; private set; }
    public DateTimeOffset EffectiveFrom { get; private set; }
    public DateTimeOffset? EffectiveTo { get; private set; }
    public string Excerpt { get; private set; } = string.Empty;
    public decimal RelevanceScore { get; private set; }

    internal static ComplianceCitation Create(
        Guid tenantId,
        Guid complianceFindingId,
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
        ComplianceValidation.RequiredId(tenantId, nameof(tenantId));
        ComplianceValidation.RequiredId(complianceFindingId, nameof(complianceFindingId));
        ComplianceValidation.RequiredId(regulatoryDocumentId, nameof(regulatoryDocumentId));
        ComplianceValidation.RequiredId(regulatoryDocumentVersionId, nameof(regulatoryDocumentVersionId));
        ComplianceValidation.RequiredId(regulatoryChunkId, nameof(regulatoryChunkId));
        ComplianceValidation.RequiredTimestamp(effectiveFrom, nameof(effectiveFrom));
        ComplianceValidation.RequiredTimestamp(createdAt, nameof(createdAt));
        if (effectiveTo.HasValue && effectiveTo <= effectiveFrom)
            throw new ArgumentOutOfRangeException(nameof(effectiveTo));

        return new ComplianceCitation
        {
            TenantId = tenantId,
            ComplianceFindingId = complianceFindingId,
            RegulatoryDocumentId = regulatoryDocumentId,
            RegulatoryDocumentVersionId = regulatoryDocumentVersionId,
            RegulatoryChunkId = regulatoryChunkId,
            Authority = ComplianceValidation.RequiredText(authority, nameof(authority), 200),
            Title = ComplianceValidation.RequiredText(title, nameof(title), 500),
            CanonicalSourceUri = ComplianceValidation.RequiredText(
                canonicalSourceUri, nameof(canonicalSourceUri), 1_000),
            VersionLabel = ComplianceValidation.RequiredText(versionLabel, nameof(versionLabel), 100),
            SectionLabel = ComplianceValidation.OptionalText(sectionLabel, nameof(sectionLabel), 200),
            PageLabel = ComplianceValidation.OptionalText(pageLabel, nameof(pageLabel), 50),
            EffectiveFrom = effectiveFrom,
            EffectiveTo = effectiveTo,
            Excerpt = ComplianceValidation.RequiredText(excerpt, nameof(excerpt), 4_000),
            RelevanceScore = ComplianceValidation.Confidence(relevanceScore, nameof(relevanceScore)),
            CreatedAt = createdAt
        };
    }
}
