using DocumentOcr.Grpc;
using Grpc.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using ShipmentWorkflow.Grpc;
using DocumentOcr.Contracts.Events;
using EventPurpose = DocumentOcr.Contracts.Events.DocumentOcrPurpose;

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
    string IdempotencyKey,
    string? InitiatingCorrelationId = null);

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
    internal DocumentIntakeOrchestrationException(
        string code,
        string detail,
        int statusCode,
        bool retryable,
        Guid? intakeId = null,
        Guid? documentId = null)
        : this(
            DocumentProblemContractCatalog.ResolveRuntimeTuple(code, statusCode, retryable),
            DocumentProblemContractCatalog.ResolveRuntimeTuple(code, statusCode, retryable) == DocumentProblemContractCatalog.DocumentContractError
                ? DocumentProblemContractCatalog.DocumentContractError.DefaultDetail
                : detail,
            intakeId,
            documentId)
    {
    }

    internal DocumentIntakeOrchestrationException(
        DocumentProblemContractCatalog.Definition contract,
        string? detail = null,
        Guid? intakeId = null,
        Guid? documentId = null)
        : base(detail ?? contract.DefaultDetail)
    {
        Contract = contract;
        IntakeId = intakeId;
        DocumentId = documentId;
    }

    internal DocumentProblemContractCatalog.Definition Contract { get; }
    public string Code => Contract.Code;
    public int StatusCode => Contract.StatusCode;
    public bool Retryable => Contract.Retryable;
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
                Purpose = (DocumentOcr.Grpc.DocumentOcrPurpose)(int)EventPurpose.ShipmentDocument,
                CorrelationId = DocumentOcrCorrelationId.FromTrace(request.InitiatingCorrelationId).ToString()
            }, cancellationToken);
        }
        catch (RpcException exception) when (DocumentsContract.IsUnavailable(exception.StatusCode))
        {
            await MarkRetryableAsync(ledger.IntakeId, "Document OCR service was unavailable while submitting the document.", cancellationToken);
            throw new DocumentIntakeOrchestrationException(
                DocumentProblemContractCatalog.DocumentOcrUnavailable,
                "The OCR dependency could not process the request.",
                ParseOptionalId(ledger.IntakeId),
                ParseOptionalId(ledger.DocumentId));
        }
        catch (RpcException exception) when (exception.StatusCode == StatusCode.AlreadyExists)
        {
            throw new DocumentIntakeOrchestrationException(
                DocumentProblemContractCatalog.IdempotencyConflict,
                "The idempotency key was already used with a different request.",
                ParseOptionalId(ledger.IntakeId),
                ParseOptionalId(ledger.DocumentId));
        }
        catch (RpcException exception) when (exception.StatusCode == StatusCode.NotFound)
        {
            throw new DocumentIntakeOrchestrationException(
                DocumentProblemContractCatalog.DocumentNotFound,
                "The authoritative document intake could not be found.",
                ParseOptionalId(ledger.IntakeId),
                ParseOptionalId(ledger.DocumentId));
        }
        catch (RpcException exception) when (exception.StatusCode == StatusCode.InvalidArgument)
        {
            throw new DocumentIntakeOrchestrationException(
                DocumentProblemContractCatalog.InvalidRequest,
                "The OCR submission request is invalid.",
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
                DocumentProblemContractCatalog.ShipmentWorkflowUnavailable,
                "The shipment intake could not be finalized. Retry the same request.",
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
            error.Contract,
            error.Detail,
            ParseOptionalId(ledger.IntakeId),
            ParseOptionalId(ledger.DocumentId));
    }

    private DocumentIntakeOrchestrationException MapShipmentException(
        RpcException exception,
        DocumentIntakeResponse? ledger = null)
    {
        var contract = exception.StatusCode switch
        {
            StatusCode.NotFound => DocumentProblemContractCatalog.DocumentIntakeNotFound,
            StatusCode.AlreadyExists => DocumentProblemContractCatalog.IdempotencyConflict,
            StatusCode.FailedPrecondition => DocumentProblemContractCatalog.InvalidStateTransition,
            _ when DocumentsContract.IsUnavailable(exception.StatusCode) => DocumentProblemContractCatalog.ShipmentWorkflowUnavailable,
            _ => DocumentProblemContractCatalog.InvalidRequest
        };
        var detail = exception.StatusCode switch
        {
            StatusCode.NotFound => "The authoritative document intake could not be found.",
            StatusCode.AlreadyExists => "The document intake was changed by another request.",
            StatusCode.FailedPrecondition => "The document intake cannot be changed from its current state.",
            _ when DocumentsContract.IsUnavailable(exception.StatusCode) => "The shipment intake dependency is temporarily unavailable. Retry the same request.",
            _ => "The document intake request is invalid."
        };
        return new DocumentIntakeOrchestrationException(
            contract,
            detail,
            ledger is null ? null : ParseOptionalId(ledger.IntakeId),
            ledger is null ? null : ParseOptionalId(ledger.DocumentId));
    }

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
            throw UploadFailure(DocumentProblemContractCatalog.UploadTenantMismatch, ledger);
        if (verified.Status is not (DocumentUploadStatus.Uploaded or DocumentUploadStatus.Consumed))
            throw UploadFailure(DocumentProblemContractCatalog.UploadNotReady, ledger);
        if (string.IsNullOrWhiteSpace(verified.StorageReference) || string.IsNullOrWhiteSpace(verified.FileName))
            throw UploadFailure(DocumentProblemContractCatalog.UploadInvalid, ledger);
    }

    private static DocumentIntakeOrchestrationException UploadFailure(
        DocumentProblemContractCatalog.Definition contract,
        DocumentIntakeResponse ledger) => new(
        contract,
        contract.DefaultDetail,
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
