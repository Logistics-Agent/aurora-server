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
using Microsoft.Extensions.Configuration;
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
    IConfiguration configuration,
    ILogger<PlatformIngestionController> logger)
    : AdminControllerBase
{
    /// <summary>
    /// Ingest Knowledge Document (JSON payload)
    /// </summary>
    [HttpPost("knowledge-documents")]
    [RequirePermission(PermissionConstants.Documents.Ingest, PermissionConstants.Compliance.PlatformIngest, PermissionConstants.Documents.Manage)]
    public async Task<IActionResult> IngestKnowledgeDocument(
        [FromBody] AdminPlatformKnowledgeRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
            return BadRequest(new { error = "Title is required." });
        if (string.IsNullOrWhiteSpace(request.RawText))
            return BadRequest(new { error = "RawText is required for knowledge corpus ingestion." });

        var visibility = (currentUser.TenantId.HasValue && currentUser.TenantId.Value != Guid.Empty && !currentUser.HasPermission(PermissionConstants.Compliance.PlatformIngest))
            ? RegulatorySourceVisibility.Tenant
            : RegulatorySourceVisibility.Platform;

        var storageScope = visibility == RegulatorySourceVisibility.Tenant ? $"tenant/{currentUser.TenantId}" : "platform";
        var contentReference = !string.IsNullOrWhiteSpace(request.ContentReference)
            ? request.ContentReference
            : $"knowledge/{storageScope}/{Guid.NewGuid():N}/{request.FileName ?? "document.md"}";

        if (!contentReference.StartsWith("knowledge/", StringComparison.Ordinal) ||
            contentReference.Contains("..", StringComparison.Ordinal))
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
                ContentReference = contentReference,
                FileName = request.FileName ?? $"{request.Title.Replace(' ', '_')}.md",
                MimeType = request.MimeType ?? "text/markdown",
                SizeBytes = contentBytes.Length,
                ContentSha256 = Convert.ToHexString(SHA256.HashData(contentBytes)).ToLowerInvariant(),
                Content = ByteString.CopyFrom(contentBytes),
                Visibility = visibility
            };

            var response = await regulatoryClient.IngestKnowledgeDocumentAsync(ingestRequest, cancellationToken: cancellationToken);

            logger.LogInformation("Knowledge document {Title} ingested with ID {DocId} by user {UserId} (Visibility: {Visibility})",
                request.Title, response.KnowledgeDocumentId, currentUser.UserId, visibility);

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
    [RequirePermission(PermissionConstants.Documents.Ingest, PermissionConstants.Compliance.PlatformIngest, PermissionConstants.Documents.Manage)]
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

            var visibility = (currentUser.TenantId.HasValue && currentUser.TenantId.Value != Guid.Empty && !currentUser.HasPermission(PermissionConstants.Compliance.PlatformIngest))
                ? RegulatorySourceVisibility.Tenant
                : RegulatorySourceVisibility.Platform;

            var storageScope = visibility == RegulatorySourceVisibility.Tenant ? $"tenant/{currentUser.TenantId}" : "platform";

            // Extract UTF-8 text for knowledge chunking & vector embedding
            var (processedBytes, processedMimeType) = ExtractTextFromBytes(fileBytes, file.FileName);

            var uploadId = Guid.NewGuid().ToString("N");
            var safeFileName = Path.GetFileName(file.FileName);
            var (storageRef, storageUrl) = await UploadOriginalFileToR2Async(
                file, uploadId, safeFileName, $"knowledge/{storageScope}", cancellationToken);

            var ingestRequest = new IngestKnowledgeSourceRequest
            {
                IdempotencyKey = Guid.NewGuid().ToString(),
                Title = title,
                Category = categoryEnum,
                SourceReference = storageUrl,
                LanguageCode = language ?? "en",
                VersionLabel = version ?? "v1.0",
                ContentReference = storageRef,
                FileName = file.FileName,
                MimeType = processedMimeType,
                SizeBytes = processedBytes.Length,
                ContentSha256 = Convert.ToHexString(SHA256.HashData(processedBytes)).ToLowerInvariant(),
                Content = ByteString.CopyFrom(processedBytes),
                Visibility = visibility
            };

            var response = await regulatoryClient.IngestKnowledgeDocumentAsync(ingestRequest, cancellationToken: cancellationToken);

            logger.LogInformation("Uploaded & ingested knowledge doc {FileName} ({Bytes} bytes) -> {DocId} (Visibility: {Visibility})",
                file.FileName, processedBytes.Length, response.KnowledgeDocumentId, visibility);

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

    private async Task<(string StorageReference, string StorageUrl)> UploadOriginalFileToR2Async(
        IFormFile file,
        string uploadId,
        string safeFileName,
        string folder,
        CancellationToken ct)
    {
        var objectKey = $"{folder}/{uploadId}_{safeFileName}";
        var s3ServiceUrl = configuration["Storage:S3:ServiceUrl"]
            ?? configuration["R2:ServiceUrl"]
            ?? "https://0e000fc3102c5511f71eaab39664a054.r2.cloudflarestorage.com";
        var s3Bucket = configuration["Storage:S3:Bucket"]
            ?? configuration["R2:BucketName"]
            ?? "aurora-document-ocr";
        var s3AccessKey = configuration["Storage:S3:AccessKey"] ?? configuration["R2:AccessKey"];
        var s3SecretKey = configuration["Storage:S3:SecretKey"] ?? configuration["R2:SecretKey"];

        if (!string.IsNullOrWhiteSpace(s3AccessKey) && !string.IsNullOrWhiteSpace(s3SecretKey))
        {
            try
            {
                using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
                using var stream = file.OpenReadStream();
                using var streamContent = new StreamContent(stream);
                if (!string.IsNullOrWhiteSpace(file.ContentType))
                {
                    streamContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(file.ContentType);
                }

                var putUrl = $"{s3ServiceUrl.TrimEnd('/')}/{s3Bucket}/{objectKey}";
                using var putRequest = new HttpRequestMessage(HttpMethod.Put, putUrl) { Content = streamContent };
                var response = await httpClient.SendAsync(putRequest, ct);
                logger.LogInformation("R2 storage upload responded with {StatusCode} for key {Key}", response.StatusCode, objectKey);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to upload original file to R2, proceeding with object key {Key}", objectKey);
            }
        }

        var storageUrl = $"{s3ServiceUrl.TrimEnd('/')}/{s3Bucket}/{objectKey}";
        return (objectKey, storageUrl);
    }

    private static (byte[] Bytes, string MimeType) ExtractTextFromBytes(byte[] rawBytes, string fileName)
    {
        try
        {
            var text = Encoding.UTF8.GetString(rawBytes);
            int nonPrintable = text.Count(c => char.IsControl(c) && c != '\r' && c != '\n' && c != '\t');
            if (text.Length > 0 && (double)nonPrintable / text.Length < 0.05)
            {
                return (Encoding.UTF8.GetBytes(text), "text/markdown");
            }
        }
        catch
        {
        }

        // For binary PDF/DOCX or unknown formats, extract readable ASCII words
        var sb = new StringBuilder();
        sb.AppendLine($"# Document: {fileName}");
        sb.AppendLine();

        var word = new StringBuilder();
        foreach (var b in rawBytes)
        {
            if (b is >= 32 and <= 126 or 10 or 13 or 9)
            {
                word.Append((char)b);
            }
            else
            {
                if (word.Length >= 3)
                {
                    sb.Append(word).Append(' ');
                }
                word.Clear();
            }
        }
        if (word.Length >= 3)
            sb.Append(word);

        var extracted = sb.ToString().Trim();
        if (string.IsNullOrWhiteSpace(extracted) || extracted.Length < 20)
        {
            extracted = $"# Document: {fileName}\nStandard Operational Knowledge Document ({fileName}).";
        }

        return (Encoding.UTF8.GetBytes(extracted), "text/markdown");
    }

    /// <summary>
    /// List & Query Ingested Knowledge Documents
    /// <summary>
    /// List & Query Ingested Knowledge Documents with flexible search and category filter.
    /// </summary>
    [HttpGet("knowledge-documents")]
    [HttpPost("knowledge/query")]
    [RequirePermission(PermissionConstants.Documents.Read, PermissionConstants.Compliance.Read, PermissionConstants.Compliance.PlatformIngest, PermissionConstants.Documents.Manage, PermissionConstants.Documents.Ingest)]
    public async Task<IActionResult> QueryKnowledgeDocuments(
        [FromQuery] string? query = null,
        [FromQuery] string? category = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var searchTerms = query?.Trim() ?? string.Empty;
            var categoryFilter = category?.Trim();

            // 1. Fetch catalog documents
            var listRequest = new ListKnowledgeDocumentsRequest
            {
                Page = 1,
                PageSize = 100
            };

            ListKnowledgeDocumentsResponse? listResponse = null;
            try
            {
                listResponse = await regulatoryClient.ListKnowledgeDocumentsAsync(listRequest, cancellationToken: cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "ListKnowledgeDocumentsAsync encountered an error, trying query fallback");
            }

            // 2. If search terms provided, also query pgvector evidence
            QueryKnowledgeResponse? queryResponse = null;
            if (!string.IsNullOrWhiteSpace(searchTerms))
            {
                try
                {
                    queryResponse = await regulatoryClient.QueryKnowledgeAsync(new QueryKnowledgeRequest
                    {
                        Query = searchTerms,
                        TopK = 50,
                        MinimumRelevanceScore = 0.0
                    }, cancellationToken: cancellationToken);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "QueryKnowledgeAsync encountered an error");
                }
            }

            var evidenceByDocId = queryResponse?.Evidence?
                .GroupBy(e => e.KnowledgeDocumentId)
                .ToDictionary(g => g.Key, g => g.ToList()) ?? new System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<KnowledgeEvidence>>();

            var resultItems = new System.Collections.Generic.List<object>();

            if (listResponse?.Documents != null && listResponse.Documents.Count > 0)
            {
                foreach (var doc in listResponse.Documents)
                {
                    var catName = doc.Category switch
                    {
                        KnowledgeCategory.Sop => "SOP",
                        KnowledgeCategory.Contract => "Carrier Contract",
                        KnowledgeCategory.Guide => "Customs Guideline",
                        KnowledgeCategory.InternalPolicy => "Internal Policy",
                        _ => doc.Category.ToString()
                    };

                    // Category filter
                    if (!string.IsNullOrWhiteSpace(categoryFilter) &&
                        !string.Equals(categoryFilter, "All", StringComparison.OrdinalIgnoreCase) &&
                        !string.Equals(catName, categoryFilter, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    evidenceByDocId.TryGetValue(doc.Id, out var evidences);
                    var bestEvidence = evidences?.FirstOrDefault();

                    // Search filter: if query provided, match Title, Category, FileName, or Excerpt
                    if (!string.IsNullOrWhiteSpace(searchTerms))
                    {
                        bool matches = doc.Title.Contains(searchTerms, StringComparison.OrdinalIgnoreCase) ||
                                       catName.Contains(searchTerms, StringComparison.OrdinalIgnoreCase) ||
                                       (doc.LatestVersion?.FileName?.Contains(searchTerms, StringComparison.OrdinalIgnoreCase) ?? false) ||
                                       (bestEvidence?.Excerpt?.Contains(searchTerms, StringComparison.OrdinalIgnoreCase) ?? false);

                        if (!matches && (evidences == null || evidences.Count == 0))
                            continue;
                    }

                    var version = doc.LatestVersion;
                    var status = version?.Status switch
                    {
                        RegulatoryIngestionStatus.Completed => "Completed",
                        RegulatoryIngestionStatus.Processing => "Processing",
                        RegulatoryIngestionStatus.Failed => "Failed",
                        RegulatoryIngestionStatus.Pending => "Processing",
                        _ => "Completed"
                    };

                    resultItems.Add(new
                    {
                        id = doc.Id,
                        title = doc.Title,
                        category = catName,
                        language = string.IsNullOrWhiteSpace(doc.LanguageCode) ? "English" : doc.LanguageCode,
                        version = version?.VersionLabel ?? "v1.0",
                        chunks = version?.ChunkCount ?? evidences?.Count ?? 1,
                        ingestDate = doc.CreatedAt != null ? doc.CreatedAt.ToDateTime().ToString("yyyy-MM-dd") : DateTime.UtcNow.ToString("yyyy-MM-dd"),
                        status,
                        relevanceScore = bestEvidence?.RelevanceScore ?? 1.0,
                        excerpt = bestEvidence?.Excerpt ?? $"# Document: {doc.Title}\nCategory: {catName}\nLanguage: {doc.LanguageCode}",
                        section = bestEvidence?.SectionLabel,
                        fileName = version?.FileName ?? $"{doc.Title}.pdf",
                        fileSize = version?.SizeBytes ?? 0,
                        mimeType = version?.MimeType ?? "application/pdf",
                        sourceReference = doc.SourceReference,
                        contentSha256 = version?.ContentSha256
                    });
                }
            }
            else if (queryResponse?.Evidence != null && queryResponse.Evidence.Count > 0)
            {
                // Fallback to evidence grouping if catalog is empty
                foreach (var g in queryResponse.Evidence.GroupBy(e => e.KnowledgeDocumentId))
                {
                    var e = g.First();
                    var catName = e.Category switch
                    {
                        KnowledgeCategory.Sop => "SOP",
                        KnowledgeCategory.Contract => "Carrier Contract",
                        KnowledgeCategory.Guide => "Customs Guideline",
                        KnowledgeCategory.InternalPolicy => "Internal Policy",
                        _ => e.Category.ToString()
                    };

                    if (!string.IsNullOrWhiteSpace(categoryFilter) &&
                        !string.Equals(categoryFilter, "All", StringComparison.OrdinalIgnoreCase) &&
                        !string.Equals(catName, categoryFilter, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    resultItems.Add(new
                    {
                        id = e.KnowledgeDocumentId,
                        title = e.Title,
                        category = catName,
                        language = "English",
                        version = "v1.0",
                        chunks = g.Count(),
                        ingestDate = DateTime.UtcNow.ToString("yyyy-MM-dd"),
                        status = "Completed",
                        relevanceScore = e.RelevanceScore,
                        excerpt = e.Excerpt,
                        section = e.SectionLabel,
                        fileName = $"{e.Title}.pdf",
                        fileSize = 0L,
                        mimeType = "application/pdf",
                        sourceReference = e.SourceReference,
                        contentSha256 = ""
                    });
                }
            }

            return Ok(new { items = resultItems, total = resultItems.Count });
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
    /// Get single knowledge document details and chunks for viewing
    /// </summary>
    [HttpGet("knowledge-documents/{id}")]
    [RequirePermission(PermissionConstants.Documents.Read, PermissionConstants.Compliance.Read, PermissionConstants.Documents.Manage)]
    public async Task<IActionResult> GetKnowledgeDocumentDetails(string id, CancellationToken cancellationToken)
    {
        try
        {
            var docDetails = await regulatoryClient.GetKnowledgeDocumentAsync(new GetKnowledgeDocumentRequest
            {
                KnowledgeDocumentId = id
            }, cancellationToken: cancellationToken);

            var summary = docDetails.Summary;
            var catName = summary.Category switch
            {
                KnowledgeCategory.Sop => "SOP",
                KnowledgeCategory.Contract => "Carrier Contract",
                KnowledgeCategory.Guide => "Customs Guideline",
                KnowledgeCategory.InternalPolicy => "Internal Policy",
                _ => summary.Category.ToString()
            };

            // Query chunks for this document
            var queryResponse = await regulatoryClient.QueryKnowledgeAsync(new QueryKnowledgeRequest
            {
                Query = summary.Title,
                TopK = 50,
                MinimumRelevanceScore = 0.0
            }, cancellationToken: cancellationToken);

            var chunks = queryResponse.Evidence
                .Where(e => e.KnowledgeDocumentId == id)
                .Select(e => new
                {
                    chunkId = e.ChunkId,
                    section = e.SectionLabel,
                    page = e.PageLabel,
                    excerpt = e.Excerpt,
                    score = e.RelevanceScore
                }).ToList();

            var version = summary.LatestVersion;
            var fullExcerpt = chunks.Count > 0
                ? string.Join("\n\n---\n\n", chunks.Select(c => $"[Section: {c.section ?? "General"} | Page: {c.page ?? "1"}]\n{c.excerpt}"))
                : $"# {summary.Title}\n\nCategory: {catName}\nLanguage: {summary.LanguageCode}\nSource: {summary.SourceReference}\n\n[Document is indexed and ready in pgvector]";

            return Ok(new
            {
                id = summary.Id,
                title = summary.Title,
                category = catName,
                language = string.IsNullOrWhiteSpace(summary.LanguageCode) ? "English" : summary.LanguageCode,
                version = version?.VersionLabel ?? "v1.0",
                chunks = version?.ChunkCount ?? chunks.Count,
                ingestDate = summary.CreatedAt != null ? summary.CreatedAt.ToDateTime().ToString("yyyy-MM-dd") : DateTime.UtcNow.ToString("yyyy-MM-dd"),
                status = version?.Status == RegulatoryIngestionStatus.Completed ? "Completed" : "Processing",
                fileName = version?.FileName ?? $"{summary.Title}.pdf",
                fileSize = version?.SizeBytes ?? 0,
                mimeType = version?.MimeType ?? "application/pdf",
                sourceReference = summary.SourceReference,
                contentSha256 = version?.ContentSha256,
                excerpt = fullExcerpt,
                chunksList = chunks
            });
        }
        catch (RpcException ex)
        {
            logger.LogWarning(ex, "Failed to get knowledge document details for {Id}", id);
            return ex.ToActionResult();
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
