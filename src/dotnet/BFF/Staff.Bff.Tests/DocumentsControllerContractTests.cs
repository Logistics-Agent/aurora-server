using System.Reflection;
using BuildingBlocks.BFF.Attributes;
using DocumentOcr.Grpc;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RegulatoryCompliance.Grpc;
using Shared.Constants;
using StaffBff.Controllers;
using StaffBff.Services;

namespace StaffBff.Tests;

public sealed class DocumentsControllerContractTests
{
    [Theory]
    [InlineData(DocumentOcrJobStatus.Unspecified, null)]
    [InlineData(DocumentOcrJobStatus.Queued, "QUEUED")]
    [InlineData(DocumentOcrJobStatus.Processing, "EXTRACTING")]
    [InlineData(DocumentOcrJobStatus.RequiresReview, "HUMAN_REVIEW")]
    [InlineData(DocumentOcrJobStatus.Completed, "COMPLETED")]
    [InlineData(DocumentOcrJobStatus.Failed, "ERROR")]
    [InlineData(DocumentOcrJobStatus.Cancelled, null)]
    [InlineData(DocumentOcrJobStatus.Rejected, null)]
    public void MapStage_maps_every_document_ocr_status(
        DocumentOcrJobStatus status,
        string? expectedStage)
    {
        Assert.Equal(expectedStage, DocumentsContract.MapStage(status));
    }

    [Theory]
    [InlineData(DocumentOcrJobStatus.Unspecified, false, "RECEIVED")]
    [InlineData(DocumentOcrJobStatus.Queued, false, "PROCESSING")]
    [InlineData(DocumentOcrJobStatus.Processing, false, "PROCESSING")]
    [InlineData(DocumentOcrJobStatus.RequiresReview, false, "NEEDS_REVIEW")]
    [InlineData(DocumentOcrJobStatus.Completed, false, "READY")]
    [InlineData(DocumentOcrJobStatus.Completed, true, "NEEDS_REVIEW")]
    [InlineData(DocumentOcrJobStatus.Failed, false, "FAILED")]
    [InlineData(DocumentOcrJobStatus.Cancelled, false, "CANCELLED")]
    [InlineData(DocumentOcrJobStatus.Rejected, false, "REJECTED")]
    public void MapStatus_maps_every_document_ocr_status(
        DocumentOcrJobStatus status,
        bool needsReview,
        string expectedStatus)
    {
        Assert.Equal(expectedStatus, DocumentsContract.MapStatus(status, needsReview));
    }

    [Theory]
    [InlineData(nameof(DocumentsController.SubmitShipmentDocument), PermissionConstants.Documents.Ingest)]
    [InlineData(nameof(DocumentsController.GetShipmentDocumentStatus), PermissionConstants.Documents.Read)]
    [InlineData(nameof(DocumentsController.ListShipmentDocuments), PermissionConstants.Documents.Read)]
    [InlineData(nameof(DocumentsController.GetShipmentDocumentReview), PermissionConstants.Ocr.Review)]
    [InlineData(nameof(DocumentsController.SubmitShipmentDocumentReview), PermissionConstants.Ocr.Review)]
    [InlineData(nameof(DocumentsController.CancelShipmentDocument), PermissionConstants.Documents.Manage)]
    [InlineData(nameof(DocumentsController.RetryShipmentDocument), PermissionConstants.Documents.Manage)]
    [InlineData(nameof(DocumentsController.SubmitRegulatorySource), PermissionConstants.Documents.Ingest)]
    [InlineData(nameof(DocumentsController.QueryRegulations), PermissionConstants.Documents.Read)]
    [InlineData(nameof(DocumentsController.SubmitKnowledgeDocument), PermissionConstants.Documents.Ingest)]
    [InlineData(nameof(DocumentsController.QueryKnowledge), PermissionConstants.Documents.Read)]
    [InlineData(nameof(DocumentsController.SubmitGeneralDocument), PermissionConstants.Documents.Ingest)]
    [InlineData(nameof(DocumentsController.PromoteGeneralDocumentToKnowledge), PermissionConstants.Documents.Manage)]
    public void Document_endpoints_require_only_the_canonical_capability(
        string methodName,
        string expectedPermission)
    {
        var method = typeof(DocumentsController).GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance);
        var permission = method?.GetCustomAttribute<RequirePermissionAttribute>();

        Assert.NotNull(permission);
        Assert.Equal(expectedPermission, permission!.RequiredPermission);
        Assert.Null(permission.LegacyFallbackPermission);
        Assert.Empty(permission.FallbackPermissions);
    }

    [Theory]
    [InlineData(StatusCode.Unavailable)]
    [InlineData(StatusCode.DeadlineExceeded)]
    public async Task List_maps_ocr_dependency_failure_to_stable_problem_details(StatusCode statusCode)
    {
        var fixture = CreateFixture();
        SetupListFailure(fixture.DocumentOcrClient, statusCode);

        var result = await fixture.Controller.ListShipmentDocuments(cancellationToken: default);

        AssertProblem(result, StatusCodes.Status503ServiceUnavailable, "DOCUMENT_OCR_UNAVAILABLE");
    }

    [Fact]
    public async Task List_preserves_successful_empty_page()
    {
        var fixture = CreateFixture();
        fixture.DocumentOcrClient
            .Setup(client => client.ListDocumentJobsAsync(
                It.IsAny<ListDocumentJobsRequest>(),
                It.IsAny<Metadata>(),
                It.IsAny<DateTime?>(),
                It.IsAny<CancellationToken>()))
            .Returns(CreateSuccessfulCall(new ListDocumentJobsResponse
            {
                Page = 2,
                PageSize = 20,
                TotalItems = 0,
                TotalPages = 0
            }));

        var result = await fixture.Controller.ListShipmentDocuments(page: 2, cancellationToken: default);

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<ListShipmentDocumentsResponse>(ok.Value);
        Assert.Empty(response.Items);
        Assert.Equal(2, response.Page);
        Assert.Equal(0, response.TotalItems);
    }

    [Theory]
    [InlineData(StatusCode.Unavailable)]
    [InlineData(StatusCode.DeadlineExceeded)]
    public async Task Get_detail_maps_ocr_dependency_failure_to_stable_problem_details(StatusCode statusCode)
    {
        var fixture = CreateFixture();
        SetupGetFailure(fixture.DocumentOcrClient, statusCode);

        var result = await fixture.Controller.GetShipmentDocumentStatus("job-1", default);

        AssertProblem(result, StatusCodes.Status503ServiceUnavailable, "DOCUMENT_OCR_UNAVAILABLE");
    }

    [Theory]
    [InlineData(StatusCode.Unavailable)]
    [InlineData(StatusCode.DeadlineExceeded)]
    public async Task Get_review_maps_ocr_dependency_failure_to_stable_problem_details(StatusCode statusCode)
    {
        var fixture = CreateFixture();
        SetupGetFailure(fixture.DocumentOcrClient, statusCode);

        var result = await fixture.Controller.GetShipmentDocumentReview("job-1", default);

        AssertProblem(result, StatusCodes.Status503ServiceUnavailable, "DOCUMENT_OCR_UNAVAILABLE");
    }

    [Theory]
    [InlineData(StatusCode.Unavailable)]
    [InlineData(StatusCode.DeadlineExceeded)]
    public async Task Submit_review_maps_ocr_dependency_failure_to_stable_problem_details(StatusCode statusCode)
    {
        var fixture = CreateFixture();
        SetupReviewFailure(fixture.DocumentOcrClient, statusCode);

        var result = await fixture.Controller.SubmitShipmentDocumentReview(
            "job-1",
            new SubmitOcrReviewRequest("CONFIRM", null, null),
            default);

        AssertProblem(result, StatusCodes.Status503ServiceUnavailable, "DOCUMENT_OCR_UNAVAILABLE");
    }

    [Fact]
    public async Task Get_detail_preserves_not_found_mapping()
    {
        var fixture = CreateFixture();
        SetupGetFailure(fixture.DocumentOcrClient, StatusCode.NotFound);

        var result = await fixture.Controller.GetShipmentDocumentStatus("job-1", default);

        AssertProblem(result, StatusCodes.Status404NotFound, "DOCUMENT_NOT_FOUND");
    }

    [Fact]
    public async Task Get_review_preserves_not_found_mapping()
    {
        var fixture = CreateFixture();
        SetupGetFailure(fixture.DocumentOcrClient, StatusCode.NotFound);

        var result = await fixture.Controller.GetShipmentDocumentReview("job-1", default);

        AssertProblem(result, StatusCodes.Status404NotFound, "DOCUMENT_NOT_FOUND");
    }

    [Fact]
    public async Task Submit_review_preserves_conflict_mapping()
    {
        var fixture = CreateFixture();
        SetupReviewFailure(fixture.DocumentOcrClient, StatusCode.FailedPrecondition);

        var result = await fixture.Controller.SubmitShipmentDocumentReview(
            "job-1",
            new SubmitOcrReviewRequest("CONFIRM", null, null),
            default);

        AssertProblem(result, StatusCodes.Status409Conflict, "INVALID_STATE_TRANSITION");
    }

    [Fact]
    public async Task Submit_review_preserves_bad_request_mapping()
    {
        var fixture = CreateFixture();

        var result = await fixture.Controller.SubmitShipmentDocumentReview(
            "job-1",
            new SubmitOcrReviewRequest("INVALID", null, null),
            default);

        AssertProblem(result, StatusCodes.Status400BadRequest, "INVALID_REQUEST");
    }

    [Fact]
    public async Task Cancel_preserves_conflict_mapping()
    {
        var fixture = CreateFixture();
        fixture.DocumentOcrClient
            .Setup(client => client.CancelDocumentJobAsync(
                It.IsAny<CancelDocumentJobRequest>(),
                It.IsAny<Metadata>(),
                It.IsAny<DateTime?>(),
                It.IsAny<CancellationToken>()))
            .Returns(CreateFailedCall<DocumentOcrJobResponse>(StatusCode.FailedPrecondition));

        var result = await fixture.Controller.CancelShipmentDocument("job-1", default);

        AssertProblem(result, StatusCodes.Status409Conflict, "INVALID_STATE_TRANSITION");
    }

    [Fact]
    public async Task Retry_preserves_not_found_mapping()
    {
        var fixture = CreateFixture();
        fixture.DocumentOcrClient
            .Setup(client => client.RetryDocumentJobAsync(
                It.IsAny<RetryDocumentJobRequest>(),
                It.IsAny<Metadata>(),
                It.IsAny<DateTime?>(),
                It.IsAny<CancellationToken>()))
            .Returns(CreateFailedCall<DocumentOcrJobResponse>(StatusCode.NotFound));

        var result = await fixture.Controller.RetryShipmentDocument("job-1", default);

        AssertProblem(result, StatusCodes.Status404NotFound, "DOCUMENT_NOT_FOUND");
    }

    private static ControllerFixture CreateFixture()
    {
        var documentOcrClient = new Mock<DocumentOcrService.DocumentOcrServiceClient>(new object[]
        {
            GrpcChannel.ForAddress("http://localhost:54321")
        });
        var regulatoryClient = new Mock<RegulatoryComplianceService.RegulatoryComplianceServiceClient>(new object[]
        {
            GrpcChannel.ForAddress("http://localhost:54322")
        });
        var controller = new DocumentsController(
            documentOcrClient.Object,
            regulatoryClient.Object,
            NullLogger<DocumentsController>.Instance);

        return new ControllerFixture(documentOcrClient, controller);
    }

    private static void SetupListFailure(
        Mock<DocumentOcrService.DocumentOcrServiceClient> client,
        StatusCode statusCode)
    {
        client
            .Setup(value => value.ListDocumentJobsAsync(
                It.IsAny<ListDocumentJobsRequest>(),
                It.IsAny<Metadata>(),
                It.IsAny<DateTime?>(),
                It.IsAny<CancellationToken>()))
            .Returns(CreateFailedCall<ListDocumentJobsResponse>(statusCode));
    }

    private static void SetupGetFailure(
        Mock<DocumentOcrService.DocumentOcrServiceClient> client,
        StatusCode statusCode)
    {
        client
            .Setup(value => value.GetDocumentJobAsync(
                It.IsAny<GetDocumentJobRequest>(),
                It.IsAny<Metadata>(),
                It.IsAny<DateTime?>(),
                It.IsAny<CancellationToken>()))
            .Returns(CreateFailedCall<DocumentOcrJobResponse>(statusCode));
    }

    private static void SetupReviewFailure(
        Mock<DocumentOcrService.DocumentOcrServiceClient> client,
        StatusCode statusCode)
    {
        client
            .Setup(value => value.ReviewDocumentJobAsync(
                It.IsAny<ReviewDocumentJobRequest>(),
                It.IsAny<Metadata>(),
                It.IsAny<DateTime?>(),
                It.IsAny<CancellationToken>()))
            .Returns(CreateFailedCall<DocumentOcrJobResponse>(statusCode));
    }

    private static ObjectResult AssertProblem(IActionResult result, int statusCode, string title)
    {
        var objectResult = Assert.IsAssignableFrom<ObjectResult>(result);
        Assert.Equal(statusCode, objectResult.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(objectResult.Value);
        Assert.Equal(title, problem.Title);
        if (title == "DOCUMENT_OCR_UNAVAILABLE")
            Assert.Equal(statusCode, problem.Status);
        return objectResult;
    }

    private static AsyncUnaryCall<TResponse> CreateFailedCall<TResponse>(StatusCode statusCode)
        where TResponse : class
    {
        return new AsyncUnaryCall<TResponse>(
            Task.FromException<TResponse>(new RpcException(new Status(statusCode, "downstream unavailable"))),
            Task.FromResult(new Metadata()),
            () => new Status(statusCode, "downstream unavailable"),
            () => new Metadata(),
            () => { });
    }

    private static AsyncUnaryCall<TResponse> CreateSuccessfulCall<TResponse>(TResponse response)
        where TResponse : class
    {
        return new AsyncUnaryCall<TResponse>(
            Task.FromResult(response),
            Task.FromResult(new Metadata()),
            () => new Status(StatusCode.OK, string.Empty),
            () => new Metadata(),
            () => { });
    }

    private sealed record ControllerFixture(
        Mock<DocumentOcrService.DocumentOcrServiceClient> DocumentOcrClient,
        DocumentsController Controller);
}
