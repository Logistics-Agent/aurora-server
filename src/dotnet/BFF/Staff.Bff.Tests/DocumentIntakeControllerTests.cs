using System.Reflection;
using System.Text.Json;
using BuildingBlocks.BFF.Attributes;
using DocumentOcr.Grpc;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RegulatoryCompliance.Grpc;
using ShipmentWorkflow.Grpc;
using Shared.Constants;
using Shared.Security;
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
    public async Task Upload_endpoint_returns_201_with_serializable_contract()
    {
        var uploadId = Guid.CreateVersion7();
        var documentClient = new Mock<DocumentOcrService.DocumentOcrServiceClient>(new object[]
        {
            GrpcChannel.ForAddress("http://localhost:54321")
        });
        documentClient
            .Setup(client => client.CreateUploadSessionAsync(
                It.IsAny<CreateUploadSessionRequest>(), It.IsAny<Metadata>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .Returns(CreateSuccessfulCall(new DocumentUploadReceipt
            {
                UploadId = uploadId.ToString(),
                StorageReference = "objects/tenant/upload/invoice.pdf",
                WriteUrl = "https://upload.test/opaque",
                FileName = "invoice.pdf",
                MimeType = "application/pdf",
                SizeBytes = 1_024,
                Status = DocumentUploadStatus.Pending,
                ExpiresAt = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTimeOffset(DateTimeOffset.UtcNow.AddMinutes(15))
            }));
        var controller = new DocumentsController(
            documentClient.Object,
            new Mock<RegulatoryComplianceService.RegulatoryComplianceServiceClient>(new object[]
            {
                GrpcChannel.ForAddress("http://localhost:54324")
            }).Object,
            NullLogger<DocumentsController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        var result = await controller.CreateUploadSession(
            new CreateDocumentUploadSessionRequest("upload-key", "invoice.pdf", "application/pdf", 1_024, null),
            CancellationToken.None);

        var created = Assert.IsType<CreatedResult>(result);
        Assert.Equal(StatusCodes.Status201Created, created.StatusCode);
        var serialized = JsonSerializer.Serialize(created.Value);
        Assert.Contains(uploadId.ToString(), serialized);
        Assert.Contains("objects/tenant/upload/invoice.pdf", serialized);
    }

    [Fact]
    public async Task Intake_endpoint_returns_202_with_serializable_contract()
    {
        var orchestrator = new Mock<IDocumentIntakeOrchestrator>();
        var response = new DocumentIntakeHttpResponse(
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            "PROCESSING",
            "QUEUED",
            "SUBMITTED",
            "invoice.pdf",
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            false,
            null);
        orchestrator
            .Setup(service => service.ComposeAsync(
                It.IsAny<Guid>(), It.IsAny<CreateDocumentIntakeRequestModel>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);
        var controller = CreateShipmentsController(orchestrator.Object);

        var result = await controller.CreateDocumentIntake(
            Guid.CreateVersion7().ToString(),
            new CreateDocumentIntakeBody(Guid.CreateVersion7().ToString(), "INVOICE", "intake-key"),
            CancellationToken.None);

        var accepted = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status202Accepted, accepted.StatusCode);
        Assert.Contains("PROCESSING", JsonSerializer.Serialize(accepted.Value));
    }

    [Fact]
    public async Task Intake_endpoint_serializes_stable_problem_details()
    {
        var orchestrator = new Mock<IDocumentIntakeOrchestrator>();
        orchestrator
            .Setup(service => service.ComposeAsync(
                It.IsAny<Guid>(), It.IsAny<CreateDocumentIntakeRequestModel>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DocumentIntakeOrchestrationException(
                "UPLOAD_TENANT_MISMATCH",
                "The upload session was not found.",
                StatusCodes.Status404NotFound,
                false));
        var controller = CreateShipmentsController(orchestrator.Object);

        var result = await controller.CreateDocumentIntake(
            Guid.CreateVersion7().ToString(),
            new CreateDocumentIntakeBody(Guid.CreateVersion7().ToString(), "INVOICE", "intake-key"),
            CancellationToken.None);

        var problemResult = Assert.IsType<ObjectResult>(result);
        var problem = Assert.IsType<ProblemDetails>(problemResult.Value);
        var serialized = JsonSerializer.Serialize(problem);
        Assert.Equal("UPLOAD_TENANT_MISMATCH", problem.Extensions["code"]);
        Assert.False((bool)problem.Extensions["retryable"]!);
        Assert.Contains("UPLOAD_TENANT_MISMATCH", serialized);
        Assert.Contains("retryable", serialized);
    }

    private static ShipmentsController CreateShipmentsController(IDocumentIntakeOrchestrator orchestrator) =>
        new(
            new Mock<ShipmentWorkflowService.ShipmentWorkflowServiceClient>(new object[]
            {
                GrpcChannel.ForAddress("http://localhost:54322")
            }).Object,
            new CurrentUserService(),
            NullLogger<ShipmentsController>.Instance,
            orchestrator);

    private static AsyncUnaryCall<TResponse> CreateSuccessfulCall<TResponse>(TResponse response)
        where TResponse : class => new(
            Task.FromResult(response),
            Task.FromResult(new Metadata()),
            () => new Status(StatusCode.OK, string.Empty),
            () => new Metadata(),
            () => { });
}
