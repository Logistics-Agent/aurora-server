using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Asp.Versioning;
using BuildingBlocks.BFF.Attributes;
using DocumentOcr.Grpc;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RegulatoryCompliance.Grpc;
using Shared.Constants;

namespace StaffBff.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/documents")]
[Route("api")]
[Authorize]
public sealed class DocumentsController(
    DocumentOcrService.DocumentOcrServiceClient documentOcrClient,
    RegulatoryComplianceService.RegulatoryComplianceServiceClient regulatoryClient,
    ILogger<DocumentsController> logger)
    : ControllerBase
{
    // ──────────────────────────────────────────────────────────────────────────
    // BOX 1: SHIPMENT DOCUMENTS (Transaction-Only, Structured Extraction)
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Box 1: Submit a shipment/transaction document (Invoice, Packing List, B/L, Customs Declaration).
    /// Routed strictly to DocumentOcr in STRUCTURED mode. Never auto-indexed into RAG.
    /// </summary>
    [HttpPost("shipment")]
    [HttpPost("shipment-documents")]
    [RequirePermission(PermissionConstants.Shipment.Create, "documents:create")]
    [ProducesResponseType(typeof(UnifiedDocumentStatusResponse), 200)]
    public async Task<IActionResult> SubmitShipmentDocument(
        [FromBody] SubmitShipmentDocumentRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.StorageReference) || string.IsNullOrWhiteSpace(request.FileName))
            return BadRequest(new ProblemDetails
            {
                Title = "INVALID_FILE",
                Detail = "StorageReference and FileName are required.",
                Status = (int)HttpStatusCode.BadRequest
            });

        var ocrRequest = new SubmitOcrJobRequest
        {
            IdempotencyKey = !string.IsNullOrWhiteSpace(request.IdempotencyKey)
                ? request.IdempotencyKey
                : Guid.NewGuid().ToString(),
            StorageReference = request.StorageReference,
            FileName = request.FileName,
            MimeType = request.MimeType ?? "application/pdf",
            SizeBytes = request.SizeBytes > 0 ? request.SizeBytes : 1024,
            DocumentTypeHint = (OcrDocumentType)(int)request.DocumentTypeHint,
            ExtractionMode = OcrExtractionMode.Structured,
            ExternalDocumentId = request.ExternalDocumentId != Guid.Empty
                ? request.ExternalDocumentId.ToString()
                : Guid.NewGuid().ToString(),
            ExternalContextId = request.ShipmentId ?? "TRANSACTION_ONLY"
        };

        var response = await documentOcrClient.SubmitOcrJobAsync(ocrRequest, cancellationToken: cancellationToken);

        var unifiedStatus = MapOcrStatus(response.Status, response.NeedsReview);
        var unifiedStage = MapOcrStage(response.Status);

        return Ok(new UnifiedDocumentStatusResponse(
            response.JobId,
            "SHIPMENT",
            unifiedStatus,
            unifiedStage,
            response.FileName,
            response.NeedsReview,
            response.Confidence,
            response.NormalizedJson,
            null,
            null,
            response.CreatedAt?.ToDateTimeOffset(),
            response.CompletedAt?.ToDateTimeOffset()));
    }

    /// <summary>
    /// Box 1: Poll/Get status and extraction result of a shipment document OCR job.
    /// </summary>
    [HttpGet("shipment/{id}")]
    [HttpGet("shipment-documents/{id}")]
    [RequirePermission(PermissionConstants.Shipment.Read, "documents:read")]
    [ProducesResponseType(typeof(UnifiedDocumentStatusResponse), 200)]
    public async Task<IActionResult> GetShipmentDocumentStatus(
        [FromRoute] string id,
        CancellationToken cancellationToken)
    {
        try
        {
            var job = await documentOcrClient.GetDocumentJobAsync(new GetDocumentJobRequest { JobId = id }, cancellationToken: cancellationToken);

            var unifiedStatus = MapOcrStatus(job.Status, job.NeedsReview);
            var unifiedStage = MapOcrStage(job.Status);

            return Ok(new UnifiedDocumentStatusResponse(
                job.JobId,
                "SHIPMENT",
                unifiedStatus,
                unifiedStage,
                job.FileName,
                job.NeedsReview,
                job.Confidence,
                job.NormalizedJson,
                job.ErrorCode,
                job.ErrorMessage,
                job.CreatedAt?.ToDateTimeOffset(),
                job.CompletedAt?.ToDateTimeOffset()));
        }
        catch (RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.NotFound)
        {
            return NotFound(new ProblemDetails
            {
                Title = "DOCUMENT_NOT_FOUND",
                Detail = $"Shipment document with ID '{id}' was not found.",
                Status = (int)HttpStatusCode.NotFound
            });
        }
    }

    /// <summary>
    /// Box 1: List recent shipment document jobs for the current tenant.
    /// </summary>
    [HttpGet("shipment-documents")]
    [RequirePermission(PermissionConstants.Shipment.Read, "documents:read")]
    [ProducesResponseType(typeof(ListShipmentDocumentsResponse), 200)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> ListShipmentDocuments(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? status = null,
        [FromQuery] string? shipmentId = null,
        CancellationToken cancellationToken = default)
    {
        var rpcRequest = new ListDocumentJobsRequest
        {
            Page = Math.Max(1, page),
            PageSize = Math.Clamp(pageSize, 1, 100),
            ExternalShipmentId = shipmentId ?? string.Empty
        };

        if (!string.IsNullOrWhiteSpace(status) && System.Enum.TryParse<DocumentOcrJobStatus>(status, true, out var parsedStatus))
        {
            rpcRequest.Status = parsedStatus;
        }

        try
        {
            var response = await documentOcrClient.ListDocumentJobsAsync(rpcRequest, cancellationToken: cancellationToken);

            var items = response.Jobs.Select(job => new UnifiedDocumentStatusResponse(
                job.JobId,
                "SHIPMENT",
                MapOcrStatus(job.Status, job.NeedsReview),
                MapOcrStage(job.Status),
                job.FileName,
                job.NeedsReview,
                job.Confidence,
                job.NormalizedJson,
                job.ErrorCode,
                job.ErrorMessage,
                job.CreatedAt?.ToDateTimeOffset(),
                job.CompletedAt?.ToDateTimeOffset()
            )).ToList();

            return Ok(new ListShipmentDocumentsResponse(items, response.Page, response.PageSize, response.TotalItems, response.TotalPages));
        }
        catch (RpcException ex) when (ex.StatusCode is Grpc.Core.StatusCode.Unavailable or Grpc.Core.StatusCode.DeadlineExceeded)
        {
            logger.LogWarning("DocumentOcr service unavailable for ListShipmentDocuments: {StatusCode}", ex.StatusCode);
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                DocumentsContract.CreateUnavailableProblemDetails());
        }
    }

    /// <summary>
    /// Box 1 OCR Human Review: Get detailed field-level review payload for a document requiring review.
    /// </summary>
    [HttpGet("shipment-documents/{id}/review")]
    [RequirePermission(PermissionConstants.Ocr.Review)]
    [ProducesResponseType(typeof(OcrReviewDetailsResponse), 200)]
    public async Task<IActionResult> GetShipmentDocumentReview(
        [FromRoute] string id,
        CancellationToken cancellationToken)
    {
        try
        {
            var job = await documentOcrClient.GetDocumentJobAsync(new GetDocumentJobRequest { JobId = id }, cancellationToken: cancellationToken);

            var fields = new List<OcrFieldReviewItem>();
            var reasons = new List<string>();

            if (!string.IsNullOrWhiteSpace(job.NormalizedJson))
            {
                try
                {
                    using var doc = JsonDocument.Parse(job.NormalizedJson);
                    var fieldConfidences = ParseFieldConfidences(job.FieldConfidenceJson);
                    foreach (var prop in doc.RootElement.EnumerateObject())
                    {
                        var fieldVal = prop.Value.ToString();
                        var fieldConf = fieldConfidences.TryGetValue(prop.Name, out var confidence)
                            ? confidence
                            : job.Confidence;
                        var fieldNeedsReview = fieldConf < 0.80 || string.IsNullOrWhiteSpace(fieldVal);

                        fields.Add(new OcrFieldReviewItem(
                            prop.Name,
                            fieldVal,
                            fieldConf,
                            fieldNeedsReview
                        ));
                    }
                }
                catch
                {
                    fields.Add(new OcrFieldReviewItem("rawExtraction", job.NormalizedJson, job.Confidence, true));
                }
            }

            if (job.Confidence < 0.80)
                reasons.Add("LOW_CONFIDENCE");
            if (fields.Any(f => f.NeedsReview))
                reasons.Add("MISSING_OR_UNCERTAIN_FIELDS");
            if (reasons.Count == 0)
                reasons.Add("MANUAL_REVIEW_REQUESTED");

            return Ok(new OcrReviewDetailsResponse(
                job.ExternalDocumentId,
                job.JobId,
                MapOcrStatus(job.Status, job.NeedsReview),
                job.ArtifactReference,
                job.DetectedDocumentType.ToString(),
                job.Confidence,
                reasons,
                fields));
        }
        catch (RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.NotFound)
        {
            return NotFound(new ProblemDetails
            {
                Title = "DOCUMENT_NOT_FOUND",
                Detail = $"Shipment document with ID '{id}' was not found.",
                Status = (int)HttpStatusCode.NotFound
            });
        }
    }

    /// <summary>
    /// Box 1 OCR Human Review: Submit review decision (CONFIRM, CORRECT, REJECT).
    /// Preserves original AI extraction alongside human corrections and audit trail.
    /// </summary>
    [HttpPost("shipment-documents/{id}/review")]
    [RequirePermission(PermissionConstants.Ocr.Review)]
    [ProducesResponseType(typeof(UnifiedDocumentStatusResponse), 200)]
    public async Task<IActionResult> SubmitShipmentDocumentReview(
        [FromRoute] string id,
        [FromBody] SubmitOcrReviewRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Action))
            return BadRequest(new ProblemDetails { Title = "INVALID_REQUEST", Detail = "Action (CONFIRM, CORRECT, REJECT) is required." });

        var action = request.Action.Trim().ToUpperInvariant();
        if (action is not ("CONFIRM" or "CORRECT" or "REJECT"))
            return BadRequest(new ProblemDetails { Title = "INVALID_REQUEST", Detail = "Action must be CONFIRM, CORRECT, or REJECT." });
        if (action == "CORRECT" && (request.Fields is null || request.Fields.Count == 0))
            return BadRequest(new ProblemDetails { Title = "INVALID_REQUEST", Detail = "Fields are required for CORRECT." });

        string? correctedJson = null;
        if (action == "CORRECT" && request.Fields != null)
        {
            if (request.Fields.Any(field => string.IsNullOrWhiteSpace(field.Name)))
                return BadRequest(new ProblemDetails { Title = "INVALID_REQUEST", Detail = "Every corrected field must have a name." });
            var dict = request.Fields.ToDictionary(f => f.Name, f => (object)f.Value);
            correctedJson = JsonSerializer.Serialize(dict);
        }

        try
        {
            var updatedJob = await documentOcrClient.ReviewDocumentJobAsync(new ReviewDocumentJobRequest
            {
                JobId = id,
                Action = action,
                CorrectedJson = correctedJson ?? string.Empty,
                Comment = request.Comment ?? string.Empty
            }, cancellationToken: cancellationToken);

            return Ok(new UnifiedDocumentStatusResponse(
                updatedJob.JobId,
                "SHIPMENT",
                MapOcrStatus(updatedJob.Status, updatedJob.NeedsReview),
                MapOcrStage(updatedJob.Status),
                updatedJob.FileName,
                updatedJob.NeedsReview,
                updatedJob.Confidence,
                updatedJob.NormalizedJson,
                updatedJob.ErrorCode,
                updatedJob.ErrorMessage,
                updatedJob.CreatedAt?.ToDateTimeOffset(),
                updatedJob.CompletedAt?.ToDateTimeOffset()));
        }
        catch (RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.FailedPrecondition)
        {
            return StatusCode((int)HttpStatusCode.Conflict, new ProblemDetails
            {
                Title = "INVALID_STATE_TRANSITION",
                Detail = ex.Status.Detail,
                Status = (int)HttpStatusCode.Conflict
            });
        }
        catch (RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.NotFound)
        {
            return NotFound(new ProblemDetails
            {
                Title = "DOCUMENT_NOT_FOUND",
                Detail = $"Shipment document with ID '{id}' was not found.",
                Status = (int)HttpStatusCode.NotFound
            });
        }
        catch (RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.InvalidArgument)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "INVALID_REQUEST",
                Detail = ex.Status.Detail,
                Status = (int)HttpStatusCode.BadRequest
            });
        }
    }

    /// <summary>
    /// Box 1: Cancel an active shipment document OCR job (Allowed only while RECEIVED or PROCESSING).
    /// </summary>
    [HttpPost("shipment-documents/{id}/cancel")]
    [RequirePermission(PermissionConstants.Shipment.Update, "documents:update")]
    [ProducesResponseType(typeof(UnifiedDocumentStatusResponse), 200)]
    public async Task<IActionResult> CancelShipmentDocument(
        [FromRoute] string id,
        CancellationToken cancellationToken)
    {
        try
        {
            var job = await documentOcrClient.CancelDocumentJobAsync(new CancelDocumentJobRequest { JobId = id }, cancellationToken: cancellationToken);

            return Ok(new UnifiedDocumentStatusResponse(
                job.JobId,
                "SHIPMENT",
                "CANCELLED",
                null,
                job.FileName,
                false,
                job.Confidence,
                job.NormalizedJson,
                "DOCUMENT_CANCELLED",
                "Job was cancelled by user.",
                job.CreatedAt?.ToDateTimeOffset(),
                DateTimeOffset.UtcNow));
        }
        catch (RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.FailedPrecondition)
        {
            return StatusCode((int)HttpStatusCode.Conflict, new ProblemDetails
            {
                Title = "INVALID_STATE_TRANSITION",
                Detail = "A completed, failed, or already cancelled document cannot be cancelled.",
                Status = (int)HttpStatusCode.Conflict
            });
        }
        catch (RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.NotFound)
        {
            return NotFound(new ProblemDetails
            {
                Title = "DOCUMENT_NOT_FOUND",
                Detail = $"Shipment document with ID '{id}' was not found.",
                Status = (int)HttpStatusCode.NotFound
            });
        }
    }

    /// <summary>
    /// Box 1: Retry a failed shipment document OCR job.
    /// </summary>
    [HttpPost("shipment-documents/{id}/retry")]
    [RequirePermission(PermissionConstants.Shipment.Update, "documents:update")]
    [ProducesResponseType(typeof(UnifiedDocumentStatusResponse), 200)]
    public async Task<IActionResult> RetryShipmentDocument(
        [FromRoute] string id,
        CancellationToken cancellationToken)
    {
        try
        {
            var job = await documentOcrClient.RetryDocumentJobAsync(new RetryDocumentJobRequest { JobId = id }, cancellationToken: cancellationToken);

            return Ok(new UnifiedDocumentStatusResponse(
                job.JobId,
                "SHIPMENT",
                "PROCESSING",
                "EXTRACTING",
                job.FileName,
                false,
                job.Confidence,
                job.NormalizedJson,
                null,
                null,
                job.CreatedAt?.ToDateTimeOffset(),
                DateTimeOffset.UtcNow));
        }
        catch (RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.FailedPrecondition)
        {
            return StatusCode((int)HttpStatusCode.Conflict, new ProblemDetails
            {
                Title = "INVALID_STATE_TRANSITION",
                Detail = ex.Status.Detail,
                Status = (int)HttpStatusCode.Conflict
            });
        }
        catch (RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.NotFound)
        {
            return NotFound(new ProblemDetails
            {
                Title = "DOCUMENT_NOT_FOUND",
                Detail = $"Shipment document with ID '{id}' was not found.",
                Status = (int)HttpStatusCode.NotFound
            });
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    // BOX 2: REGULATORY SOURCES (Compliance Law/Rules, TENANT Scope)
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Box 2: Submit a tenant regulatory source (Circular, Decree, Classification Rule).
    /// Scope is strictly TENANT. Ingested into pgvector regulatory corpus.
    /// </summary>
    [HttpPost("regulatory")]
    [HttpPost("regulatory-sources")]
    [RequirePermission(PermissionConstants.Documents.Ingest, "documents:create")]
    [ProducesResponseType(typeof(UnifiedDocumentStatusResponse), 200)]
    public async Task<IActionResult> SubmitRegulatorySource(
        [FromBody] SubmitRegulatorySourceRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Title) || string.IsNullOrWhiteSpace(request.Authority))
            return BadRequest(new ProblemDetails
            {
                Title = "INVALID_FILE",
                Detail = "Title and Authority are required.",
                Status = (int)HttpStatusCode.BadRequest
            });
        if (string.IsNullOrWhiteSpace(request.RawText))
            return BadRequest(new ProblemDetails
            {
                Title = "INVALID_CONTENT",
                Detail = "RawText is required for regulatory corpus ingestion. Submit binary documents through OCR first.",
                Status = (int)HttpStatusCode.BadRequest
            });
        if (!Uri.TryCreate(request.CanonicalSourceUri, UriKind.Absolute, out var canonicalUri) ||
            canonicalUri.Scheme != Uri.UriSchemeHttps || !string.IsNullOrEmpty(canonicalUri.UserInfo))
            return BadRequest(new ProblemDetails
            {
                Title = "INVALID_SOURCE_URI",
                Detail = "CanonicalSourceUri must be an HTTPS provenance URI.",
                Status = (int)HttpStatusCode.BadRequest
            });
        if (string.IsNullOrWhiteSpace(request.ContentReference) ||
            !request.ContentReference.StartsWith("regulatory/", StringComparison.Ordinal) ||
            request.ContentReference.Contains("..", StringComparison.Ordinal))
            return BadRequest(new ProblemDetails
            {
                Title = "INVALID_CONTENT_REFERENCE",
                Detail = "ContentReference must be a regulatory/{path} storage key.",
                Status = (int)HttpStatusCode.BadRequest
            });

        var regulatoryBytes = Encoding.UTF8.GetBytes(request.RawText);

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
            FileName = request.FileName ?? "regulatory-doc.md",
            MimeType = request.MimeType ?? "text/markdown",
            SizeBytes = regulatoryBytes.Length,
            ContentSha256 = Convert.ToHexString(SHA256.HashData(regulatoryBytes)).ToLowerInvariant(),
            Content = ByteString.CopyFrom(regulatoryBytes),
            Visibility = RegulatorySourceVisibility.Tenant // Staff can only create TENANT scope
        };

        var response = await regulatoryClient.IngestRegulatorySourceAsync(ingestRequest, cancellationToken: cancellationToken);

        var (status, stage) = MapIngestionStatus(response.Status);

        return Ok(new UnifiedDocumentStatusResponse(
            response.RegulatoryDocumentId,
            "REGULATORY",
            status,
            stage,
            request.FileName ?? request.Title,
            false,
            1.0,
            null,
            null,
            null,
            response.ReceivedAt?.ToDateTimeOffset(),
            response.Status == RegulatoryIngestionStatus.Completed ? DateTimeOffset.UtcNow : null));
    }

    /// <summary>
    /// Box 2: Query regulatory corpus (PLATFORM + TENANT).
    /// Evidence-first response with citations and relevance scores.
    /// </summary>
    [HttpPost("regulatory/query")]
    [RequirePermission(PermissionConstants.Documents.Read, PermissionConstants.Compliance.Read)]
    [ProducesResponseType(typeof(RegulatoryQueryResponse), 200)]
    public async Task<IActionResult> QueryRegulations(
        [FromBody] RegulatoryQueryRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Query))
            return BadRequest(new ProblemDetails { Title = "INVALID_QUERY", Detail = "Query text is required." });

        var rpcRequest = new QueryRegulationsRequest
        {
            Query = request.Query,
            JurisdictionCode = request.JurisdictionCode ?? string.Empty,
            LanguageCode = "vi",
            EffectiveAt = request.EffectiveAt.HasValue ? Timestamp.FromDateTimeOffset(request.EffectiveAt.Value) : Timestamp.FromDateTimeOffset(DateTimeOffset.UtcNow),
            TopK = request.TopK > 0 ? request.TopK : 10,
            MinimumRelevanceScore = (double)(request.MinimumRelevanceScore > 0 ? request.MinimumRelevanceScore : 0.4m)
        };

        if (request.RegulationTypes != null)
        {
            foreach (var t in request.RegulationTypes)
            {
                rpcRequest.RegulationTypes.Add((RegulationType)t);
            }
        }

        var response = await regulatoryClient.QueryRegulationsAsync(rpcRequest, cancellationToken: cancellationToken);

        var results = response.Evidence.Select(e => new RegulatoryEvidenceItem(
            e.Citation.RegulatoryDocumentId,
            e.Citation.DocumentVersionId,
            e.Citation.ChunkId,
            e.Citation.Title,
            e.Citation.Authority,
            e.JurisdictionCode,
            e.RegulationType.ToString(),
            e.Citation.SectionLabel,
            e.Citation.PageLabel,
            e.Citation.Excerpt,
            e.Citation.RelevanceScore,
            new CitationDetails(e.Citation.DocumentVersionId, e.Citation.ChunkId, e.Citation.CanonicalSourceUri)
        )).ToList();

        return Ok(new RegulatoryQueryResponse(
            request.Query,
            response.RetrievalTraceId,
            response.EvidenceSufficiency.ToString(),
            results,
            response.GeneratedExplanation));
    }

    // ──────────────────────────────────────────────────────────────────────────
    // BOX 3: KNOWLEDGE DOCUMENTS (Company SOP, Contract, Guide, Policy)
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Box 3: Submit a tenant knowledge document (SOP, Contract, Guidelines).
    /// Scope is strictly TENANT. Ingested into pgvector knowledge corpus.
    /// </summary>
    [HttpPost("knowledge")]
    [HttpPost("knowledge-documents")]
    [RequirePermission(PermissionConstants.Documents.Ingest, "documents:create")]
    [ProducesResponseType(typeof(UnifiedDocumentStatusResponse), 200)]
    public async Task<IActionResult> SubmitKnowledgeDocument(
        [FromBody] SubmitKnowledgeDocumentRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
            return BadRequest(new ProblemDetails
            {
                Title = "INVALID_FILE",
                Detail = "Title is required.",
                Status = (int)HttpStatusCode.BadRequest
            });
        if (string.IsNullOrWhiteSpace(request.RawText))
            return BadRequest(new ProblemDetails
            {
                Title = "INVALID_CONTENT",
                Detail = "RawText is required for knowledge corpus ingestion. Submit binary documents through OCR first.",
                Status = (int)HttpStatusCode.BadRequest
            });
        if (string.IsNullOrWhiteSpace(request.ContentReference) ||
            !request.ContentReference.StartsWith("knowledge/", StringComparison.Ordinal) ||
            request.ContentReference.Contains("..", StringComparison.Ordinal))
            return BadRequest(new ProblemDetails
            {
                Title = "INVALID_CONTENT_REFERENCE",
                Detail = "ContentReference must be a knowledge/{path} storage key.",
                Status = (int)HttpStatusCode.BadRequest
            });

        var knowledgeBytes = Encoding.UTF8.GetBytes(request.RawText);

        var ingestRequest = new IngestKnowledgeSourceRequest
        {
            IdempotencyKey = !string.IsNullOrWhiteSpace(request.IdempotencyKey)
                ? request.IdempotencyKey
                : Guid.NewGuid().ToString(),
            Title = request.Title,
            Category = (KnowledgeCategory)(int)request.Category,
            SourceReference = request.SourceReference ?? $"sop://tenant/{Guid.NewGuid()}",
            LanguageCode = request.LanguageCode ?? "vi",
            VersionLabel = request.VersionLabel ?? "1.0",
            ContentReference = request.ContentReference,
            FileName = request.FileName ?? "knowledge.md",
            MimeType = request.MimeType ?? "text/markdown",
            SizeBytes = knowledgeBytes.Length,
            ContentSha256 = Convert.ToHexString(SHA256.HashData(knowledgeBytes)).ToLowerInvariant(),
            Content = ByteString.CopyFrom(knowledgeBytes),
            Visibility = RegulatorySourceVisibility.Tenant
        };

        var response = await regulatoryClient.IngestKnowledgeDocumentAsync(ingestRequest, cancellationToken: cancellationToken);

        var (status, stage) = MapIngestionStatus(response.Status);

        return Ok(new UnifiedDocumentStatusResponse(
            response.KnowledgeDocumentId,
            "KNOWLEDGE",
            status,
            stage,
            request.FileName ?? request.Title,
            false,
            1.0,
            null,
            null,
            null,
            response.ReceivedAt?.ToDateTimeOffset(),
            response.Status == RegulatoryIngestionStatus.Completed ? DateTimeOffset.UtcNow : null));
    }

    /// <summary>
    /// Box 3: Query knowledge corpus (SOPs, Guides, Contracts for PLATFORM + TENANT).
    /// </summary>
    [HttpPost("knowledge/query")]
    [RequirePermission(PermissionConstants.Documents.Read, PermissionConstants.Compliance.Read)]
    [ProducesResponseType(typeof(KnowledgeQueryResponse), 200)]
    public async Task<IActionResult> QueryKnowledge(
        [FromBody] KnowledgeQueryRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Query))
            return BadRequest(new ProblemDetails { Title = "INVALID_QUERY", Detail = "Query text is required." });

        var rpcRequest = new QueryKnowledgeRequest
        {
            Query = request.Query,
            TopK = request.TopK > 0 ? request.TopK : 10,
            MinimumRelevanceScore = (double)(request.MinimumRelevanceScore > 0 ? request.MinimumRelevanceScore : 0.4m)
        };

        if (request.Categories != null)
        {
            foreach (var cat in request.Categories)
            {
                rpcRequest.Categories.Add((KnowledgeCategory)cat);
            }
        }

        var response = await regulatoryClient.QueryKnowledgeAsync(rpcRequest, cancellationToken: cancellationToken);

        var results = response.Evidence.Select(e => new KnowledgeEvidenceItem(
            e.KnowledgeDocumentId,
            e.DocumentVersionId,
            e.ChunkId,
            e.Title,
            e.Category.ToString(),
            e.SectionLabel,
            e.PageLabel,
            e.Excerpt,
            e.RelevanceScore,
            new CitationDetails(e.DocumentVersionId, e.ChunkId, string.Empty)
        )).ToList();

        return Ok(new KnowledgeQueryResponse(request.Query, response.RetrievalTraceId, results));
    }

    // ──────────────────────────────────────────────────────────────────────────
    // BOX 4: GENERAL / INTERNAL DOCUMENTS (Store-Only, DO_NOT_INDEX)
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Box 4: Register attachment / internal reference file.
    /// Stored as attachment metadata only. Default policy: DO_NOT_INDEX.
    /// </summary>
    [HttpPost("general")]
    [HttpPost("general-documents")]
    [ProducesResponseType(typeof(UnifiedDocumentStatusResponse), 200)]
    public IActionResult SubmitGeneralDocument([FromBody] SubmitGeneralDocumentRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.FileName) || string.IsNullOrWhiteSpace(request.StorageReference))
            return BadRequest(new ProblemDetails
            {
                Title = "INVALID_FILE",
                Detail = "FileName and StorageReference are required.",
                Status = (int)HttpStatusCode.BadRequest
            });

        var id = Guid.NewGuid().ToString();
        var now = DateTimeOffset.UtcNow;

        return Ok(new UnifiedDocumentStatusResponse(
            id,
            "GENERAL",
            "READY",
            "READY",
            request.FileName,
            false,
            1.0,
            null,
            null,
            null,
            now,
            now));
    }

    /// <summary>
    /// Box 4 Promote: Promote a general document to a Knowledge Document (SOP/Guide/Contract) without re-uploading binary.
    /// Reuses existing StorageReference, triggers text extraction, chunking, knowledge.embed, and pgvector indexing.
    /// </summary>
    [HttpPost("general-documents/{id}/promote-to-knowledge")]
    [ProducesResponseType(typeof(UnifiedDocumentStatusResponse), 200)]
    public async Task<IActionResult> PromoteGeneralDocumentToKnowledge(
        [FromRoute] string id,
        [FromBody] PromoteGeneralDocumentRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
            return BadRequest(new ProblemDetails
            {
                Title = "INVALID_REQUEST",
                Detail = "Title is required for knowledge promotion.",
                Status = (int)HttpStatusCode.BadRequest
            });

        if (string.IsNullOrWhiteSpace(request.StorageReference))
            return BadRequest(new ProblemDetails
            {
                Title = "INVALID_FILE",
                Detail = "Existing StorageReference is required for promotion.",
                Status = (int)HttpStatusCode.BadRequest
            });

        var ingestRequest = new IngestKnowledgeSourceRequest
        {
            IdempotencyKey = Guid.NewGuid().ToString(),
            Title = request.Title,
            Category = (KnowledgeCategory)request.Category,
            SourceReference = $"general-promoted://{id}",
            LanguageCode = request.LanguageCode ?? "vi",
            VersionLabel = "1.0",
            ContentReference = request.StorageReference,
            FileName = request.FileName ?? "promoted-knowledge.pdf",
            MimeType = request.MimeType ?? "application/pdf",
            SizeBytes = request.SizeBytes > 0 ? request.SizeBytes : 1024,
            ContentSha256 = new string('0', 64),
            Visibility = RegulatorySourceVisibility.Tenant
        };

        var response = await regulatoryClient.IngestKnowledgeDocumentAsync(ingestRequest, cancellationToken: cancellationToken);
        var (status, stage) = MapIngestionStatus(response.Status);

        return Ok(new UnifiedDocumentStatusResponse(
            response.KnowledgeDocumentId,
            "KNOWLEDGE",
            status,
            stage,
            request.FileName ?? request.Title,
            false,
            1.0,
            null,
            null,
            null,
            response.ReceivedAt?.ToDateTimeOffset(),
            response.Status == RegulatoryIngestionStatus.Completed ? DateTimeOffset.UtcNow : null));
    }

    // ──────────────────────────────────────────────────────────────────────────
    // STATUS & STAGE HELPERS
    // ──────────────────────────────────────────────────────────────────────────

    private static string MapOcrStatus(DocumentOcrJobStatus status, bool needsReview) => status switch
    {
        DocumentOcrJobStatus.Queued => "PROCESSING",
        DocumentOcrJobStatus.Processing => "PROCESSING",
        DocumentOcrJobStatus.RequiresReview => "NEEDS_REVIEW",
        DocumentOcrJobStatus.Completed => needsReview ? "NEEDS_REVIEW" : "READY",
        DocumentOcrJobStatus.Rejected => "REJECTED",
        DocumentOcrJobStatus.Failed => "FAILED",
        DocumentOcrJobStatus.Cancelled => "CANCELLED",
        _ => "RECEIVED"
    };

    private static string? MapOcrStage(DocumentOcrJobStatus status)
        => DocumentsContract.MapStage(status);

    private static Dictionary<string, double> ParseFieldConfidences(string? fieldConfidenceJson)
    {
        if (string.IsNullOrWhiteSpace(fieldConfidenceJson))
            return new(StringComparer.OrdinalIgnoreCase);

        try
        {
            using var document = JsonDocument.Parse(fieldConfidenceJson);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                return new(StringComparer.OrdinalIgnoreCase);

            return document.RootElement.EnumerateObject()
                .Where(property => property.Value.ValueKind == JsonValueKind.Number && property.Value.TryGetDouble(out _))
                .ToDictionary(
                    property => property.Name,
                    property => property.Value.GetDouble(),
                    StringComparer.OrdinalIgnoreCase);
        }
        catch (JsonException)
        {
            return new(StringComparer.OrdinalIgnoreCase);
        }
    }

    private static (string status, string? stage) MapIngestionStatus(RegulatoryIngestionStatus status) => status switch
    {
        RegulatoryIngestionStatus.Pending => ("RECEIVED", "RECEIVING"),
        RegulatoryIngestionStatus.Processing => ("PROCESSING", "EMBEDDING"),
        RegulatoryIngestionStatus.Completed => ("READY", "READY"),
        RegulatoryIngestionStatus.Failed => ("FAILED", null),
        _ => ("PROCESSING", "INDEXING")
    };
}

internal static class DocumentsContract
{
    internal static ProblemDetails CreateUnavailableProblemDetails() => new()
    {
        Title = "DOCUMENT_OCR_UNAVAILABLE",
        Detail = "Document OCR service is temporarily unavailable. Please retry shortly.",
        Status = StatusCodes.Status503ServiceUnavailable
    };

    internal static string? MapStage(DocumentOcrJobStatus status) => status switch
    {
        DocumentOcrJobStatus.Queued => "QUEUED",
        DocumentOcrJobStatus.Processing => "EXTRACTING",
        DocumentOcrJobStatus.RequiresReview => "HUMAN_REVIEW",
        DocumentOcrJobStatus.Completed => "COMPLETED",
        DocumentOcrJobStatus.Failed => "ERROR",
        _ => null
    };
}

// ──────────────────────────────────────────────────────────────────────────────
// PUBLIC DTO CONTRACTS FOR FRONTEND
// ──────────────────────────────────────────────────────────────────────────────

public sealed record UnifiedDocumentStatusResponse(
    string Id,
    string DocumentType,      // SHIPMENT | REGULATORY | KNOWLEDGE | GENERAL
    string Status,            // RECEIVED | PROCESSING | READY | NEEDS_REVIEW | REJECTED | FAILED | CANCELLED
    string? Stage,            // RECEIVING | EXTRACTING | OCR | NORMALIZING | CHUNKING | EMBEDDING | INDEXING | READY | null
    string FileName,
    bool NeedsReview,
    double? Confidence,
    string? NormalizedJson,
    string? ErrorCode,
    string? ErrorMessage,
    DateTimeOffset? CreatedAt,
    DateTimeOffset? UpdatedAt);

public sealed record SubmitShipmentDocumentRequest(
    string? IdempotencyKey,
    string StorageReference,
    string FileName,
    string? MimeType,
    long SizeBytes,
    int DocumentTypeHint,
    Guid ExternalDocumentId,
    string? ShipmentId);

public sealed record ListShipmentDocumentsResponse(
    IReadOnlyList<UnifiedDocumentStatusResponse> Items,
    int Page,
    int PageSize,
    int TotalItems,
    int TotalPages);

public sealed record OcrReviewDetailsResponse(
    string DocumentId,
    string JobId,
    string Status,
    string? OriginalDocumentReference,
    string DocumentType,
    double OverallConfidence,
    IReadOnlyList<string> ReviewReasons,
    IReadOnlyList<OcrFieldReviewItem> Fields);

public sealed record OcrFieldReviewItem(
    string Name,
    string Value,
    double Confidence,
    bool NeedsReview);

public sealed record SubmitOcrReviewRequest(
    string Action,                     // CONFIRM | CORRECT | REJECT
    IReadOnlyList<OcrFieldCorrection>? Fields,
    string? Comment);

public sealed record OcrFieldCorrection(
    string Name,
    string Value);

public sealed record SubmitRegulatorySourceRequest(
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

public sealed record RegulatoryQueryRequest(
    string Query,
    string? JurisdictionCode,
    DateTimeOffset? EffectiveAt,
    IReadOnlyList<int>? RegulationTypes,
    int TopK,
    decimal MinimumRelevanceScore);

public sealed record RegulatoryQueryResponse(
    string Query,
    string RetrievalTraceId,
    string EvidenceSufficiency,
    IReadOnlyList<RegulatoryEvidenceItem> Results,
    string? GeneratedExplanation);

public sealed record RegulatoryEvidenceItem(
    string SourceId,
    string DocumentVersionId,
    string ChunkId,
    string Title,
    string Authority,
    string Jurisdiction,
    string RegulationType,
    string Section,
    string Page,
    string Excerpt,
    double Score,
    CitationDetails Citation);

public sealed record CitationDetails(
    string DocumentVersionId,
    string ChunkId,
    string CanonicalSourceUri);

public sealed record SubmitKnowledgeDocumentRequest(
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

public sealed record KnowledgeQueryRequest(
    string Query,
    IReadOnlyList<int>? Categories,
    int TopK,
    decimal MinimumRelevanceScore);

public sealed record KnowledgeQueryResponse(
    string Query,
    string RetrievalTraceId,
    IReadOnlyList<KnowledgeEvidenceItem> Results);

public sealed record KnowledgeEvidenceItem(
    string KnowledgeDocumentId,
    string DocumentVersionId,
    string ChunkId,
    string Title,
    string Category,
    string Section,
    string Page,
    string Excerpt,
    double Score,
    CitationDetails Citation);

public sealed record SubmitGeneralDocumentRequest(
    string FileName,
    string StorageReference,
    string? MimeType,
    long SizeBytes,
    string? Description);

public sealed record PromoteGeneralDocumentRequest(
    string Title,
    int Category,
    string? StorageReference,
    string? FileName,
    string? MimeType,
    long SizeBytes,
    string? LanguageCode,
    IReadOnlyList<string>? Tags);
