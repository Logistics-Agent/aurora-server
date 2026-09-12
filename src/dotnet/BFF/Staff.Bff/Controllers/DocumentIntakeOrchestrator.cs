using DocumentOcr.Grpc;
using Grpc.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using ShipmentWorkflow.Grpc;
using StaffBff.Controllers;

namespace StaffBff.Services;

public sealed record CreateDocumentIntakeRequestModel(
    Guid UploadId,
    string DocumentTypeHint,
    string IdempotencyKey);

public sealed record DocumentIntakeHttpResponse(
    Guid IntakeId,
    Guid ShipmentId,
    Guid DocumentId,
    Guid UploadId,
    Guid OcrJobId,
    string Status,
    string? Stage,
    string IntakeStatus,
    string FileName,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    bool Retryable,
    int? RetryAfterSeconds);

public sealed class DocumentIntakeOrchestrationException : Exception
{
    public DocumentIntakeOrchestrationException(string code, string detail, int statusCode, bool retryable, Guid? intakeId = null, Guid? documentId = null)
        : base(detail)
    {
        Code = code;
        StatusCode = statusCode;
        Retryable = retryable;
        IntakeId = intakeId;
        DocumentId = documentId;
    }

    public string Code { get; }
    public int StatusCode { get; }
    public bool Retryable { get; }
    public Guid? IntakeId { get; }
    public Guid? DocumentId { get; }
}

public sealed class DocumentIntakeOrchestrator(
    DocumentOcrService.DocumentOcrServiceClient documentOcrClient,
    ShipmentWorkflowService.ShipmentWorkflowServiceClient shipmentClient,
    ILogger<DocumentIntakeOrchestrator> logger)
{
    public async Task<DocumentIntakeHttpResponse> ComposeAsync(Guid shipmentId, CreateDocumentIntakeRequestModel request, CancellationToken cancellationToken)
    {
        ValidateRequest(shipmentId, request);
        var session = await GetUploadSessionAsync(request.UploadId, cancellationToken);
        ValidateUploadIdentity(session, request.UploadId);

        var ledger = await CreateOrReplayLedgerAsync(shipmentId, request, session, cancellationToken);
        DocumentUploadReceipt verified;
        if (ledger.Status == "SUBMITTED" && session.Status == DocumentUploadStatus.Consumed)
        {
            verified = session;
        }
        else
        {
            try
            {
                verified = await VerifyUploadSessionAsync(request.UploadId, ledger, cancellationToken);
            }
            catch (DocumentIntakeOrchestrationException exception) when (exception.Retryable)
            {
                await MarkRetryableAsync(ledger.IntakeId, exception.Message, cancellationToken);
                throw;
            }
        }
        ValidateVerifiedUpload(session, verified, request.UploadId, ledger);

        DocumentOcrJobResponse job;
        try
        {
            job = await documentOcrClient.SubmitOcrJobAsync(new SubmitOcrJobRequest
            {
                IdempotencyKey = request.IdempotencyKey,
                StorageReference = verified.StorageReference,
                FileName = verified.FileName,
                MimeType = string.IsNullOrWhiteSpace(verified.VerifiedMimeType) ? verified.MimeType : verified.VerifiedMimeType,
                SizeBytes = verified.VerifiedSizeBytes > 0 ? verified.VerifiedSizeBytes : verified.SizeBytes,
                DocumentTypeHint = ParseOcrDocumentType(request.DocumentTypeHint),
                ExtractionMode = OcrExtractionMode.Structured,
                ExternalDocumentId = ledger.DocumentId,
                ExternalShipmentId = shipmentId.ToString(),
                ExternalContextId = "SHIPMENT_DOCUMENT"
            }, cancellationToken: cancellationToken);
        }
        catch (RpcException exception) when (DocumentsContract.IsUnavailable(exception.StatusCode))
        {
            await MarkRetryableAsync(ledger.IntakeId, "Document OCR service is unavailable.", cancellationToken);
            throw new DocumentIntakeOrchestrationException("DOCUMENT_OCR_UNAVAILABLE", "The OCR dependency could not process the request.", StatusCodes.Status503ServiceUnavailable, true, ParseOptionalId(ledger.IntakeId), ParseOptionalId(ledger.DocumentId));
        }
        catch (RpcException exception) when (exception.StatusCode == StatusCode.AlreadyExists)
        {
            throw new DocumentIntakeOrchestrationException("IDEMPOTENCY_CONFLICT", "The idempotency key was already used with a different request.", StatusCodes.Status409Conflict, false, ParseOptionalId(ledger.IntakeId), ParseOptionalId(ledger.DocumentId));
        }
        catch (RpcException exception) when (exception.StatusCode == StatusCode.NotFound)
        {
            throw new DocumentIntakeOrchestrationException("DOCUMENT_NOT_FOUND", "The authoritative document intake could not be found.", StatusCodes.Status404NotFound, false, ParseOptionalId(ledger.IntakeId), ParseOptionalId(ledger.DocumentId));
        }
        catch (RpcException exception) when (exception.StatusCode == StatusCode.InvalidArgument)
        {
            throw new DocumentIntakeOrchestrationException("INVALID_REQUEST", "The OCR submission request is invalid.", StatusCodes.Status400BadRequest, false, ParseOptionalId(ledger.IntakeId), ParseOptionalId(ledger.DocumentId));
        }

        DocumentIntakeResponse submitted;
        try
        {
            submitted = await shipmentClient.MarkDocumentIntakeSubmittedAsync(new MarkDocumentIntakeSubmittedRequest { IntakeId = ledger.IntakeId }, cancellationToken: cancellationToken);
        }
        catch (RpcException exception) when (exception.StatusCode == StatusCode.NotFound)
        {
            throw new DocumentIntakeOrchestrationException("DOCUMENT_INTAKE_NOT_FOUND", "The authoritative document intake could not be found.", StatusCodes.Status404NotFound, false, ParseOptionalId(ledger.IntakeId), ParseOptionalId(ledger.DocumentId));
        }
        catch (RpcException exception) when (exception.StatusCode == StatusCode.FailedPrecondition)
        {
            throw new DocumentIntakeOrchestrationException("INVALID_STATE_TRANSITION", "The document intake cannot be marked submitted from its current state.", StatusCodes.Status409Conflict, false, ParseOptionalId(ledger.IntakeId), ParseOptionalId(ledger.DocumentId));
        }
        catch (RpcException exception) when (exception.StatusCode == StatusCode.AlreadyExists)
        {
            throw new DocumentIntakeOrchestrationException("IDEMPOTENCY_CONFLICT", "The document intake was changed by another request.", StatusCodes.Status409Conflict, false, ParseOptionalId(ledger.IntakeId), ParseOptionalId(ledger.DocumentId));
        }
        catch (RpcException exception) when (DocumentsContract.IsUnavailable(exception.StatusCode))
        {
            await MarkRetryableAsync(ledger.IntakeId, "ShipmentWorkflow was unavailable while finalizing the intake.", cancellationToken);
            throw new DocumentIntakeOrchestrationException("SHIPMENT_WORKFLOW_UNAVAILABLE", "The shipment intake could not be finalized. Retry the same request.", StatusCodes.Status503ServiceUnavailable, true, ParseOptionalId(ledger.IntakeId), ParseOptionalId(ledger.DocumentId));
        }

        return new DocumentIntakeHttpResponse(
            ParseRequiredId(ledger.IntakeId, "intake id"), shipmentId,
            ParseRequiredId(ledger.DocumentId, "document id"), request.UploadId,
            ParseRequiredId(job.JobId, "OCR job id"), DocumentsContract.MapStatus(job.Status, job.NeedsReview),
            DocumentsContract.MapStage(job.Status), submitted.Status, job.FileName,
            job.CreatedAt is null ? DateTimeOffset.UtcNow : job.CreatedAt.ToDateTimeOffset(),
            submitted.UpdatedAt is null ? null : submitted.UpdatedAt.ToDateTimeOffset(), false, null);
    }

    private async Task<DocumentUploadReceipt> GetUploadSessionAsync(Guid uploadId, CancellationToken cancellationToken)
    {
        try
        {
            return await documentOcrClient.GetUploadSessionAsync(new GetUploadSessionRequest { UploadId = uploadId.ToString() }, cancellationToken: cancellationToken);
        }
        catch (RpcException exception) when (exception.StatusCode == StatusCode.NotFound)
        {
            throw new DocumentIntakeOrchestrationException("UPLOAD_NOT_FOUND", "The upload session was not found.", StatusCodes.Status404NotFound, false);
        }
        catch (RpcException exception) when (DocumentsContract.IsUnavailable(exception.StatusCode))
        {
            throw new DocumentIntakeOrchestrationException("DOCUMENT_OCR_UNAVAILABLE", "The upload session dependency is temporarily unavailable.", StatusCodes.Status503ServiceUnavailable, true);
        }
    }

    private async Task<DocumentIntakeResponse> CreateOrReplayLedgerAsync(Guid shipmentId, CreateDocumentIntakeRequestModel request, DocumentUploadReceipt session, CancellationToken cancellationToken)
    {
        try
        {
            return await shipmentClient.CreateDocumentIntakeAsync(new CreateDocumentIntakeRequest
            {
                ShipmentId = shipmentId.ToString(), UploadId = request.UploadId.ToString(), StorageReference = session.StorageReference,
                FileName = session.FileName, DocumentType = request.DocumentTypeHint, IdempotencyKey = request.IdempotencyKey
            }, cancellationToken: cancellationToken);
        }
        catch (RpcException exception) when (exception.StatusCode == StatusCode.AlreadyExists)
        {
            throw new DocumentIntakeOrchestrationException("IDEMPOTENCY_CONFLICT", "The idempotency key was already used with a different request.", StatusCodes.Status409Conflict, false);
        }
        catch (RpcException exception) when (exception.StatusCode == StatusCode.NotFound)
        {
            throw new DocumentIntakeOrchestrationException("SHIPMENT_NOT_FOUND", "The shipment was not found.", StatusCodes.Status404NotFound, false);
        }
        catch (RpcException exception) when (exception.StatusCode == StatusCode.InvalidArgument)
        {
            throw new DocumentIntakeOrchestrationException("INVALID_REQUEST", "The document intake request is invalid.", StatusCodes.Status400BadRequest, false);
        }
        catch (RpcException exception) when (DocumentsContract.IsUnavailable(exception.StatusCode))
        {
            throw new DocumentIntakeOrchestrationException("SHIPMENT_WORKFLOW_UNAVAILABLE", "The shipment intake dependency is temporarily unavailable.", StatusCodes.Status503ServiceUnavailable, true);
        }
    }

    private async Task<DocumentUploadReceipt> VerifyUploadSessionAsync(Guid uploadId, DocumentIntakeResponse ledger, CancellationToken cancellationToken)
    {
        try
        {
            return await documentOcrClient.VerifyUploadSessionAsync(new VerifyUploadSessionRequest { UploadId = uploadId.ToString() }, cancellationToken: cancellationToken);
        }
        catch (RpcException exception)
        {
            var validationCode = exception.Trailers.GetValue("document-upload-validation-code");
            if (!string.IsNullOrWhiteSpace(validationCode))
            {
                var status = validationCode switch
                {
                    "UPLOAD_EXPIRED" => StatusCodes.Status409Conflict,
                    "UPLOAD_CONTENT_MISMATCH" => StatusCodes.Status422UnprocessableEntity,
                    _ => StatusCodes.Status400BadRequest
                };
                throw new DocumentIntakeOrchestrationException(validationCode, "The uploaded object did not satisfy the upload session contract.", status, false, ParseOptionalId(ledger.IntakeId), ParseOptionalId(ledger.DocumentId));
            }
            if (exception.StatusCode == StatusCode.NotFound)
                throw new DocumentIntakeOrchestrationException("UPLOAD_NOT_FOUND", "The upload session was not found.", StatusCodes.Status404NotFound, false, ParseOptionalId(ledger.IntakeId), ParseOptionalId(ledger.DocumentId));
            if (DocumentsContract.IsUnavailable(exception.StatusCode))
                throw new DocumentIntakeOrchestrationException("DOCUMENT_OCR_UNAVAILABLE", "The upload session dependency is temporarily unavailable.", StatusCodes.Status503ServiceUnavailable, true, ParseOptionalId(ledger.IntakeId), ParseOptionalId(ledger.DocumentId));
            throw new DocumentIntakeOrchestrationException("UPLOAD_INVALID", "The upload session could not be verified.", StatusCodes.Status422UnprocessableEntity, false, ParseOptionalId(ledger.IntakeId), ParseOptionalId(ledger.DocumentId));
        }
    }

    private async Task MarkRetryableAsync(string intakeId, string reason, CancellationToken cancellationToken)
    {
        try
        {
            await shipmentClient.MarkDocumentIntakeRetryableAsync(new MarkDocumentIntakeRetryableRequest { IntakeId = intakeId, FailureReason = reason }, cancellationToken: cancellationToken);
        }
        catch (RpcException exception) when (DocumentsContract.IsUnavailable(exception.StatusCode))
        {
            logger.LogWarning("ShipmentWorkflow was unavailable while recording retryable document intake failure. IntakeId: {IntakeId}", intakeId);
        }
    }

    private static void ValidateRequest(Guid shipmentId, CreateDocumentIntakeRequestModel request)
    {
        if (shipmentId == Guid.Empty || request.UploadId == Guid.Empty || string.IsNullOrWhiteSpace(request.IdempotencyKey) || request.IdempotencyKey.Length > 150)
            throw InvalidRequest("The shipment, upload, and idempotency key are required.");
        _ = ParseOcrDocumentType(request.DocumentTypeHint);
    }

    private static void ValidateUploadIdentity(DocumentUploadReceipt session, Guid uploadId)
    {
        if (!Guid.TryParse(session.UploadId, out var sessionUploadId) || sessionUploadId != uploadId || string.IsNullOrWhiteSpace(session.StorageReference) || string.IsNullOrWhiteSpace(session.FileName))
            throw InvalidRequest("The upload session metadata is invalid.");
    }

    private static void ValidateVerifiedUpload(DocumentUploadReceipt session, DocumentUploadReceipt verified, Guid uploadId, DocumentIntakeResponse ledger)
    {
        if (verified.Status == DocumentUploadStatus.Expired)
            throw UploadFailure("UPLOAD_EXPIRED", StatusCodes.Status409Conflict, ledger);
        if (verified.Status != DocumentUploadStatus.Uploaded && verified.Status != DocumentUploadStatus.Consumed)
            throw UploadFailure("UPLOAD_NOT_READY", StatusCodes.Status409Conflict, ledger);
        if (!Guid.TryParse(verified.UploadId, out var verifiedUploadId) || verifiedUploadId != uploadId || !string.Equals(verified.StorageReference, session.StorageReference, StringComparison.Ordinal) || !string.Equals(verified.FileName, session.FileName, StringComparison.Ordinal))
            throw UploadFailure("UPLOAD_CONTENT_MISMATCH", StatusCodes.Status422UnprocessableEntity, ledger);
    }

    private static DocumentIntakeOrchestrationException UploadFailure(string code, int statusCode, DocumentIntakeResponse ledger) => new(code, "The uploaded object did not satisfy the upload session contract.", statusCode, false, ParseOptionalId(ledger.IntakeId), ParseOptionalId(ledger.DocumentId));

    private static OcrDocumentType ParseOcrDocumentType(string value) => value.Trim().ToUpperInvariant() switch
    {
        "INVOICE" or "COMMERCIAL_INVOICE" => OcrDocumentType.CommercialInvoice,
        "PACKING_LIST" => OcrDocumentType.PackingList,
        "BILL_OF_LADING" => OcrDocumentType.BillOfLading,
        "CUSTOMS_DECLARATION" => OcrDocumentType.CustomsDeclaration,
        "CERTIFICATE_OF_ORIGIN" => OcrDocumentType.CertificateOfOrigin,
        "PROOF_OF_DELIVERY" => OcrDocumentType.ProofOfDelivery,
        "OTHER" => OcrDocumentType.Other,
        _ => throw InvalidRequest("The document type hint is invalid.")
    };

    private static DocumentIntakeOrchestrationException InvalidRequest(string detail) => new("INVALID_REQUEST", detail, StatusCodes.Status400BadRequest, false);
    private static Guid ParseRequiredId(string value, string name) => Guid.TryParse(value, out var id) && id != Guid.Empty ? id : throw InvalidRequest($"The downstream response did not contain a valid {name}.");
    private static Guid? ParseOptionalId(string value) => Guid.TryParse(value, out var id) && id != Guid.Empty ? id : null;
}

internal static class RpcMetadataExtensions
{
    internal static string? GetValue(this Metadata metadata, string key) => metadata.FirstOrDefault(entry => string.Equals(entry.Key, key, StringComparison.OrdinalIgnoreCase))?.Value;
}
