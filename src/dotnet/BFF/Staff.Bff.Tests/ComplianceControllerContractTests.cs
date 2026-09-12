using DocumentOcr.Grpc;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RegulatoryCompliance.Grpc;
using Shared.Constants;
using Shared.Security;
using ShipmentWorkflow.Grpc;
using StaffBff.Controllers;
using StaffBff.Services;

namespace StaffBff.Tests;

public sealed class ComplianceControllerContractTests
{
    [Fact]
    public void Start_evaluation_uses_shipment_route_and_compliance_permission()
    {
        var method = typeof(ComplianceController).GetMethod(nameof(ComplianceController.StartEvaluation));

        Assert.NotNull(method);
        var route = Assert.Single(method!.GetCustomAttributes(typeof(HttpPostAttribute), inherit: true));
        Assert.Equal(
            "~/api/v{version:apiVersion}/shipments/{shipmentId}/compliance-evaluations",
            ((HttpPostAttribute)route).Template);
        var permission = Assert.Single(method.GetCustomAttributes(typeof(BuildingBlocks.BFF.Attributes.RequirePermissionAttribute), true));
        Assert.Equal(PermissionConstants.Compliance.Read, ((BuildingBlocks.BFF.Attributes.RequirePermissionAttribute)permission).RequiredPermission);
    }

    [Fact]
    public async Task Start_evaluation_composes_authoritative_snapshot_and_returns_accepted_location()
    {
        var tenantId = Guid.CreateVersion7();
        var shipmentId = Guid.CreateVersion7();
        var documentId = Guid.CreateVersion7();
        var shipmentClient = CreateClient<ShipmentWorkflowService.ShipmentWorkflowServiceClient>();
        var documentClient = CreateClient<DocumentOcrService.DocumentOcrServiceClient>();
        var complianceClient = CreateClient<RegulatoryComplianceService.RegulatoryComplianceServiceClient>();

        shipmentClient
            .Setup(client => client.GetShipmentAsync(
                It.IsAny<GetShipmentRequest>(), It.IsAny<Metadata>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .Returns(Success(new ShipmentResponse
            {
                Id = shipmentId.ToString(),
                TenantId = tenantId.ToString(),
                OriginCountry = "VN",
                DestinationCountry = "SG",
                TransportMode = "SEA",
                UpdatedAt = Timestamp.FromDateTimeOffset(DateTimeOffset.Parse("2026-09-13T01:00:00Z")),
                CargoItems =
                {
                    new CargoItemResponse { Name = "Machinery", Quantity = 1, Unit = "unit", WeightKg = 10 }
                }
            }));
        documentClient
            .Setup(client => client.ListDocumentJobsAsync(
                It.IsAny<ListDocumentJobsRequest>(), It.IsAny<Metadata>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .Returns(Success(new ListDocumentJobsResponse
            {
                Jobs =
                {
                    new DocumentOcrJobResponse
                    {
                        ExternalDocumentId = documentId.ToString(),
                        DetectedDocumentType = OcrDocumentType.CommercialInvoice,
                        Status = DocumentOcrJobStatus.Completed,
                        NormalizedJson = "{\"invoiceNo\":\"INV-1\"}",
                        Confidence = 0.95
                    }
                }
            }));
        EvaluateComplianceRequest? captured = null;
        complianceClient
            .Setup(client => client.EvaluateComplianceAsync(
                It.IsAny<EvaluateComplianceRequest>(), It.IsAny<Metadata>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .Callback<EvaluateComplianceRequest, Metadata, DateTime?, CancellationToken>((request, _, _, _) => captured = request)
            .Returns(Success(new ComplianceEvaluationResponse
            {
                EvaluationId = Guid.CreateVersion7().ToString(),
                ExternalShipmentId = shipmentId.ToString(),
                Status = ComplianceEvaluationStatus.Pending
            }));

        var controller = CreateController(shipmentClient, documentClient, complianceClient, tenantId);

        var result = await controller.StartEvaluation(
            shipmentId.ToString(),
            new StartComplianceEvaluationRequest("evaluation-key", DateTimeOffset.Parse("2026-09-13T02:00:00Z")));

        var accepted = Assert.IsType<AcceptedResult>(result);
        Assert.StartsWith($"/api/v1/compliance/evaluations/{accepted.Value!.GetType().GetProperty("EvaluationId")!.GetValue(accepted.Value)}", accepted.Location);
        Assert.NotNull(captured);
        Assert.Equal("evaluation-key", captured!.IdempotencyKey);
        Assert.Equal(shipmentId.ToString(), captured.ExternalShipmentId);
        Assert.Single(captured.Cargo);
        Assert.Single(captured.Documents);
        Assert.Equal(documentId.ToString(), captured.Documents[0].ExternalDocumentId);
    }

    [Fact]
    public async Task Start_evaluation_returns_bad_request_for_malformed_shipment_id()
    {
        var shipmentClient = CreateClient<ShipmentWorkflowService.ShipmentWorkflowServiceClient>();
        var documentClient = CreateClient<DocumentOcrService.DocumentOcrServiceClient>();
        var complianceClient = CreateClient<RegulatoryComplianceService.RegulatoryComplianceServiceClient>();
        var controller = CreateController(shipmentClient, documentClient, complianceClient, Guid.CreateVersion7());

        var result = await controller.StartEvaluation(
            "not-a-guid",
            new StartComplianceEvaluationRequest("evaluation-key", null));

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("INVALID_REQUEST", Assert.IsType<ProblemDetails>(badRequest.Value).Title);
        shipmentClient.VerifyNoOtherCalls();
        documentClient.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task List_evaluations_maps_paging_and_shipment_filter()
    {
        var shipmentClient = CreateClient<ShipmentWorkflowService.ShipmentWorkflowServiceClient>();
        var documentClient = CreateClient<DocumentOcrService.DocumentOcrServiceClient>();
        var complianceClient = CreateClient<RegulatoryComplianceService.RegulatoryComplianceServiceClient>();
        var shipmentId = Guid.CreateVersion7();
        ListComplianceEvaluationsRequest? captured = null;
        complianceClient
            .Setup(client => client.ListComplianceEvaluationsAsync(
                It.IsAny<ListComplianceEvaluationsRequest>(), It.IsAny<Metadata>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .Callback<ListComplianceEvaluationsRequest, Metadata, DateTime?, CancellationToken>((request, _, _, _) => captured = request)
            .Returns(Success(new ListComplianceEvaluationsResponse { Page = 2, PageSize = 5, TotalItems = 0 }));

        var controller = CreateController(shipmentClient, documentClient, complianceClient, Guid.CreateVersion7());

        var result = await controller.ListEvaluations(2, 5, shipmentId.ToString());

        Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(captured);
        Assert.Equal(2, captured!.Page);
        Assert.Equal(5, captured.PageSize);
        Assert.Equal(shipmentId.ToString(), captured.ExternalShipmentId);
    }

    private static ComplianceController CreateController(
        Mock<ShipmentWorkflowService.ShipmentWorkflowServiceClient> shipmentClient,
        Mock<DocumentOcrService.DocumentOcrServiceClient> documentClient,
        Mock<RegulatoryComplianceService.RegulatoryComplianceServiceClient> complianceClient,
        Guid tenantId)
    {
        var user = new CurrentUserService();
        user.Populate(Guid.CreateVersion7(), tenantId, null, 1, RoleConstants.Staff, []);
        return new ComplianceController(
            complianceClient.Object,
            new ComplianceSnapshotComposer(shipmentClient.Object, documentClient.Object, user),
            user,
            NullLogger<ComplianceController>.Instance);
    }

    private static Mock<TClient> CreateClient<TClient>()
        where TClient : ClientBase<TClient> =>
        new(new object[] { GrpcChannel.ForAddress("http://localhost:54321") });

    private static AsyncUnaryCall<TResponse> Success<TResponse>(TResponse response)
        where TResponse : class =>
        new(
            Task.FromResult(response),
            Task.FromResult(new Metadata()),
            () => new Status(StatusCode.OK, string.Empty),
            () => new Metadata(),
            () => { });
}
