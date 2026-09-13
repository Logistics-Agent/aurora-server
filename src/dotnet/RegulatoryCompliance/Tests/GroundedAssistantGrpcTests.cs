using Grpc.Core;
using RegulatoryCompliance.Application.Assistant;
using RegulatoryCompliance.Application.Evaluations;
using RegulatoryCompliance.Application.Ingestion;
using RegulatoryCompliance.Application.Retrieval;
using RegulatoryCompliance.Grpc;
using RegulatoryCompliance.GrpcServices;

namespace RegulatoryCompliance.Tests;

public sealed class GroundedAssistantGrpcTests
{
    [Fact]
    public async Task GenerateGroundedAnswer_RejectsUnknownMode()
    {
        var service = CreateService();
        var request = new GenerateGroundedAnswerRequest
        {
            Query = "What documents are required?",
            Mode = (RegulatoryCompliance.Grpc.AssistantSearchMode)99,
            TopK = 10,
            MinimumRelevanceScore = 0.4
        };

        var exception = await Assert.ThrowsAsync<RpcException>(() =>
            service.GenerateGroundedAnswer(request, null!));

        Assert.Equal(StatusCode.InvalidArgument, exception.StatusCode);
    }

    [Fact]
    public async Task GenerateGroundedAnswer_RejectsInvalidRetrievalBounds()
    {
        var service = CreateService();
        var request = new GenerateGroundedAnswerRequest
        {
            Query = "What documents are required?",
            Mode = RegulatoryCompliance.Grpc.AssistantSearchMode.All,
            TopK = 21,
            MinimumRelevanceScore = 0.4
        };

        var exception = await Assert.ThrowsAsync<RpcException>(() =>
            service.GenerateGroundedAnswer(request, null!));

        Assert.Equal(StatusCode.InvalidArgument, exception.StatusCode);
    }

    [Fact]
    public async Task GenerateGroundedAnswer_ForwardsVerifiedContextIds()
    {
        var grounded = new CapturingGroundedAnswerService();
        var service = new RegulatoryComplianceGrpcService(
            null!,
            null!,
            null!,
            null!,
            grounded);
        var shipmentId = Guid.NewGuid();
        var evaluationId = Guid.NewGuid();

        var response = await service.GenerateGroundedAnswer(new GenerateGroundedAnswerRequest
        {
            Query = "Why is this shipment high risk?",
            Mode = RegulatoryCompliance.Grpc.AssistantSearchMode.All,
            TopK = 5,
            MinimumRelevanceScore = 0.6,
            Context = new AssistantContext
            {
                ShipmentId = shipmentId.ToString(),
                EvaluationId = evaluationId.ToString()
            }
        }, null!);

        Assert.Equal(shipmentId, grounded.Input!.Context!.ShipmentId);
        Assert.Equal(evaluationId, grounded.Input.Context.EvaluationId);
        Assert.Equal(shipmentId.ToString(), response.Context.ShipmentId);
        Assert.Equal(evaluationId.ToString(), response.Context.EvaluationId);
    }

    private static RegulatoryComplianceGrpcService CreateService() =>
        new(
            null!,
            null!,
            null!,
            null!,
            new ThrowingGroundedAnswerService());

    private sealed class ThrowingGroundedAnswerService : IGroundedAnswerService
    {
        public Task<GroundedAnswerResult> GenerateAnswerAsync(
            GroundedAnswerInput input,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("The test request should be rejected before execution.");
    }

    private sealed class CapturingGroundedAnswerService : IGroundedAnswerService
    {
        public GroundedAnswerInput? Input { get; private set; }

        public Task<GroundedAnswerResult> GenerateAnswerAsync(
            GroundedAnswerInput input,
            CancellationToken cancellationToken = default)
        {
            Input = input;
            return Task.FromResult(new GroundedAnswerResult(
                input.Query,
                "grounded answer",
                [],
                [],
                [],
                true,
                [],
                new AssistantGovernanceResult("decision", "DETERMINISTIC_FALLBACK", false, "compliance.answer", 0),
                Guid.NewGuid(),
                new VerifiedAssistantContextSummary(
                    input.Context!.ShipmentId!.Value,
                    input.Context.EvaluationId!.Value,
                    "CURRENT",
                    "snapshot-hash")));
        }
    }
}
