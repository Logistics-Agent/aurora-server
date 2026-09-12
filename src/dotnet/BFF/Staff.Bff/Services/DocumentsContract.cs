using DocumentOcr.Grpc;
using Grpc.Core;
using Microsoft.AspNetCore.Mvc;

namespace StaffBff.Services;

internal static class DocumentsContract
{
    internal static bool IsUnavailable(StatusCode statusCode) =>
        statusCode is StatusCode.Unavailable or StatusCode.DeadlineExceeded;

    internal static ProblemDetails CreateUnavailableProblemDetails() => CreateProblemDetails(
        "DOCUMENT_OCR_UNAVAILABLE",
        "Document OCR service is temporarily unavailable. Please retry shortly.",
        StatusCodes.Status503ServiceUnavailable,
        retryable: true);

    internal static ProblemDetails CreateProblemDetails(
        string code,
        string detail,
        int status,
        bool retryable)
    {
        var problem = new ProblemDetails
        {
            Title = code,
            Detail = detail,
            Status = status
        };
        problem.Extensions["code"] = code;
        problem.Extensions["retryable"] = retryable;
        return problem;
    }

    internal static ProblemDetails CreateProblemDetails(DocumentUploadError error) =>
        CreateProblemDetails(error.Code, error.Detail, error.StatusCode, error.Retryable);

    internal static string MapUploadStatus(DocumentUploadStatus status) => status switch
    {
        DocumentUploadStatus.Pending => "PENDING",
        DocumentUploadStatus.Uploaded => "UPLOADED",
        DocumentUploadStatus.Consumed => "CONSUMED",
        DocumentUploadStatus.Expired => "EXPIRED",
        _ => "PENDING"
    };

    internal static string MapStatus(DocumentOcrJobStatus status, bool needsReview) => status switch
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

internal sealed record DocumentUploadError(
    string Code,
    string Detail,
    int StatusCode,
    bool Retryable);

internal static class DocumentUploadErrorMapper
{
    internal static DocumentUploadError Map(Grpc.Core.RpcException exception)
    {
        var trailerCode = exception.Trailers
            .FirstOrDefault(item => string.Equals(
                item.Key,
                "document-upload-validation-code",
                StringComparison.OrdinalIgnoreCase))?.Value;
        if (!string.IsNullOrWhiteSpace(trailerCode))
            return FromCode(trailerCode);
        if (exception.StatusCode == Grpc.Core.StatusCode.AlreadyExists)
            return FromCode("UPLOAD_IDEMPOTENCY_CONFLICT");
        if (exception.StatusCode == Grpc.Core.StatusCode.NotFound)
            return FromCode("UPLOAD_NOT_FOUND");
        if (DocumentsContract.IsUnavailable(exception.StatusCode))
            return FromCode("DOCUMENT_OCR_UNAVAILABLE");
        return new("UPLOAD_INVALID", "The upload request is invalid.", StatusCodes.Status422UnprocessableEntity, false);
    }

    private static DocumentUploadError FromCode(string code) => code switch
    {
        "UPLOAD_EXPIRED" => new(code, "The upload session has expired.", StatusCodes.Status409Conflict, false),
        "UPLOAD_OBJECT_NOT_FOUND" => new(code, "The uploaded object was not found.", StatusCodes.Status404NotFound, false),
        "UPLOAD_TENANT_MISMATCH" => new(code, "The upload session was not found.", StatusCodes.Status404NotFound, false),
        "UPLOAD_MIME_MISMATCH" or "UPLOAD_SIZE_MISMATCH" or "UPLOAD_HASH_MISMATCH" or "UPLOAD_CONTENT_MISMATCH" or "UPLOAD_SIZE_EXCEEDED"
            => new(code, "The uploaded object did not satisfy the upload session contract.", StatusCodes.Status422UnprocessableEntity, false),
        "UPLOAD_IDEMPOTENCY_CONFLICT" => new(code, "The upload idempotency key was already used with a different request.", StatusCodes.Status409Conflict, false),
        "UPLOAD_INVALID_REQUEST" => new("INVALID_UPLOAD_REQUEST", "The upload request is invalid.", StatusCodes.Status400BadRequest, false),
        "UPLOAD_VERIFICATION_IN_PROGRESS" => new(code, "The upload session is still being verified.", StatusCodes.Status409Conflict, true),
        "UPLOAD_NOT_VERIFIED" => new(code, "The upload session must be verified before it is consumed.", StatusCodes.Status409Conflict, false),
        "UPLOAD_NOT_FOUND" => new(code, "The upload session was not found.", StatusCodes.Status404NotFound, false),
        "DOCUMENT_OCR_UNAVAILABLE" => new(code, "The OCR dependency is temporarily unavailable. Retry the same request.", StatusCodes.Status503ServiceUnavailable, true),
        _ when code.StartsWith("UPLOAD_", StringComparison.Ordinal) => new(code, "The uploaded object did not satisfy the upload session contract.", StatusCodes.Status422UnprocessableEntity, false),
        _ => new("UPLOAD_INVALID", "The upload request is invalid.", StatusCodes.Status422UnprocessableEntity, false)
    };
}
