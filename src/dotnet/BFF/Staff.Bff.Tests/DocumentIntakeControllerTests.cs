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
        var documentClient = CreateDocumentClient();
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
        var controller = CreateDocumentsController(documentClient.Object);

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
    public async Task Upload_endpoint_invalid_body_returns_canonical_problem_details()
    {
        var controller = CreateDocumentsController(CreateDocumentClient().Object);

        var result = await controller.CreateUploadSession(null!, CancellationToken.None);

        var problemResult = Assert.IsAssignableFrom<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, problemResult.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(problemResult.Value);
        var serialized = JsonSerializer.Serialize(problem);
        Assert.Equal("INVALID_UPLOAD_REQUEST", problem.Extensions["code"]);
        Assert.False((bool)problem.Extensions["retryable"]!);
        Assert.Contains("INVALID_UPLOAD_REQUEST", serialized);
        Assert.Contains("\"retryable\":false", serialized);
    }

    [Fact]
    public async Task Upload_endpoint_requires_caller_idempotency_key_without_trace_fallback()
    {
        var documentClient = CreateDocumentClient();
        var controller = CreateDocumentsController(documentClient.Object);

        var result = await controller.CreateUploadSession(
            new CreateDocumentUploadSessionRequest("", "invoice.pdf", "application/pdf", 1_024, null),
            CancellationToken.None);

        var problemResult = Assert.IsType<BadRequestObjectResult>(result);
        var problem = Assert.IsType<ProblemDetails>(problemResult.Value);
        Assert.Equal("INVALID_UPLOAD_REQUEST", problem.Extensions["code"]);
        documentClient.Verify(client => client.CreateUploadSessionAsync(
            It.IsAny<CreateUploadSessionRequest>(), It.IsAny<Metadata>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Upload_endpoint_fails_closed_without_trusted_tenant()
    {
        var documentClient = CreateDocumentClient();
        var controller = CreateDocumentsController(documentClient.Object, new CurrentUserService());

        var result = await controller.CreateUploadSession(
            new CreateDocumentUploadSessionRequest("upload-key", "invoice.pdf", "application/pdf", 1_024, null),
            CancellationToken.None);

        var problemResult = Assert.IsType<UnauthorizedObjectResult>(result);
        var problem = Assert.IsType<ProblemDetails>(problemResult.Value);
        Assert.Equal("TENANT_CONTEXT_REQUIRED", problem.Extensions["code"]);
        documentClient.Verify(client => client.CreateUploadSessionAsync(
            It.IsAny<CreateUploadSessionRequest>(), It.IsAny<Metadata>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Upload_endpoint_invalid_response_id_returns_canonical_problem_details()
    {
        var documentClient = CreateDocumentClient();
        documentClient
            .Setup(client => client.CreateUploadSessionAsync(
                It.IsAny<CreateUploadSessionRequest>(), It.IsAny<Metadata>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .Returns(CreateSuccessfulCall(new DocumentUploadReceipt
            {
                UploadId = "not-a-guid",
                StorageReference = "objects/tenant/upload/invoice.pdf",
                FileName = "invoice.pdf",
                MimeType = "application/pdf",
                Status = DocumentUploadStatus.Pending
            }));
        var controller = CreateDocumentsController(documentClient.Object);

        var result = await controller.CreateUploadSession(
            new CreateDocumentUploadSessionRequest("upload-key", "invoice.pdf", "application/pdf", 1_024, null),
            CancellationToken.None);

        var problemResult = Assert.IsAssignableFrom<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, problemResult.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(problemResult.Value);
        Assert.Equal("INVALID_UPLOAD_REQUEST", problem.Extensions["code"]);
        Assert.False((bool)problem.Extensions["retryable"]!);
    }

    [Theory]
    [InlineData("UPLOAD_INVALID_REQUEST", 400, false)]
    [InlineData("UPLOAD_TENANT_MISMATCH", 404, false)]
    [InlineData("UPLOAD_OBJECT_NOT_FOUND", 404, false)]
    [InlineData("UPLOAD_EXPIRED", 409, false)]
    [InlineData("UPLOAD_CONTENT_MISMATCH", 422, false)]
    [InlineData("DOCUMENT_OCR_UNAVAILABLE", 503, true)]
    public async Task Upload_endpoint_serializes_trailer_error_contract(
        string code,
        int expectedStatus,
        bool retryable)
    {
        var documentClient = CreateDocumentClient();
        documentClient
            .Setup(client => client.CreateUploadSessionAsync(
                It.IsAny<CreateUploadSessionRequest>(), It.IsAny<Metadata>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .Returns(CreateFailedCall<DocumentUploadReceipt>(
                new RpcException(
                    new Status(StatusCode.InvalidArgument, "upload failed"),
                    new Metadata { { "document-upload-validation-code", code } })));
        var controller = CreateDocumentsController(documentClient.Object);

        var result = await controller.CreateUploadSession(
            new CreateDocumentUploadSessionRequest("upload-key", "invoice.pdf", "application/pdf", 1_024, null),
            CancellationToken.None);

        var problemResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(expectedStatus, problemResult.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(problemResult.Value);
        Assert.Equal(code == "UPLOAD_INVALID_REQUEST" ? "INVALID_UPLOAD_REQUEST" : code, problem.Extensions["code"]);
        Assert.Equal(retryable, problem.Extensions["retryable"]);
    }

    [Fact]
    public async Task Upload_endpoint_already_exists_returns_conflict_problem_details()
    {
        var documentClient = CreateDocumentClient();
        documentClient
            .Setup(client => client.CreateUploadSessionAsync(
                It.IsAny<CreateUploadSessionRequest>(), It.IsAny<Metadata>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .Returns(CreateFailedCall<DocumentUploadReceipt>(
                new RpcException(new Status(StatusCode.AlreadyExists, "conflict"))));
        var controller = CreateDocumentsController(documentClient.Object);

        var result = await controller.CreateUploadSession(
            new CreateDocumentUploadSessionRequest("upload-key", "invoice.pdf", "application/pdf", 1_024, null),
            CancellationToken.None);

        var problemResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status409Conflict, problemResult.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(problemResult.Value);
        Assert.Equal("UPLOAD_IDEMPOTENCY_CONFLICT", problem.Extensions["code"]);
        Assert.False((bool)problem.Extensions["retryable"]!);
    }

    [Fact]
    public async Task Legacy_submit_requires_idempotency_key()
    {
        var documentClient = CreateDocumentClient();
        var controller = CreateDocumentsController(documentClient.Object);

        var result = await controller.SubmitShipmentDocument(
            new SubmitShipmentDocumentRequest("", "objects/tenant/upload/invoice.pdf", "invoice.pdf", "application/pdf", 1_024, 1, Guid.Empty, "shipment-1"),
            CancellationToken.None);

        var problemResult = Assert.IsType<BadRequestObjectResult>(result);
        var problem = Assert.IsType<ProblemDetails>(problemResult.Value);
        Assert.Equal("INVALID_FILE", problem.Extensions["code"]);
        documentClient.Verify(client => client.SubmitOcrJobAsync(
            It.IsAny<SubmitOcrJobRequest>(), It.IsAny<Metadata>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Legacy_submit_derives_a_stable_external_document_id()
    {
        var documentClient = CreateDocumentClient();
        var response = new DocumentOcrJobResponse
        {
            JobId = "job-1",
            FileName = "invoice.pdf",
            Status = DocumentOcrJobStatus.Queued
        };
        SubmitOcrJobRequest? firstRequest = null;
        SubmitOcrJobRequest? secondRequest = null;
        documentClient
            .Setup(client => client.SubmitOcrJobAsync(
                It.IsAny<SubmitOcrJobRequest>(), It.IsAny<Metadata>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .Callback<SubmitOcrJobRequest, Metadata, DateTime?, CancellationToken>((request, _, _, _) =>
            {
                if (firstRequest is null)
                    firstRequest = request;
                else
                    secondRequest = request;
            })
            .Returns(CreateSuccessfulCall(response));
        var controller = CreateDocumentsController(documentClient.Object);
        var request = new SubmitShipmentDocumentRequest(
            "legacy-key", "objects/tenant/upload/invoice.pdf", "invoice.pdf", "application/pdf", 1_024, 1, Guid.Empty, "shipment-1");

        await controller.SubmitShipmentDocument(request, CancellationToken.None);
        await controller.SubmitShipmentDocument(request, CancellationToken.None);

        Assert.NotNull(firstRequest);
        Assert.NotNull(secondRequest);
        Assert.Equal("legacy-key", firstRequest!.IdempotencyKey);
        Assert.Equal(firstRequest.ExternalDocumentId, secondRequest!.ExternalDocumentId);
        Assert.NotEqual(Guid.Empty.ToString(), firstRequest.ExternalDocumentId);
    }

    [Fact]
    public void Upload_and_intake_document_all_problem_status_contracts()
    {
        var expected = new[]
        {
            StatusCodes.Status400BadRequest,
            StatusCodes.Status404NotFound,
            StatusCodes.Status409Conflict,
            StatusCodes.Status422UnprocessableEntity,
            StatusCodes.Status503ServiceUnavailable
        };

        var uploadStatuses = typeof(DocumentsController)
            .GetMethod(nameof(DocumentsController.CreateUploadSession))!
            .GetCustomAttributes<ProducesResponseTypeAttribute>()
            .Select(attribute => attribute.StatusCode)
            .ToHashSet();
        var intakeStatuses = typeof(ShipmentsController)
            .GetMethod(nameof(ShipmentsController.CreateDocumentIntake))!
            .GetCustomAttributes<ProducesResponseTypeAttribute>()
            .Select(attribute => attribute.StatusCode)
            .ToHashSet();

        Assert.All(expected, status => Assert.Contains(status, uploadStatuses));
        Assert.All(expected, status => Assert.Contains(status, intakeStatuses));
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
    public async Task Intake_endpoint_fails_closed_without_trusted_tenant()
    {
        var orchestrator = new Mock<IDocumentIntakeOrchestrator>();
        var controller = CreateShipmentsControllerWithoutTenant(orchestrator.Object);

        var result = await controller.CreateDocumentIntake(
            Guid.CreateVersion7().ToString(),
            new CreateDocumentIntakeBody(Guid.CreateVersion7().ToString(), "INVOICE", "intake-key"),
            CancellationToken.None);

        var problemResult = Assert.IsType<UnauthorizedObjectResult>(result);
        var problem = Assert.IsType<ProblemDetails>(problemResult.Value);
        Assert.Equal("TENANT_CONTEXT_REQUIRED", problem.Extensions["code"]);
        orchestrator.Verify(service => service.ComposeAsync(
            It.IsAny<Guid>(), It.IsAny<CreateDocumentIntakeRequestModel>(), It.IsAny<CancellationToken>()), Times.Never);
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

    [Fact]
    public async Task Intake_endpoint_fails_closed_for_mismatched_exception_tuple()
    {
        var orchestrator = new Mock<IDocumentIntakeOrchestrator>();
        orchestrator
            .Setup(service => service.ComposeAsync(
                It.IsAny<Guid>(), It.IsAny<CreateDocumentIntakeRequestModel>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DocumentIntakeOrchestrationException(
                "DOCUMENT_NOT_FOUND",
                "do not leak this detail",
                StatusCodes.Status409Conflict,
                retryable: true));
        var controller = CreateShipmentsController(orchestrator.Object);

        var result = await controller.CreateDocumentIntake(
            Guid.CreateVersion7().ToString(),
            new CreateDocumentIntakeBody(Guid.CreateVersion7().ToString(), "INVOICE", "intake-key"),
            CancellationToken.None);

        var problemResult = Assert.IsType<ObjectResult>(result);
        var problem = Assert.IsType<ProblemDetails>(problemResult.Value);
        Assert.Equal(StatusCodes.Status500InternalServerError, problemResult.StatusCode);
        Assert.Equal("DOCUMENT_CONTRACT_ERROR", problem.Extensions["code"]);
        Assert.False((bool)problem.Extensions["retryable"]!);
        Assert.DoesNotContain("do not leak this detail", JsonSerializer.Serialize(problem));
    }

    private static ShipmentsController CreateShipmentsController(IDocumentIntakeOrchestrator orchestrator) =>
        new(
            new Mock<ShipmentWorkflowService.ShipmentWorkflowServiceClient>(new object[]
            {
                GrpcChannel.ForAddress("http://localhost:54322")
            }).Object,
            CreateTenantUser(),
            NullLogger<ShipmentsController>.Instance,
            orchestrator);

    private static Mock<DocumentOcrService.DocumentOcrServiceClient> CreateDocumentClient() =>
        new(new object[] { GrpcChannel.ForAddress("http://localhost:54321") });

    private static DocumentsController CreateDocumentsController(
        DocumentOcrService.DocumentOcrServiceClient documentClient,
        ICurrentUserService? currentUser = null) =>
        new(
            documentClient,
            new Mock<RegulatoryComplianceService.RegulatoryComplianceServiceClient>(new object[]
            {
                GrpcChannel.ForAddress("http://localhost:54324")
            }).Object,
            currentUser ?? CreateTenantUser(),
            NullLogger<DocumentsController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

    private static ICurrentUserService CreateTenantUser()
    {
        var currentUser = new CurrentUserService();
        currentUser.Populate(Guid.CreateVersion7(), Guid.CreateVersion7(), null, 1, RoleConstants.Staff, []);
        return currentUser;
    }

    private static ShipmentsController CreateShipmentsControllerWithoutTenant(
        IDocumentIntakeOrchestrator orchestrator) =>
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

    private static AsyncUnaryCall<TResponse> CreateFailedCall<TResponse>(RpcException exception)
        where TResponse : class => new(
            Task.FromException<TResponse>(exception),
            Task.FromResult(new Metadata()),
            () => exception.Status,
            () => new Metadata(),
            () => { });
}
