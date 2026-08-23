using RegulatoryCompliance.Domain.Enums;
using Shared.Entity;

namespace RegulatoryCompliance.Domain.Entities;

public sealed class KnowledgeDocument : AuditableEntity
{
    private readonly List<KnowledgeDocumentVersion> _versions = [];

    private KnowledgeDocument() { }

    public Guid? TenantId { get; private set; }
    public Guid ScopeKey { get; private set; }
    public SourceVisibility Visibility { get; private set; }
    public KnowledgeCategory Category { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string SourceReference { get; private set; } = string.Empty;
    public string LanguageCode { get; private set; } = string.Empty;
    public IReadOnlyCollection<KnowledgeDocumentVersion> Versions => _versions.AsReadOnly();

    public static KnowledgeDocument CreatePlatform(
        KnowledgeCategory category,
        string title,
        string sourceReference,
        string languageCode,
        DateTimeOffset createdAt) =>
        Create(null, SourceVisibility.Platform, category, title, sourceReference, languageCode, createdAt);

    public static KnowledgeDocument CreateTenant(
        Guid tenantId,
        KnowledgeCategory category,
        string title,
        string sourceReference,
        string languageCode,
        DateTimeOffset createdAt) =>
        Create(ComplianceValidation.RequiredId(tenantId, nameof(tenantId)), SourceVisibility.Tenant,
            category, title, sourceReference, languageCode, createdAt);

    public KnowledgeDocumentVersion AddVersion(
        string ingestionKey,
        string versionLabel,
        string contentSha256,
        string contentReference,
        string fileName,
        string mimeType,
        long sizeBytes,
        DateTimeOffset createdAt,
        Guid? supersedesVersionId = null)
    {
        var version = KnowledgeDocumentVersion.Create(
            TenantId,
            ScopeKey,
            Visibility,
            Id,
            ingestionKey,
            versionLabel,
            contentSha256,
            contentReference,
            fileName,
            mimeType,
            sizeBytes,
            createdAt,
            supersedesVersionId);

        _versions.Add(version);
        return version;
    }

    private static KnowledgeDocument Create(
        Guid? tenantId,
        SourceVisibility visibility,
        KnowledgeCategory category,
        string title,
        string sourceReference,
        string languageCode,
        DateTimeOffset createdAt)
    {
        ComplianceValidation.RequiredTimestamp(createdAt, nameof(createdAt));
        var validTitle = ComplianceValidation.RequiredText(title, nameof(title), 500);
        var validSourceRef = ComplianceValidation.RequiredText(sourceReference, nameof(sourceReference), 1000);
        var validLang = ComplianceValidation.RequiredText(languageCode, nameof(languageCode), 20);

        return new KnowledgeDocument
        {
            TenantId = tenantId,
            ScopeKey = tenantId ?? Guid.Empty,
            Visibility = visibility,
            Category = category,
            Title = validTitle,
            SourceReference = validSourceRef,
            LanguageCode = validLang
        };
    }
}
