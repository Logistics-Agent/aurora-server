using RegulatoryCompliance.Domain.Enums;
using Shared.Entity;

namespace RegulatoryCompliance.Domain.Entities;

public sealed class RegulatoryDocument : AuditableEntity
{
    private readonly List<RegulatoryDocumentVersion> _versions = [];

    private RegulatoryDocument() { }

    public Guid? TenantId { get; private set; }
    public Guid ScopeKey { get; private set; }
    public SourceVisibility Visibility { get; private set; }
    public string Authority { get; private set; } = string.Empty;
    public string Title { get; private set; } = string.Empty;
    public string CanonicalSourceUri { get; private set; } = string.Empty;
    public string JurisdictionCode { get; private set; } = string.Empty;
    public RegulationType RegulationType { get; private set; }
    public string LanguageCode { get; private set; } = string.Empty;
    public IReadOnlyCollection<RegulatoryDocumentVersion> Versions => _versions.AsReadOnly();

    public static RegulatoryDocument CreatePlatform(
        string authority,
        string title,
        string canonicalSourceUri,
        string jurisdictionCode,
        RegulationType regulationType,
        string languageCode,
        DateTimeOffset createdAt) =>
        Create(null, SourceVisibility.Platform, authority, title, canonicalSourceUri,
            jurisdictionCode, regulationType, languageCode, createdAt);

    public static RegulatoryDocument CreateTenant(
        Guid tenantId,
        string authority,
        string title,
        string canonicalSourceUri,
        string jurisdictionCode,
        RegulationType regulationType,
        string languageCode,
        DateTimeOffset createdAt) =>
        Create(ComplianceValidation.RequiredId(tenantId, nameof(tenantId)), SourceVisibility.Tenant,
            authority, title, canonicalSourceUri, jurisdictionCode, regulationType, languageCode, createdAt);

    public RegulatoryDocumentVersion AddVersion(
        string ingestionKey,
        string versionLabel,
        DateTimeOffset publishedAt,
        DateTimeOffset effectiveFrom,
        DateTimeOffset? effectiveTo,
        string contentSha256,
        string contentReference,
        string fileName,
        string mimeType,
        long sizeBytes,
        DateTimeOffset createdAt,
        Guid? supersedesVersionId = null)
    {
        if (effectiveTo.HasValue && effectiveTo <= effectiveFrom)
            throw new ArgumentOutOfRangeException(nameof(effectiveTo), "EffectiveTo must follow EffectiveFrom.");
        if (sizeBytes <= 0)
            throw new ArgumentOutOfRangeException(nameof(sizeBytes), "SizeBytes must be positive.");

        var normalizedIngestionKey = ComplianceValidation.RequiredText(
            ingestionKey, nameof(ingestionKey), 150);
        var normalizedLabel = ComplianceValidation.RequiredText(versionLabel, nameof(versionLabel), 100);
        var normalizedHash = ComplianceValidation.Sha256(contentSha256, nameof(contentSha256));
        if (_versions.Any(version => version.IngestionKey == normalizedIngestionKey))
            throw new InvalidOperationException("The ingestion key already exists for this document.");
        if (_versions.Any(version => version.VersionLabel == normalizedLabel))
            throw new InvalidOperationException("The version label already exists for this document.");
        if (_versions.Any(version => version.ContentSha256 == normalizedHash))
            throw new InvalidOperationException("The source content has already been registered.");
        var supersededVersion = supersedesVersionId.HasValue
            ? _versions.SingleOrDefault(version => version.Id == supersedesVersionId.Value)
            : null;
        if (supersedesVersionId.HasValue && supersededVersion is null)
            throw new InvalidOperationException("A version can only supersede an existing version of this document.");

        var version = RegulatoryDocumentVersion.Create(
            TenantId,
            ScopeKey,
            Visibility,
            Id,
            normalizedIngestionKey,
            normalizedLabel,
            publishedAt,
            effectiveFrom,
            effectiveTo,
            normalizedHash,
            contentReference,
            fileName,
            mimeType,
            sizeBytes,
            createdAt,
            supersedesVersionId);
        supersededVersion?.MarkSuperseded(createdAt);
        _versions.Add(version);
        return version;
    }

    private static RegulatoryDocument Create(
        Guid? tenantId,
        SourceVisibility visibility,
        string authority,
        string title,
        string canonicalSourceUri,
        string jurisdictionCode,
        RegulationType regulationType,
        string languageCode,
        DateTimeOffset createdAt)
    {
        if (!Enum.IsDefined(visibility))
            throw new ArgumentOutOfRangeException(nameof(visibility));
        if (!Enum.IsDefined(regulationType))
            throw new ArgumentOutOfRangeException(nameof(regulationType));
        ComplianceValidation.RequiredTimestamp(createdAt, nameof(createdAt));

        return new RegulatoryDocument
        {
            TenantId = tenantId,
            ScopeKey = tenantId ?? Guid.Empty,
            Visibility = visibility,
            Authority = ComplianceValidation.RequiredText(authority, nameof(authority), 200),
            Title = ComplianceValidation.RequiredText(title, nameof(title), 500),
            CanonicalSourceUri = ComplianceValidation.RequiredText(
                canonicalSourceUri, nameof(canonicalSourceUri), 1_000),
            JurisdictionCode = ComplianceValidation.RequiredText(
                jurisdictionCode, nameof(jurisdictionCode), 30).ToUpperInvariant(),
            RegulationType = regulationType,
            LanguageCode = ComplianceValidation.RequiredText(
                languageCode, nameof(languageCode), 15).ToLowerInvariant(),
            CreatedAt = createdAt
        };
    }
}
