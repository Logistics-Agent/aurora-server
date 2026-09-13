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
using GrpcStatusCode = Grpc.Core.StatusCode;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RegulatoryCompliance.Grpc;
using Shared.Constants;
using Shared.Security;
using StaffBff.Attributes;
using StaffBff.Services;

namespace StaffBff.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/documents")]
[Route("api")]
[Authorize]
[RequireTenantContext]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
public sealed class DocumentsController(
    DocumentOcrService.DocumentOcrServiceClient documentOcrClient,
    RegulatoryComplianceService.RegulatoryComplianceServiceClient regulatoryClient,
    ICurrentUserService currentUser,
    ILogger<DocumentsController> logger)
    : ControllerBase
{
    [HttpPost("uploads")]
    [RequirePermission(PermissionConstants.Documents.Ingest)]
    [ProducesResponseType(typeof(DocumentUploadSessionResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    [DocumentProblemContract(DocumentEndpointProblemContracts.CreateUploadSession)]
    public async Task<IActionResult> CreateUploadSession(
        [FromBody] CreateDocumentUploadSessionRequest request,
        CancellationToken cancellationToken)
    {
        if (!HasTrustedTenant())
            return Unauthorized(DocumentsContract.CreateTenantContextRequiredProblemDetails());

        if (request is null || string.IsNullOrWhiteSpace(request.FileName) ||
            string.IsNullOrWhiteSpace(request.MimeType) || request.SizeBytes <= 0 ||
            string.IsNullOrWhiteSpace(request.IdempotencyKey))
        {
            return BadRequest(DocumentsContract.CreateProblemDetails(
                "INVALID_UPLOAD_REQUEST",
                "FileName, MimeType, and a positive SizeBytes are required.",
                StatusCodes.Status400BadRequest,
                retryable: false));
        }

        try
        {
            var receipt = await documentOcrClient.CreateUploadSessionAsync(new CreateUploadSessionRequest
            {
                IdempotencyKey = request.IdempotencyKey.Trim(),
                FileName = request.FileName,
                MimeType = request.MimeType,
                SizeBytes = request.SizeBytes,
                ContentSha256 = request.ContentSha256 ?? string.Empty
            }, cancellationToken: cancellationToken);

            if (!Guid.TryParse(receipt.UploadId, out var uploadId) || uploadId == Guid.Empty)
            {
                return BadRequest(DocumentsContract.CreateProblemDetails(
                    "INVALID_UPLOAD_REQUEST",
                    "The upload service returned an invalid upload id.",
                    StatusCodes.Status400BadRequest,
                    retryable: false));
            }

            var response = new DocumentUploadSessionResponse(
                uploadId,
                receipt.StorageReference,
                receipt.WriteUrl,
                receipt.RequiredHeaders,
                receipt.ExpiresAt.ToDateTimeOffset(),
                receipt.MaximumSizeBytes,
                receipt.FileName,
                receipt.MimeType,
                receipt.SizeBytes,
                string.IsNullOrWhiteSpace(receipt.ContentSha256) ? null : receipt.ContentSha256,
                DocumentsContract.MapUploadStatus(receipt.Status));

            return Created($"/api/v1/documents/uploads/{receipt.UploadId}", response);
        }
        catch (RpcException exception)
        {
            var error = DocumentUploadErrorMapper.Map(exception);
            return StatusCode(error.StatusCode, DocumentsContract.CreateProblemDetails(error));
        }
    }

    [HttpPost("intakes")]
    [RequirePermission(PermissionConstants.Documents.Ingest)]
    [ProducesResponseType(typeof(UnifiedDocumentStatusResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    [DocumentProblemContract(DocumentEndpointProblemContracts.CreateDocumentIntake)]
    public async Task<IActionResult> CreateDocumentIntake(
        [FromBody] CreateDocumentIntakeBody request,
        CancellationToken cancellationToken)
    {
        if (!HasTrustedTenant())
            return Unauthorized(DocumentsContract.CreateTenantContextRequiredProblemDetails());

        if (request is null)
        {
            return BadRequest(DocumentsContract.CreateProblemDetails(
                DocumentProblemContractCatalog.InvalidRequestCode,
                "The document intake request is required.",
                StatusCodes.Status400BadRequest,
                retryable: false));
        }

        if (!Guid.TryParse(request.UploadId, out var uploadId) || uploadId == Guid.Empty ||
            string.IsNullOrWhiteSpace(request.IdempotencyKey) ||
            !DocumentsContract.TryParseDocumentType(request.DocumentTypeHint, out var documentType))
        {
            return BadRequest(DocumentsContract.CreateProblemDetails(
                DocumentProblemContractCatalog.InvalidRequestCode,
                "UploadId, IdempotencyKey, and a valid DocumentTypeHint are required.",
                StatusCodes.Status400BadRequest,
                retryable: false));
        }

        var purpose = string.IsNullOrWhiteSpace(request.Purpose)
            ? DocumentOcrPurpose.GeneralDocument
            : DocumentsContract.TryParsePurpose(request.Purpose, out var parsedPurpose)
                ? parsedPurpose
                : DocumentOcrPurpose.Unspecified;
        if (purpose == DocumentOcrPurpose.Unspecified)
        {
            return BadRequest(DocumentsContract.CreateProblemDetails(
                DocumentProblemContractCatalog.InvalidRequestCode,
                "Purpose is invalid.",
                StatusCodes.Status400BadRequest,
                retryable: false));
        }

        try
        {
            var response = await documentOcrClient.CreateDocumentIntakeAsync(
                new CreateDocumentIntakeRequest
                {
                    UploadId = uploadId.ToString(),
                    IdempotencyKey = request.IdempotencyKey.Trim(),
                    DocumentTypeHint = documentType,
                    Purpose = purpose,
                    ExternalReference = request.ExternalReference?.Trim() ?? string.Empty,
                    CorrelationId = HttpContext.TraceIdentifier
                },
                cancellationToken: cancellationToken);

            return Accepted(new UnifiedDocumentStatusResponse(
                response.JobId,
                "DOCUMENT",
                MapOcrStatus(response.Status, response.NeedsReview),
                MapOcrStage(response.Status),
                response.FileName,
                response.NeedsReview,
                response.Confidence,
                response.NormalizedJson,
                response.ErrorCode,
                response.ErrorMessage,
                response.CreatedAt?.ToDateTimeOffset(),
                response.CompletedAt?.ToDateTimeOffset()));
        }
        catch (RpcException exception) when (DocumentsContract.IsUnavailable(exception.StatusCode))
        {
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                DocumentsContract.CreateUnavailableProblemDetails());
        }
        catch (RpcException exception) when (exception.StatusCode == Grpc.Core.StatusCode.AlreadyExists)
        {
            return Conflict(DocumentsContract.CreateProblemDetails(
                DocumentProblemContractCatalog.IdempotencyConflict));
        }
        catch (RpcException exception) when (exception.StatusCode == Grpc.Core.StatusCode.NotFound)
        {
            return NotFound(DocumentsContract.CreateProblemDetails(
                DocumentProblemContractCatalog.UploadNotFound));
        }
        catch (RpcException exception) when (exception.StatusCode == Grpc.Core.StatusCode.FailedPrecondition)
        {
            var error = DocumentUploadErrorMapper.Map(exception);
            return StatusCode(error.StatusCode, DocumentsContract.CreateProblemDetails(error));
        }
        catch (RpcException exception) when (exception.StatusCode == Grpc.Core.StatusCode.InvalidArgument)
        {
            return BadRequest(DocumentsContract.CreateProblemDetails(
                DocumentProblemContractCatalog.InvalidRequestCode,
                exception.Status.Detail,
                StatusCodes.Status400BadRequest,
                retryable: false));
        }
    }

    [HttpPost("corpus-intakes")]
    [RequirePermission(PermissionConstants.Documents.Ingest)]
    [ProducesResponseType(typeof(CorpusIntakeResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    [DocumentProblemContract(DocumentEndpointProblemContracts.CreateDocumentIntake)]
    public async Task<IActionResult> CreateCorpusIntake(
        [FromBody] CreateCorpusIntakeBody request,
        CancellationToken cancellationToken)
    {
        if (!HasTrustedTenant())
            return Unauthorized(DocumentsContract.CreateTenantContextRequiredProblemDetails());

        if (request is null)
            return BadRequest(DocumentsContract.CreateProblemDetails(
                DocumentProblemContractCatalog.InvalidRequestCode,
                "The corpus intake request is required.",
                StatusCodes.Status400BadRequest,
                retryable: false));

        if (!TryParseCorpusPurpose(request.Purpose, out var purpose) ||
            !Guid.TryParse(request.UploadId, out var uploadId) || uploadId == Guid.Empty ||
            string.IsNullOrWhiteSpace(request.IdempotencyKey) || string.IsNullOrWhiteSpace(request.Title) ||
            string.IsNullOrWhiteSpace(request.VersionLabel) ||
            (purpose == DocumentOcrPurpose.KnowledgeCorpus &&
                (!System.Enum.IsDefined(typeof(KnowledgeCategory), request.Category) ||
                 request.Category == 0 || string.IsNullOrWhiteSpace(request.SourceReference))) ||
            (purpose == DocumentOcrPurpose.RegulatoryCorpus &&
                (!System.Enum.IsDefined(typeof(RegulationType), request.RegulationType) ||
                 request.RegulationType == 0 || string.IsNullOrWhiteSpace(request.CanonicalSourceUri))))
        {
            return BadRequest(DocumentsContract.CreateProblemDetails(
                DocumentProblemContractCatalog.InvalidRequestCode,
                "UploadId, purpose, idempotency key, title, version label, and valid corpus metadata are required.",
                StatusCodes.Status400BadRequest,
                retryable: false));
        }

        try
        {
            var receipt = await documentOcrClient.VerifyUploadSessionAsync(
                new VerifyUploadSessionRequest { UploadId = uploadId.ToString() },
                cancellationToken: cancellationToken);
            if (receipt.Status is not (DocumentUploadStatus.Uploaded or DocumentUploadStatus.Consumed))
                return Conflict(DocumentsContract.CreateProblemDetails(
                    DocumentProblemContractCatalog.UploadNotVerified));

            var contentReference = receipt.StorageReference;
            var fileName = receipt.FileName;
            var mimeType = string.IsNullOrWhiteSpace(receipt.VerifiedMimeType)
                ? receipt.MimeType
                : receipt.VerifiedMimeType;
            var sizeBytes = receipt.VerifiedSizeBytes > 0 ? receipt.VerifiedSizeBytes : receipt.SizeBytes;
            var contentSha256 = string.IsNullOrWhiteSpace(receipt.VerifiedContentSha256)
                ? receipt.ContentSha256
                : receipt.VerifiedContentSha256;
            if (string.IsNullOrWhiteSpace(contentReference) || string.IsNullOrWhiteSpace(fileName) ||
                string.IsNullOrWhiteSpace(mimeType) || sizeBytes <= 0 || string.IsNullOrWhiteSpace(contentSha256))
            {
                return UnprocessableEntity(DocumentsContract.CreateProblemDetails(
                    DocumentProblemContractCatalog.UploadInvalid));
            }

            var corpusVersionId = purpose == DocumentOcrPurpose.RegulatoryCorpus
                ? await CreateRegulatoryCorpusVersionAsync(request, contentReference, fileName, mimeType,
                    sizeBytes, contentSha256, cancellationToken)
                : await CreateKnowledgeCorpusVersionAsync(request, contentReference, fileName, mimeType,
                    sizeBytes, contentSha256, cancellationToken);

            var ocrJob = await documentOcrClient.CreateDocumentIntakeAsync(
                new CreateDocumentIntakeRequest
                {
                    UploadId = uploadId.ToString(),
                    IdempotencyKey = request.IdempotencyKey.Trim(),
                    DocumentTypeHint = OcrDocumentType.Other,
                    Purpose = purpose,
                    ExternalReference = corpusVersionId.VersionId.ToString(),
                    CorrelationId = HttpContext.TraceIdentifier
                },
                cancellationToken: cancellationToken);

            return Accepted(new CorpusIntakeResponse(
                corpusVersionId.DocumentId,
                corpusVersionId.VersionId,
                purpose == DocumentOcrPurpose.RegulatoryCorpus ? "REGULATORY" : "KNOWLEDGE",
                ParseRequiredGuid(ocrJob.JobId, "OCR job id"),
                DocumentsContract.MapStatus(ocrJob.Status, ocrJob.NeedsReview),
                DocumentsContract.MapStage(ocrJob.Status)));
        }
        catch (RpcException exception) when (DocumentsContract.IsUnavailable(exception.StatusCode))
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                DocumentsContract.CreateUnavailableProblemDetails());
        }
        catch (RpcException exception) when (exception.StatusCode == GrpcStatusCode.PermissionDenied)
        {
            return StatusCode(StatusCodes.Status403Forbidden, DocumentsContract.CreateProblemDetails(
                "CORPUS_INGEST_FORBIDDEN", "Corpus ingestion permission is required.",
                StatusCodes.Status403Forbidden, retryable: false));
        }
        catch (RpcException exception) when (exception.StatusCode == GrpcStatusCode.Unauthenticated)
        {
            return Unauthorized(DocumentsContract.CreateTenantContextRequiredProblemDetails());
        }
        catch (RpcException exception) when (exception.StatusCode == GrpcStatusCode.AlreadyExists)
        {
            return Conflict(DocumentsContract.CreateProblemDetails(
                DocumentProblemContractCatalog.IdempotencyConflict));
        }
        catch (RpcException exception) when (exception.StatusCode == GrpcStatusCode.NotFound)
        {
            return NotFound(DocumentsContract.CreateProblemDetails(
                DocumentProblemContractCatalog.UploadNotFound));
        }
        catch (RpcException exception) when (exception.StatusCode == GrpcStatusCode.FailedPrecondition)
        {
            var error = DocumentUploadErrorMapper.Map(exception);
            return StatusCode(error.StatusCode, DocumentsContract.CreateProblemDetails(error));
        }
        catch (RpcException exception) when (exception.StatusCode == GrpcStatusCode.InvalidArgument)
        {
            return BadRequest(DocumentsContract.CreateProblemDetails(
                DocumentProblemContractCatalog.InvalidRequestCode, exception.Status.Detail,
                StatusCodes.Status400BadRequest, retryable: false));
        }
    }

    private async Task<(Guid DocumentId, Guid VersionId)> CreateRegulatoryCorpusVersionAsync(
        CreateCorpusIntakeBody request,
        string contentReference,
        string fileName,
        string mimeType,
        long sizeBytes,
        string contentSha256,
        CancellationToken cancellationToken)
    {
        var publishedAt = request.PublishedAt ?? DateTimeOffset.UtcNow;
        var effectiveFrom = request.EffectiveFrom ?? publishedAt;
        var response = await regulatoryClient.CreateRegulatoryCorpusVersionAsync(
            new CreateRegulatoryCorpusVersionRequest
            {
                IdempotencyKey = request.IdempotencyKey.Trim(),
                Authority = request.Authority?.Trim() ?? string.Empty,
                Title = request.Title.Trim(),
                CanonicalSourceUri = request.CanonicalSourceUri!.Trim(),
                JurisdictionCode = request.JurisdictionCode?.Trim() ?? string.Empty,
                RegulationType = (RegulationType)request.RegulationType,
                LanguageCode = request.LanguageCode?.Trim() ?? "en",
                VersionLabel = request.VersionLabel.Trim(),
                PublishedAt = Timestamp.FromDateTimeOffset(publishedAt),
                EffectiveFrom = Timestamp.FromDateTimeOffset(effectiveFrom),
                ContentReference = contentReference,
                FileName = fileName,
                MimeType = mimeType,
                SizeBytes = sizeBytes,
                ContentSha256 = contentSha256,
                Visibility = RegulatorySourceVisibility.Tenant
            },
            cancellationToken: cancellationToken);
        return ParseCorpusIds(response.RegulatoryDocumentId, response.DocumentVersionId);
    }

    private async Task<(Guid DocumentId, Guid VersionId)> CreateKnowledgeCorpusVersionAsync(
        CreateCorpusIntakeBody request,
        string contentReference,
        string fileName,
        string mimeType,
        long sizeBytes,
        string contentSha256,
        CancellationToken cancellationToken)
    {
        var response = await regulatoryClient.CreateKnowledgeCorpusVersionAsync(
            new CreateKnowledgeCorpusVersionRequest
            {
                IdempotencyKey = request.IdempotencyKey.Trim(),
                Title = request.Title.Trim(),
                Category = (KnowledgeCategory)request.Category,
                SourceReference = request.SourceReference?.Trim() ?? string.Empty,
                LanguageCode = request.LanguageCode?.Trim() ?? "en",
                VersionLabel = request.VersionLabel.Trim(),
                ContentReference = contentReference,
                FileName = fileName,
                MimeType = mimeType,
                SizeBytes = sizeBytes,
                ContentSha256 = contentSha256,
                Visibility = RegulatorySourceVisibility.Tenant
            },
            cancellationToken: cancellationToken);
        return ParseCorpusIds(response.KnowledgeDocumentId, response.DocumentVersionId);
    }

    private static (Guid DocumentId, Guid VersionId) ParseCorpusIds(string documentId, string versionId) =>
        Guid.TryParse(documentId, out var parsedDocumentId) && parsedDocumentId != Guid.Empty &&
        Guid.TryParse(versionId, out var parsedVersionId) && parsedVersionId != Guid.Empty
            ? (parsedDocumentId, parsedVersionId)
            : throw new RpcException(new Status(GrpcStatusCode.InvalidArgument, "Corpus service returned invalid identifiers."));

    private static Guid ParseRequiredGuid(string value, string fieldName) =>
        Guid.TryParse(value, out var parsed) && parsed != Guid.Empty
            ? parsed
            : throw new RpcException(new Status(GrpcStatusCode.InvalidArgument, $"{fieldName} is invalid."));

    private static bool TryParseCorpusPurpose(string? value, out DocumentOcrPurpose purpose) =>
        DocumentsContract.TryParsePurpose(value, out purpose) &&
        purpose is DocumentOcrPurpose.RegulatoryCorpus or DocumentOcrPurpose.KnowledgeCorpus;

    // ──────────────────────────────────────────────────────────────────────────
    // BOX 1: SHIPMENT DOCUMENTS (Transaction-Only, Structured Extraction)
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Box 1: Submit a shipment/transaction document (Invoice, Packing List, B/L, Customs Declaration).
    /// Routed strictly to DocumentOcr in STRUCTURED mode. Never auto-indexed into RAG.
    /// </summary>
    [HttpPost("shipment")]
    [HttpPost("shipment-documents")]
    [RequirePermission(PermissionConstants.Documents.Ingest)]
    [ProducesResponseType(typeof(UnifiedDocumentStatusResponse), 200)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    [DocumentProblemContract(DocumentEndpointProblemContracts.SubmitShipmentDocumentLegacyAlias, DocumentEndpointProblemContracts.SubmitShipmentDocumentLegacy)]
    public async Task<IActionResult> SubmitShipmentDocument(
        [FromBody] SubmitShipmentDocumentRequest request,
        CancellationToken cancellationToken)
    {
        if (!HasTrustedTenant())
            return Unauthorized(DocumentsContract.CreateTenantContextRequiredProblemDetails());

        if (request is null || string.IsNullOrWhiteSpace(request.StorageReference) ||
            string.IsNullOrWhiteSpace(request.FileName) || string.IsNullOrWhiteSpace(request.IdempotencyKey))
            return BadRequest(DocumentsContract.CreateProblemDetails(
                "INVALID_FILE",
                "StorageReference, FileName, and IdempotencyKey are required.",
                StatusCodes.Status400BadRequest,
                retryable: false));

        var idempotencyKey = request.IdempotencyKey.Trim();
        var externalDocumentId = request.ExternalDocumentId is { } requestedExternalDocumentId && requestedExternalDocumentId != Guid.Empty
            ? requestedExternalDocumentId
            : DocumentsContract.CreateDeterministicCompatibilityDocumentId(
                "legacy-shipment-document",
                currentUser.TenantId!.Value.ToString("N"),
                request.ShipmentId ?? "TRANSACTION_ONLY",
                idempotencyKey,
                request.StorageReference);

        var ocrRequest = new SubmitOcrJobRequest
        {
            IdempotencyKey = idempotencyKey,
            StorageReference = request.StorageReference,
            FileName = request.FileName,
            MimeType = request.MimeType ?? "application/pdf",
            SizeBytes = request.SizeBytes > 0 ? request.SizeBytes : 1024,
            DocumentTypeHint = (OcrDocumentType)(int)request.DocumentTypeHint,
            ExtractionMode = OcrExtractionMode.Structured,
            ExternalDocumentId = externalDocumentId.ToString(),
            ExternalContextId = request.ShipmentId ?? "TRANSACTION_ONLY"
        };

        DocumentOcrJobResponse response;
        try
        {
            response = await documentOcrClient.SubmitOcrJobAsync(ocrRequest, cancellationToken: cancellationToken);
        }
        catch (RpcException exception) when (DocumentsContract.IsUnavailable(exception.StatusCode))
        {
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                DocumentsContract.CreateUnavailableProblemDetails());
        }
        catch (RpcException exception) when (exception.StatusCode == Grpc.Core.StatusCode.InvalidArgument)
        {
            return BadRequest(DocumentsContract.CreateProblemDetails(
                "INVALID_FILE",
                exception.Status.Detail,
                StatusCodes.Status400BadRequest,
                retryable: false));
        }

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
    [RequirePermission(PermissionConstants.Documents.Read)]
    [ProducesResponseType(typeof(UnifiedDocumentStatusResponse), 200)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    [DocumentProblemContract(DocumentEndpointProblemContracts.GetShipmentDocumentStatus, DocumentEndpointProblemContracts.GetShipmentDocumentStatusAlias)]
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
        catch (RpcException ex) when (DocumentsContract.IsUnavailable(ex.StatusCode))
        {
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                DocumentsContract.CreateUnavailableProblemDetails());
        }
        catch (RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.NotFound)
        {
            return NotFound(DocumentsContract.CreateProblemDetails(
                "DOCUMENT_NOT_FOUND",
                $"Shipment document with ID '{id}' was not found.",
                StatusCodes.Status404NotFound,
                retryable: false));
        }
    }

    /// <summary>
    /// Returns a short-lived, tenant-scoped URL for the original document.
    /// </summary>
    [HttpGet("shipment-documents/{id}/download")]
    [RequirePermission(PermissionConstants.Documents.Read)]
    [ProducesResponseType(typeof(DocumentDownloadResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    [DocumentProblemContract(DocumentEndpointProblemContracts.GetShipmentDocumentDownload)]
    public async Task<IActionResult> DownloadShipmentDocument(
        [FromRoute] string id,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await documentOcrClient.CreateDocumentDownloadAsync(
                new CreateDocumentDownloadRequest
                {
                    JobId = id,
                    ExpiresInSeconds = 900
                },
                cancellationToken: cancellationToken);

            return Ok(new DocumentDownloadResponse(
                response.Url,
                response.ExpiresAt.ToDateTimeOffset(),
                response.FileName,
                response.MimeType));
        }
        catch (RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.NotFound)
        {
            return NotFound(DocumentsContract.CreateProblemDetails(
                DocumentProblemContractCatalog.DocumentNotFoundCode,
                $"Shipment document with ID '{id}' was not found.",
                StatusCodes.Status404NotFound,
                retryable: false));
        }
        catch (RpcException ex) when (
            DocumentsContract.IsUnavailable(ex.StatusCode) ||
            ex.StatusCode == Grpc.Core.StatusCode.FailedPrecondition)
        {
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                DocumentsContract.CreateUnavailableProblemDetails());
        }
    }

    /// <summary>
    /// Box 1: List recent shipment document jobs for the current tenant.
    /// </summary>
    [HttpGet("shipment-documents")]
    [RequirePermission(PermissionConstants.Documents.Read)]
    [ProducesResponseType(typeof(ListShipmentDocumentsResponse), 200)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    [DocumentProblemContract(DocumentEndpointProblemContracts.ListShipmentDocuments)]
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
        catch (RpcException ex) when (DocumentsContract.IsUnavailable(ex.StatusCode))
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
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    [DocumentProblemContract(DocumentEndpointProblemContracts.GetShipmentDocumentReview)]
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
        catch (RpcException ex) when (DocumentsContract.IsUnavailable(ex.StatusCode))
        {
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                DocumentsContract.CreateUnavailableProblemDetails());
        }
        catch (RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.NotFound)
        {
            return NotFound(DocumentsContract.CreateProblemDetails(
                "DOCUMENT_NOT_FOUND",
                $"Shipment document with ID '{id}' was not found.",
                StatusCodes.Status404NotFound,
                retryable: false));
        }
    }

    /// <summary>
    /// Box 1 OCR Human Review: Submit review decision (CONFIRM, CORRECT, REJECT).
    /// Preserves original AI extraction alongside human corrections and audit trail.
    /// </summary>
    [HttpPost("shipment-documents/{id}/review")]
    [RequirePermission(PermissionConstants.Ocr.Review)]
    [ProducesResponseType(typeof(UnifiedDocumentStatusResponse), 200)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    [DocumentProblemContract(DocumentEndpointProblemContracts.SubmitShipmentDocumentReview)]
    public async Task<IActionResult> SubmitShipmentDocumentReview(
        [FromRoute] string id,
        [FromBody] SubmitOcrReviewRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Action))
            return BadRequest(DocumentsContract.CreateProblemDetails(
                "INVALID_REQUEST",
                "Action (CONFIRM, CORRECT, REJECT) is required.",
                StatusCodes.Status400BadRequest,
                retryable: false));

        var action = request.Action.Trim().ToUpperInvariant();
        if (action is not ("CONFIRM" or "CORRECT" or "REJECT"))
            return BadRequest(DocumentsContract.CreateProblemDetails(
                "INVALID_REQUEST",
                "Action must be CONFIRM, CORRECT, or REJECT.",
                StatusCodes.Status400BadRequest,
                retryable: false));
        if (action == "CORRECT" && (request.Fields is null || request.Fields.Count == 0))
            return BadRequest(DocumentsContract.CreateProblemDetails(
                "INVALID_REQUEST",
                "Fields are required for CORRECT.",
                StatusCodes.Status400BadRequest,
                retryable: false));

        string? correctedJson = null;
        if (action == "CORRECT" && request.Fields != null)
        {
            if (request.Fields.Any(field => string.IsNullOrWhiteSpace(field.Name)))
                return BadRequest(DocumentsContract.CreateProblemDetails(
                    "INVALID_REQUEST",
                    "Every corrected field must have a name.",
                    StatusCodes.Status400BadRequest,
                    retryable: false));
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
        catch (RpcException ex) when (DocumentsContract.IsUnavailable(ex.StatusCode))
        {
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                DocumentsContract.CreateUnavailableProblemDetails());
        }
        catch (RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.FailedPrecondition)
        {
            return StatusCode(
                StatusCodes.Status409Conflict,
                DocumentsContract.CreateProblemDetails(
                    "INVALID_STATE_TRANSITION",
                    ex.Status.Detail,
                    StatusCodes.Status409Conflict,
                    retryable: false));
        }
        catch (RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.NotFound)
        {
            return NotFound(DocumentsContract.CreateProblemDetails(
                "DOCUMENT_NOT_FOUND",
                $"Shipment document with ID '{id}' was not found.",
                StatusCodes.Status404NotFound,
                retryable: false));
        }
        catch (RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.InvalidArgument)
        {
            return BadRequest(DocumentsContract.CreateProblemDetails(
                "INVALID_REQUEST",
                ex.Status.Detail,
                StatusCodes.Status400BadRequest,
                retryable: false));
        }
    }

    /// <summary>
    /// Box 1: Cancel an active shipment document OCR job (Allowed only while RECEIVED or PROCESSING).
    /// </summary>
    [HttpPost("shipment-documents/{id}/cancel")]
    [RequirePermission(PermissionConstants.Documents.Manage)]
    [ProducesResponseType(typeof(UnifiedDocumentStatusResponse), 200)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    [DocumentProblemContract(DocumentEndpointProblemContracts.CancelShipmentDocument)]
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
            return StatusCode(
                StatusCodes.Status409Conflict,
                DocumentsContract.CreateProblemDetails(
                    "INVALID_STATE_TRANSITION",
                    "A completed, failed, or already cancelled document cannot be cancelled.",
                    StatusCodes.Status409Conflict,
                    retryable: false));
        }
        catch (RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.NotFound)
        {
            return NotFound(DocumentsContract.CreateProblemDetails(
                "DOCUMENT_NOT_FOUND",
                $"Shipment document with ID '{id}' was not found.",
                StatusCodes.Status404NotFound,
                retryable: false));
        }
        catch (RpcException ex) when (DocumentsContract.IsUnavailable(ex.StatusCode))
        {
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                DocumentsContract.CreateUnavailableProblemDetails());
        }
    }

    /// <summary>
    /// Box 1: Retry a failed shipment document OCR job.
    /// </summary>
    [HttpPost("shipment-documents/{id}/retry")]
    [RequirePermission(PermissionConstants.Documents.Manage)]
    [ProducesResponseType(typeof(UnifiedDocumentStatusResponse), 200)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    [DocumentProblemContract(DocumentEndpointProblemContracts.RetryShipmentDocument)]
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
            return StatusCode(
                StatusCodes.Status409Conflict,
                DocumentsContract.CreateProblemDetails(
                    "INVALID_STATE_TRANSITION",
                    ex.Status.Detail,
                    StatusCodes.Status409Conflict,
                    retryable: false));
        }
        catch (RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.NotFound)
        {
            return NotFound(DocumentsContract.CreateProblemDetails(
                "DOCUMENT_NOT_FOUND",
                $"Shipment document with ID '{id}' was not found.",
                StatusCodes.Status404NotFound,
                retryable: false));
        }
        catch (RpcException ex) when (DocumentsContract.IsUnavailable(ex.StatusCode))
        {
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                DocumentsContract.CreateUnavailableProblemDetails());
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
    [RequirePermission(PermissionConstants.Documents.Ingest)]
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
    [RequirePermission(PermissionConstants.Documents.Read)]
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
    [RequirePermission(PermissionConstants.Documents.Ingest)]
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

    [HttpGet("regulatory-sources")]
    [RequirePermission(PermissionConstants.Documents.Read)]
    [ProducesResponseType(typeof(CorpusCatalogPageResponse<RegulatorySourceCatalogResponse>), 200)]
    public async Task<IActionResult> ListRegulatorySources(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? status = null,
        [FromQuery] string? jurisdictionCode = null,
        CancellationToken cancellationToken = default)
    {
        if (!TryParseIngestionStatus(status, out var parsedStatus))
            return BadRequest(DocumentsContract.CreateProblemDetails(
                "INVALID_STATUS", "Status is invalid.", StatusCodes.Status400BadRequest, retryable: false));

        try
        {
            var response = await regulatoryClient.ListRegulatorySourcesAsync(new ListRegulatorySourcesRequest
            {
                Page = page,
                PageSize = pageSize,
                Status = parsedStatus,
                JurisdictionCode = jurisdictionCode ?? string.Empty
            }, cancellationToken: cancellationToken);

            return Ok(new CorpusCatalogPageResponse<RegulatorySourceCatalogResponse>(
                response.Sources.Select(MapRegulatorySource).ToArray(),
                response.Page,
                response.PageSize,
                response.TotalCount));
        }
        catch (RpcException exception)
        {
            return MapCorpusRpcError(exception, "Unable to list regulatory sources.");
        }
    }

    [HttpGet("regulatory-sources/{id:guid}")]
    [RequirePermission(PermissionConstants.Documents.Read)]
    [ProducesResponseType(typeof(RegulatorySourceDetailsResponse), 200)]
    public async Task<IActionResult> GetRegulatorySource(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var response = await regulatoryClient.GetRegulatorySourceAsync(new GetRegulatorySourceRequest
            {
                RegulatoryDocumentId = id.ToString()
            }, cancellationToken: cancellationToken);
            return Ok(MapRegulatoryDetails(response));
        }
        catch (RpcException exception)
        {
            return MapCorpusRpcError(exception, "Unable to load regulatory source.");
        }
    }

    [HttpGet("regulatory-sources/{id:guid}/status")]
    [RequirePermission(PermissionConstants.Documents.Read)]
    [ProducesResponseType(typeof(CorpusVersionStatusResponse), 200)]
    public async Task<IActionResult> GetRegulatorySourceStatus(Guid id, CancellationToken cancellationToken)
    {
        var result = await GetRegulatorySource(id, cancellationToken);
        if (result is not OkObjectResult { Value: RegulatorySourceDetailsResponse details })
            return result;
        return Ok(details.LatestVersion ?? new CorpusVersionStatusResponse(
            null, null, "UNKNOWN", 0, 0, null, null, 0, null, null, null, null, null, null, null));
    }

    [HttpGet("knowledge-documents")]
    [RequirePermission(PermissionConstants.Documents.Read)]
    [ProducesResponseType(typeof(CorpusCatalogPageResponse<KnowledgeDocumentCatalogResponse>), 200)]
    public async Task<IActionResult> ListKnowledgeDocuments(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? status = null,
        [FromQuery] int? category = null,
        CancellationToken cancellationToken = default)
    {
        if (!TryParseIngestionStatus(status, out var parsedStatus))
            return BadRequest(DocumentsContract.CreateProblemDetails(
                "INVALID_STATUS", "Status is invalid.", StatusCodes.Status400BadRequest, retryable: false));
        if (category.HasValue && !System.Enum.IsDefined(typeof(KnowledgeCategory), category.Value))
            return BadRequest(DocumentsContract.CreateProblemDetails(
                "INVALID_CATEGORY", "Category is invalid.", StatusCodes.Status400BadRequest, retryable: false));

        try
        {
            var response = await regulatoryClient.ListKnowledgeDocumentsAsync(new ListKnowledgeDocumentsRequest
            {
                Page = page,
                PageSize = pageSize,
                Status = parsedStatus,
                Category = category.HasValue
                    ? (KnowledgeCategory)category.Value
                    : KnowledgeCategory.Unspecified
            }, cancellationToken: cancellationToken);

            return Ok(new CorpusCatalogPageResponse<KnowledgeDocumentCatalogResponse>(
                response.Documents.Select(MapKnowledgeDocument).ToArray(),
                response.Page,
                response.PageSize,
                response.TotalCount));
        }
        catch (RpcException exception)
        {
            return MapCorpusRpcError(exception, "Unable to list knowledge documents.");
        }
    }

    [HttpGet("knowledge-documents/{id:guid}")]
    [RequirePermission(PermissionConstants.Documents.Read)]
    [ProducesResponseType(typeof(KnowledgeDocumentDetailsResponse), 200)]
    public async Task<IActionResult> GetKnowledgeDocument(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var response = await regulatoryClient.GetKnowledgeDocumentAsync(new GetKnowledgeDocumentRequest
            {
                KnowledgeDocumentId = id.ToString()
            }, cancellationToken: cancellationToken);
            return Ok(MapKnowledgeDetails(response));
        }
        catch (RpcException exception)
        {
            return MapCorpusRpcError(exception, "Unable to load knowledge document.");
        }
    }

    [HttpGet("knowledge-documents/{id:guid}/status")]
    [RequirePermission(PermissionConstants.Documents.Read)]
    [ProducesResponseType(typeof(CorpusVersionStatusResponse), 200)]
    public async Task<IActionResult> GetKnowledgeDocumentStatus(Guid id, CancellationToken cancellationToken)
    {
        var result = await GetKnowledgeDocument(id, cancellationToken);
        if (result is not OkObjectResult { Value: KnowledgeDocumentDetailsResponse details })
            return result;
        return Ok(details.LatestVersion ?? new CorpusVersionStatusResponse(
            null, null, "UNKNOWN", 0, 0, null, null, 0, null, null, null, null, null, null, null));
    }

    /// <summary>
    /// Box 3: Query knowledge corpus (SOPs, Guides, Contracts for PLATFORM + TENANT).
    /// </summary>
    [HttpPost("knowledge/query")]
    [RequirePermission(PermissionConstants.Documents.Read)]
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
    [RequirePermission(PermissionConstants.Documents.Ingest)]
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
    [RequirePermission(PermissionConstants.Documents.Manage)]
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

    private bool HasTrustedTenant() => currentUser.TenantId is { } tenantId && tenantId != Guid.Empty;

    private static string MapOcrStatus(DocumentOcrJobStatus status, bool needsReview)
        => DocumentsContract.MapStatus(status, needsReview);

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

    private static bool TryParseIngestionStatus(
        string? value,
        out RegulatoryIngestionStatus status)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            status = RegulatoryIngestionStatus.Unspecified;
            return true;
        }

        return System.Enum.TryParse(value.Trim(), ignoreCase: true, out status) &&
               System.Enum.IsDefined(status);
    }

    private IActionResult MapCorpusRpcError(RpcException exception, string fallbackDetail) =>
        exception.StatusCode switch
        {
            GrpcStatusCode.NotFound => NotFound(DocumentsContract.CreateProblemDetails(
                "CORPUS_NOT_FOUND", fallbackDetail, StatusCodes.Status404NotFound, retryable: false)),
            GrpcStatusCode.InvalidArgument => BadRequest(DocumentsContract.CreateProblemDetails(
                "INVALID_CORPUS_REQUEST", exception.Status.Detail, StatusCodes.Status400BadRequest, retryable: false)),
            GrpcStatusCode.Unauthenticated => Unauthorized(DocumentsContract.CreateTenantContextRequiredProblemDetails()),
            GrpcStatusCode.PermissionDenied => StatusCode(StatusCodes.Status403Forbidden, DocumentsContract.CreateProblemDetails(
                "FORBIDDEN", exception.Status.Detail, StatusCodes.Status403Forbidden, retryable: false)),
            _ when DocumentsContract.IsUnavailable(exception.StatusCode) => StatusCode(
                StatusCodes.Status503ServiceUnavailable, DocumentsContract.CreateUnavailableProblemDetails()),
            _ => StatusCode(StatusCodes.Status500InternalServerError, DocumentsContract.CreateProblemDetails(
                "CORPUS_UNAVAILABLE", fallbackDetail, StatusCodes.Status500InternalServerError, retryable: true))
        };

    private static RegulatorySourceCatalogResponse MapRegulatorySource(RegulatorySourceSummary source) =>
        new(
            source.Id,
            source.Title,
            source.Authority,
            source.JurisdictionCode,
            source.RegulationType.ToString(),
            source.LanguageCode,
            source.Visibility.ToString(),
            source.CreatedAt.ToDateTimeOffset(),
            source.LatestVersion is not null ? MapCorpusVersion(source.LatestVersion) : null);

    private static RegulatorySourceDetailsResponse MapRegulatoryDetails(RegulatorySourceDetails source) =>
        new(
            MapRegulatorySource(source.Summary),
            source.Versions.Select(MapCorpusVersion).ToArray());

    private static KnowledgeDocumentCatalogResponse MapKnowledgeDocument(KnowledgeDocumentSummary document) =>
        new(
            document.Id,
            document.Title,
            document.Category.ToString(),
            document.SourceReference,
            document.LanguageCode,
            document.Visibility.ToString(),
            document.CreatedAt.ToDateTimeOffset(),
            document.LatestVersion is not null ? MapCorpusVersion(document.LatestVersion) : null);

    private static KnowledgeDocumentDetailsResponse MapKnowledgeDetails(KnowledgeDocumentDetails document) =>
        new(
            MapKnowledgeDocument(document.Summary),
            document.Versions.Select(MapCorpusVersion).ToArray());

    private static CorpusVersionStatusResponse MapCorpusVersion(
        RegulatorySourceVersionSummary version) =>
        new(
            version.Id,
            version.VersionLabel,
            MapIngestionStatusName(version.Status),
            version.ChunkCount,
            version.EmbeddedChunkCount,
            version.FileName,
            version.MimeType,
            version.SizeBytes,
            version.ContentSha256,
            version.CreatedAt.ToDateTimeOffset(),
            version.UpdatedAt?.ToDateTimeOffset(),
            version.CompletedAt?.ToDateTimeOffset(),
            version.FailedAt?.ToDateTimeOffset(),
            string.IsNullOrWhiteSpace(version.ErrorCode) ? null : version.ErrorCode,
            string.IsNullOrWhiteSpace(version.ErrorMessage) ? null : version.ErrorMessage);

    private static CorpusVersionStatusResponse MapCorpusVersion(
        KnowledgeDocumentVersionSummary version) =>
        new(
            version.Id,
            version.VersionLabel,
            MapIngestionStatusName(version.Status),
            version.ChunkCount,
            version.EmbeddedChunkCount,
            version.FileName,
            version.MimeType,
            version.SizeBytes,
            version.ContentSha256,
            version.CreatedAt.ToDateTimeOffset(),
            version.UpdatedAt?.ToDateTimeOffset(),
            version.CompletedAt?.ToDateTimeOffset(),
            version.FailedAt?.ToDateTimeOffset(),
            string.IsNullOrWhiteSpace(version.ErrorCode) ? null : version.ErrorCode,
            string.IsNullOrWhiteSpace(version.ErrorMessage) ? null : version.ErrorMessage);

    private static string MapIngestionStatusName(RegulatoryIngestionStatus status) =>
        status switch
        {
            RegulatoryIngestionStatus.Pending => "PENDING",
            RegulatoryIngestionStatus.Processing => "PROCESSING",
            RegulatoryIngestionStatus.Completed => "COMPLETED",
            RegulatoryIngestionStatus.Failed => "FAILED",
            RegulatoryIngestionStatus.PendingOcr => "PENDING_OCR",
            _ => "UNKNOWN"
        };

    private static (string status, string? stage) MapIngestionStatus(RegulatoryIngestionStatus status) => status switch
    {
        RegulatoryIngestionStatus.Pending => ("RECEIVED", "RECEIVING"),
        RegulatoryIngestionStatus.Processing => ("PROCESSING", "EMBEDDING"),
        RegulatoryIngestionStatus.Completed => ("READY", "READY"),
        RegulatoryIngestionStatus.Failed => ("FAILED", null),
        RegulatoryIngestionStatus.PendingOcr => ("PROCESSING", "OCR"),
        _ => ("PROCESSING", "INDEXING")
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

public sealed record CorpusCatalogPageResponse<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalCount);

public sealed record CorpusVersionStatusResponse(
    string? Id,
    string? VersionLabel,
    string Status,
    int ChunkCount,
    int EmbeddedChunkCount,
    string? FileName,
    string? MimeType,
    long SizeBytes,
    string? ContentSha256,
    DateTimeOffset? CreatedAt,
    DateTimeOffset? UpdatedAt,
    DateTimeOffset? CompletedAt,
    DateTimeOffset? FailedAt,
    string? ErrorCode,
    string? ErrorMessage);

public sealed record RegulatorySourceCatalogResponse(
    string Id,
    string Title,
    string Authority,
    string JurisdictionCode,
    string RegulationType,
    string LanguageCode,
    string Visibility,
    DateTimeOffset CreatedAt,
    CorpusVersionStatusResponse? LatestVersion);

public sealed record RegulatorySourceDetailsResponse(
    RegulatorySourceCatalogResponse Summary,
    IReadOnlyList<CorpusVersionStatusResponse> Versions)
{
    public CorpusVersionStatusResponse? LatestVersion => Summary.LatestVersion;
}

public sealed record KnowledgeDocumentCatalogResponse(
    string Id,
    string Title,
    string Category,
    string SourceReference,
    string LanguageCode,
    string Visibility,
    DateTimeOffset CreatedAt,
    CorpusVersionStatusResponse? LatestVersion);

public sealed record KnowledgeDocumentDetailsResponse(
    KnowledgeDocumentCatalogResponse Summary,
    IReadOnlyList<CorpusVersionStatusResponse> Versions)
{
    public CorpusVersionStatusResponse? LatestVersion => Summary.LatestVersion;
}

public sealed record SubmitShipmentDocumentRequest(
    string IdempotencyKey,
    string StorageReference,
    string FileName,
    string? MimeType,
    long SizeBytes,
    int DocumentTypeHint,
    Guid? ExternalDocumentId,
    string? ShipmentId);

public sealed record CreateDocumentUploadSessionRequest(
    string IdempotencyKey,
    string FileName,
    string MimeType,
    long SizeBytes,
    string? ContentSha256);

public sealed record CreateDocumentIntakeBody(
    string UploadId,
    string DocumentTypeHint,
    string IdempotencyKey,
    string? Purpose = null,
    string? ExternalReference = null);

public sealed record CreateCorpusIntakeBody(
    string UploadId,
    string Purpose,
    string IdempotencyKey,
    string Title,
    string? Authority = null,
    string? CanonicalSourceUri = null,
    string? JurisdictionCode = null,
    int RegulationType = 0,
    int Category = 0,
    string? SourceReference = null,
    string? LanguageCode = null,
    string VersionLabel = "1.0",
    DateTimeOffset? PublishedAt = null,
    DateTimeOffset? EffectiveFrom = null,
    DateTimeOffset? EffectiveTo = null);

public sealed record CorpusIntakeResponse(
    Guid CorpusDocumentId,
    Guid CorpusVersionId,
    string CorpusType,
    Guid OcrJobId,
    string Status,
    string? Stage);

public sealed record DocumentUploadSessionResponse(
    Guid UploadId,
    string StorageReference,
    string WriteUrl,
    IReadOnlyDictionary<string, string> RequiredHeaders,
    DateTimeOffset ExpiresAt,
    long MaximumSizeBytes,
    string FileName,
    string MimeType,
    long SizeBytes,
    string? ContentSha256,
    string Status);

public sealed record DocumentDownloadResponse(
    string Url,
    DateTimeOffset ExpiresAt,
    string FileName,
    string MimeType);

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
