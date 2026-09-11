using DocumentOcr.Grpc;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RegulatoryCompliance.Grpc;
using StaffBff.Controllers;

namespace StaffBff.Tests;

public sealed class DocumentsControllerContractTests
{
    [Fact]
    public void RequiresReview_maps_to_human_review()
    {
        Assert.Equal("HUMAN_REVIEW", DocumentsContract.MapStage(DocumentOcrJobStatus.RequiresReview));
    }

    [Fact]
    public async Task List_does_not_convert_unavailable_rpc_to_empty_success()
    {
        var documentOcrClient = new Mock<DocumentOcrService.DocumentOcrServiceClient>(new object[] { GrpcChannel.ForAddress("http://localhost:54321") });
        var regulatoryClient = new Mock<RegulatoryComplianceService.RegulatoryComplianceServiceClient>(new object[] { GrpcChannel.ForAddress("http://localhost:54322") });
        documentOcrClient
            .Setup(client => client.ListDocumentJobsAsync(
                It.IsAny<ListDocumentJobsRequest>(),
                It.IsAny<Metadata>(),
                It.IsAny<DateTime?>(),
                It.IsAny<CancellationToken>()))
            .Returns(CreateFailedCall<ListDocumentJobsResponse>(StatusCode.Unavailable));

        var controller = new DocumentsController(
            documentOcrClient.Object,
            regulatoryClient.Object,
            NullLogger<DocumentsController>.Instance);

        var result = await controller.ListShipmentDocuments(cancellationToken: default);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, objectResult.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(objectResult.Value);
        Assert.Equal("DOCUMENT_OCR_UNAVAILABLE", problem.Title);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, problem.Status);
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
}
