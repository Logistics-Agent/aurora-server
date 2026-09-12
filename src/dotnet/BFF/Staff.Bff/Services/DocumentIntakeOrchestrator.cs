using DocumentOcr.Grpc;
using Grpc.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using ShipmentWorkflow.Grpc;

namespace StaffBff.Services;

public interface IDocumentIntakeOrchestrator
{
    Task<DocumentIntakeHttpResponse> ComposeAsync(
        Guid shipmentId,
        CreateDocumentIntakeRequestModel request,
        CancellationToken cancellationToken);
}

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
    public DocumentIntakeOrchestrationException(
        string code,
        string detail,
        int statusCode,
        bool retryable,
        Guid? intakeId = null,
        Guid? documentId = null)
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
    IDocumentIntakeOcrGateway ocrGateway,
    IShipmentDocumentIntakeGateway shipmentGateway,
    ILogger<DocumentIntakeOrchestrator> logger) : IDocumentIntakeOrchestrator
{
    public async Task<DocumentIntakeHttpResponse> ComposeAsync(
        Guid shipmentId,
        CreateDocumentIntakeRequestModel request,
        CancellationToken cancellationToken)
    {
        ValidateRequest(shipmentId, request);
        var ledger = await CreateLedgerAsync(shipmentId, request, cancellationToken);
        var verified = await VerifyAsync(request.UploadId, ledger, cancellationToken);
        ValidateVerifiedUpload(verified, request.UploadId, ledger);
        await AttachAsync(ledger, verified, cancellationToken);
        await ConsumeAsync(request.UploadId, ledger, cancellationToken);
        var job = await SubmitOcrAsync(shipmentId, request, ledger, verified, cancellationToken);
        var submitted = await MarkSubmittedAsync(ledger, cancellationToken);

        return new DocumentIntakeHttpResponse(
            ParseRequiredId(ledger.IntakeId, "intake id"),
            shipmentId,
            ParseRequiredId(ledger.DocumentId, "document id"),
            request.UploadId,
            ParseRequiredId(job.JobId, "OCR job id"),
            DocumentsContract.MapStatus(job.Status, job.NeedsReview),
            DocumentsContract.MapStage(job.Status),
            submitted.Status,
            job.FileName,
            job.CreatedAt is null ? DateTimeOffset.UtcNow : job.CreatedAt.ToDateTimeOffset(),
            submitted.UpdatedAt is null ? null : submitted.UpdatedAt.ToDateTimeOffset(),
            false,
            null);
    }

    private async Task<DocumentIntakeResponse> CreateLedgerAsync(
        Guid shipmentId,
        CreateDocumentIntakeRequestModel request,
        CancellationToken cancellationToken)
    {
        try
        {
            return await shipmentGateway.CreateAsync(new CreateDocumentIntakeRequest
            {
                ShipmentId = shipmentId.ToString(),
                UploadId = request.UploadId.ToString(),
                StorageReference = $"pending/{request.UploadId:N}",
                FileName = "pending-upload",
                DocumentType = request.DocumentTypeHint,
                IdempotencyKey = request.IdempotencyKey
            }, cancellationToken);
        }
        catch (RpcException exception)
        {
            throw MapShipmentException(exception);
        }
    }

    private async Task<DocumentUploadReceipt> VerifyAsync(
        Guid uploadId,
        DocumentIntakeResponse ledger,
        CancellationToken cancellationToken)
    {
        try
        {
            return await ocrGateway.VerifyAsync(uploadId, cancellationToken);
        }
        catch (RpcException exception)
        {
            throw await MapUploadExceptionAsync(exception, ledger, cancellationToken);
        }
    }

    private async Task AttachAsync(
        DocumentIntakeResponse ledger,
        DocumentUploadReceipt verified,
        CancellationToken cancellationToken)
    {
        try
        {
            await shipmentGateway.AttachAsync(new AttachDocumentIntakeRequest
            {
                IntakeId = ledger.IntakeId,
                UploadId = verified.UploadId,
                StorageReference = verified.StorageReference,
                FileName = verified.FileName
            }, cancellationToken);
        }
        catch (RpcException exception)
        {
            if (DocumentsContract.IsUnavailable(exception.StatusCode))
                await MarkRetryableAsync(ledger.IntakeId, "ShipmentWorkflow was unavailable while attaching the document.", cancellationToken);
            throw MapShipmentException(exception, ledger);
        }
    }

    private async Task ConsumeAsync(
        Guid uploadId,
        DocumentIntakeResponse ledger,
        CancellationToken cancellationToken)
    {
        try
        {
            await ocrGateway.ConsumeAsync(uploadId, cancellationToken);
        }
        catch (RpcException exception)
        {
            throw await MapUploadExceptionAsync(exception, ledger, cancellationToken);
        }
    }

    private async Task<DocumentOcrJobResponse> SubmitOcrAsync(
        Guid shipmentId,
        CreateDocumentIntakeRequestModel request,
        DocumentIntakeResponse ledger,
        DocumentUploadReceipt verified,
        CancellationToken cancellationToken)
    {
        try
        {
            return await ocrGateway.SubmitAsync(new SubmitOcrJobRequest
            {
                IdempotencyKey = request.IdempotencyKey,
                StorageReference = verified.StorageReference,
                FileName = verified.FileName,
                MimeType = string.IsNullOrWhiteSpace(verified.VerifiedMimeType)
                    ? verified.MimeType
                    : verified.VerifiedMimeType,
                SizeBytes = verified.VerifiedSizeBytes > 0
                    ? verified.VerifiedSizeBytes
                    : verified.SizeBytes,
                DocumentTypeHint = ParseOcrDocumentType(request.DocumentTypeHint),
                ExtractionMode = OcrExtractionMode.Structured,
                ExternalDocumentId = ledger.DocumentId,
                ExternalShipmentId = shipmentId.ToString(),
                ExternalContextId = "SHIPMENT_DOCUMENT"
            }, cancellationToken);
        }
        catch (RpcException exception) when (DocumentsContract.IsUnavailable(exception.StatusCode))
        {
            await MarkRetryableAsync(ledger.IntakeId, "Document OCR service was unavailable while submitting the document.", cancellationToken);
            throw new DocumentIntakeOrchestrationException(
                "DOCUMENT_OCR_UNAVAILABLE",
                "The OCR dependency could not process the request.",
                StatusCodes.Status503ServiceUnavailable,
                true,
                ParseOptionalId(ledger.IntakeId),
                ParseOptionalId(ledger.DocumentId));
        }
        catch (RpcException exception) when (exception.StatusCode == StatusCode.AlreadyExists)
        {
            throw new DocumentIntakeOrchestrationException(
                "IDEMPOTENCY_CONFLICT",
                "The idempotency key was already used with a different request.",
                StatusCodes.Status409Conflict,
                false,
                ParseOptionalId(ledger.IntakeId),
                ParseOptionalId(ledger.DocumentId));
        }
        catch (RpcException exception) when (exception.StatusCode == StatusCode.NotFound)
        {
            throw new DocumentIntakeOrchestrationException(
                "DOCUMENT_NOT_FOUND",
                "The authoritative document intake could not be found.",
                StatusCodes.Status404NotFound,
                false,
                ParseOptionalId(ledger.IntakeId),
                ParseOptionalId(ledger.DocumentId));
        }
        catch (RpcException exception) when (exception.StatusCode == StatusCode.InvalidArgument)
        {
            throw new DocumentIntakeOrchestrationException(
                "INVALID_REQUEST",
                "The OCR submission request is invalid.",
                StatusCodes.Status400BadRequest,
                false,
                ParseOptionalId(ledger.IntakeId),
                ParseOptionalId(ledger.DocumentId));
        }
    }

    private async Task<DocumentIntakeResponse> MarkSubmittedAsync(
        DocumentIntakeResponse ledger,
        CancellationToken cancellationToken)
    {
        try
        {
            return await shipmentGateway.MarkSubmittedAsync(
                new MarkDocumentIntakeSubmittedRequest { IntakeId = ledger.IntakeId },
                cancellationToken);
        }
        catch (RpcException exception) when (DocumentsContract.IsUnavailable(exception.StatusCode))
        {
            await MarkRetryableAsync(ledger.IntakeId, "ShipmentWorkflow was unavailable while finalizing the intake.", cancellationToken);
            throw new DocumentIntakeOrchestrationException(
                "SHIPMENT_WORKFLOW_UNAVAILABLE",
                "The shipment intake could not be finalized. Retry the same request.",
                StatusCodes.Status503ServiceUnavailable,
                true,
                ParseOptionalId(ledger.IntakeId),
                ParseOptionalId(ledger.DocumentId));
        }
        catch (RpcException exception) when (exception.StatusCode == StatusCode.NotFound)
        {
            throw MapShipmentException(exception, ledger);
        }
        catch (RpcException exception) when (exception.StatusCode is StatusCode.AlreadyExists or StatusCode.FailedPrecondition)
        {
            throw MapShipmentException(exception, ledger);
        }
    }

    private async Task<DocumentIntakeOrchestrationException> MapUploadExceptionAsync(
        RpcException exception,
        DocumentIntakeResponse ledger,
        CancellationToken cancellationToken)
    {
        var error = DocumentUploadErrorMapper.Map(exception);
        if (error.Retryable)
            await MarkRetryableAsync(ledger.IntakeId, error.Detail, cancellationToken);
        return new DocumentIntakeOrchestrationException(
            error.Code,
            error.Detail,
            error.StatusCode,
            error.Retryable,
            ParseOptionalId(ledger.IntakeId),
            ParseOptionalId(ledger.DocumentId));
    }

    private DocumentIntakeOrchestrationException MapShipmentException(
        RpcException exception,
        DocumentIntakeResponse? ledger = null) => new(
        exception.StatusCode switch
        {
            StatusCode.NotFound => "DOCUMENT_INTAKE_NOT_FOUND",
            StatusCode.AlreadyExists => "IDEMPOTENCY_CONFLICT",
            StatusCode.FailedPrecondition => "INVALID_STATE_TRANSITION",
            _ when DocumentsContract.IsUnavailable(exception.StatusCode) => "SHIPMENT_WORKFLOW_UNAVAILABLE",
            _ => "INVALID_REQUEST"
        },
        exception.StatusCode switch
        {
            StatusCode.NotFound => "The authoritative document intake could not be found.",
            StatusCode.AlreadyExists => "The document intake was changed by another request.",
            StatusCode.FailedPrecondition => "The document intake cannot be changed from its current state.",
            _ when DocumentsContract.IsUnavailable(exception.StatusCode) => "The shipment intake dependency is temporarily unavailable. Retry the same request.",
            _ => "The document intake request is invalid."
        },
        exception.StatusCode switch
        {
            StatusCode.NotFound => StatusCodes.Status404NotFound,
            StatusCode.AlreadyExists or StatusCode.FailedPrecondition => StatusCodes.Status409Conflict,
            _ when DocumentsContract.IsUnavailable(exception.StatusCode) => StatusCodes.Status503ServiceUnavailable,
            _ => StatusCodes.Status400BadRequest
        },
        DocumentsContract.IsUnavailable(exception.StatusCode),
        ledger is null ? null : ParseOptionalId(ledger.IntakeId),
        ledger is null ? null : ParseOptionalId(ledger.DocumentId));

    private async Task MarkRetryableAsync(
        string intakeId,
        string reason,
        CancellationToken cancellationToken)
    {
        try
        {
            await shipmentGateway.MarkRetryableAsync(
                new MarkDocumentIntakeRetryableRequest
                {
                    IntakeId = intakeId,
                    FailureReason = reason
                },
                cancellationToken);
        }
        catch (RpcException exception) when (exception.StatusCode != StatusCode.Cancelled)
        {
            logger.LogWarning(
                "Unable to record retryable document intake state; preserving original failure. IntakeId: {IntakeId}; StatusCode: {StatusCode}",
                intakeId,
                exception.StatusCode);
        }
    }

    private static void ValidateRequest(Guid shipmentId, CreateDocumentIntakeRequestModel request)
    {
        if (shipmentId == Guid.Empty || request.UploadId == Guid.Empty ||
            string.IsNullOrWhiteSpace(request.IdempotencyKey) || request.IdempotencyKey.Length > 150)
            throw InvalidRequest("The shipment, upload, and idempotency key are required.");
        _ = ParseOcrDocumentType(request.DocumentTypeHint);
    }

    private static void ValidateVerifiedUpload(
        DocumentUploadReceipt verified,
        Guid uploadId,
        DocumentIntakeResponse ledger)
    {
        if (!Guid.TryParse(verified.UploadId, out var verifiedUploadId) || verifiedUploadId != uploadId)
            throw UploadFailure("UPLOAD_TENANT_MISMATCH", StatusCodes.Status404NotFound, ledger);
        if (verified.Status is not (DocumentUploadStatus.Uploaded or DocumentUploadStatus.Consumed))
            throw UploadFailure("UPLOAD_NOT_READY", StatusCodes.Status409Conflict, ledger);
        if (string.IsNullOrWhiteSpace(verified.StorageReference) || string.IsNullOrWhiteSpace(verified.FileName))
            throw UploadFailure("UPLOAD_INVALID", StatusCodes.Status422UnprocessableEntity, ledger);
    }

    private static DocumentIntakeOrchestrationException UploadFailure(
        string code,
        int statusCode,
        DocumentIntakeResponse ledger) => new(
        code,
        "The uploaded object did not satisfy the upload session contract.",
        statusCode,
        false,
        ParseOptionalId(ledger.IntakeId),
        ParseOptionalId(ledger.DocumentId));

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

    private static DocumentIntakeOrchestrationException InvalidRequest(string detail) => new(
        "INVALID_REQUEST",
        detail,
        StatusCodes.Status400BadRequest,
        false);

    private static Guid ParseRequiredId(string value, string name) =>
        Guid.TryParse(value, out var id) && id != Guid.Empty
            ? id
            : throw InvalidRequest($"The downstream response did not contain a valid {name}.");

    private static Guid? ParseOptionalId(string value) =>
        Guid.TryParse(value, out var id) && id != Guid.Empty ? id : null;
}
