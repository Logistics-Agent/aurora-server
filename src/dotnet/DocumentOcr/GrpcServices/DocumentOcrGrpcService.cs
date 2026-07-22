using DocumentOcr.Application.Jobs;
using DocumentOcr.Domain.Entities;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Shared.Security;
using OcrGrpc = DocumentOcr.Grpc;
using DomainDocumentType = DocumentOcr.Domain.Enums.OcrDocumentType;
using DomainJobStatus = DocumentOcr.Domain.Enums.DocumentOcrJobStatus;

namespace DocumentOcr.GrpcServices;

public sealed class DocumentOcrGrpcService(
    IDocumentOcrJobService jobService,
    ICurrentUserService currentUser)
    : OcrGrpc.DocumentOcrService.DocumentOcrServiceBase
{
    public override async Task<OcrGrpc.DocumentOcrJobResponse> SubmitDocumentJob(
        OcrGrpc.SubmitDocumentJobRequest request,
        ServerCallContext context)
    {
        RequireTenant();
        try
        {
            var externalDocumentId = ParseRequiredId(
                request.ExternalDocumentId, "ExternalDocumentId");
            var externalShipmentId = ParseOptionalId(
                request.ExternalShipmentId, "ExternalShipmentId");
            var documentType = ParseDocumentType(request.DocumentTypeHint);
            var job = await jobService.SubmitAsync(
                new SubmitDocumentJobInput(
                    request.IdempotencyKey,
                    request.StorageReference,
                    request.FileName,
                    request.MimeType,
                    request.SizeBytes,
                    documentType,
                    externalDocumentId,
                    externalShipmentId),
                context.CancellationToken);
            return MapJob(job);
        }
        catch (ArgumentException exception)
        {
            throw InvalidArgument(exception.Message);
        }
    }

    public override async Task<OcrGrpc.DocumentOcrJobResponse> GetDocumentJob(
        OcrGrpc.GetDocumentJobRequest request,
        ServerCallContext context)
    {
        RequireTenant();
        var jobId = ParseRequiredId(request.JobId, "JobId");
        return MapJob(await jobService.GetAsync(jobId, context.CancellationToken));
    }

    public override async Task<OcrGrpc.ListDocumentJobsResponse> ListDocumentJobs(
        OcrGrpc.ListDocumentJobsRequest request,
        ServerCallContext context)
    {
        RequireTenant();
        try
        {
            var status = ParseStatus(request.Status);
            var page = await jobService.ListAsync(
                new ListDocumentJobsInput(
                    request.Page,
                    request.PageSize,
                    status,
                    ParseOptionalId(request.ExternalDocumentId, "ExternalDocumentId"),
                    ParseOptionalId(request.ExternalShipmentId, "ExternalShipmentId")),
                context.CancellationToken);
            var response = new OcrGrpc.ListDocumentJobsResponse
            {
                Page = page.Page,
                PageSize = page.PageSize,
                TotalItems = page.TotalItems,
                TotalPages = page.TotalPages
            };
            response.Jobs.AddRange(page.Items.Select(MapJob));
            return response;
        }
        catch (ArgumentException exception)
        {
            throw InvalidArgument(exception.Message);
        }
    }

    internal static OcrGrpc.DocumentOcrJobResponse MapJob(DocumentOcrJob job)
    {
        var response = new OcrGrpc.DocumentOcrJobResponse
        {
            JobId = job.Id.ToString(),
            Status = (OcrGrpc.DocumentOcrJobStatus)(int)job.Status,
            FileName = job.FileName,
            MimeType = job.MimeType,
            DocumentTypeHint = (OcrGrpc.OcrDocumentType)(int)job.DocumentTypeHint,
            DetectedDocumentType = job.DetectedDocumentType.HasValue
                ? (OcrGrpc.OcrDocumentType)(int)job.DetectedDocumentType.Value
                : OcrGrpc.OcrDocumentType.Unspecified,
            NormalizedJson = job.NormalizedJson ?? string.Empty,
            NeedsReview = job.NeedsReview ?? false,
            ErrorCode = job.ErrorCode ?? string.Empty,
            ErrorMessage = job.ErrorMessage ?? string.Empty,
            ExternalDocumentId = job.ExternalDocumentId.ToString(),
            ExternalShipmentId = job.ExternalShipmentId?.ToString() ?? string.Empty,
            CreatedAt = Timestamp.FromDateTimeOffset(job.CreatedAt)
        };
        if (job.Confidence.HasValue)
            response.Confidence = Convert.ToDouble(job.Confidence.Value);
        if (job.UpdatedAt.HasValue)
            response.UpdatedAt = Timestamp.FromDateTimeOffset(job.UpdatedAt.Value);
        if (job.ProcessingStartedAt.HasValue)
            response.StartedAt = Timestamp.FromDateTimeOffset(job.ProcessingStartedAt.Value);
        if (job.CompletedAt.HasValue)
            response.CompletedAt = Timestamp.FromDateTimeOffset(job.CompletedAt.Value);
        if (job.NextAttemptAt.HasValue)
            response.NextAttemptAt = Timestamp.FromDateTimeOffset(job.NextAttemptAt.Value);
        return response;
    }

    private void RequireTenant()
    {
        if (!currentUser.TenantId.HasValue || currentUser.TenantId == Guid.Empty)
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Tenant context is required."));
    }

    private static Guid ParseRequiredId(string value, string fieldName) =>
        Guid.TryParse(value, out var parsed) && parsed != Guid.Empty
            ? parsed
            : throw InvalidArgument($"{fieldName} is invalid.");

    private static Guid? ParseOptionalId(string value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        return ParseRequiredId(value, fieldName);
    }

    private static DomainDocumentType ParseDocumentType(OcrGrpc.OcrDocumentType value)
    {
        var parsed = (DomainDocumentType)(int)value;
        return System.Enum.IsDefined(parsed)
            ? parsed
            : throw InvalidArgument("DocumentTypeHint is invalid.");
    }

    private static DomainJobStatus? ParseStatus(OcrGrpc.DocumentOcrJobStatus value)
    {
        if (value == OcrGrpc.DocumentOcrJobStatus.Unspecified)
            return null;
        var parsed = (DomainJobStatus)(int)value;
        return System.Enum.IsDefined(parsed)
            ? parsed
            : throw InvalidArgument("Status is invalid.");
    }

    private static RpcException InvalidArgument(string message) =>
        new(new Status(StatusCode.InvalidArgument, message));
}
