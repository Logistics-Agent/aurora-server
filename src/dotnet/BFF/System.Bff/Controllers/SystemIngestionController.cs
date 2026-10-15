using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Asp.Versioning;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RegulatoryCompliance.Grpc;
using Shared.Security;

namespace SystemBff.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/system/ingestion")]
[Route("api/system")]
public sealed class SystemIngestionController(
    RegulatoryComplianceService.RegulatoryComplianceServiceClient regulatoryClient,
    ICurrentUserService currentUser,
    IConfiguration configuration,
    ILogger<SystemIngestionController> logger,
    AiGovernance.Grpc.AiExecutionService.AiExecutionServiceClient aiExecutionClient,
    DocumentOcr.Grpc.DocumentOcrService.DocumentOcrServiceClient documentOcrClient)
    : ControllerBase
{
    private Metadata CreateHeaders()
    {
        var headers = new Metadata
        {
            { "x-service-id", "system-bff" }
        };

        var tenantId = currentUser.TenantId ?? Guid.Empty;
        headers.Add("x-tenant-id", tenantId.ToString());

        if (currentUser.UserId.HasValue)
            headers.Add("x-user-id", currentUser.UserId.Value.ToString());

        if (!string.IsNullOrEmpty(currentUser.Role))
            headers.Add("x-role", currentUser.Role);

        return headers;
    }

    /// <summary>
    /// Document Pipeline: Upload Original File (.pdf, .docx, .md, .txt) -> R2 Storage -> Text Extraction -> OCR Fallback -> Normalize -> Chunk -> Embedding
    /// </summary>
    [HttpPost("regulatory-sources/upload")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadAndIngestRegulatorySource(
        [FromForm] string title,
        [FromForm] string authority,
        [FromForm] int? regulationType,
        [FromForm] string? jurisdictionCode,
        [FromForm] string? canonicalSourceUri,
        [FromForm] string? versionLabel,
        [FromForm] DateTimeOffset? effectiveFrom,
        [FromForm] DateTimeOffset? publishedAt,
        [FromForm] string? idempotencyKey,
        IFormFile file,
        CancellationToken cancellationToken)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { error = "A valid document file (.pdf, .docx, .md, .txt) is required." });

        if (string.IsNullOrWhiteSpace(title))
            title = Path.GetFileNameWithoutExtension(file.FileName);

        if (string.IsNullOrWhiteSpace(authority))
            return BadRequest(new { error = "Authority is required for regulatory ingestion." });

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (ext is not (".pdf" or ".docx" or ".md" or ".txt"))
            return BadRequest(new { error = $"Unsupported document extension '{ext}'. Only .pdf, .docx, .md, and .txt are supported." });

        var uploadId = Guid.NewGuid().ToString("N");
        var safeFileName = Path.GetFileName(file.FileName);

        try
        {
            // Step 1: Upload original file to Cloudflare R2
            var (storageReference, storageUrl) = await UploadOriginalFileToR2Async(file, uploadId, safeFileName, "regulatory", cancellationToken);

            // Read file bytes for extraction
            using var ms = new MemoryStream();
            await file.CopyToAsync(ms, cancellationToken);
            var fileBytes = ms.ToArray();

            // Step 2: Text Extraction & OCR fallback when required
            var (extractedText, extractionStatus, pageCount) = await ExtractTextFromDocumentAsync(
                fileBytes,
                safeFileName,
                ext,
                file.ContentType,
                storageReference,
                cancellationToken);

            // Step 3: Normalize text
            var normalizedText = NormalizeDocumentText(extractedText);
            if (string.IsNullOrWhiteSpace(normalizedText))
            {
                return BadRequest(new
                {
                    error = "Failed to extract usable text from the document. The document has no text layer and OCR could not extract readable content."
                });
            }

            var contentBytes = Encoding.UTF8.GetBytes(normalizedText);
            var computedSha256 = Convert.ToHexString(SHA256.HashData(contentBytes)).ToLowerInvariant();

            // Step 4: Call Regulatory Ingestion (Chunks, embeddings, pgvector)
            var resolvedCanonicalUri = !string.IsNullOrWhiteSpace(canonicalSourceUri) &&
                                       (canonicalSourceUri.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                                        canonicalSourceUri.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                ? canonicalSourceUri
                : storageUrl;

            var ingestRequest = new IngestRegulatorySourceRequest
            {
                IdempotencyKey = !string.IsNullOrWhiteSpace(idempotencyKey)
                    ? idempotencyKey
                    : Guid.NewGuid().ToString(),
                Authority = authority,
                Title = title,
                CanonicalSourceUri = resolvedCanonicalUri,
                JurisdictionCode = string.IsNullOrWhiteSpace(jurisdictionCode) ? "GLOBAL" : jurisdictionCode.Trim().ToUpperInvariant(),
                RegulationType = (RegulationType)(regulationType ?? 1),
                LanguageCode = "en",
                VersionLabel = string.IsNullOrWhiteSpace(versionLabel) ? "1.0" : versionLabel.Trim(),
                PublishedAt = Timestamp.FromDateTimeOffset(publishedAt ?? DateTimeOffset.UtcNow),
                EffectiveFrom = Timestamp.FromDateTimeOffset(effectiveFrom ?? DateTimeOffset.UtcNow),
                ContentReference = storageReference,
                FileName = safeFileName,
                MimeType = "text/plain", // pure extracted text for embedding
                SizeBytes = contentBytes.Length,
                ContentSha256 = computedSha256,
                Content = ByteString.CopyFrom(contentBytes),
                Visibility = RegulatorySourceVisibility.Platform
            };

            var headers = CreateHeaders();
            var response = await regulatoryClient.IngestRegulatorySourceAsync(ingestRequest, headers, cancellationToken: cancellationToken);

            // Snippet preview (limited 400 chars, no placeholders)
            var previewLength = Math.Min(400, normalizedText.Length);
            var preview = normalizedText[..previewLength] + (normalizedText.Length > previewLength ? "..." : "");

            logger.LogInformation("Successfully ingested regulatory document {Title} ({FileName}). Extracted {Chars} chars ({Chunks} chunks, Status: {Status})",
                title, safeFileName, normalizedText.Length, response.ChunkCount, extractionStatus);

            return Ok(new
            {
                regulatoryDocumentId = response.RegulatoryDocumentId,
                documentVersionId = response.DocumentVersionId,
                status = response.Status.ToString(),
                chunkCount = response.ChunkCount,
                replayed = response.Replayed,
                extractionStatus,
                pageCount,
                storageReference,
                canonicalSourceUri = resolvedCanonicalUri,
                extractedPreview = preview,
                source = "SYSTEM_AUTOMATION"
            });
        }
        catch (RpcException ex)
        {
            logger.LogWarning(ex, "gRPC error during regulatory file ingestion");
            return StatusCode((int)ex.StatusCode, new { error = ex.Status.Detail ?? ex.Message });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error during regulatory file ingestion");
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Automated System Ingestion: Global Laws & Treaties (Raw Text Mode)
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
            : (content.Length > 0
                ? Convert.ToHexString(SHA256.HashData(content.ToByteArray())).ToLowerInvariant()
                : new string('0', 64));

        var ingestRequest = new IngestRegulatorySourceRequest
        {
            IdempotencyKey = !string.IsNullOrWhiteSpace(request.IdempotencyKey)
                ? request.IdempotencyKey
                : Guid.NewGuid().ToString(),
            Authority = request.Authority,
            Title = request.Title,
            CanonicalSourceUri = !string.IsNullOrWhiteSpace(request.CanonicalSourceUri)
                ? request.CanonicalSourceUri
                : (!string.IsNullOrWhiteSpace(request.ContentReference) && (request.ContentReference.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || request.ContentReference.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                    ? request.ContentReference
                    : $"https://aurora.system/regulatory/law/{Guid.NewGuid()}"),
            JurisdictionCode = request.JurisdictionCode ?? "GLOBAL",
            RegulationType = (RegulationType)(int)request.RegulationType,
            LanguageCode = request.LanguageCode ?? "en",
            VersionLabel = request.VersionLabel ?? "1.0",
            PublishedAt = Timestamp.FromDateTimeOffset(request.PublishedAt ?? DateTimeOffset.UtcNow),
            EffectiveFrom = Timestamp.FromDateTimeOffset(request.EffectiveFrom ?? DateTimeOffset.UtcNow),
            ContentReference = !string.IsNullOrWhiteSpace(request.ContentReference)
                ? request.ContentReference
                : $"regulatory/system-{Guid.NewGuid()}",
            FileName = request.FileName ?? "system-law.txt",
            MimeType = request.MimeType ?? "text/plain",
            SizeBytes = computedSizeBytes,
            ContentSha256 = computedSha256,
            Content = content,
            Visibility = RegulatorySourceVisibility.Platform
        };

        var headers = CreateHeaders();
        var response = await regulatoryClient.IngestRegulatorySourceAsync(ingestRequest, headers, cancellationToken: cancellationToken);

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
            var headers = CreateHeaders();
            var rpcRequest = new ListRegulatorySourcesRequest
            {
                Page = 1,
                PageSize = 100,
                JurisdictionCode = jurisdictionCode?.Trim() ?? string.Empty
            };

            var response = await regulatoryClient.ListRegulatorySourcesAsync(rpcRequest, headers, cancellationToken: cancellationToken);

            var searchTerm = query?.Trim();
            var items = response.Sources
                .Where(s => string.IsNullOrWhiteSpace(searchTerm) ||
                            s.Title.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                            s.Authority.Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
                .Select(s => new
                {
                    id = s.Id,
                    title = s.Title,
                    authority = s.Authority,
                    canonicalSourceUri = !string.IsNullOrWhiteSpace(s.CanonicalSourceUri)
                        ? s.CanonicalSourceUri
                        : $"https://aurora.system/laws/{s.Id}",
                    jurisdictionCode = s.JurisdictionCode,
                    regulationType = s.RegulationType.ToString(),
                    languageCode = s.LanguageCode,
                    versionLabel = s.LatestVersion?.VersionLabel ?? "1.0",
                    chunkCount = s.LatestVersion?.ChunkCount ?? 0,
                    status = s.LatestVersion?.Status.ToString() ?? "Completed",
                    relevanceScore = 1.0,
                    excerpt = $"{s.Title} — Authority: {s.Authority} ({s.JurisdictionCode})"
                }).ToList();

            return Ok(new { items, total = items.Count });
        }
        catch (RpcException)
        {
            return Ok(new { items = Array.Empty<object>(), total = 0 });
        }
        catch (Exception)
        {
            return Ok(new { items = Array.Empty<object>(), total = 0 });
        }
    }

    /// <summary>
    /// Knowledge Document Pipeline: Upload Original File (.pdf, .docx, .md, .txt) -> R2 -> Extract -> Ingest
    /// </summary>
    [HttpPost("knowledge-documents/upload")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadAndIngestKnowledgeDocument(
        [FromForm] string title,
        [FromForm] int? category,
        [FromForm] string? versionLabel,
        [FromForm] string? sourceReference,
        [FromForm] string? idempotencyKey,
        IFormFile file,
        CancellationToken cancellationToken)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { error = "A valid document file (.pdf, .docx, .md, .txt) is required." });

        if (string.IsNullOrWhiteSpace(title))
            title = Path.GetFileNameWithoutExtension(file.FileName);

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (ext is not (".pdf" or ".docx" or ".md" or ".txt"))
            return BadRequest(new { error = $"Unsupported document extension '{ext}'. Only .pdf, .docx, .md, and .txt are supported." });

        var uploadId = Guid.NewGuid().ToString("N");
        var safeFileName = Path.GetFileName(file.FileName);

        try
        {
            // Upload to R2
            var (storageReference, storageUrl) = await UploadOriginalFileToR2Async(file, uploadId, safeFileName, "knowledge", cancellationToken);

            using var ms = new MemoryStream();
            await file.CopyToAsync(ms, cancellationToken);
            var fileBytes = ms.ToArray();

            // Extract text
            var (extractedText, extractionStatus, pageCount) = await ExtractTextFromDocumentAsync(
                fileBytes,
                safeFileName,
                ext,
                file.ContentType,
                storageReference,
                cancellationToken);

            var normalizedText = NormalizeDocumentText(extractedText);
            if (string.IsNullOrWhiteSpace(normalizedText))
                return BadRequest(new { error = "Failed to extract usable text content from document." });

            var contentBytes = Encoding.UTF8.GetBytes(normalizedText);
            var computedSha256 = Convert.ToHexString(SHA256.HashData(contentBytes)).ToLowerInvariant();

            var ingestRequest = new IngestKnowledgeSourceRequest
            {
                IdempotencyKey = !string.IsNullOrWhiteSpace(idempotencyKey)
                    ? idempotencyKey
                    : Guid.NewGuid().ToString(),
                Title = title,
                Category = (KnowledgeCategory)(category ?? 1),
                SourceReference = !string.IsNullOrWhiteSpace(sourceReference) ? sourceReference : storageUrl,
                LanguageCode = "en",
                VersionLabel = string.IsNullOrWhiteSpace(versionLabel) ? "1.0" : versionLabel.Trim(),
                ContentReference = storageReference,
                FileName = safeFileName,
                MimeType = "text/plain",
                SizeBytes = contentBytes.Length,
                ContentSha256 = computedSha256,
                Content = ByteString.CopyFrom(contentBytes),
                Visibility = RegulatorySourceVisibility.Platform
            };

            var headers = CreateHeaders();
            var response = await regulatoryClient.IngestKnowledgeDocumentAsync(ingestRequest, headers, cancellationToken: cancellationToken);

            var previewLength = Math.Min(400, normalizedText.Length);
            var preview = normalizedText[..previewLength] + (normalizedText.Length > previewLength ? "..." : "");

            return Ok(new
            {
                knowledgeDocumentId = response.KnowledgeDocumentId,
                documentVersionId = response.DocumentVersionId,
                status = response.Status.ToString(),
                chunkCount = response.ChunkCount,
                replayed = response.Replayed,
                extractionStatus,
                pageCount,
                storageReference,
                canonicalSourceUri = ingestRequest.SourceReference,
                extractedPreview = preview,
                source = "SYSTEM_AUTOMATION"
            });
        }
        catch (RpcException ex)
        {
            logger.LogWarning(ex, "gRPC error during knowledge file ingestion");
            return StatusCode((int)ex.StatusCode, new { error = ex.Status.Detail ?? ex.Message });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error during knowledge file ingestion");
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Automated System Ingestion: Global Knowledge & Standards (Raw Text Mode)
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
            : (content.Length > 0
                ? Convert.ToHexString(SHA256.HashData(content.ToByteArray())).ToLowerInvariant()
                : new string('0', 64));

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
            ContentReference = !string.IsNullOrWhiteSpace(request.ContentReference) && request.ContentReference.StartsWith("knowledge/", StringComparison.Ordinal)
                ? request.ContentReference
                : $"knowledge/system-{Guid.NewGuid()}",
            FileName = request.FileName ?? "system-knowledge.txt",
            MimeType = request.MimeType ?? "text/plain",
            SizeBytes = computedSizeBytes,
            ContentSha256 = computedSha256,
            Content = content,
            Visibility = RegulatorySourceVisibility.Platform
        };

        var headers = CreateHeaders();
        var response = await regulatoryClient.IngestKnowledgeDocumentAsync(ingestRequest, headers, cancellationToken: cancellationToken);

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
            var headers = CreateHeaders();
            var rpcRequest = new ListKnowledgeDocumentsRequest
            {
                Page = 1,
                PageSize = 100
            };

            var response = await regulatoryClient.ListKnowledgeDocumentsAsync(rpcRequest, headers, cancellationToken: cancellationToken);

            var searchTerm = query?.Trim();
            var items = response.Documents
                .Where(d => string.IsNullOrWhiteSpace(searchTerm) ||
                            d.Title.Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
                .Select(d => new
                {
                    id = d.Id,
                    title = d.Title,
                    category = d.Category == KnowledgeCategory.Sop ? "SOP" :
                               d.Category == KnowledgeCategory.Contract ? "Contract" :
                               d.Category == KnowledgeCategory.Guide ? "Guide" :
                               d.Category == KnowledgeCategory.InternalPolicy ? "Internal Policy" : d.Category.ToString(),
                    canonicalSourceUri = d.SourceReference,
                    sourceReference = d.SourceReference,
                    versionLabel = d.LatestVersion?.VersionLabel ?? "1.0",
                    chunkCount = d.LatestVersion?.ChunkCount ?? 0,
                    status = d.LatestVersion?.Status.ToString() ?? "Completed",
                    relevanceScore = 1.0,
                    excerpt = $"{d.Title} — Category: {d.Category}"
                }).ToList();

            return Ok(new { items, total = items.Count });
        }
        catch (RpcException)
        {
            return Ok(new { items = Array.Empty<object>(), total = 0 });
        }
        catch (Exception)
        {
            return Ok(new { items = Array.Empty<object>(), total = 0 });
        }
    }

    // ── Pipeline Helpers: R2 Storage & Extraction ─────────────────────────────

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
            ?? "https://6dfb4cbaef26965be1b54caae4bdf3ce.r2.cloudflarestorage.com";
        var s3Bucket = configuration["Storage:S3:Bucket"]
            ?? configuration["R2:BucketName"]
            ?? "aurora-document-ocr";
        var s3AccessKey = configuration["Storage:S3:AccessKey"] ?? configuration["R2:AccessKey"];
        var s3SecretKey = configuration["Storage:S3:SecretKey"] ?? configuration["R2:SecretKey"];
        var s3Region = configuration["Storage:S3:Region"] ?? "auto";

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

    private async Task<(string Text, string ExtractionStatus, int PageCount)> ExtractTextFromDocumentAsync(
        byte[] fileBytes,
        string fileName,
        string ext,
        string contentType,
        string storageReference,
        CancellationToken ct)
    {
        if (ext is ".txt" or ".md")
        {
            var text = Encoding.UTF8.GetString(fileBytes);
            int pageCount = Math.Max(1, text.Split('\n').Length / 40);
            return (text, "PLAIN_TEXT", pageCount);
        }

        if (ext == ".docx")
        {
            var (docxText, docxPages) = ExtractTextFromDocx(fileBytes);
            if (!string.IsNullOrWhiteSpace(docxText))
                return (docxText, "DOCX_NATIVE", docxPages);
        }

        if (ext == ".pdf")
        {
            var (pdfText, pdfPages, hasUsableText) = ExtractTextFromPdf(fileBytes);
            if (hasUsableText)
            {
                return (pdfText, "NATIVE_TEXT", pdfPages);
            }

            // Fallback to OCR service when native PDF text layer is absent or insufficient (< 50 chars)
            logger.LogInformation("PDF document '{FileName}' has no usable native text layer. Initiating OCR Fallback pipeline.", fileName);
            var ocrText = await FallbackOcrExtractionAsync(fileBytes, storageReference, fileName, contentType, ct);
            if (!string.IsNullOrWhiteSpace(ocrText))
            {
                return (ocrText, "OCR_FALLBACK", pdfPages);
            }
        }

        // Generic fallback if all parsing attempts fail
        return (string.Empty, "EXTRACTION_FAILED", 1);
    }

    private static (string Text, int EstimatedPages) ExtractTextFromDocx(byte[] bytes)
    {
        try
        {
            using var ms = new MemoryStream(bytes);
            using var archive = new ZipArchive(ms, ZipArchiveMode.Read);
            var entry = archive.GetEntry("word/document.xml");
            if (entry == null) return (string.Empty, 1);

            using var entryStream = entry.Open();
            var doc = XDocument.Load(entryStream);
            XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";

            var paragraphs = new List<string>();
            int pageBreaks = 0;

            foreach (var p in doc.Descendants(w + "p"))
            {
                var runs = p.Descendants(w + "r");
                var textBuilder = new StringBuilder();
                foreach (var r in runs)
                {
                    if (r.Elements(w + "br").Any(b => (string?)b.Attribute(w + "type") == "page") ||
                        r.Elements(w + "lastRenderedPageBreak").Any())
                    {
                        pageBreaks++;
                    }
                    foreach (var t in r.Elements(w + "t"))
                    {
                        textBuilder.Append(t.Value);
                    }
                }
                var pText = textBuilder.ToString().Trim();
                if (!string.IsNullOrEmpty(pText))
                {
                    paragraphs.Add(pText);
                }
            }

            var fullText = string.Join("\n\n", paragraphs);
            int pages = Math.Max(1, Math.Max(pageBreaks + 1, (paragraphs.Count + 4) / 5));
            return (fullText, pages);
        }
        catch
        {
            return (string.Empty, 1);
        }
    }

    private static (string Text, int PageCount, bool HasUsableText) ExtractTextFromPdf(byte[] bytes)
    {
        var rawTextBuilder = new StringBuilder();
        int pageCount = 0;

        try
        {
            var pdfAscii = Encoding.Latin1.GetString(bytes);
            var pageMatches = Regex.Matches(pdfAscii, @"/Type\s*/Page\b(?!\s*s)");
            pageCount = Math.Max(1, pageMatches.Count);

            var streamRegex = new Regex(@"stream[\r\n]+([\s\S]*?)[\r\n]+endstream", RegexOptions.Compiled);
            var matches = streamRegex.Matches(pdfAscii);

            foreach (Match match in matches)
            {
                var streamContent = match.Groups[1].Value;
                var streamBytes = Encoding.Latin1.GetBytes(streamContent);

                int dictStartIndex = Math.Max(0, match.Index - 300);
                var headerSection = pdfAscii.Substring(dictStartIndex, match.Index - dictStartIndex);

                byte[]? decompressed = null;
                if (headerSection.Contains("/FlateDecode"))
                {
                    try
                    {
                        using var inMs = new MemoryStream(streamBytes);
                        using var outMs = new MemoryStream();
                        using (var zlib = new ZLibStream(inMs, CompressionMode.Decompress))
                        {
                            zlib.CopyTo(outMs);
                        }
                        decompressed = outMs.ToArray();
                    }
                    catch
                    {
                        try
                        {
                            int offset = streamBytes.Length > 2 && streamBytes[0] == 0x78 ? 2 : 0;
                            using var inMs = new MemoryStream(streamBytes, offset, streamBytes.Length - offset);
                            using var outMs = new MemoryStream();
                            using var def = new DeflateStream(inMs, CompressionMode.Decompress);
                            def.CopyTo(outMs);
                            decompressed = outMs.ToArray();
                        }
                        catch { }
                    }
                }

                var textData = decompressed != null ? Encoding.Latin1.GetString(decompressed) : streamContent;

                var btMatches = Regex.Matches(textData, @"BT[\s\S]*?ET");
                foreach (Match bt in btMatches)
                {
                    var block = bt.Value;
                    var tjMatches = Regex.Matches(block, @"\[(.*?)\]\s*TJ", RegexOptions.Singleline);
                    foreach (Match tj in tjMatches)
                    {
                        var inner = tj.Groups[1].Value;
                        var strMatches = Regex.Matches(inner, @"\((.*?)\)");
                        foreach (Match str in strMatches)
                        {
                            var unescaped = UnescapePdfString(str.Groups[1].Value);
                            if (!string.IsNullOrWhiteSpace(unescaped))
                                rawTextBuilder.Append(unescaped).Append(' ');
                        }
                        rawTextBuilder.AppendLine();
                    }

                    var singleMatches = Regex.Matches(block, @"\((.*?)\)\s*(?:Tj|'|"")");
                    foreach (Match sm in singleMatches)
                    {
                        var unescaped = UnescapePdfString(sm.Groups[1].Value);
                        if (!string.IsNullOrWhiteSpace(unescaped))
                            rawTextBuilder.AppendLine(unescaped);
                    }
                }
            }
        }
        catch
        {
            pageCount = Math.Max(1, pageCount);
        }

        var fullExtracted = rawTextBuilder.ToString().Trim();
        bool isUsable = fullExtracted.Length >= 50 &&
                        fullExtracted.Count(char.IsLetterOrDigit) > 20;

        return (fullExtracted, Math.Max(1, pageCount), isUsable);
    }

    private static string UnescapePdfString(string input)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < input.Length; i++)
        {
            if (input[i] == '\\' && i + 1 < input.Length)
            {
                char next = input[++i];
                switch (next)
                {
                    case 'n': sb.Append('\n'); break;
                    case 'r': sb.Append('\r'); break;
                    case 't': sb.Append('\t'); break;
                    case 'b': sb.Append('\b'); break;
                    case 'f': sb.Append('\f'); break;
                    case '(': sb.Append('('); break;
                    case ')': sb.Append(')'); break;
                    case '\\': sb.Append('\\'); break;
                    default:
                        if (char.IsDigit(next) && i + 2 < input.Length && char.IsDigit(input[i + 1]) && char.IsDigit(input[i + 2]))
                        {
                            try
                            {
                                int octal = Convert.ToInt32(input.Substring(i, 3), 8);
                                sb.Append((char)octal);
                                i += 2;
                            }
                            catch { sb.Append(next); }
                        }
                        else
                        {
                            sb.Append(next);
                        }
                        break;
                }
            }
            else
            {
                sb.Append(input[i]);
            }
        }
        return sb.ToString();
    }

    private async Task<string> FallbackOcrExtractionAsync(
        byte[] fileBytes,
        string storageReference,
        string fileName,
        string mimeType,
        CancellationToken ct)
    {
        try
        {
            var headers = CreateHeaders();
            var generateRequest = new AiGovernance.Grpc.AiGenerateRequest
            {
                CapabilityCode = "ocr.extract",
                Prompt = "Extract all text and structured articles from this scanned regulatory document accurately verbatim. Maintain article headers and paragraphs.",
                MaxOutputTokens = 8192,
                EstimatedInputTokens = Math.Max(200, (int)(fileBytes.Length / 100))
            };

            if (!string.IsNullOrWhiteSpace(storageReference))
            {
                generateRequest.InputParts.Add(new AiGovernance.Grpc.AiInputPart
                {
                    File = new AiGovernance.Grpc.AiFileReference
                    {
                        StorageReference = storageReference,
                        MimeType = mimeType,
                        FileName = fileName
                    }
                });
            }

            var ocrResponse = await aiExecutionClient.GenerateAsync(generateRequest, headers, cancellationToken: ct);
            if (!string.IsNullOrWhiteSpace(ocrResponse.Content))
            {
                return ocrResponse.Content.Trim();
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "AiExecutionService OCR extraction fallback failed, trying DocumentOcr gRPC");
        }

        try
        {
            var headers = CreateHeaders();
            var ocrJobReq = new DocumentOcr.Grpc.SubmitOcrJobRequest
            {
                IdempotencyKey = Guid.NewGuid().ToString(),
                StorageReference = storageReference,
                FileName = fileName,
                MimeType = mimeType,
                SizeBytes = fileBytes.Length,
                DocumentTypeHint = DocumentOcr.Grpc.OcrDocumentType.Other,
                ExtractionMode = DocumentOcr.Grpc.OcrExtractionMode.FullText,
                ExternalDocumentId = Guid.NewGuid().ToString(),
                Purpose = DocumentOcr.Grpc.DocumentOcrPurpose.RegulatoryCorpus
            };

            var jobResponse = await documentOcrClient.SubmitOcrJobAsync(ocrJobReq, headers, cancellationToken: ct);
            if (!string.IsNullOrWhiteSpace(jobResponse.FullTextContent))
            {
                return jobResponse.FullTextContent.Trim();
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "DocumentOcrService SubmitOcrJob fallback failed");
        }

        return string.Empty;
    }

    private static string NormalizeDocumentText(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return string.Empty;

        var text = raw.Replace("\r\n", "\n").Replace('\r', '\n');
        var cleaned = new StringBuilder(text.Length);
        foreach (var ch in text)
        {
            if (ch == '\n' || ch == '\t' || !char.IsControl(ch))
                cleaned.Append(ch);
        }

        var normalized = Regex.Replace(cleaned.ToString(), @"\n{3,}", "\n\n");
        return normalized.Trim();
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
