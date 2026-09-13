using DocumentOcr.Contracts.Events;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using RegulatoryCompliance.Domain.Enums;
using RegulatoryCompliance.Infrastructure.Persistences;

namespace RegulatoryCompliance.Application.Events;

public sealed class DocumentOcrIntegrationFaultConsumer(
    RegulatoryComplianceDbContext dbContext,
    TimeProvider timeProvider,
    ILogger<DocumentOcrIntegrationFaultConsumer>? logger = null) :
    IConsumer<Fault<DocumentOcrCompletedEvent>>
{
    private const string FailureCode = "CORPUS_PIPELINE_FAILED";
    private const int MaximumFailureMessageLength = 2_000;
    private readonly ILogger<DocumentOcrIntegrationFaultConsumer> _logger =
        logger ?? NullLogger<DocumentOcrIntegrationFaultConsumer>.Instance;

    public Task Consume(ConsumeContext<Fault<DocumentOcrCompletedEvent>> context)
    {
        var detail = context.Message.Exceptions.FirstOrDefault()?.Message
            ?? "Document OCR completion processing exhausted its retries.";
        return HandleAsync(context.Message.Message, detail, context.CancellationToken);
    }

    public async Task HandleAsync(
        DocumentOcrCompletedEvent message,
        string failureDetail,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        var errorMessage = BuildFailureMessage(failureDetail);
        var now = timeProvider.GetUtcNow();

        switch (message.Purpose)
        {
            case DocumentOcrPurpose.ShipmentDocument:
            case DocumentOcrPurpose.GeneralDocument:
                return;
            case DocumentOcrPurpose.KnowledgeCorpus:
                await MarkKnowledgeVersionFailedAsync(message, errorMessage, now, cancellationToken);
                return;
            case DocumentOcrPurpose.RegulatoryCorpus:
                await MarkRegulatoryVersionFailedAsync(message, errorMessage, now, cancellationToken);
                return;
            default:
                _logger.LogWarning(
                    "Ignoring fault for unsupported Document OCR purpose {Purpose} on JobId {JobId}.",
                    message.Purpose,
                    message.JobId);
                return;
        }
    }

    private async Task MarkKnowledgeVersionFailedAsync(
        DocumentOcrCompletedEvent message,
        string errorMessage,
        DateTimeOffset failedAt,
        CancellationToken cancellationToken)
    {
        var versionId = DocumentOcrEventContract.ParseResourceId(
            message.ExternalContextId, nameof(DocumentOcrCompletedEvent));
        var version = await dbContext.KnowledgeDocumentVersions
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(
                item => item.TenantId == message.TenantId && item.Id == versionId,
                cancellationToken);

        if (version == null || !CanFail(version.IngestionStatus))
            return;

        version.MarkFailed(FailureCode, errorMessage, failedAt);
        await dbContext.SaveChangesAsync(cancellationToken);
        _logger.LogError(
            "KnowledgeDocumentVersion {VersionId} failed after Document OCR integration retries were exhausted.",
            versionId);
    }

    private async Task MarkRegulatoryVersionFailedAsync(
        DocumentOcrCompletedEvent message,
        string errorMessage,
        DateTimeOffset failedAt,
        CancellationToken cancellationToken)
    {
        var versionId = DocumentOcrEventContract.ParseResourceId(
            message.ExternalContextId, nameof(DocumentOcrCompletedEvent));
        var version = await dbContext.RegulatoryDocumentVersions
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(
                item => item.TenantId == message.TenantId && item.Id == versionId,
                cancellationToken);

        if (version == null || !CanFail(version.IngestionStatus))
            return;

        version.FailIngestion(FailureCode, errorMessage, failedAt);
        await dbContext.SaveChangesAsync(cancellationToken);
        _logger.LogError(
            "RegulatoryDocumentVersion {VersionId} failed after Document OCR integration retries were exhausted.",
            versionId);
    }

    private static bool CanFail(RegulatoryIngestionStatus status) =>
        status is RegulatoryIngestionStatus.PendingOcr or RegulatoryIngestionStatus.Processing;

    private static string BuildFailureMessage(string failureDetail)
    {
        var detail = string.IsNullOrWhiteSpace(failureDetail)
            ? "Unknown downstream processing error."
            : failureDetail.Trim();
        var message = $"Corpus processing failed after retries: {detail}";
        return message.Length <= MaximumFailureMessageLength
            ? message
            : message[..MaximumFailureMessageLength];
    }
}
