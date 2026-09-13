using System.Security.Cryptography;
using System.Text;
using MassTransit;
using DocumentOcr.Contracts.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using RegulatoryCompliance.Application.Embeddings;
using RegulatoryCompliance.Application.Ingestion;
using RegulatoryCompliance.Domain.Enums;
using RegulatoryCompliance.Infrastructure.Persistences;

namespace RegulatoryCompliance.Application.Events;

public sealed class DocumentOcrIntegrationConsumer(
    RegulatoryComplianceDbContext dbContext,
    IEmbeddingProvider embeddingProvider,
    IRegulatoryChunker chunker,
    TimeProvider timeProvider,
    ILogger<DocumentOcrIntegrationConsumer>? logger = null) :
    IConsumer<DocumentOcrCompletedEvent>,
    IConsumer<DocumentOcrFailedEvent>,
    IConsumer<DocumentOcrRequiresReviewEvent>
{
    private readonly ILogger<DocumentOcrIntegrationConsumer> _logger = logger ?? NullLogger<DocumentOcrIntegrationConsumer>.Instance;

    public Task Consume(ConsumeContext<DocumentOcrCompletedEvent> context) =>
        HandleAsync(context.Message, context.CancellationToken);

    public Task Consume(ConsumeContext<DocumentOcrFailedEvent> context) =>
        HandleAsync(context.Message, context.CancellationToken);

    public Task Consume(ConsumeContext<DocumentOcrRequiresReviewEvent> context) =>
        HandleAsync(context.Message, context.CancellationToken);

    public async Task HandleAsync(DocumentOcrCompletedEvent message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        DocumentOcrEventContract.ValidateVersion(nameof(DocumentOcrCompletedEvent), message.ContractVersion);
        _logger.LogInformation("Processing DocumentOcrCompletedEvent for JobId: {JobId}, Context: {Context}",
            message.JobId, message.ExternalContextId);

        var now = timeProvider.GetUtcNow();
        switch (message.Purpose)
        {
            case DocumentOcrPurpose.ShipmentDocument:
            case DocumentOcrPurpose.GeneralDocument:
                return;
            case DocumentOcrPurpose.KnowledgeCorpus:
                await ProcessKnowledgeDocumentOcrAsync(
                    DocumentOcrEventContract.ParseResourceId(message.ExternalContextId, nameof(DocumentOcrCompletedEvent)),
                    message,
                    now,
                    cancellationToken);
                return;
            case DocumentOcrPurpose.RegulatoryCorpus:
                await ProcessRegulatoryDocumentOcrAsync(
                    DocumentOcrEventContract.ParseResourceId(message.ExternalContextId, nameof(DocumentOcrCompletedEvent)),
                    message,
                    now,
                    cancellationToken);
                return;
            default:
                throw new NotSupportedException($"Unsupported Document OCR purpose '{message.Purpose}'.");
        }
    }

    public async Task HandleAsync(DocumentOcrFailedEvent message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        DocumentOcrEventContract.ValidateVersion(nameof(DocumentOcrFailedEvent), message.ContractVersion);
        _logger.LogWarning("Processing DocumentOcrFailedEvent for JobId: {JobId}, Error: {Error}",
            message.JobId, message.ErrorMessage);

        var now = timeProvider.GetUtcNow();

        switch (message.Purpose)
        {
            case DocumentOcrPurpose.ShipmentDocument:
            case DocumentOcrPurpose.GeneralDocument:
                return;
            case DocumentOcrPurpose.KnowledgeCorpus:
            {
                var versionId = DocumentOcrEventContract.ParseResourceId(
                    message.ExternalContextId, nameof(DocumentOcrFailedEvent));
                var version = await dbContext.KnowledgeDocumentVersions
                    .IgnoreQueryFilters()
                    .SingleOrDefaultAsync(v => v.TenantId == message.TenantId && v.Id == versionId, cancellationToken);
                if (version != null && version.IngestionStatus == RegulatoryIngestionStatus.PendingOcr)
                {
                    version.MarkFailed(message.ErrorCode, message.ErrorMessage, now);
                    await dbContext.SaveChangesAsync(cancellationToken);
                }
                return;
            }
            case DocumentOcrPurpose.RegulatoryCorpus:
            {
                var versionId = DocumentOcrEventContract.ParseResourceId(
                    message.ExternalContextId, nameof(DocumentOcrFailedEvent));
                var version = await dbContext.RegulatoryDocumentVersions
                    .IgnoreQueryFilters()
                    .SingleOrDefaultAsync(v => v.TenantId == message.TenantId && v.Id == versionId, cancellationToken);
                if (version != null && version.IngestionStatus == RegulatoryIngestionStatus.PendingOcr)
                {
                    version.FailIngestion(message.ErrorCode, message.ErrorMessage, now);
                    await dbContext.SaveChangesAsync(cancellationToken);
                }
                return;
            }
            default:
                throw new NotSupportedException($"Unsupported Document OCR purpose '{message.Purpose}'.");
        }
    }

    public Task HandleAsync(
        DocumentOcrRequiresReviewEvent message,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        DocumentOcrEventContract.ValidateVersion(nameof(DocumentOcrRequiresReviewEvent), message.ContractVersion);
        if (message.Purpose is not (DocumentOcrPurpose.ShipmentDocument or DocumentOcrPurpose.GeneralDocument or DocumentOcrPurpose.KnowledgeCorpus or DocumentOcrPurpose.RegulatoryCorpus))
            throw new NotSupportedException($"Unsupported Document OCR purpose '{message.Purpose}'.");

        _logger.LogInformation(
            "Document OCR requires review for JobId {JobId}; corpus ingestion waits for the reviewed completion event.",
            message.JobId);
        return Task.CompletedTask;
    }

    private async Task ProcessKnowledgeDocumentOcrAsync(
        Guid versionId,
        DocumentOcrCompletedEvent message,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var version = await dbContext.KnowledgeDocumentVersions
            .IgnoreQueryFilters()
            .Include(v => v.Chunks)
            .SingleOrDefaultAsync(v => v.TenantId == message.TenantId && v.Id == versionId, cancellationToken);

        if (version == null)
        {
            _logger.LogWarning("KnowledgeDocumentVersion {Id} not found for OCR resume.", versionId);
            return;
        }

        if (version.IngestionStatus == RegulatoryIngestionStatus.Completed)
        {
            _logger.LogInformation("KnowledgeDocumentVersion {Id} already completed.", versionId);
            return;
        }

        string fullText = await ResolveFullTextAsync(message, cancellationToken);
        if (string.IsNullOrWhiteSpace(fullText))
        {
            version.MarkFailed("EMPTY_OCR_TEXT", "OCR produced empty text content.", now);
            await dbContext.SaveChangesAsync(cancellationToken);
            return;
        }

        version.ResumeIngestionFromOcr(now);

        var drafts = chunker.Chunk(fullText);
        foreach (var draft in drafts)
        {
            var chunk = version.AddChunk(
                draft.Sequence,
                draft.SectionLabel,
                draft.PageLabel,
                draft.Text,
                draft.TokenCount,
                draft.StartOffset,
                draft.EndOffset,
                draft.ContentSha256,
                now);
            dbContext.KnowledgeChunks.Add(chunk);

            var embeddings = await embeddingProvider.GenerateAsync([chunk.NormalizedText], cancellationToken);
            if (embeddings.Count > 0)
            {
                chunk.MarkEmbedded(embeddings[0], embeddingProvider.Model.Name, embeddingProvider.Model.Version, embeddingProvider.Model.Dimension, now);
            }
        }

        version.MarkCompleted(now);
        await dbContext.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Successfully ingested {Count} chunks for KnowledgeDocumentVersion {Id}", drafts.Count, versionId);
    }

    private async Task ProcessRegulatoryDocumentOcrAsync(
        Guid versionId,
        DocumentOcrCompletedEvent message,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var version = await dbContext.RegulatoryDocumentVersions
            .IgnoreQueryFilters()
            .Include(v => v.Chunks)
            .SingleOrDefaultAsync(v => v.TenantId == message.TenantId && v.Id == versionId, cancellationToken);

        if (version == null)
        {
            _logger.LogWarning("RegulatoryDocumentVersion {Id} not found for OCR resume.", versionId);
            return;
        }

        if (version.IngestionStatus == RegulatoryIngestionStatus.Completed)
        {
            _logger.LogInformation("RegulatoryDocumentVersion {Id} already completed.", versionId);
            return;
        }

        string fullText = await ResolveFullTextAsync(message, cancellationToken);
        if (string.IsNullOrWhiteSpace(fullText))
        {
            version.FailIngestion("EMPTY_OCR_TEXT", "OCR produced empty text content.", now);
            await dbContext.SaveChangesAsync(cancellationToken);
            return;
        }

        version.ResumeIngestionFromOcr(now);

        var drafts = chunker.Chunk(fullText);
        foreach (var draft in drafts)
        {
            var chunk = version.AddChunk(
                draft.Sequence,
                draft.SectionLabel,
                draft.PageLabel,
                draft.Text,
                draft.TokenCount,
                draft.StartOffset,
                draft.EndOffset,
                draft.ContentSha256,
                now);
            dbContext.RegulatoryChunks.Add(chunk);

            var embeddings = await embeddingProvider.GenerateAsync([chunk.NormalizedText], cancellationToken);
            if (embeddings.Count > 0)
            {
                chunk.MarkEmbedded(embeddings[0], embeddingProvider.Model.Name, embeddingProvider.Model.Version, embeddingProvider.Model.Dimension, now);
            }
        }

        version.CompleteIngestion(now);
        await dbContext.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Successfully ingested {Count} chunks for RegulatoryDocumentVersion {Id}", drafts.Count, versionId);
    }

    private static async Task<string> ResolveFullTextAsync(DocumentOcrCompletedEvent message, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(message.FullTextContent))
            return message.FullTextContent;

        if (!string.IsNullOrWhiteSpace(message.ArtifactReference))
        {
            var relative = message.ArtifactReference.StartsWith("ocr-artifacts/", StringComparison.OrdinalIgnoreCase)
                ? message.ArtifactReference["ocr-artifacts/".Length..]
                : message.ArtifactReference;

            var fullPath = Path.Combine(AppContext.BaseDirectory, "storage", "artifacts", relative);
            if (File.Exists(fullPath))
            {
                return await File.ReadAllTextAsync(fullPath, Encoding.UTF8, cancellationToken);
            }
        }

        return string.Empty;
    }
}
