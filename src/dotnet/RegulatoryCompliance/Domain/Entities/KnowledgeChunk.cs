using RegulatoryCompliance.Domain.Enums;
using Shared.Entity;

namespace RegulatoryCompliance.Domain.Entities;

public sealed class KnowledgeChunk : AuditableEntity
{
    private KnowledgeChunk() { }

    public Guid? TenantId { get; private set; }
    public Guid ScopeKey { get; private set; }
    public SourceVisibility Visibility { get; private set; }
    public Guid KnowledgeDocumentVersionId { get; private set; }
    public int Sequence { get; private set; }
    public string? SectionLabel { get; private set; }
    public string? PageLabel { get; private set; }
    public string NormalizedText { get; private set; } = string.Empty;
    public int TokenCount { get; private set; }
    public int CharacterCount { get; private set; }
    public int StartOffset { get; private set; }
    public int EndOffset { get; private set; }
    public string ContentSha256 { get; private set; } = string.Empty;
    public ChunkEmbeddingStatus EmbeddingStatus { get; private set; }
    public float[]? Embedding { get; private set; }
    public string? EmbeddingModel { get; private set; }
    public string? EmbeddingModelVersion { get; private set; }
    public string? EmbeddedContentHash { get; private set; }
    public DateTimeOffset? EmbeddedAt { get; private set; }
    public string? EmbeddingError { get; private set; }

    internal static KnowledgeChunk Create(
        Guid? tenantId,
        Guid scopeKey,
        SourceVisibility visibility,
        Guid knowledgeDocumentVersionId,
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
        ComplianceValidation.RequiredId(knowledgeDocumentVersionId, nameof(knowledgeDocumentVersionId));
        if (sequence <= 0)
            throw new ArgumentOutOfRangeException(nameof(sequence));
        if (tokenCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(tokenCount));
        if (startOffset < 0 || endOffset <= startOffset)
            throw new ArgumentOutOfRangeException(nameof(startOffset), "Chunk offsets are invalid.");
        ComplianceValidation.RequiredTimestamp(createdAt, nameof(createdAt));
        var text = ComplianceValidation.RequiredText(normalizedText, nameof(normalizedText), 20_000);

        return new KnowledgeChunk
        {
            TenantId = tenantId,
            ScopeKey = scopeKey,
            Visibility = visibility,
            KnowledgeDocumentVersionId = knowledgeDocumentVersionId,
            Sequence = sequence,
            SectionLabel = ComplianceValidation.OptionalText(sectionLabel, nameof(sectionLabel), 200),
            PageLabel = ComplianceValidation.OptionalText(pageLabel, nameof(pageLabel), 50),
            NormalizedText = text,
            TokenCount = tokenCount,
            CharacterCount = text.Length,
            StartOffset = startOffset,
            EndOffset = endOffset,
            ContentSha256 = ComplianceValidation.Sha256(contentSha256, nameof(contentSha256)),
            EmbeddingStatus = ChunkEmbeddingStatus.Pending,
            CreatedAt = createdAt
        };
    }

    public bool NeedsEmbedding(string modelName, string modelVersion) =>
        EmbeddingStatus != ChunkEmbeddingStatus.Completed ||
        EmbeddedContentHash != ContentSha256 ||
        EmbeddingModel != modelName ||
        EmbeddingModelVersion != modelVersion;

    public void MarkEmbedded(
        float[] embedding,
        string modelName,
        string modelVersion,
        int expectedDimension,
        DateTimeOffset embeddedAt)
    {
        ArgumentNullException.ThrowIfNull(embedding);
        if (embedding.Length != expectedDimension || embedding.Any(value => !float.IsFinite(value)))
            throw new ArgumentException("Embedding dimension or values are invalid.", nameof(embedding));
        ComplianceValidation.RequiredTimestamp(embeddedAt, nameof(embeddedAt));

        Embedding = [.. embedding];
        EmbeddingModel = ComplianceValidation.RequiredText(modelName, nameof(modelName), 200);
        EmbeddingModelVersion = ComplianceValidation.RequiredText(modelVersion, nameof(modelVersion), 100);
        EmbeddedContentHash = ContentSha256;
        EmbeddingStatus = ChunkEmbeddingStatus.Completed;
        EmbeddedAt = embeddedAt;
        EmbeddingError = null;
        UpdatedAt = embeddedAt;
    }

    public void MarkEmbeddingFailed(string error, DateTimeOffset failedAt)
    {
        ComplianceValidation.RequiredTimestamp(failedAt, nameof(failedAt));
        EmbeddingStatus = ChunkEmbeddingStatus.Failed;
        EmbeddingError = ComplianceValidation.RequiredText(error, nameof(error), 2_000);
        UpdatedAt = failedAt;
    }
}
