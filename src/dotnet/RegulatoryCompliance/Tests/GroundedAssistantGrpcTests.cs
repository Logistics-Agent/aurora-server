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
}
