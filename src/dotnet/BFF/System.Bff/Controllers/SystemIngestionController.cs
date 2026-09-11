using Asp.Versioning;
using BuildingBlocks.BFF.Extensions;
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

        var contentBytes = !string.IsNullOrEmpty(request.RawText)
            ? System.Text.Encoding.UTF8.GetBytes(request.RawText)
            : Array.Empty<byte>();

        var actualHash = contentBytes.Length > 0
            ? Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(contentBytes)).ToLowerInvariant()
            : new string('0', 64);

        var canonicalUri = !string.IsNullOrWhiteSpace(request.CanonicalSourceUri) && request.CanonicalSourceUri.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            ? request.CanonicalSourceUri
            : $"https://platform.aurora.io/laws/{Guid.NewGuid()}";

        var contentRef = !string.IsNullOrWhiteSpace(request.ContentReference) && request.ContentReference.StartsWith("regulatory/", StringComparison.Ordinal)
            ? request.ContentReference
            : $"regulatory/system-{Guid.NewGuid()}.txt";

        var mimeType = !string.IsNullOrWhiteSpace(request.MimeType) && request.MimeType.Equals("text/markdown", StringComparison.OrdinalIgnoreCase)
            ? "text/markdown"
            : "text/plain";

        var ingestRequest = new IngestRegulatorySourceRequest
        {
            IdempotencyKey = !string.IsNullOrWhiteSpace(request.IdempotencyKey)
                ? request.IdempotencyKey
                : Guid.NewGuid().ToString(),
            Authority = request.Authority,
            Title = request.Title,
            CanonicalSourceUri = canonicalUri,
            JurisdictionCode = request.JurisdictionCode ?? "GLOBAL",
            RegulationType = (RegulationType)(int)request.RegulationType,
            LanguageCode = request.LanguageCode ?? "en",
            VersionLabel = request.VersionLabel ?? "1.0",
            PublishedAt = Timestamp.FromDateTimeOffset(request.PublishedAt ?? DateTimeOffset.UtcNow),
            EffectiveFrom = Timestamp.FromDateTimeOffset(request.EffectiveFrom ?? DateTimeOffset.UtcNow),
            ContentReference = contentRef,
            FileName = !string.IsNullOrWhiteSpace(request.FileName) ? request.FileName : "system-law.txt",
            MimeType = mimeType,
            SizeBytes = contentBytes.Length > 0 ? contentBytes.Length : 1024,
            ContentSha256 = actualHash,
            Content = ByteString.CopyFromUtf8(request.RawText ?? string.Empty),
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

        var mimeType = !string.IsNullOrWhiteSpace(request.MimeType) && request.MimeType.Equals("text/markdown", StringComparison.OrdinalIgnoreCase)
            ? "text/markdown"
            : "text/plain";

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
            FileName = request.FileName ?? "system-knowledge.txt",
            MimeType = mimeType,
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

    /// <summary>
    /// List & Query System Regulatory Sources
    /// </summary>
    [HttpGet("regulatory-sources")]
    public async Task<IActionResult> ListSystemRegulatorySources(
        [FromQuery] string? query = null,
        [FromQuery] string? jurisdictionCode = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var req = new QueryRegulationsRequest
            {
                Query = query?.Trim() ?? string.Empty,
                JurisdictionCode = jurisdictionCode?.Trim() ?? string.Empty,
                TopK = 50,
                MinimumRelevanceScore = 0.0
            };
            var response = await regulatoryClient.QueryRegulationsAsync(req, cancellationToken: cancellationToken);

            var items = response.Evidence.Select(e => new
            {
                id = e.Citation?.RegulatoryDocumentId ?? Guid.NewGuid().ToString(),
                title = e.Citation?.Title ?? "Regulatory Document",
                authority = e.Citation?.Authority ?? "Authority",
                canonicalSourceUri = e.Citation?.CanonicalSourceUri ?? string.Empty,
                jurisdictionCode = e.JurisdictionCode,
                regulationType = e.RegulationType.ToString(),
                languageCode = e.LanguageCode,
                versionLabel = e.Citation?.VersionLabel ?? "1.0",
                chunkCount = 1,
                excerpt = e.Citation?.Excerpt ?? string.Empty,
                relevanceScore = e.Citation?.RelevanceScore ?? 1.0,
                status = "COMPLETED"
            }).ToList();

            return Ok(new { items, total = items.Count });
        }
        catch (Grpc.Core.RpcException ex)
        {
            return ex.ToActionResult();
        }
    }

    /// <summary>
    /// List & Query System Knowledge Documents
    /// </summary>
    [HttpGet("knowledge-documents")]
    public async Task<IActionResult> QuerySystemKnowledgeDocuments(
        [FromQuery] string? query = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var req = new QueryKnowledgeRequest
            {
                Query = query?.Trim() ?? string.Empty,
                TopK = 50,
                MinimumRelevanceScore = 0.0
            };
            var response = await regulatoryClient.QueryKnowledgeAsync(req, cancellationToken: cancellationToken);

            var items = response.Evidence
                .GroupBy(e => e.KnowledgeDocumentId)
                .Select(g =>
                {
                    var first = g.First();
                    return new
                    {
                        id = first.KnowledgeDocumentId,
                        title = first.Title,
                        category = first.Category.ToString(),
                        versionLabel = "1.0",
                        chunkCount = g.Count(),
                        excerpt = first.Excerpt,
                        relevanceScore = first.RelevanceScore,
                        status = "COMPLETED"
                    };
                }).ToList();

            return Ok(new { items, total = items.Count });
        }
        catch (Grpc.Core.RpcException ex)
        {
            return ex.ToActionResult();
        }
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
