using System.Reflection;
using BuildingBlocks.BFF.Attributes;
using DocumentOcr.Grpc;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ShipmentWorkflow.Grpc;
using Shared.Constants;
using StaffBff.Controllers;
using StaffBff.Services;

namespace StaffBff.Tests;

public sealed class DocumentIntakeControllerTests
{
    [Fact]
    public void Upload_and_intake_endpoints_require_documents_ingest()
    {
        var upload = typeof(DocumentsController).GetMethod(nameof(DocumentsController.CreateUploadSession));
        var intake = typeof(ShipmentsController).GetMethod(nameof(ShipmentsController.CreateDocumentIntake));

        Assert.Equal(PermissionConstants.Documents.Ingest,
            upload?.GetCustomAttribute<RequirePermissionAttribute>()?.RequiredPermission);
        Assert.Equal(PermissionConstants.Documents.Ingest,
            intake?.GetCustomAttribute<RequirePermissionAttribute>()?.RequiredPermission);
    }

    [Fact]
    public async Task Intake_replays_after_ocr_unavailable_without_duplicate_attachment()
    {
        var fixture = CreateOrchestratorFixture();
        var uploadId = Guid.CreateVersion7();
        var shipmentId = Guid.CreateVersion7();
        var documentId = Guid.CreateVersion7();
        var intakeId = Guid.CreateVersion7();
        var jobId = Guid.CreateVersion7();
        var attempts = 0;

        fixture.DocumentOcrClient
            .Setup(client => client.GetUploadSessionAsync(
                It.IsAny<GetUploadSessionRequest>(), It.IsAny<Metadata>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .Returns(CreateSuccessfulCall(new DocumentUploadReceipt
            {
                UploadId = uploadId.ToString(),
                StorageReference = "objects/tenant/upload/invoice.pdf",
                FileName = "invoice.pdf",
                Status = DocumentUploadStatus.Uploaded
            }));
        fixture.DocumentOcrClient
            .Setup(client => client.VerifyUploadSessionAsync(
                It.IsAny<VerifyUploadSessionRequest>(), It.IsAny<Metadata>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .Returns(CreateSuccessfulCall(new DocumentUploadReceipt
            {
                UploadId = uploadId.ToString(),
                StorageReference = "objects/tenant/upload/invoice.pdf",
                FileName = "invoice.pdf",
                Status = DocumentUploadStatus.Uploaded
            }));
        fixture.ShipmentClient
            .Setup(client => client.CreateDocumentIntakeAsync(
                It.IsAny<CreateDocumentIntakeRequest>(), It.IsAny<Metadata>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .Returns(() => CreateSuccessfulCall(new DocumentIntakeResponse
            {
                IntakeId = intakeId.ToString(),
                ShipmentId = shipmentId.ToString(),
                DocumentId = documentId.ToString(),
                UploadId = uploadId.ToString(),
                StorageReference = "objects/tenant/upload/invoice.pdf",
                FileName = "invoice.pdf",
                Status = "PENDING_OCR",
                Stage = "PENDING_OCR"
            }));
        fixture.DocumentOcrClient
            .Setup(client => client.SubmitOcrJobAsync(
                It.IsAny<SubmitOcrJobRequest>(), It.IsAny<Metadata>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                attempts++;
                return attempts == 1
                    ? CreateFailedCall<DocumentOcrJobResponse>(StatusCode.Unavailable)
                    : CreateSuccessfulCall(new DocumentOcrJobResponse
                    {
                        JobId = jobId.ToString(),
                        Status = DocumentOcrJobStatus.Queued,
                        FileName = "invoice.pdf"
                    });
            });
        fixture.ShipmentClient
            .Setup(client => client.MarkDocumentIntakeRetryableAsync(
                It.IsAny<MarkDocumentIntakeRetryableRequest>(), It.IsAny<Metadata>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .Returns(CreateSuccessfulCall(new DocumentIntakeResponse
            {
                IntakeId = intakeId.ToString(),
                ShipmentId = shipmentId.ToString(),
                DocumentId = documentId.ToString(),
                UploadId = uploadId.ToString(),
                StorageReference = "objects/tenant/upload/invoice.pdf",
                FileName = "invoice.pdf",
                Status = "FAILED_RETRYABLE",
                Stage = "FAILED_RETRYABLE"
            }));
        fixture.ShipmentClient
            .Setup(client => client.MarkDocumentIntakeSubmittedAsync(
                It.IsAny<MarkDocumentIntakeSubmittedRequest>(), It.IsAny<Metadata>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .Returns(CreateSuccessfulCall(new DocumentIntakeResponse
            {
                IntakeId = intakeId.ToString(),
                ShipmentId = shipmentId.ToString(),
                DocumentId = documentId.ToString(),
                UploadId = uploadId.ToString(),
                StorageReference = "objects/tenant/upload/invoice.pdf",
                FileName = "invoice.pdf",
                Status = "SUBMITTED",
                Stage = "SUBMITTED"
            }));

        var request = new CreateDocumentIntakeRequestModel(uploadId, "INVOICE", "intake-key");
        var exception = await Assert.ThrowsAsync<DocumentIntakeOrchestrationException>(() =>
            fixture.Orchestrator.ComposeAsync(shipmentId, request, CancellationToken.None));

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, exception.StatusCode);
        Assert.Equal("DOCUMENT_OCR_UNAVAILABLE", exception.Code);
        Assert.Equal(intakeId, exception.IntakeId);
        Assert.Equal(documentId, exception.DocumentId);

        var result = await fixture.Orchestrator.ComposeAsync(shipmentId, request, CancellationToken.None);

        Assert.Equal(intakeId, result.IntakeId);
        Assert.Equal(documentId, result.DocumentId);
        Assert.Equal(jobId, result.OcrJobId);
        Assert.Equal("PROCESSING", result.Status);
        Assert.Equal("QUEUED", result.Stage);
        fixture.ShipmentClient.Verify(client => client.CreateDocumentIntakeAsync(
            It.IsAny<CreateDocumentIntakeRequest>(), It.IsAny<Metadata>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        fixture.DocumentOcrClient.Verify(client => client.SubmitOcrJobAsync(
            It.Is<SubmitOcrJobRequest>(request => request.ExternalDocumentId == documentId.ToString() && request.ExternalShipmentId == shipmentId.ToString()),
            It.IsAny<Metadata>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task Upload_validation_trailer_maps_to_stable_problem_code()
    {
        var fixture = CreateOrchestratorFixture();
        var uploadId = Guid.CreateVersion7();
        var shipmentId = Guid.CreateVersion7();
        var intakeId = Guid.CreateVersion7();
        var documentId = Guid.CreateVersion7();

        fixture.DocumentOcrClient
            .Setup(client => client.GetUploadSessionAsync(
                It.IsAny<GetUploadSessionRequest>(), It.IsAny<Metadata>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .Returns(CreateSuccessfulCall(new DocumentUploadReceipt
            {
                UploadId = uploadId.ToString(),
                StorageReference = "objects/tenant/upload/invoice.pdf",
                FileName = "invoice.pdf",
                Status = DocumentUploadStatus.Pending
            }));
        fixture.ShipmentClient
            .Setup(client => client.CreateDocumentIntakeAsync(
                It.IsAny<CreateDocumentIntakeRequest>(), It.IsAny<Metadata>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .Returns(CreateSuccessfulCall(new DocumentIntakeResponse
            {
                IntakeId = intakeId.ToString(),
                ShipmentId = shipmentId.ToString(),
                DocumentId = documentId.ToString(),
                UploadId = uploadId.ToString(),
                StorageReference = "objects/tenant/upload/invoice.pdf",
                FileName = "invoice.pdf",
                Status = "PENDING_ATTACHMENT"
            }));
        fixture.DocumentOcrClient
            .Setup(client => client.VerifyUploadSessionAsync(
                It.IsAny<VerifyUploadSessionRequest>(), It.IsAny<Metadata>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .Returns(CreateFailedCall<DocumentUploadReceipt>(
                StatusCode.InvalidArgument,
                ("document-upload-validation-code", "UPLOAD_CONTENT_MISMATCH")));

        var exception = await Assert.ThrowsAsync<DocumentIntakeOrchestrationException>(() =>
            fixture.Orchestrator.ComposeAsync(
                shipmentId,
                new CreateDocumentIntakeRequestModel(uploadId, "INVOICE", "intake-key"),
                CancellationToken.None));

        Assert.Equal(StatusCodes.Status422UnprocessableEntity, exception.StatusCode);
        Assert.Equal("UPLOAD_CONTENT_MISMATCH", exception.Code);
        Assert.Equal(intakeId, exception.IntakeId);
    }

    private static OrchestratorFixture CreateOrchestratorFixture()
    {
        var documentClient = new Mock<DocumentOcrService.DocumentOcrServiceClient>(new object[]
        {
            GrpcChannel.ForAddress("http://localhost:54321")
        });
        var shipmentClient = new Mock<ShipmentWorkflowService.ShipmentWorkflowServiceClient>(new object[]
        {
            GrpcChannel.ForAddress("http://localhost:54322")
        });
        var orchestrator = new DocumentIntakeOrchestrator(
            documentClient.Object,
            shipmentClient.Object,
            NullLogger<DocumentIntakeOrchestrator>.Instance);
        return new OrchestratorFixture(documentClient, shipmentClient, orchestrator);
    }

    private static AsyncUnaryCall<TResponse> CreateSuccessfulCall<TResponse>(TResponse response)
        where TResponse : class => new(
            Task.FromResult(response),
            Task.FromResult(new Metadata()),
            () => new Status(StatusCode.OK, string.Empty),
            () => new Metadata(),
            () => { });

    private static AsyncUnaryCall<TResponse> CreateFailedCall<TResponse>(
        StatusCode statusCode,
        params (string Key, string Value)[] trailers)
        where TResponse : class
    {
        var metadata = new Metadata();
        foreach (var trailer in trailers)
            metadata.Add(trailer.Key, trailer.Value);
        return new AsyncUnaryCall<TResponse>(
            Task.FromException<TResponse>(new RpcException(new Status(statusCode, "downstream failure"), metadata)),
            Task.FromResult(metadata),
            () => new Status(statusCode, "downstream failure"),
            () => metadata,
            () => { });
    }

    private sealed record OrchestratorFixture(
        Mock<DocumentOcrService.DocumentOcrServiceClient> DocumentOcrClient,
        Mock<ShipmentWorkflowService.ShipmentWorkflowServiceClient> ShipmentClient,
        DocumentIntakeOrchestrator Orchestrator);
}
