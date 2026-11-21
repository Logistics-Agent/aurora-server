using System.Net;
using BuildingBlocks.BFF.Extensions;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Grpc.Net.Client;
using Grpc.Net.ClientFactory;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using RegulatoryCompliance.Grpc;
using ShipmentWorkflow.Grpc;
using StaffBff.Controllers;

namespace StaffBff.Tests;

public sealed class AssistantControllerTests
{
    [Fact]
    public async Task QueryAssistant_ResolvesShipmentCodeAndForwardsVerifiedSummaryWithNonNullContextStrings()
    {
        var shipmentUuid = Guid.CreateVersion7();
        var shipmentClient = CreateClient<ShipmentWorkflowService.ShipmentWorkflowServiceClient>();
        var regulatoryClient = CreateClient<RegulatoryComplianceService.RegulatoryComplianceServiceClient>();

        GetShipmentRequest? capturedShipmentLookup = null;
        shipmentClient
            .Setup(client => client.GetShipmentAsync(
                It.IsAny<GetShipmentRequest>(), It.IsAny<Metadata>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .Callback<GetShipmentRequest, Metadata, DateTime?, CancellationToken>((req, _, _, _) => capturedShipmentLookup = req)
            .Returns(Success(new ShipmentResponse
            {
                Id = shipmentUuid.ToString(),
                ShipmentNo = "SHP-20261120-01A1F75409",
                OrderId = "ORD-2026-001",
                Status = "Draft",
                CustomerName = "Acme Logistics",
                OriginAddress = "Ho Chi Minh City",
                OriginCountry = "VN",
                DestinationAddress = "Singapore",
                DestinationCountry = "SG",
                TransportMode = "SEA",
                Priority = "STANDARD",
                CreatedAt = Timestamp.FromDateTimeOffset(DateTimeOffset.Parse("2026-11-20T06:00:00Z"))
            }));

        GenerateGroundedAnswerRequest? capturedAssistantRequest = null;
        regulatoryClient
            .Setup(client => client.GenerateGroundedAnswerAsync(
                It.IsAny<GenerateGroundedAnswerRequest>(), It.IsAny<Metadata>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .Callback<GenerateGroundedAnswerRequest, Metadata, DateTime?, CancellationToken>((req, _, _, _) => capturedAssistantRequest = req)
            .Returns(Success(new GenerateGroundedAnswerResponse
            {
                Query = "Đơn hàng này được tạo ngày mấy",
                Answer = "Đơn hàng SHP-20261120-01A1F75409 được tạo ngày 2026-11-20.",
                InsufficientEvidence = false,
                RetrievalTraceId = Guid.CreateVersion7().ToString(),
                Governance = new AssistantGovernanceMetadata
                {
                    DecisionId = "decision-1",
                    AutomationLevel = "ASSISTED",
                    RequiresApproval = false,
                    CapabilityCode = "compliance.answer",
                    TotalTokens = 100
                },
                Context = new RegulatoryCompliance.Grpc.AssistantContextSummary
                {
                    ShipmentId = shipmentUuid.ToString(),
                    EvaluationId = string.Empty,
                    Freshness = "CURRENT",
                    SnapshotHash = string.Empty
                }
            }));

        var controller = CreateController(regulatoryClient, shipmentClient);

        var result = await controller.QueryAssistant(
            new AssistantQueryRequest(
                Query: "Đơn hàng này được tạo ngày mấy",
                Mode: "ALL",
                JurisdictionCode: "VN",
                EffectiveAt: null,
                RegulationTypes: null,
                Categories: null,
                TopK: 5,
                MinimumScore: 0.6m,
                Context: new AssistantContextRequest("SHP-20261120-01A1F75409", null)),
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var payload = Assert.IsType<AssistantQueryResponse>(ok.Value);
        Assert.Equal("Đơn hàng SHP-20261120-01A1F75409 được tạo ngày 2026-11-20.", payload.Answer);
        Assert.NotNull(payload.Context);
        Assert.Equal(shipmentUuid.ToString(), payload.Context!.ShipmentId);
        Assert.Equal(string.Empty, payload.Context.EvaluationId);
        Assert.Equal(string.Empty, payload.Context.SnapshotHash);

        Assert.NotNull(capturedShipmentLookup);
        Assert.Equal("SHP-20261120-01A1F75409", capturedShipmentLookup!.Id);

        Assert.NotNull(capturedAssistantRequest);
        Assert.NotNull(capturedAssistantRequest!.Context);
        Assert.Equal(shipmentUuid.ToString(), capturedAssistantRequest.Context.ShipmentId);
        Assert.Contains("ShipmentNo: SHP-20261120-01A1F75409", capturedAssistantRequest.Context.ShipmentSummary);
        Assert.Contains("CreatedAt: 2026-11-20 06:00:00 UTC (2026-11-20 13:00:00 +07:00)", capturedAssistantRequest.Context.ShipmentSummary);
    }

    [Fact]
    public async Task QueryAssistant_ResolvesUuidShipmentAndEvaluationIdsTogether()
    {
        var shipmentUuid = Guid.CreateVersion7();
        var evaluationUuid = Guid.CreateVersion7();
        var shipmentClient = CreateClient<ShipmentWorkflowService.ShipmentWorkflowServiceClient>();
        var regulatoryClient = CreateClient<RegulatoryComplianceService.RegulatoryComplianceServiceClient>();

        shipmentClient
            .Setup(client => client.GetShipmentAsync(
                It.IsAny<GetShipmentRequest>(), It.IsAny<Metadata>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .Returns(Success(new ShipmentResponse
            {
                Id = shipmentUuid.ToString(),
                ShipmentNo = "SHP-UUID-1",
                Status = "InTransit"
            }));

        GenerateGroundedAnswerRequest? captured = null;
        regulatoryClient
            .Setup(client => client.GenerateGroundedAnswerAsync(
                It.IsAny<GenerateGroundedAnswerRequest>(), It.IsAny<Metadata>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .Callback<GenerateGroundedAnswerRequest, Metadata, DateTime?, CancellationToken>((req, _, _, _) => captured = req)
            .Returns(Success(CreateBasicAnswerResponse("ok", shipmentUuid.ToString(), evaluationUuid.ToString())));

        var controller = CreateController(regulatoryClient, shipmentClient);

        var result = await controller.QueryAssistant(
            new AssistantQueryRequest(
                Query: "Tình trạng tuân thủ",
                Mode: "ALL",
                JurisdictionCode: "VN",
                EffectiveAt: null,
                RegulationTypes: null,
                Categories: null,
                TopK: 5,
                MinimumScore: 0.6m,
                Context: new AssistantContextRequest(shipmentUuid.ToString(), evaluationUuid.ToString())),
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var payload = Assert.IsType<AssistantQueryResponse>(ok.Value);
        Assert.Equal(shipmentUuid.ToString(), payload.Context!.ShipmentId);
        Assert.Equal(evaluationUuid.ToString(), payload.Context.EvaluationId);
        Assert.NotNull(captured?.Context);
        Assert.Equal(shipmentUuid.ToString(), captured!.Context.ShipmentId);
        Assert.Equal(evaluationUuid.ToString(), captured.Context.EvaluationId);
    }

    [Fact]
    public async Task QueryAssistant_HandlesMinimalAndRichShipmentsAndClampsOutOfRangeScores()
    {
        var shipmentUuid = Guid.CreateVersion7();
        var shipmentClient = CreateClient<ShipmentWorkflowService.ShipmentWorkflowServiceClient>();
        var regulatoryClient = CreateClient<RegulatoryComplianceService.RegulatoryComplianceServiceClient>();

        var minimalShipment = new ShipmentResponse
        {
            Id = shipmentUuid.ToString(),
            ShipmentNo = "SHP-MIN",
            Status = "Draft"
        };
        var richShipment = new ShipmentResponse
        {
            Id = shipmentUuid.ToString(),
            ShipmentNo = "SHP-RICH",
            Status = "InTransit",
            CreatedAt = Timestamp.FromDateTimeOffset(DateTimeOffset.Parse("2026-11-19T10:00:00Z")),
            UpdatedAt = Timestamp.FromDateTimeOffset(DateTimeOffset.Parse("2026-11-20T01:00:00Z")),
            EstimatedPickupTime = Timestamp.FromDateTimeOffset(DateTimeOffset.Parse("2026-11-21T02:00:00Z")),
            EstimatedDeliveryTime = Timestamp.FromDateTimeOffset(DateTimeOffset.Parse("2026-11-25T08:00:00Z")),
            ActualPickupTime = Timestamp.FromDateTimeOffset(DateTimeOffset.Parse("2026-11-21T02:30:00Z")),
            ActualDeliveryTime = Timestamp.FromDateTimeOffset(DateTimeOffset.Parse("2026-11-25T07:45:00Z")),
            Notes = new string('N', 350),
            CargoItems =
            {
                new CargoItemResponse
                {
                    Name = "Lithium Battery",
                    Quantity = 2,
                    WeightKg = 15,
                    HsCode = "850760",
                    IsDangerousGoods = true
                }
            },
            Documents =
            {
                new ShipmentDocumentResponse { DocumentType = "COMMERCIAL_INVOICE", FileName = "inv.pdf" },
                new ShipmentDocumentResponse { DocumentType = string.Empty, FileName = "packing.pdf" }
            },
            Milestones =
            {
                new ShipmentMilestoneResponse
                {
                    Status = "PICKED_UP",
                    Description = "Origin warehouse",
                    RecordedAt = Timestamp.FromDateTimeOffset(DateTimeOffset.Parse("2026-11-21T02:30:00Z"))
                },
                new ShipmentMilestoneResponse
                {
                    Status = "DEPARTED",
                    Description = "Cat Lai port",
                    RecordedAt = Timestamp.FromDateTimeOffset(DateTimeOffset.Parse("2026-11-22T05:00:00Z"))
                }
            }
        };

        shipmentClient
            .SetupSequence(client => client.GetShipmentAsync(
                It.IsAny<GetShipmentRequest>(), It.IsAny<Metadata>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .Returns(Success(minimalShipment))
            .Returns(Success(richShipment));

        var capturedRequests = new List<GenerateGroundedAnswerRequest>();
        var responseWithScores = CreateBasicAnswerResponse("ok", shipmentUuid.ToString(), string.Empty);
        responseWithScores.Governance.TotalTokens = -5;
        responseWithScores.RegulatoryCitations.Add(new RegulatoryCitation
        {
            EvidenceId = "R1",
            Title = "Law",
            Score = 1.4
        });
        responseWithScores.KnowledgeReferences.Add(new KnowledgeReference
        {
            EvidenceId = "K1",
            Title = "SOP",
            Score = -0.2
        });

        regulatoryClient
            .Setup(client => client.GenerateGroundedAnswerAsync(
                It.IsAny<GenerateGroundedAnswerRequest>(), It.IsAny<Metadata>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .Callback<GenerateGroundedAnswerRequest, Metadata, DateTime?, CancellationToken>((req, _, _, _) => capturedRequests.Add(req))
            .Returns(() => Success(responseWithScores));

        var controller = CreateController(regulatoryClient, shipmentClient);

        var minimalResult = await controller.QueryAssistant(
            new AssistantQueryRequest("Check", "ALL", "VN", null, null, null, 5, 0.5m, new AssistantContextRequest("SHP-MIN", null)),
            CancellationToken.None);
        var minimalOk = Assert.IsType<OkObjectResult>(minimalResult);
        var minimalPayload = Assert.IsType<AssistantQueryResponse>(minimalOk.Value);
        Assert.Equal(1d, minimalPayload.RegulatoryCitations[0].Score);
        Assert.Equal(0d, minimalPayload.KnowledgeReferences[0].Score);
        Assert.Equal(0L, minimalPayload.Governance.TotalTokens);

        var richResult = await controller.QueryAssistant(
            new AssistantQueryRequest("Check", "ALL", "VN", null, null, null, 5, 0.5m, new AssistantContextRequest("SHP-RICH", null)),
            CancellationToken.None);
        Assert.IsType<OkObjectResult>(richResult);

        Assert.Equal(2, capturedRequests.Count);
        Assert.DoesNotContain("CreatedAt:", capturedRequests[0].Context.ShipmentSummary);
        var richSummary = capturedRequests[1].Context.ShipmentSummary;
        Assert.Contains("CargoItems (1): Lithium Battery x2, 15kg (HS: 850760) [DG]", richSummary);
        Assert.Contains("Documents (2): COMMERCIAL_INVOICE, packing.pdf", richSummary);
        Assert.Contains("LatestMilestone: DEPARTED at", richSummary);
        Assert.Contains($"Notes: {new string('N', 300)}...", richSummary);
    }

    [Fact]
    public async Task QueryAssistant_OmitsContextWhenNullOrWhitespaceAndAppliesDefaults()
    {
        var shipmentClient = CreateClient<ShipmentWorkflowService.ShipmentWorkflowServiceClient>();
        var regulatoryClient = CreateClient<RegulatoryComplianceService.RegulatoryComplianceServiceClient>();

        var captured = new List<GenerateGroundedAnswerRequest>();
        regulatoryClient
            .Setup(client => client.GenerateGroundedAnswerAsync(
                It.IsAny<GenerateGroundedAnswerRequest>(), It.IsAny<Metadata>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .Callback<GenerateGroundedAnswerRequest, Metadata, DateTime?, CancellationToken>((req, _, _, _) => captured.Add(req))
            .Returns(() => Success(CreateBasicAnswerResponse("Hello!", null, null)));

        var controller = CreateController(regulatoryClient, shipmentClient);

        var nullContextResult = await controller.QueryAssistant(
            new AssistantQueryRequest("Hello what you can do", null, null, null, null, null, 0, 0m, null),
            CancellationToken.None);
        var blankContextResult = await controller.QueryAssistant(
            new AssistantQueryRequest("Hello what you can do", null, null, null, null, null, 0, 0m, new AssistantContextRequest("   ", "  ")),
            CancellationToken.None);

        var ok1 = Assert.IsType<OkObjectResult>(nullContextResult);
        Assert.Null(Assert.IsType<AssistantQueryResponse>(ok1.Value).Context);
        Assert.IsType<OkObjectResult>(blankContextResult);

        Assert.Equal(2, captured.Count);
        Assert.All(captured, req =>
        {
            Assert.Null(req.Context);
            Assert.Equal(AssistantSearchMode.All, req.Mode);
            Assert.Equal("VN", req.JurisdictionCode);
            Assert.Equal(10, req.TopK);
            Assert.Equal(0.4d, req.MinimumRelevanceScore, 4);
        });
        shipmentClient.VerifyNoOtherCalls();
    }

    [Theory]
    [MemberData(nameof(InvalidRequestCases))]
    public async Task QueryAssistant_RejectsInvalidRequestsBeforeCallingDownstreamServices(
        AssistantQueryRequest request,
        string expectedTitle)
    {
        var shipmentClient = CreateClient<ShipmentWorkflowService.ShipmentWorkflowServiceClient>();
        var regulatoryClient = CreateClient<RegulatoryComplianceService.RegulatoryComplianceServiceClient>();
        var controller = CreateController(regulatoryClient, shipmentClient);

        var result = await controller.QueryAssistant(request, CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var problem = Assert.IsType<ProblemDetails>(badRequest.Value);
        Assert.Equal(expectedTitle, problem.Title);
        shipmentClient.VerifyNoOtherCalls();
        regulatoryClient.VerifyNoOtherCalls();
    }

    public static TheoryData<AssistantQueryRequest, string> InvalidRequestCases() => new()
    {
        {
            new AssistantQueryRequest("   ", "ALL", "VN", null, null, null, 5, 0.5m),
            "INVALID_QUERY"
        },
        {
            new AssistantQueryRequest(new string('a', 2001), "ALL", "VN", null, null, null, 5, 0.5m),
            "INVALID_QUERY"
        },
        {
            new AssistantQueryRequest("Valid query", "UNKNOWN_MODE", "VN", null, null, null, 5, 0.5m),
            "INVALID_MODE"
        },
        {
            new AssistantQueryRequest("Valid query", "ALL", "VN", null, null, null, 21, 0.5m),
            "INVALID_TOP_K"
        },
        {
            new AssistantQueryRequest("Valid query", "ALL", "VN", null, null, null, 5, 1.1m),
            "INVALID_SCORE"
        },
        {
            new AssistantQueryRequest("Valid query", "ALL", "VN", null, null, null, 5, 0.5m, new AssistantContextRequest(Guid.Empty.ToString(), null)),
            "INVALID_ASSISTANT_CONTEXT"
        },
        {
            new AssistantQueryRequest("Valid query", "ALL", "VN", null, null, null, 5, 0.5m, new AssistantContextRequest(new string('S', 129), null)),
            "INVALID_ASSISTANT_CONTEXT"
        },
        {
            new AssistantQueryRequest("Valid query", "ALL", "VN", null, null, null, 5, 0.5m, new AssistantContextRequest("SHP-20261120-01A1F75409", "not-a-uuid")),
            "INVALID_ASSISTANT_CONTEXT"
        }
    };

    [Theory]
    [InlineData(StatusCode.NotFound, 404, "SHIPMENT_NOT_FOUND")]
    [InlineData(StatusCode.InvalidArgument, 400, "INVALID_ASSISTANT_CONTEXT")]
    [InlineData(StatusCode.PermissionDenied, 403, "PERMISSION_DENIED")]
    [InlineData(StatusCode.Unavailable, 503, "SHIPMENT_SERVICE_UNAVAILABLE")]
    [InlineData(StatusCode.DeadlineExceeded, 503, "SHIPMENT_SERVICE_UNAVAILABLE")]
    [InlineData(StatusCode.Internal, 502, "SHIPMENT_SERVICE_ERROR")]
    public async Task QueryAssistant_MapsShipmentLookupRpcExceptions(
        StatusCode grpcStatus,
        int expectedHttpStatus,
        string expectedTitle)
    {
        var shipmentClient = CreateClient<ShipmentWorkflowService.ShipmentWorkflowServiceClient>();
        var regulatoryClient = CreateClient<RegulatoryComplianceService.RegulatoryComplianceServiceClient>();

        shipmentClient
            .Setup(client => client.GetShipmentAsync(
                It.IsAny<GetShipmentRequest>(), It.IsAny<Metadata>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .Returns(Failure<ShipmentResponse>(new Status(grpcStatus, "shipment lookup error")));

        var controller = CreateController(regulatoryClient, shipmentClient);

        var result = await controller.QueryAssistant(
            new AssistantQueryRequest("Check", "ALL", "VN", null, null, null, 5, 0.6m, new AssistantContextRequest("SHP-123", null)),
            CancellationToken.None);

        var objectResult = Assert.IsAssignableFrom<ObjectResult>(result);
        Assert.Equal(expectedHttpStatus, objectResult.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(objectResult.Value);
        Assert.Equal(expectedTitle, problem.Title);
        regulatoryClient.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(StatusCode.Aborted, "mismatch", 409, "ASSISTANT_CONTEXT_MISMATCH")]
    [InlineData(StatusCode.FailedPrecondition, "ASSISTANT_CONTEXT_UNAVAILABLE: missing", 412, "ASSISTANT_CONTEXT_UNAVAILABLE")]
    [InlineData(StatusCode.FailedPrecondition, "policy blocked", 412, "GOVERNANCE_BLOCKED")]
    [InlineData(StatusCode.InvalidArgument, "bad arg", 400, "INVALID_REQUEST")]
    [InlineData(StatusCode.ResourceExhausted, "quota exceeded", 429, "AI_QUOTA_EXCEEDED")]
    [InlineData(StatusCode.Unavailable, "ai down", 503, "AI_SERVICE_UNAVAILABLE")]
    public async Task QueryAssistant_MapsRegulatoryRpcExceptions(
        StatusCode grpcStatus,
        string detail,
        int expectedHttpStatus,
        string expectedTitle)
    {
        var shipmentClient = CreateClient<ShipmentWorkflowService.ShipmentWorkflowServiceClient>();
        var regulatoryClient = CreateClient<RegulatoryComplianceService.RegulatoryComplianceServiceClient>();

        regulatoryClient
            .Setup(client => client.GenerateGroundedAnswerAsync(
                It.IsAny<GenerateGroundedAnswerRequest>(), It.IsAny<Metadata>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .Returns(Failure<GenerateGroundedAnswerResponse>(new Status(grpcStatus, detail)));

        var controller = CreateController(regulatoryClient, shipmentClient);

        var result = await controller.QueryAssistant(
            new AssistantQueryRequest("Check", "ALL", "VN", null, null, null, 5, 0.6m),
            CancellationToken.None);

        var objectResult = Assert.IsAssignableFrom<ObjectResult>(result);
        Assert.Equal(expectedHttpStatus, objectResult.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(objectResult.Value);
        Assert.Equal(expectedTitle, problem.Title);
    }

    [Fact]
    public void AddBffGrpcClients_PrioritizesNegotiationAgentUrlAndFallsBackToNegotiationServiceUrl()
    {
        var servicesWithAgent = new ServiceCollection();
        var configWithAgent = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Grpc:IamTenant:Url"] = "http://iam-tenant:5000",
                ["Grpc:Notification:ServiceApiKey"] = "test-api-key",
                ["Grpc:NegotiationService:Url"] = "http://negotiation-service:5006",
                ["Grpc:NegotiationAgent:Url"] = "http://10.20.2.15:8080"
            })
            .Build();
        servicesWithAgent.AddBffGrpcClients(configWithAgent);
        using var providerWithAgent = servicesWithAgent.BuildServiceProvider();
        var optionsWithAgent = providerWithAgent
            .GetRequiredService<IOptionsMonitor<GrpcClientFactoryOptions>>()
            .Get(nameof(Negotiation.Grpc.NegotiationService.NegotiationServiceClient));
        Assert.Equal(new Uri("http://10.20.2.15:8080"), optionsWithAgent.Address);

        var servicesFallback = new ServiceCollection();
        var configFallback = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Grpc:IamTenant:Url"] = "http://iam-tenant:5000",
                ["Grpc:Notification:ServiceApiKey"] = "test-api-key",
                ["Grpc:NegotiationService:Url"] = "http://negotiation-service:5006"
            })
            .Build();
        servicesFallback.AddBffGrpcClients(configFallback);
        using var providerFallback = servicesFallback.BuildServiceProvider();
        var optionsFallback = providerFallback
            .GetRequiredService<IOptionsMonitor<GrpcClientFactoryOptions>>()
            .Get(nameof(Negotiation.Grpc.NegotiationService.NegotiationServiceClient));
        Assert.Equal(new Uri("http://negotiation-service:5006"), optionsFallback.Address);
    }

    private static AssistantController CreateController(
        Mock<RegulatoryComplianceService.RegulatoryComplianceServiceClient> regulatoryClient,
        Mock<ShipmentWorkflowService.ShipmentWorkflowServiceClient> shipmentClient) =>
        new(
            regulatoryClient.Object,
            shipmentClient.Object,
            NullLogger<AssistantController>.Instance);

    private static GenerateGroundedAnswerResponse CreateBasicAnswerResponse(
        string answer,
        string? shipmentId,
        string? evaluationId)
    {
        var response = new GenerateGroundedAnswerResponse
        {
            Query = "query",
            Answer = answer,
            InsufficientEvidence = false,
            RetrievalTraceId = Guid.CreateVersion7().ToString(),
            Governance = new AssistantGovernanceMetadata
            {
                DecisionId = "decision-1",
                AutomationLevel = "ASSISTED",
                RequiresApproval = false,
                CapabilityCode = "compliance.answer",
                TotalTokens = 42
            }
        };

        if (shipmentId is not null || evaluationId is not null)
        {
            response.Context = new RegulatoryCompliance.Grpc.AssistantContextSummary
            {
                ShipmentId = shipmentId ?? string.Empty,
                EvaluationId = evaluationId ?? string.Empty,
                Freshness = "CURRENT",
                SnapshotHash = string.Empty
            };
        }

        return response;
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

    private static AsyncUnaryCall<TResponse> Failure<TResponse>(Status status)
        where TResponse : class =>
        new(
            Task.FromException<TResponse>(new RpcException(status)),
            Task.FromResult(new Metadata()),
            () => status,
            () => new Metadata(),
            () => { });
}
