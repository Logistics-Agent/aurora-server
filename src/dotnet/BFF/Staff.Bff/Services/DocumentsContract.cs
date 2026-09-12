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
        var contract = DocumentProblemContractCatalog.ResolveRuntimeTuple(code, status, retryable);
        var safeDetail = contract == DocumentProblemContractCatalog.DocumentContractError
            ? contract.DefaultDetail
            : detail;
        return CreateProblemDetails(contract, safeDetail);
    }

    internal static ProblemDetails CreateProblemDetails(
        DocumentProblemContractCatalog.Definition contract,
        string? detail = null)
    {
        var problem = new ProblemDetails
        {
            Title = contract.Code,
            Detail = detail ?? contract.DefaultDetail,
            Status = contract.StatusCode
        };
        problem.Extensions["code"] = contract.Code;
        problem.Extensions["retryable"] = contract.Retryable;
        return problem;
    }

    internal static ProblemDetails CreateProblemDetails(DocumentUploadError error) =>
        CreateProblemDetails(error.Contract, error.Detail);

    internal static ProblemDetails CreateProblemDetails(DocumentIntakeOrchestrationException exception) =>
        CreateProblemDetails(exception.Contract, exception.Message);

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
    DocumentProblemContractCatalog.Definition Contract,
    string Detail)
{
    internal string Code => Contract.Code;
    internal int StatusCode => Contract.StatusCode;
    internal bool Retryable => Contract.Retryable;
}

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
            return From(DocumentProblemContractCatalog.ResolveUploadCode(trailerCode));
        if (exception.StatusCode == Grpc.Core.StatusCode.AlreadyExists)
            return From(DocumentProblemContractCatalog.UploadIdempotencyConflict);
        if (exception.StatusCode == Grpc.Core.StatusCode.NotFound)
            return From(DocumentProblemContractCatalog.UploadNotFound);
        if (DocumentsContract.IsUnavailable(exception.StatusCode))
            return From(DocumentProblemContractCatalog.DocumentOcrUnavailable);
        return From(DocumentProblemContractCatalog.UploadInvalid);
    }

    private static DocumentUploadError From(DocumentProblemContractCatalog.Definition contract) =>
        new(contract, contract.DefaultDetail);
}
