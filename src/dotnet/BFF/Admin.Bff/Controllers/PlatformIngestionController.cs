using Asp.Versioning;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RegulatoryCompliance.Grpc;

namespace AdminBff.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/ingestion")]
[Route("api/admin")]
[Route("api/platform")]
[Authorize]
public sealed class PlatformIngestionController(
    RegulatoryComplianceService.RegulatoryComplianceServiceClient regulatoryClient)
    : ControllerBase
{
    /// <summary>
    /// Platform Admin Ingestion: Regulatory Source (PLATFORM scope)
    /// </summary>
    [HttpPost("regulatory-sources")]
    public async Task<IActionResult> IngestPlatformRegulatorySource(
        [FromBody] AdminPlatformRegulatoryRequest request,
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
            CanonicalSourceUri = request.CanonicalSourceUri ?? $"urn:platform:law:{Guid.NewGuid()}",
            JurisdictionCode = request.JurisdictionCode ?? "VN",
            RegulationType = (RegulationType)(int)request.RegulationType,
            LanguageCode = request.LanguageCode ?? "vi",
            VersionLabel = request.VersionLabel ?? "1.0",
            PublishedAt = Timestamp.FromDateTimeOffset(request.PublishedAt ?? DateTimeOffset.UtcNow),
            EffectiveFrom = Timestamp.FromDateTimeOffset(request.EffectiveFrom ?? DateTimeOffset.UtcNow),
            ContentReference = request.ContentReference ?? request.StorageReference ?? string.Empty,
            FileName = request.FileName ?? "platform-law.pdf",
            MimeType = request.MimeType ?? "application/pdf",
            SizeBytes = request.SizeBytes > 0 ? request.SizeBytes : 1024,
            ContentSha256 = request.ContentSha256 ?? new string('0', 64),
            Content = !string.IsNullOrEmpty(request.RawText)
                ? ByteString.CopyFromUtf8(request.RawText)
                : ByteString.Empty,
            Visibility = RegulatorySourceVisibility.Platform // Platform Admin can create PLATFORM scope
        };

        var response = await regulatoryClient.IngestRegulatorySourceAsync(ingestRequest, cancellationToken: cancellationToken);

        return Ok(new
        {
            regulatoryDocumentId = response.RegulatoryDocumentId,
            documentVersionId = response.DocumentVersionId,
            status = response.Status.ToString(),
            chunkCount = response.ChunkCount,
            replayed = response.Replayed,
            scope = "PLATFORM"
        });
    }

    /// <summary>
    /// Platform Admin Ingestion: Knowledge Document (PLATFORM scope)
    /// </summary>
    [HttpPost("knowledge-documents")]
    public async Task<IActionResult> IngestPlatformKnowledgeDocument(
        [FromBody] AdminPlatformKnowledgeRequest request,
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
            SourceReference = request.SourceReference ?? $"global-sop://{Guid.NewGuid()}",
            LanguageCode = request.LanguageCode ?? "vi",
            VersionLabel = request.VersionLabel ?? "1.0",
            ContentReference = request.ContentReference ?? request.StorageReference ?? string.Empty,
            FileName = request.FileName ?? "platform-knowledge.pdf",
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
            scope = "PLATFORM"
        });
    }
}

public sealed record AdminPlatformRegulatoryRequest(
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
    string? StorageReference,
    string? FileName,
    string? MimeType,
    long SizeBytes,
    string? ContentSha256,
    string? RawText);

public sealed record AdminPlatformKnowledgeRequest(
    string? IdempotencyKey,
    string Title,
    int Category,
    string? SourceReference,
    string? LanguageCode,
    string? VersionLabel,
    string? ContentReference,
    string? StorageReference,
    string? FileName,
    string? MimeType,
    long SizeBytes,
    string? ContentSha256,
    string? RawText);
