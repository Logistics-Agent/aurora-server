using DocumentOcr.Grpc;
using ShipmentWorkflow.Grpc;

namespace StaffBff.Services;

public interface IDocumentIntakeOcrGateway
{
    Task<DocumentUploadReceipt> VerifyAsync(Guid uploadId, CancellationToken cancellationToken);
    Task<DocumentUploadReceipt> ConsumeAsync(Guid uploadId, CancellationToken cancellationToken);
    Task<DocumentOcrJobResponse> SubmitAsync(SubmitOcrJobRequest request, CancellationToken cancellationToken);
}

public interface IShipmentDocumentIntakeGateway
{
    Task<DocumentIntakeResponse> CreateAsync(CreateDocumentIntakeRequest request, CancellationToken cancellationToken);
    Task<DocumentIntakeResponse> AttachAsync(AttachDocumentIntakeRequest request, CancellationToken cancellationToken);
    Task<DocumentIntakeResponse> MarkRetryableAsync(MarkDocumentIntakeRetryableRequest request, CancellationToken cancellationToken);
    Task<DocumentIntakeResponse> MarkSubmittedAsync(MarkDocumentIntakeSubmittedRequest request, CancellationToken cancellationToken);
}

public sealed class DocumentIntakeOcrGateway(
    DocumentOcrService.DocumentOcrServiceClient client) : IDocumentIntakeOcrGateway
{
    public Task<DocumentUploadReceipt> VerifyAsync(Guid uploadId, CancellationToken cancellationToken) =>
        client.VerifyUploadSessionAsync(
            new VerifyUploadSessionRequest { UploadId = uploadId.ToString() },
            cancellationToken: cancellationToken).ResponseAsync;

    public Task<DocumentUploadReceipt> ConsumeAsync(Guid uploadId, CancellationToken cancellationToken) =>
        client.ConsumeUploadSessionAsync(
            new ConsumeUploadSessionRequest { UploadId = uploadId.ToString() },
            cancellationToken: cancellationToken).ResponseAsync;

    public Task<DocumentOcrJobResponse> SubmitAsync(SubmitOcrJobRequest request, CancellationToken cancellationToken) =>
        client.SubmitOcrJobAsync(request, cancellationToken: cancellationToken).ResponseAsync;
}

public sealed class ShipmentDocumentIntakeGateway(
    ShipmentWorkflowService.ShipmentWorkflowServiceClient client) : IShipmentDocumentIntakeGateway
{
    public Task<DocumentIntakeResponse> CreateAsync(CreateDocumentIntakeRequest request, CancellationToken cancellationToken) =>
        client.CreateDocumentIntakeAsync(request, cancellationToken: cancellationToken).ResponseAsync;

    public Task<DocumentIntakeResponse> AttachAsync(AttachDocumentIntakeRequest request, CancellationToken cancellationToken) =>
        client.AttachDocumentIntakeAsync(request, cancellationToken: cancellationToken).ResponseAsync;

    public Task<DocumentIntakeResponse> MarkRetryableAsync(MarkDocumentIntakeRetryableRequest request, CancellationToken cancellationToken) =>
        client.MarkDocumentIntakeRetryableAsync(request, cancellationToken: cancellationToken).ResponseAsync;

    public Task<DocumentIntakeResponse> MarkSubmittedAsync(MarkDocumentIntakeSubmittedRequest request, CancellationToken cancellationToken) =>
        client.MarkDocumentIntakeSubmittedAsync(request, cancellationToken: cancellationToken).ResponseAsync;
}
