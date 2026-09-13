using DocumentOcr.Grpc;
using Grpc.Core;
using Grpc.Net.Client;
using Moq;
using ShipmentWorkflow.Grpc;
using Shared.Constants;
using Shared.Security;
using StaffBff.Services;

namespace StaffBff.Tests;

public sealed class ComplianceSnapshotComposerTests
{
    [Fact]
    public async Task Compose_uses_shipment_and_only_reviewed_completed_ocr_documents()
    {
        var tenantId = Guid.CreateVersion7();
        var shipmentId = Guid.CreateVersion7();
        var shipmentClient = CreateClient<ShipmentWorkflowService.ShipmentWorkflowServiceClient>();
        var documentClient = CreateClient<DocumentOcrService.DocumentOcrServiceClient>();
        var currentUser = CreateTenantUser(tenantId);

        shipmentClient
            .Setup(client => client.GetShipmentAsync(
                It.Is<GetShipmentRequest>(request => request.Id == shipmentId.ToString()),
                It.IsAny<Metadata>(),
                It.IsAny<DateTime?>(),
                It.IsAny<CancellationToken>()))
            .Returns(Success(new ShipmentResponse
            {
                Id = shipmentId.ToString(),
                TenantId = tenantId.ToString(),
                OriginCountry = "vn",
                DestinationCountry = "sg",
                TransportMode = "sea",
                UpdatedAt = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTimeOffset(
                    DateTimeOffset.Parse("2026-09-13T01:00:00Z")),
                CargoItems =
                {
                    new CargoItemResponse
                    {
                        Name = "Machinery",
                        HsCode = "847989",
                        Quantity = 2,
                        Unit = "unit",
                        WeightKg = 10,
                        VolumeM3 = 1.5,
                        PackageType = "crate"
                    }
                }
            }));

        documentClient
            .Setup(client => client.ListDocumentJobsAsync(
                It.Is<ListDocumentJobsRequest>(request =>
                    request.ExternalShipmentId == shipmentId.ToString() && request.PageSize == 100),
                It.IsAny<Metadata>(),
                It.IsAny<DateTime?>(),
                It.IsAny<CancellationToken>()))
            .Returns(Success(new ListDocumentJobsResponse
            {
                Jobs =
                {
                    new DocumentOcrJobResponse
                    {
                        JobId = Guid.CreateVersion7().ToString(),
                        ExternalDocumentId = Guid.CreateVersion7().ToString(),
                        DetectedDocumentType = OcrDocumentType.CommercialInvoice,
                        Status = DocumentOcrJobStatus.Completed,
                        NormalizedJson = "{\"invoiceNo\":\"INV-1\"}",
                        Confidence = 0.97,
                        NeedsReview = false
                    },
                    new DocumentOcrJobResponse
                    {
                        JobId = Guid.CreateVersion7().ToString(),
                        ExternalDocumentId = Guid.CreateVersion7().ToString(),
                        DetectedDocumentType = OcrDocumentType.PackingList,
                        Status = DocumentOcrJobStatus.RequiresReview,
                        NormalizedJson = "{\"items\":[]}",
                        Confidence = 0.5,
                        NeedsReview = true
                    }
                }
            }));

        var composer = new ComplianceSnapshotComposer(shipmentClient.Object, documentClient.Object, currentUser);

        var snapshot = await composer.ComposeAsync(shipmentId.ToString(), DateTimeOffset.Parse("2026-09-13T02:00:00Z"));

        var cargo = Assert.Single(snapshot.Cargo);
        Assert.Equal("Machinery", cargo.Name);
        var document = Assert.Single(snapshot.Documents);
        Assert.Equal("CommercialInvoice", document.DocumentType);
        Assert.Contains("requires_review", snapshot.MissingEvidence.Single(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal("2026-09-13T01:00:00.0000000+00:00", snapshot.ShipmentVersion);
    }

    [Fact]
    public async Task Compose_rejects_malformed_shipment_id_without_calling_dependencies()
    {
        var shipmentClient = CreateClient<ShipmentWorkflowService.ShipmentWorkflowServiceClient>();
        var documentClient = CreateClient<DocumentOcrService.DocumentOcrServiceClient>();
        var composer = new ComplianceSnapshotComposer(
            shipmentClient.Object,
            documentClient.Object,
            CreateTenantUser(Guid.CreateVersion7()));

        await Assert.ThrowsAsync<ArgumentException>(() =>
            composer.ComposeAsync("not-a-guid", DateTimeOffset.UtcNow));

        shipmentClient.VerifyNoOtherCalls();
        documentClient.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Compose_rejects_incomplete_authoritative_shipment_fields()
    {
        var tenantId = Guid.CreateVersion7();
        var shipmentId = Guid.CreateVersion7();
        var shipmentClient = CreateClient<ShipmentWorkflowService.ShipmentWorkflowServiceClient>();
        var documentClient = CreateClient<DocumentOcrService.DocumentOcrServiceClient>();
        shipmentClient
            .Setup(client => client.GetShipmentAsync(
                It.IsAny<GetShipmentRequest>(),
                It.IsAny<Metadata>(),
                It.IsAny<DateTime?>(),
                It.IsAny<CancellationToken>()))
            .Returns(Success(new ShipmentResponse
            {
                Id = shipmentId.ToString(),
                TenantId = tenantId.ToString(),
                OriginCountry = "VN"
            }));

        var composer = new ComplianceSnapshotComposer(
            shipmentClient.Object,
            documentClient.Object,
            CreateTenantUser(tenantId));

        var exception = await Assert.ThrowsAsync<ComplianceSnapshotIncompleteException>(() =>
            composer.ComposeAsync(shipmentId.ToString(), DateTimeOffset.UtcNow));

        Assert.Contains("destinationCountry", exception.MissingFields);
        Assert.Contains("transportMode", exception.MissingFields);
        Assert.Contains("shipmentVersion", exception.MissingFields);
        documentClient.VerifyNoOtherCalls();
    }

    private static Mock<TClient> CreateClient<TClient>()
        where TClient : ClientBase<TClient>
    {
        return new Mock<TClient>(new object[] { GrpcChannel.ForAddress("http://localhost:54321") });
    }

    private static AsyncUnaryCall<TResponse> Success<TResponse>(TResponse response)
        where TResponse : class =>
        new(
            Task.FromResult(response),
            Task.FromResult(new Metadata()),
            () => new Status(StatusCode.OK, string.Empty),
            () => new Metadata(),
            () => { });

    private static ICurrentUserService CreateTenantUser(Guid tenantId)
    {
        var currentUser = new CurrentUserService();
        currentUser.Populate(Guid.CreateVersion7(), tenantId, null, 1, RoleConstants.Staff, []);
        return currentUser;
    }
}
