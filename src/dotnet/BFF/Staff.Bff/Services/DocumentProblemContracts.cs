using Microsoft.AspNetCore.Http;

namespace StaffBff.Services;

internal static class DocumentProblemContractCatalog
{
    internal sealed class Definition
    {
        internal Definition(string code, int statusCode, bool retryable, string defaultDetail)
        {
            Code = code;
            StatusCode = statusCode;
            Retryable = retryable;
            DefaultDetail = defaultDetail;
        }

        internal string Code { get; }
        internal int StatusCode { get; }
        internal bool Retryable { get; }
        internal string DefaultDetail { get; }
    }

    internal const string InvalidRequestCode = "INVALID_REQUEST";
    internal const string InvalidUploadRequestCode = "INVALID_UPLOAD_REQUEST";
    internal const string InvalidFileCode = "INVALID_FILE";
    internal const string DocumentNotFoundCode = "DOCUMENT_NOT_FOUND";
    internal const string UploadNotFoundCode = "UPLOAD_NOT_FOUND";
    internal const string UploadObjectNotFoundCode = "UPLOAD_OBJECT_NOT_FOUND";
    internal const string UploadTenantMismatchCode = "UPLOAD_TENANT_MISMATCH";
    internal const string DocumentIntakeNotFoundCode = "DOCUMENT_INTAKE_NOT_FOUND";
    internal const string UploadExpiredCode = "UPLOAD_EXPIRED";
    internal const string UploadIdempotencyConflictCode = "UPLOAD_IDEMPOTENCY_CONFLICT";
    internal const string UploadNotVerifiedCode = "UPLOAD_NOT_VERIFIED";
    internal const string IdempotencyConflictCode = "IDEMPOTENCY_CONFLICT";
    internal const string InvalidStateTransitionCode = "INVALID_STATE_TRANSITION";
    internal const string UploadVerificationInProgressCode = "UPLOAD_VERIFICATION_IN_PROGRESS";
    internal const string UploadNotReadyCode = "UPLOAD_NOT_READY";
    internal const string UploadInvalidCode = "UPLOAD_INVALID";
    internal const string UploadMimeMismatchCode = "UPLOAD_MIME_MISMATCH";
    internal const string UploadSizeMismatchCode = "UPLOAD_SIZE_MISMATCH";
    internal const string UploadHashMismatchCode = "UPLOAD_HASH_MISMATCH";
    internal const string UploadContentMismatchCode = "UPLOAD_CONTENT_MISMATCH";
    internal const string UploadSizeExceededCode = "UPLOAD_SIZE_EXCEEDED";
    internal const string DocumentOcrUnavailableCode = "DOCUMENT_OCR_UNAVAILABLE";
    internal const string ShipmentWorkflowUnavailableCode = "SHIPMENT_WORKFLOW_UNAVAILABLE";
    internal const string DocumentContractErrorCode = "DOCUMENT_CONTRACT_ERROR";

    internal static Definition InvalidRequest { get; } =
        new(InvalidRequestCode, StatusCodes.Status400BadRequest, false, "The document request is invalid.");

    internal static Definition InvalidUploadRequest { get; } =
        new(InvalidUploadRequestCode, StatusCodes.Status400BadRequest, false, "The upload request is invalid.");

    internal static Definition InvalidFile { get; } =
        new(InvalidFileCode, StatusCodes.Status400BadRequest, false, "The document file is invalid.");

    internal static Definition DocumentNotFound { get; } =
        new(DocumentNotFoundCode, StatusCodes.Status404NotFound, false, "The document could not be found.");

    internal static Definition UploadNotFound { get; } =
        new(UploadNotFoundCode, StatusCodes.Status404NotFound, false, "The upload session was not found.");

    internal static Definition UploadObjectNotFound { get; } =
        new(UploadObjectNotFoundCode, StatusCodes.Status404NotFound, false, "The uploaded object was not found.");

    internal static Definition UploadTenantMismatch { get; } =
        new(UploadTenantMismatchCode, StatusCodes.Status404NotFound, false, "The upload session was not found.");

    internal static Definition DocumentIntakeNotFound { get; } =
        new(DocumentIntakeNotFoundCode, StatusCodes.Status404NotFound, false, "The authoritative document intake could not be found.");

    internal static Definition UploadExpired { get; } =
        new(UploadExpiredCode, StatusCodes.Status409Conflict, false, "The upload session has expired.");

    internal static Definition UploadIdempotencyConflict { get; } =
        new(UploadIdempotencyConflictCode, StatusCodes.Status409Conflict, false, "The upload idempotency key was already used with a different request.");

    internal static Definition UploadNotVerified { get; } =
        new(UploadNotVerifiedCode, StatusCodes.Status409Conflict, false, "The upload session must be verified before it is consumed.");

    internal static Definition IdempotencyConflict { get; } =
        new(IdempotencyConflictCode, StatusCodes.Status409Conflict, false, "The idempotency key was already used with a different request.");

    internal static Definition InvalidStateTransition { get; } =
        new(InvalidStateTransitionCode, StatusCodes.Status409Conflict, false, "The document intake cannot be changed from its current state.");

    internal static Definition UploadVerificationInProgress { get; } =
        new(UploadVerificationInProgressCode, StatusCodes.Status409Conflict, true, "The upload session is still being verified.");

    internal static Definition UploadNotReady { get; } =
        new(UploadNotReadyCode, StatusCodes.Status409Conflict, false, "The upload session is not ready to be consumed.");

    internal static Definition UploadInvalid { get; } =
        new(UploadInvalidCode, StatusCodes.Status422UnprocessableEntity, false, "The uploaded object did not satisfy the upload session contract.");

    internal static Definition UploadMimeMismatch { get; } =
        new(UploadMimeMismatchCode, StatusCodes.Status422UnprocessableEntity, false, UploadInvalid.DefaultDetail);

    internal static Definition UploadSizeMismatch { get; } =
        new(UploadSizeMismatchCode, StatusCodes.Status422UnprocessableEntity, false, UploadInvalid.DefaultDetail);

    internal static Definition UploadHashMismatch { get; } =
        new(UploadHashMismatchCode, StatusCodes.Status422UnprocessableEntity, false, UploadInvalid.DefaultDetail);

    internal static Definition UploadContentMismatch { get; } =
        new(UploadContentMismatchCode, StatusCodes.Status422UnprocessableEntity, false, UploadInvalid.DefaultDetail);

    internal static Definition UploadSizeExceeded { get; } =
        new(UploadSizeExceededCode, StatusCodes.Status422UnprocessableEntity, false, UploadInvalid.DefaultDetail);

    internal static Definition DocumentOcrUnavailable { get; } =
        new(DocumentOcrUnavailableCode, StatusCodes.Status503ServiceUnavailable, true, "The OCR dependency is temporarily unavailable. Retry the same request.");

    internal static Definition ShipmentWorkflowUnavailable { get; } =
        new(ShipmentWorkflowUnavailableCode, StatusCodes.Status503ServiceUnavailable, true, "The shipment intake dependency is temporarily unavailable. Retry the same request.");

    internal static Definition DocumentContractError { get; } =
        new(DocumentContractErrorCode, StatusCodes.Status500InternalServerError, false, "The document service returned an undeclared error contract.");

    internal static IReadOnlyList<Definition> All { get; } =
    [
        InvalidRequest,
        InvalidUploadRequest,
        InvalidFile,
        DocumentNotFound,
        UploadNotFound,
        UploadObjectNotFound,
        UploadTenantMismatch,
        DocumentIntakeNotFound,
        UploadExpired,
        UploadIdempotencyConflict,
        UploadNotVerified,
        IdempotencyConflict,
        InvalidStateTransition,
        UploadVerificationInProgress,
        UploadNotReady,
        UploadInvalid,
        UploadMimeMismatch,
        UploadSizeMismatch,
        UploadHashMismatch,
        UploadContentMismatch,
        UploadSizeExceeded,
        DocumentOcrUnavailable,
        ShipmentWorkflowUnavailable,
        DocumentContractError
    ];

    private static readonly IReadOnlyDictionary<string, Definition> ByCode =
        All.ToDictionary(definition => definition.Code, StringComparer.Ordinal);

    internal static bool TryGet(string code, out Definition definition) =>
        ByCode.TryGetValue(code, out definition!);

    internal static Definition ResolveUploadCode(string code) => code switch
    {
        "UPLOAD_INVALID_REQUEST" => InvalidUploadRequest,
        _ when TryGet(code, out var definition) && definition.Code.StartsWith("UPLOAD_", StringComparison.Ordinal) => definition,
        _ => UploadInvalid
    };

    internal static Definition ResolveRuntimeTuple(string code, int statusCode, bool retryable) =>
        TryGet(code, out var definition) &&
        definition.StatusCode == statusCode &&
        definition.Retryable == retryable
            ? definition
            : DocumentContractError;
}

internal static class DocumentEndpointProblemContracts
{
    internal const string CreateUploadSession = "CreateUploadSession";
    internal const string CreateDocumentIntake = "CreateDocumentIntake";
    internal const string ListShipmentDocuments = "ListShipmentDocuments";
    internal const string GetShipmentDocumentStatus = "GetShipmentDocumentStatus";
    internal const string GetShipmentDocumentStatusAlias = "GetShipmentDocumentStatusAlias";
    internal const string GetShipmentDocumentReview = "GetShipmentDocumentReview";
    internal const string SubmitShipmentDocumentReview = "SubmitShipmentDocumentReview";
    internal const string CancelShipmentDocument = "CancelShipmentDocument";
    internal const string RetryShipmentDocument = "RetryShipmentDocument";
    internal const string SubmitShipmentDocumentLegacyAlias = "SubmitShipmentDocumentLegacyAlias";
    internal const string SubmitShipmentDocumentLegacy = "SubmitShipmentDocumentLegacy";

    private static readonly IReadOnlyDictionary<string, IReadOnlyList<DocumentProblemContractCatalog.Definition>> ByOperation =
        new Dictionary<string, IReadOnlyList<DocumentProblemContractCatalog.Definition>>(StringComparer.Ordinal)
        {
            [CreateUploadSession] =
            [
                DocumentProblemContractCatalog.InvalidUploadRequest,
                DocumentProblemContractCatalog.UploadNotFound,
                DocumentProblemContractCatalog.UploadObjectNotFound,
                DocumentProblemContractCatalog.UploadTenantMismatch,
                DocumentProblemContractCatalog.UploadExpired,
                DocumentProblemContractCatalog.UploadIdempotencyConflict,
                DocumentProblemContractCatalog.UploadNotVerified,
                DocumentProblemContractCatalog.UploadVerificationInProgress,
                DocumentProblemContractCatalog.UploadInvalid,
                DocumentProblemContractCatalog.UploadMimeMismatch,
                DocumentProblemContractCatalog.UploadSizeMismatch,
                DocumentProblemContractCatalog.UploadHashMismatch,
                DocumentProblemContractCatalog.UploadContentMismatch,
                DocumentProblemContractCatalog.UploadSizeExceeded,
                DocumentProblemContractCatalog.DocumentOcrUnavailable
            ],
            [CreateDocumentIntake] =
            [
                DocumentProblemContractCatalog.InvalidRequest,
                DocumentProblemContractCatalog.DocumentNotFound,
                DocumentProblemContractCatalog.UploadNotFound,
                DocumentProblemContractCatalog.UploadObjectNotFound,
                DocumentProblemContractCatalog.UploadTenantMismatch,
                DocumentProblemContractCatalog.DocumentIntakeNotFound,
                DocumentProblemContractCatalog.UploadExpired,
                DocumentProblemContractCatalog.UploadIdempotencyConflict,
                DocumentProblemContractCatalog.UploadNotVerified,
                DocumentProblemContractCatalog.IdempotencyConflict,
                DocumentProblemContractCatalog.InvalidStateTransition,
                DocumentProblemContractCatalog.UploadVerificationInProgress,
                DocumentProblemContractCatalog.UploadNotReady,
                DocumentProblemContractCatalog.UploadInvalid,
                DocumentProblemContractCatalog.UploadMimeMismatch,
                DocumentProblemContractCatalog.UploadSizeMismatch,
                DocumentProblemContractCatalog.UploadHashMismatch,
                DocumentProblemContractCatalog.UploadContentMismatch,
                DocumentProblemContractCatalog.UploadSizeExceeded,
                DocumentProblemContractCatalog.DocumentOcrUnavailable,
                DocumentProblemContractCatalog.ShipmentWorkflowUnavailable,
                DocumentProblemContractCatalog.DocumentContractError
            ],
            [ListShipmentDocuments] =
            [DocumentProblemContractCatalog.DocumentOcrUnavailable],
            [GetShipmentDocumentStatus] =
            [DocumentProblemContractCatalog.DocumentNotFound, DocumentProblemContractCatalog.DocumentOcrUnavailable],
            [GetShipmentDocumentStatusAlias] =
            [DocumentProblemContractCatalog.DocumentNotFound, DocumentProblemContractCatalog.DocumentOcrUnavailable],
            [GetShipmentDocumentReview] =
            [DocumentProblemContractCatalog.DocumentOcrUnavailable],
            [SubmitShipmentDocumentReview] =
            [DocumentProblemContractCatalog.InvalidRequest, DocumentProblemContractCatalog.DocumentNotFound, DocumentProblemContractCatalog.InvalidStateTransition, DocumentProblemContractCatalog.DocumentOcrUnavailable],
            [CancelShipmentDocument] =
            [DocumentProblemContractCatalog.DocumentNotFound, DocumentProblemContractCatalog.InvalidStateTransition, DocumentProblemContractCatalog.DocumentOcrUnavailable],
            [RetryShipmentDocument] =
            [DocumentProblemContractCatalog.DocumentNotFound, DocumentProblemContractCatalog.InvalidStateTransition, DocumentProblemContractCatalog.DocumentOcrUnavailable],
            [SubmitShipmentDocumentLegacyAlias] =
            [DocumentProblemContractCatalog.InvalidFile, DocumentProblemContractCatalog.DocumentOcrUnavailable],
            [SubmitShipmentDocumentLegacy] =
            [DocumentProblemContractCatalog.InvalidFile, DocumentProblemContractCatalog.DocumentOcrUnavailable]
        };

    internal static IReadOnlyList<DocumentProblemContractCatalog.Definition> Get(string operationId) =>
        ByOperation.TryGetValue(operationId, out var definitions)
            ? definitions
            : [];
}
