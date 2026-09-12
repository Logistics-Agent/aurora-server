using System;
using System.Security.Cryptography;
using Asp.Versioning;
using BuildingBlocks.BFF.Extensions;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
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

        var content = !string.IsNullOrEmpty(request.RawText)
            ? ByteString.CopyFromUtf8(request.RawText)
            : ByteString.Empty;

        var computedSizeBytes = content.Length > 0
            ? content.Length
            : (request.SizeBytes > 0 ? request.SizeBytes : 1024);

        var computedSha256 = !string.IsNullOrWhiteSpace(request.ContentSha256)
            ? request.ContentSha256
            : Convert.ToHexString(SHA256.HashData(content.ToByteArray())).ToLowerInvariant();

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
            SizeBytes = computedSizeBytes,
            ContentSha256 = computedSha256,
            Content = content,
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
    /// List & Query System Ingested Regulatory Sources
    /// </summary>
    [HttpGet("regulatory-sources")]
    public async Task<IActionResult> QueryRegulatorySources(
        [FromQuery] string? query = null,
        [FromQuery] string? jurisdictionCode = null,
        [FromQuery] DateTimeOffset? effectiveAt = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var searchTerms = query?.Trim() ?? string.Empty;
            var rpcRequest = new QueryRegulationsRequest
            {
                Query = searchTerms,
                JurisdictionCode = jurisdictionCode ?? string.Empty,
                EffectiveAt = Timestamp.FromDateTimeOffset(effectiveAt ?? DateTimeOffset.UtcNow),
                TopK = 50,
                MinimumRelevanceScore = 0.0
            };

            var response = await regulatoryClient.QueryRegulationsAsync(rpcRequest, cancellationToken: cancellationToken);

            var items = response.Evidence
                .GroupBy(e => e.Citation?.RegulatoryDocumentId ?? Guid.NewGuid().ToString())
                .Select(g =>
                {
                    var e = g.First();
                    return new
                    {
                        id = e.Citation?.RegulatoryDocumentId ?? Guid.NewGuid().ToString(),
                        title = e.Citation?.Title ?? "Regulatory Source",
                        authority = e.Citation?.Authority ?? "Authority",
                        canonicalSourceUri = e.Citation?.CanonicalSourceUri ?? string.Empty,
                        jurisdictionCode = e.JurisdictionCode,
                        regulationType = e.RegulationType.ToString(),
                        languageCode = e.LanguageCode,
                        versionLabel = e.Citation?.VersionLabel ?? "1.0",
                        chunkCount = g.Count(),
                        status = "Active",
                        relevanceScore = e.Citation?.RelevanceScore ?? 0.0,
                        excerpt = e.Citation?.Excerpt ?? string.Empty
                    };
                }).ToList();

            return Ok(new { items, total = items.Count });
        }
        catch (RpcException ex)
        {
            return ex.ToActionResult();
        }
        catch (Exception)
        {
            return Ok(new { items = Array.Empty<object>(), total = 0 });
        }
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

        var content = !string.IsNullOrEmpty(request.RawText)
            ? ByteString.CopyFromUtf8(request.RawText)
            : ByteString.Empty;

        var computedSizeBytes = content.Length > 0
            ? content.Length
            : (request.SizeBytes > 0 ? request.SizeBytes : 1024);

        var computedSha256 = !string.IsNullOrWhiteSpace(request.ContentSha256)
            ? request.ContentSha256
            : Convert.ToHexString(SHA256.HashData(content.ToByteArray())).ToLowerInvariant();

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
            SizeBytes = computedSizeBytes,
            ContentSha256 = computedSha256,
            Content = content,
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
    /// List & Query System Ingested Knowledge Documents
    /// </summary>
    [HttpGet("knowledge-documents")]
    public async Task<IActionResult> QueryKnowledgeDocuments(
        [FromQuery] string? query = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var searchTerms = query?.Trim() ?? string.Empty;
            var rpcRequest = new QueryKnowledgeRequest
            {
                Query = searchTerms,
                TopK = 50,
                MinimumRelevanceScore = 0.0
            };

            var response = await regulatoryClient.QueryKnowledgeAsync(rpcRequest, cancellationToken: cancellationToken);

            var items = response.Evidence
                .GroupBy(e => e.KnowledgeDocumentId)
                .Select(g =>
                {
                    var e = g.First();
                    return new
                    {
                        id = e.KnowledgeDocumentId,
                        title = e.Title,
                        category = e.Category == KnowledgeCategory.Sop ? "SOP" :
                                   e.Category == KnowledgeCategory.Contract ? "Contract" :
                                   e.Category == KnowledgeCategory.Guide ? "Guide" :
                                   e.Category == KnowledgeCategory.InternalPolicy ? "Internal Policy" : e.Category.ToString(),
                        versionLabel = "1.0",
                        chunkCount = g.Count(),
                        status = "Active",
                        relevanceScore = e.RelevanceScore,
                        excerpt = e.Excerpt
                    };
                }).ToList();

            return Ok(new { items, total = items.Count });
        }
        catch (RpcException ex)
        {
            return ex.ToActionResult();
        }
        catch (Exception)
        {
            return Ok(new { items = Array.Empty<object>(), total = 0 });
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
