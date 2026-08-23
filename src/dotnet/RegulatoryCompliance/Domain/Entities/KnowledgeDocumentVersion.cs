using RegulatoryCompliance.Domain.Enums;
using Shared.Entity;

namespace RegulatoryCompliance.Domain.Entities;

public sealed class KnowledgeDocumentVersion : AuditableEntity
{
    private readonly List<KnowledgeChunk> _chunks = [];

    private KnowledgeDocumentVersion() { }

    public Guid? TenantId { get; private set; }
    public Guid ScopeKey { get; private set; }
    public SourceVisibility Visibility { get; private set; }
    public Guid KnowledgeDocumentId { get; private set; }
    public string IngestionKey { get; private set; } = string.Empty;
    public string VersionLabel { get; private set; } = string.Empty;
    public string ContentSha256 { get; private set; } = string.Empty;
    public string ContentReference { get; private set; } = string.Empty;
    public string FileName { get; private set; } = string.Empty;
    public string MimeType { get; private set; } = string.Empty;
    public long SizeBytes { get; private set; }
    public RegulatoryIngestionStatus IngestionStatus { get; private set; }
    public Guid? SupersedesVersionId { get; private set; }
    public DateTimeOffset? SupersededAt { get; private set; }
    public int ChunkCount { get; private set; }
    public string? ErrorCode { get; private set; }
    public string? ErrorMessage { get; private set; }
    public DateTimeOffset? ProcessingStartedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public DateTimeOffset? FailedAt { get; private set; }
    public IReadOnlyCollection<KnowledgeChunk> Chunks => _chunks.AsReadOnly();

    internal static KnowledgeDocumentVersion Create(
        Guid? tenantId,
        Guid scopeKey,
        SourceVisibility visibility,
        Guid knowledgeDocumentId,
        string ingestionKey,
        string versionLabel,
        string contentSha256,
        string contentReference,
        string fileName,
        string mimeType,
        long sizeBytes,
        DateTimeOffset createdAt,
        Guid? supersedesVersionId)
    {
        ComplianceValidation.RequiredId(knowledgeDocumentId, nameof(knowledgeDocumentId));
        ComplianceValidation.RequiredTimestamp(createdAt, nameof(createdAt));

        return new KnowledgeDocumentVersion
        {
            TenantId = tenantId,
            ScopeKey = scopeKey,
            Visibility = visibility,
            KnowledgeDocumentId = knowledgeDocumentId,
            IngestionKey = ComplianceValidation.RequiredText(ingestionKey, nameof(ingestionKey), 128),
            VersionLabel = ComplianceValidation.RequiredText(versionLabel, nameof(versionLabel), 100),
            ContentSha256 = ComplianceValidation.RequiredText(contentSha256, nameof(contentSha256), 128),
            ContentReference = ComplianceValidation.RequiredText(contentReference, nameof(contentReference), 1000),
            FileName = ComplianceValidation.RequiredText(fileName, nameof(fileName), 255),
            MimeType = ComplianceValidation.RequiredText(mimeType, nameof(mimeType), 100),
            SizeBytes = sizeBytes >= 0 ? sizeBytes : throw new ArgumentOutOfRangeException(nameof(sizeBytes)),
            IngestionStatus = RegulatoryIngestionStatus.Pending,
            SupersedesVersionId = supersedesVersionId
        };
    }

    public KnowledgeChunk AddChunk(
        int sequence,
        string? sectionLabel,
        string? pageLabel,
        string normalizedText,
        int tokenCount,
        int startOffset,
        int endOffset,
        string contentSha256,
        DateTimeOffset createdAt)
    {
        var chunk = KnowledgeChunk.Create(
            TenantId,
            ScopeKey,
            Visibility,
            Id,
            sequence,
            sectionLabel,
            pageLabel,
            normalizedText,
            tokenCount,
            startOffset,
            endOffset,
            contentSha256,
            createdAt);

        _chunks.Add(chunk);
        ChunkCount = _chunks.Count;
        return chunk;
    }

    public void MarkPendingOcr(DateTimeOffset startedAt)
    {
        IngestionStatus = RegulatoryIngestionStatus.PendingOcr;
        ProcessingStartedAt = startedAt;
    }

    public void ResumeIngestionFromOcr(DateTimeOffset timestamp)
    {
        IngestionStatus = RegulatoryIngestionStatus.Processing;
        UpdatedAt = timestamp;
    }

    public void MarkProcessing(DateTimeOffset startedAt)
    {
        IngestionStatus = RegulatoryIngestionStatus.Processing;
        ProcessingStartedAt = startedAt;
    }

    public void MarkCompleted(DateTimeOffset completedAt)
    {
        IngestionStatus = RegulatoryIngestionStatus.Completed;
        CompletedAt = completedAt;
        ErrorCode = null;
        ErrorMessage = null;
    }

    public void MarkFailed(string errorCode, string errorMessage, DateTimeOffset failedAt)
    {
        IngestionStatus = RegulatoryIngestionStatus.Failed;
        ErrorCode = ComplianceValidation.RequiredText(errorCode, nameof(errorCode), 100);
        ErrorMessage = ComplianceValidation.RequiredText(errorMessage, nameof(errorMessage), 2000);
        FailedAt = failedAt;
    }
}
