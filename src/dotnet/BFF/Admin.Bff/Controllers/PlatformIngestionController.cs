using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Asp.Versioning;
using BuildingBlocks.BFF.Attributes;
using BuildingBlocks.BFF.Extensions;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using RegulatoryCompliance.Grpc;
using Shared.Constants;
using Shared.Security;

namespace AdminBff.Controllers;

[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/ingestion")]
[Route("api/v{version:apiVersion}/admin/knowledge")]
[Route("api/admin")]
[Route("api/platform")]
public sealed class PlatformIngestionController(
    RegulatoryComplianceService.RegulatoryComplianceServiceClient regulatoryClient,
    ICurrentUserService currentUser,
    ILogger<PlatformIngestionController> logger)
    : AdminControllerBase
{
    /// <summary>
    /// Ingest Knowledge Document (JSON payload)
    /// </summary>
    [HttpPost("knowledge-documents")]
    [RequirePermission(PermissionConstants.Compliance.PlatformIngest)]
    public async Task<IActionResult> IngestKnowledgeDocument(
        [FromBody] AdminPlatformKnowledgeRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
            return BadRequest(new { error = "Title is required." });
        if (string.IsNullOrWhiteSpace(request.RawText))
            return BadRequest(new { error = "RawText is required for knowledge corpus ingestion." });

        if (string.IsNullOrWhiteSpace(request.ContentReference) ||
            !request.ContentReference.StartsWith("knowledge/", StringComparison.Ordinal) ||
            request.ContentReference.Contains("..", StringComparison.Ordinal))
            return BadRequest(new { error = "ContentReference must be a knowledge/{path} storage key." });
        var contentBytes = Encoding.UTF8.GetBytes(request.RawText);

        try
        {
            var ingestRequest = new IngestKnowledgeSourceRequest
            {
                IdempotencyKey = !string.IsNullOrWhiteSpace(request.IdempotencyKey)
                    ? request.IdempotencyKey
                    : Guid.NewGuid().ToString(),
                Title = request.Title,
                Category = (KnowledgeCategory)(int)request.Category,
                SourceReference = request.SourceReference ?? $"https://knowledge.aurora.local/{Guid.NewGuid()}",
                LanguageCode = request.LanguageCode ?? "en",
                VersionLabel = request.VersionLabel ?? "1.0",
                ContentReference = request.ContentReference,
                FileName = request.FileName ?? $"{request.Title.Replace(' ', '_')}.md",
                MimeType = request.MimeType ?? "text/markdown",
                SizeBytes = contentBytes.Length,
                ContentSha256 = Convert.ToHexString(SHA256.HashData(contentBytes)).ToLowerInvariant(),
                Content = ByteString.CopyFrom(contentBytes),
                Visibility = RegulatorySourceVisibility.Platform
            };

            var response = await regulatoryClient.IngestKnowledgeDocumentAsync(ingestRequest, cancellationToken: cancellationToken);

            logger.LogInformation("Knowledge document {Title} ingested with ID {DocId} by user {UserId}",
                request.Title, response.KnowledgeDocumentId, currentUser.UserId);

            return Ok(new
            {
                knowledgeDocumentId = response.KnowledgeDocumentId,
                documentVersionId = response.DocumentVersionId,
                status = response.Status.ToString(),
                chunkCount = response.ChunkCount,
                replayed = response.Replayed,
                receivedAt = response.ReceivedAt?.ToDateTimeOffset() ?? DateTimeOffset.UtcNow
            });
        }
        catch (RpcException ex)
        {
            logger.LogWarning(ex, "gRPC error during IngestKnowledgeDocument");
            return ex.ToActionResult();
        }
    }

    /// <summary>
    /// Upload & Ingest Knowledge Document File (Multipart Form)
    /// </summary>
    [HttpPost("knowledge-documents/upload")]
    [RequirePermission(PermissionConstants.Compliance.PlatformIngest)]
    public async Task<IActionResult> UploadKnowledgeDocument(
        [FromForm] string title,
        [FromForm] string? category,
        [FromForm] string? language,
        [FromForm] string? version,
        IFormFile file,
        CancellationToken cancellationToken)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { error = "A valid file is required." });

        if (string.IsNullOrWhiteSpace(title))
            title = Path.GetFileNameWithoutExtension(file.FileName);

        if (file.ContentType is not ("text/plain" or "text/markdown"))
            return BadRequest(new { error = "Only text/plain and text/markdown files can be indexed. Run PDF files through OCR first." });

        try
        {
            using var ms = new MemoryStream();
            await file.CopyToAsync(ms, cancellationToken);
            var fileBytes = ms.ToArray();

            var categoryEnum = category?.ToLowerInvariant() switch
            {
                "sop" or "0" => KnowledgeCategory.Sop,
                "carrier contract" or "carriercontract" or "contract" or "1" => KnowledgeCategory.Contract,
                "customs guideline" or "customsguideline" or "customs" or "guide" or "2" => KnowledgeCategory.Guide,
                "policy" or "internal policy" or "3" => KnowledgeCategory.InternalPolicy,
                _ => KnowledgeCategory.Sop
            };

            var ingestRequest = new IngestKnowledgeSourceRequest
            {
                IdempotencyKey = Guid.NewGuid().ToString(),
                Title = title,
                Category = categoryEnum,
                SourceReference = $"https://knowledge.aurora.local/{Uri.EscapeDataString(file.FileName)}",
                LanguageCode = language ?? "en",
                VersionLabel = version ?? "v1.0",
                ContentReference = $"knowledge/platform/{Guid.NewGuid():N}/{Path.GetFileName(file.FileName)}",
                FileName = file.FileName,
                MimeType = file.ContentType ?? "application/pdf",
                SizeBytes = file.Length,
                ContentSha256 = Convert.ToHexString(SHA256.HashData(fileBytes)).ToLowerInvariant(),
                Content = ByteString.CopyFrom(fileBytes),
                Visibility = RegulatorySourceVisibility.Platform
            };

            var response = await regulatoryClient.IngestKnowledgeDocumentAsync(ingestRequest, cancellationToken: cancellationToken);

            logger.LogInformation("Uploaded & ingested knowledge doc {FileName} ({Bytes} bytes) -> {DocId}",
                file.FileName, file.Length, response.KnowledgeDocumentId);

            return Ok(new
            {
                id = response.KnowledgeDocumentId,
                title,
                category = category ?? "SOP",
                language = language ?? "English",
                version = version ?? "v1.0",
                chunks = response.ChunkCount,
                ingestDate = DateTime.UtcNow.ToString("yyyy-MM-dd"),
                status = response.Status == RegulatoryIngestionStatus.Completed ? "Completed" : "Processing"
            });
        }
        catch (RpcException ex)
        {
            logger.LogWarning(ex, "gRPC error during file upload ingestion");
            return ex.ToActionResult();
        }
    }

    /// <summary>
    /// List & Query Ingested Knowledge Documents
    /// </summary>
    [HttpGet("knowledge-documents")]
    [HttpPost("knowledge/query")]
    [RequirePermission(PermissionConstants.Documents.Read, PermissionConstants.Compliance.Read)]
    public async Task<IActionResult> QueryKnowledgeDocuments(
        [FromQuery] string? query = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var searchTerms = string.IsNullOrWhiteSpace(query) ? "SOP guidelines policy contract" : query;
            var rpcRequest = new QueryKnowledgeRequest
            {
                Query = searchTerms,
                TopK = 50,
                MinimumRelevanceScore = 0.01
            };

            var response = await regulatoryClient.QueryKnowledgeAsync(rpcRequest, cancellationToken: cancellationToken);

            var items = response.Evidence.Select(e => new
            {
                id = e.KnowledgeDocumentId,
                title = e.Title,
                category = e.Category == KnowledgeCategory.Sop ? "SOP" :
                           e.Category == KnowledgeCategory.Contract ? "Carrier Contract" :
                           e.Category == KnowledgeCategory.Guide ? "Customs Guideline" : e.Category.ToString(),
                language = "English",
                version = "v1.0",
                chunks = 1,
                ingestDate = DateTime.UtcNow.ToString("yyyy-MM-dd"),
                status = "Completed",
                relevanceScore = e.RelevanceScore,
                excerpt = e.Excerpt,
                section = e.SectionLabel
            }).ToList();

            return Ok(new { items, total = items.Count });
        }
        catch (RpcException ex)
        {
            logger.LogWarning(ex, "gRPC error during QueryKnowledgeDocuments: {Detail}", ex.Status.Detail);
            return ex.ToActionResult();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error querying knowledge documents");
            return Problem(title: "KNOWLEDGE_QUERY_FAILED", detail: "Knowledge query failed.");
        }
    }

    /// <summary>
    /// Platform Admin Ingestion: Regulatory Source (PLATFORM scope)
    /// </summary>
    [HttpPost("regulatory-sources")]
    [RequirePermission(PermissionConstants.Compliance.PlatformIngest)]
    public async Task<IActionResult> IngestPlatformRegulatorySource(
        [FromBody] AdminPlatformRegulatoryRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Title) || string.IsNullOrWhiteSpace(request.Authority))
            return BadRequest(new { error = "Title and Authority are required." });
        if (string.IsNullOrWhiteSpace(request.RawText))
            return BadRequest(new { error = "RawText is required for regulatory corpus ingestion." });
        if (!Uri.TryCreate(request.CanonicalSourceUri, UriKind.Absolute, out var canonicalUri) ||
            canonicalUri.Scheme != Uri.UriSchemeHttps || !string.IsNullOrEmpty(canonicalUri.UserInfo))
            return BadRequest(new { error = "CanonicalSourceUri must be an HTTPS provenance URI." });
        if (string.IsNullOrWhiteSpace(request.ContentReference) ||
            !request.ContentReference.StartsWith("regulatory/", StringComparison.Ordinal) ||
            request.ContentReference.Contains("..", StringComparison.Ordinal))
            return BadRequest(new { error = "ContentReference must be a regulatory/{path} storage key." });
        var regulatoryBytes = Encoding.UTF8.GetBytes(request.RawText);

        try
        {
            var ingestRequest = new IngestRegulatorySourceRequest
            {
                IdempotencyKey = !string.IsNullOrWhiteSpace(request.IdempotencyKey)
                    ? request.IdempotencyKey
                    : Guid.NewGuid().ToString(),
                Authority = request.Authority,
                Title = request.Title,
                CanonicalSourceUri = request.CanonicalSourceUri,
                JurisdictionCode = request.JurisdictionCode ?? "VN",
                RegulationType = (RegulationType)(int)request.RegulationType,
                LanguageCode = request.LanguageCode ?? "vi",
                VersionLabel = request.VersionLabel ?? "1.0",
                PublishedAt = Timestamp.FromDateTimeOffset(request.PublishedAt ?? DateTimeOffset.UtcNow),
                EffectiveFrom = Timestamp.FromDateTimeOffset(request.EffectiveFrom ?? DateTimeOffset.UtcNow),
                ContentReference = request.ContentReference,
                FileName = request.FileName ?? "platform-law.md",
                MimeType = request.MimeType ?? "text/markdown",
                SizeBytes = regulatoryBytes.Length,
                ContentSha256 = Convert.ToHexString(SHA256.HashData(regulatoryBytes)).ToLowerInvariant(),
                Content = ByteString.CopyFrom(regulatoryBytes),
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
                scope = "PLATFORM"
            });
        }
        catch (RpcException ex)
        {
            logger.LogWarning(ex, "gRPC error during IngestPlatformRegulatorySource");
            return ex.ToActionResult();
        }
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
