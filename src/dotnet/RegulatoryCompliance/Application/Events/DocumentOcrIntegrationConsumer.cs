using System.Security.Cryptography;
using System.Text;
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
    ILogger<DocumentOcrIntegrationConsumer>? logger = null)
{
    private readonly ILogger<DocumentOcrIntegrationConsumer> _logger = logger ?? NullLogger<DocumentOcrIntegrationConsumer>.Instance;

    public async Task HandleAsync(DocumentOcrCompletedEvent message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        _logger.LogInformation("Processing DocumentOcrCompletedEvent for JobId: {JobId}, Context: {Context}",
            message.JobId, message.ExternalContextId);

        var now = timeProvider.GetUtcNow();

        // 1. Check if this is a Knowledge Document
        if (message.ExternalContextId != null && message.ExternalContextId.StartsWith("knowledge:", StringComparison.OrdinalIgnoreCase))
        {
            var versionIdStr = message.ExternalContextId["knowledge:".Length..];
            if (Guid.TryParse(versionIdStr, out var versionId))
            {
                await ProcessKnowledgeDocumentOcrAsync(versionId, message, now, cancellationToken);
                return;
            }
        }

        // 2. Check if this is a Regulatory Document
        if (message.ExternalContextId != null && message.ExternalContextId.StartsWith("regulatory:", StringComparison.OrdinalIgnoreCase))
        {
            var versionIdStr = message.ExternalContextId["regulatory:".Length..];
            if (Guid.TryParse(versionIdStr, out var versionId))
            {
                await ProcessRegulatoryDocumentOcrAsync(versionId, message, now, cancellationToken);
                return;
            }
        }
    }

    public async Task HandleAsync(DocumentOcrFailedEvent message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        _logger.LogWarning("Processing DocumentOcrFailedEvent for JobId: {JobId}, Error: {Error}",
            message.JobId, message.ErrorMessage);

        var now = timeProvider.GetUtcNow();

        if (message.ExternalContextId != null && message.ExternalContextId.StartsWith("knowledge:", StringComparison.OrdinalIgnoreCase))
        {
            var versionIdStr = message.ExternalContextId["knowledge:".Length..];
            if (Guid.TryParse(versionIdStr, out var versionId))
            {
                var version = await dbContext.KnowledgeDocumentVersions
                    .SingleOrDefaultAsync(v => v.Id == versionId, cancellationToken);
                if (version != null && version.IngestionStatus == RegulatoryIngestionStatus.PendingOcr)
                {
                    version.MarkFailed(message.ErrorCode, message.ErrorMessage, now);
                    await dbContext.SaveChangesAsync(cancellationToken);
                }
            }
        }
        else if (message.ExternalContextId != null && message.ExternalContextId.StartsWith("regulatory:", StringComparison.OrdinalIgnoreCase))
        {
            var versionIdStr = message.ExternalContextId["regulatory:".Length..];
            if (Guid.TryParse(versionIdStr, out var versionId))
            {
                var version = await dbContext.RegulatoryDocumentVersions
                    .SingleOrDefaultAsync(v => v.Id == versionId, cancellationToken);
                if (version != null && version.IngestionStatus == RegulatoryIngestionStatus.PendingOcr)
                {
                    version.FailIngestion(message.ErrorCode, message.ErrorMessage, now);
                    await dbContext.SaveChangesAsync(cancellationToken);
                }
            }
        }
    }

    private async Task ProcessKnowledgeDocumentOcrAsync(
        Guid versionId,
        DocumentOcrCompletedEvent message,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var version = await dbContext.KnowledgeDocumentVersions
            .Include(v => v.Chunks)
            .SingleOrDefaultAsync(v => v.Id == versionId, cancellationToken);

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
            .Include(v => v.Chunks)
            .SingleOrDefaultAsync(v => v.Id == versionId, cancellationToken);

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

        return message.NormalizedJson;
    }
}
