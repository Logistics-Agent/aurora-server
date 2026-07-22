using RegulatoryCompliance.Domain.Enums;
using Shared.Entity;

namespace RegulatoryCompliance.Domain.Entities;

public sealed class RegulatoryChunk : AuditableEntity
{
    private RegulatoryChunk() { }

    public Guid? TenantId { get; private set; }
    public Guid ScopeKey { get; private set; }
    public SourceVisibility Visibility { get; private set; }
    public Guid RegulatoryDocumentVersionId { get; private set; }
    public int Sequence { get; private set; }
    public string? SectionLabel { get; private set; }
    public string? PageLabel { get; private set; }
    public string NormalizedText { get; private set; } = string.Empty;
    public int TokenCount { get; private set; }
    public int CharacterCount { get; private set; }
    public string ContentSha256 { get; private set; } = string.Empty;

    internal static RegulatoryChunk Create(
        Guid? tenantId,
        Guid scopeKey,
        SourceVisibility visibility,
        Guid regulatoryDocumentVersionId,
        int sequence,
        string? sectionLabel,
        string? pageLabel,
        string normalizedText,
        int tokenCount,
        string contentSha256,
        DateTimeOffset createdAt)
    {
        ComplianceValidation.RequiredId(regulatoryDocumentVersionId, nameof(regulatoryDocumentVersionId));
        if (sequence <= 0)
            throw new ArgumentOutOfRangeException(nameof(sequence));
        if (tokenCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(tokenCount));
        ComplianceValidation.RequiredTimestamp(createdAt, nameof(createdAt));
        var text = ComplianceValidation.RequiredText(normalizedText, nameof(normalizedText), 20_000);

        return new RegulatoryChunk
        {
            TenantId = tenantId,
            ScopeKey = scopeKey,
            Visibility = visibility,
            RegulatoryDocumentVersionId = regulatoryDocumentVersionId,
            Sequence = sequence,
            SectionLabel = ComplianceValidation.OptionalText(sectionLabel, nameof(sectionLabel), 200),
            PageLabel = ComplianceValidation.OptionalText(pageLabel, nameof(pageLabel), 50),
            NormalizedText = text,
            TokenCount = tokenCount,
            CharacterCount = text.Length,
            ContentSha256 = ComplianceValidation.Sha256(contentSha256, nameof(contentSha256)),
            CreatedAt = createdAt
        };
    }
}
