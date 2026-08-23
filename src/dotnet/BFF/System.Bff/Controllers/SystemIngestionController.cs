using Asp.Versioning;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Microsoft.AspNetCore.Mvc;
using RegulatoryCompliance.Grpc;

namespace SystemBff.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/system/ingestion")]
[Route("api/system")]
public sealed class SystemIngestionController(
    RegulatoryComplianceService.RegulatoryComplianceServiceClient regulatoryClient)
    : ControllerBase
{
    /// <summary>
    /// Automated System Ingestion: Global Laws & Treaties
    /// </summary>
    [HttpPost("regulatory-sources")]
    public async Task<IActionResult> IngestSystemRegulatorySource(
        [FromBody] SystemRegulatoryRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Title) || string.IsNullOrWhiteSpace(request.Authority))
            return BadRequest(new { error = "Title and Authority are required." });

        var ingestRequest = new IngestRegulatorySourceRequest
        {
            IdempotencyKey = !string.IsNullOrWhiteSpace(request.IdempotencyKey)
                ? request.IdempotencyKey
                : Guid.NewGuid().ToString(),
            Authority = request.Authority,
            Title = request.Title,
            CanonicalSourceUri = request.CanonicalSourceUri ?? $"urn:system:law:{Guid.NewGuid()}",
            JurisdictionCode = request.JurisdictionCode ?? "GLOBAL",
            RegulationType = (RegulationType)(int)request.RegulationType,
            LanguageCode = request.LanguageCode ?? "en",
            VersionLabel = request.VersionLabel ?? "1.0",
            PublishedAt = Timestamp.FromDateTimeOffset(request.PublishedAt ?? DateTimeOffset.UtcNow),
            EffectiveFrom = Timestamp.FromDateTimeOffset(request.EffectiveFrom ?? DateTimeOffset.UtcNow),
            ContentReference = request.ContentReference ?? string.Empty,
            FileName = request.FileName ?? "system-law.pdf",
            MimeType = request.MimeType ?? "application/pdf",
            SizeBytes = request.SizeBytes > 0 ? request.SizeBytes : 1024,
            ContentSha256 = request.ContentSha256 ?? new string('0', 64),
            Content = !string.IsNullOrEmpty(request.RawText)
                ? ByteString.CopyFromUtf8(request.RawText)
                : ByteString.Empty,
            Visibility = RegulatorySourceVisibility.Platform
        };

        var response = await regulatoryClient.IngestRegulatorySourceAsync(ingestRequest, cancellationToken: cancellationToken);

        return Ok(new
        {
            regulatoryDocumentId = response.RegulatoryDocumentId,
            documentVersionId = response.DocumentVersionId,
            status = response.Status.ToString(),
            chunkCount = response.ChunkCount,
            replayed = response.Replayed,
            source = "SYSTEM_AUTOMATION"
        });
    }

    /// <summary>
    /// Automated System Ingestion: Global Knowledge & Standards
    /// </summary>
    [HttpPost("knowledge-documents")]
    public async Task<IActionResult> IngestSystemKnowledgeDocument(
        [FromBody] SystemKnowledgeRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
            return BadRequest(new { error = "Title is required." });

        var ingestRequest = new IngestKnowledgeSourceRequest
        {
            IdempotencyKey = !string.IsNullOrWhiteSpace(request.IdempotencyKey)
                ? request.IdempotencyKey
                : Guid.NewGuid().ToString(),
            Title = request.Title,
            Category = (KnowledgeCategory)(int)request.Category,
            SourceReference = request.SourceReference ?? $"urn:system:knowledge:{Guid.NewGuid()}",
            LanguageCode = request.LanguageCode ?? "en",
            VersionLabel = request.VersionLabel ?? "1.0",
            ContentReference = request.ContentReference ?? string.Empty,
            FileName = request.FileName ?? "system-knowledge.pdf",
            MimeType = request.MimeType ?? "application/pdf",
            SizeBytes = request.SizeBytes > 0 ? request.SizeBytes : 1024,
            ContentSha256 = request.ContentSha256 ?? new string('0', 64),
            Content = !string.IsNullOrEmpty(request.RawText)
                ? ByteString.CopyFromUtf8(request.RawText)
                : ByteString.Empty,
            Visibility = RegulatorySourceVisibility.Platform
        };

        var response = await regulatoryClient.IngestKnowledgeDocumentAsync(ingestRequest, cancellationToken: cancellationToken);

        return Ok(new
        {
            knowledgeDocumentId = response.KnowledgeDocumentId,
            documentVersionId = response.DocumentVersionId,
            status = response.Status.ToString(),
            chunkCount = response.ChunkCount,
            replayed = response.Replayed,
            source = "SYSTEM_AUTOMATION"
        });
    }
}

public sealed record SystemRegulatoryRequest(
    string? IdempotencyKey,
    string Authority,
    string Title,
    string? CanonicalSourceUri,
    string? JurisdictionCode,
    int RegulationType,
    string? LanguageCode,
    string? VersionLabel,
    DateTimeOffset? PublishedAt,
    DateTimeOffset? EffectiveFrom,
    string? ContentReference,
    string? FileName,
    string? MimeType,
    long SizeBytes,
    string? ContentSha256,
    string? RawText);

public sealed record SystemKnowledgeRequest(
    string? IdempotencyKey,
    string Title,
    int Category,
    string? SourceReference,
    string? LanguageCode,
    string? VersionLabel,
    string? ContentReference,
    string? FileName,
    string? MimeType,
    long SizeBytes,
    string? ContentSha256,
    string? RawText);
