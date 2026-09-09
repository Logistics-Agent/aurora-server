using System;
using System.IO;
using System.Linq;
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
    [RequirePermission(PermissionConstants.Documents.Ingest, "documents:create")]
    public async Task<IActionResult> IngestKnowledgeDocument(
        [FromBody] AdminPlatformKnowledgeRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
            return BadRequest(new { error = "Title is required." });

        try
        {
            var ingestRequest = new IngestKnowledgeSourceRequest
            {
                IdempotencyKey = !string.IsNullOrWhiteSpace(request.IdempotencyKey)
                    ? request.IdempotencyKey
                    : Guid.NewGuid().ToString(),
                Title = request.Title,
                Category = (KnowledgeCategory)(int)request.Category,
                SourceReference = request.SourceReference ?? $"sop://{currentUser.TenantId}/{Guid.NewGuid()}",
                LanguageCode = request.LanguageCode ?? "en",
                VersionLabel = request.VersionLabel ?? "1.0",
                ContentReference = request.ContentReference ?? request.StorageReference ?? string.Empty,
                FileName = request.FileName ?? $"{request.Title.Replace(' ', '_')}.pdf",
                MimeType = request.MimeType ?? "application/pdf",
                SizeBytes = request.SizeBytes > 0 ? request.SizeBytes : 1024,
                ContentSha256 = request.ContentSha256 ?? new string('0', 64),
                Content = !string.IsNullOrEmpty(request.RawText)
                    ? ByteString.CopyFromUtf8(request.RawText)
                    : ByteString.Empty,
                Visibility = RegulatorySourceVisibility.Tenant
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
    [RequirePermission(PermissionConstants.Documents.Ingest, "documents:create")]
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
                SourceReference = $"sop://{currentUser.TenantId}/{file.FileName}",
                LanguageCode = language ?? "en",
                VersionLabel = version ?? "v1.0",
                ContentReference = $"storage://knowledge/{currentUser.TenantId}/{file.FileName}",
                FileName = file.FileName,
                MimeType = file.ContentType ?? "application/pdf",
                SizeBytes = file.Length,
                ContentSha256 = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(fileBytes)),
                Content = ByteString.CopyFrom(fileBytes),
                Visibility = RegulatorySourceVisibility.Tenant
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
    [RequirePermission(PermissionConstants.Documents.Ingest, "documents:read", "compliance:read")]
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
            // Fallback gracefully with empty list if knowledge service is not yet populated
            return Ok(new { items = Array.Empty<object>(), total = 0 });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error querying knowledge documents");
            return Ok(new { items = Array.Empty<object>(), total = 0 });
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

        try
        {
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
