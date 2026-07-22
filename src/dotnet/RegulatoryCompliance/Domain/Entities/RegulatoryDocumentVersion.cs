using RegulatoryCompliance.Domain.Enums;
using Shared.Entity;

namespace RegulatoryCompliance.Domain.Entities;

public sealed class RegulatoryDocumentVersion : AuditableEntity
{
    private readonly List<RegulatoryChunk> _chunks = [];

    private RegulatoryDocumentVersion() { }

    public Guid? TenantId { get; private set; }
    public Guid ScopeKey { get; private set; }
    public SourceVisibility Visibility { get; private set; }
    public Guid RegulatoryDocumentId { get; private set; }
    public string IngestionKey { get; private set; } = string.Empty;
    public string VersionLabel { get; private set; } = string.Empty;
    public DateTimeOffset PublishedAt { get; private set; }
    public DateTimeOffset EffectiveFrom { get; private set; }
    public DateTimeOffset? EffectiveTo { get; private set; }
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
    public IReadOnlyCollection<RegulatoryChunk> Chunks => _chunks.AsReadOnly();

    internal static RegulatoryDocumentVersion Create(
        Guid? tenantId,
        Guid scopeKey,
        SourceVisibility visibility,
        Guid regulatoryDocumentId,
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
        Guid? supersedesVersionId)
    {
        ComplianceValidation.RequiredId(regulatoryDocumentId, nameof(regulatoryDocumentId));
        ComplianceValidation.RequiredTimestamp(publishedAt, nameof(publishedAt));
        ComplianceValidation.RequiredTimestamp(effectiveFrom, nameof(effectiveFrom));
        ComplianceValidation.RequiredTimestamp(createdAt, nameof(createdAt));

        return new RegulatoryDocumentVersion
        {
            TenantId = tenantId,
            ScopeKey = scopeKey,
            Visibility = visibility,
            RegulatoryDocumentId = regulatoryDocumentId,
            IngestionKey = ingestionKey,
            VersionLabel = versionLabel,
            PublishedAt = publishedAt,
            EffectiveFrom = effectiveFrom,
            EffectiveTo = effectiveTo,
            ContentSha256 = contentSha256,
            ContentReference = ComplianceValidation.RequiredText(
                contentReference, nameof(contentReference), 1_000),
            FileName = ComplianceValidation.RequiredText(fileName, nameof(fileName), 255),
            MimeType = ComplianceValidation.RequiredText(mimeType, nameof(mimeType), 150).ToLowerInvariant(),
            SizeBytes = sizeBytes,
            IngestionStatus = RegulatoryIngestionStatus.Pending,
            SupersedesVersionId = supersedesVersionId,
            CreatedAt = createdAt
        };
    }

    public void StartIngestion(DateTimeOffset startedAt)
    {
        EnsureStatus(RegulatoryIngestionStatus.Pending);
        ComplianceValidation.RequiredTimestamp(startedAt, nameof(startedAt));
        IngestionStatus = RegulatoryIngestionStatus.Processing;
        ProcessingStartedAt = startedAt;
        UpdatedAt = startedAt;
    }

    public RegulatoryChunk AddChunk(
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
        EnsureStatus(RegulatoryIngestionStatus.Processing);
        var expectedSequence = _chunks.Count + 1;
        if (sequence != expectedSequence)
            throw new ArgumentOutOfRangeException(nameof(sequence), $"The next chunk sequence must be {expectedSequence}.");

        var chunk = RegulatoryChunk.Create(
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
        return chunk;
    }

    public void CompleteIngestion(DateTimeOffset completedAt)
    {
        EnsureStatus(RegulatoryIngestionStatus.Processing);
        ComplianceValidation.RequiredTimestamp(completedAt, nameof(completedAt));
        if (_chunks.Count == 0)
            throw new InvalidOperationException("At least one chunk is required to complete ingestion.");

        IngestionStatus = RegulatoryIngestionStatus.Completed;
        ChunkCount = _chunks.Count;
        CompletedAt = completedAt;
        ErrorCode = null;
        ErrorMessage = null;
        UpdatedAt = completedAt;
    }

    public void FailIngestion(string errorCode, string errorMessage, DateTimeOffset failedAt)
    {
        EnsureStatus(RegulatoryIngestionStatus.Processing);
        ComplianceValidation.RequiredTimestamp(failedAt, nameof(failedAt));
        IngestionStatus = RegulatoryIngestionStatus.Failed;
        ErrorCode = ComplianceValidation.RequiredText(errorCode, nameof(errorCode), 100);
        ErrorMessage = ComplianceValidation.RequiredText(errorMessage, nameof(errorMessage), 2_000);
        FailedAt = failedAt;
        UpdatedAt = failedAt;
    }

    internal void MarkSuperseded(DateTimeOffset supersededAt)
    {
        ComplianceValidation.RequiredTimestamp(supersededAt, nameof(supersededAt));
        if (SupersededAt.HasValue)
            throw new InvalidOperationException("The document version has already been superseded.");
        SupersededAt = supersededAt;
        UpdatedAt = supersededAt;
    }

    private void EnsureStatus(RegulatoryIngestionStatus required)
    {
        if (IngestionStatus != required)
            throw new InvalidOperationException($"Document version must be {required} but is {IngestionStatus}.");
    }
}
