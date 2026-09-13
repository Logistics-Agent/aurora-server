using AiGovernance.Grpc;
using Grpc.Core;
using RegulatoryCompliance.Application.Embeddings;
using Shared.Security;

namespace RegulatoryCompliance.Infrastructure.Providers;

public sealed class AiGovernanceEmbeddingProvider(
    AiExecutionService.AiExecutionServiceClient aiExecutionClient,
    ICurrentUserService currentUser,
    string capabilityCode = "compliance.embed") : IEmbeddingProvider
{
    private const string TenantServiceId = "regulatory-compliance-rag";
    private const string PlatformServiceId = "regulatory-compliance-platform-rag";

    public EmbeddingModelDescriptor Model { get; } = new("gemini-embedding-2", "v1", 768);

    public async Task<IReadOnlyList<float[]>> GenerateAsync(
        IReadOnlyList<EmbeddingInput> inputs,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        if (inputs.Count == 0)
            return [];
        if (inputs.Count > EmbeddingBatchProcessor.MaximumBatchSize)
            throw new ArgumentOutOfRangeException(nameof(inputs));

        var results = new List<float[]>(inputs.Count);

        foreach (var input in inputs)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ArgumentNullException.ThrowIfNull(input);

            var text = input.Text;
            var estimatedTokens = Math.Max(1, text.Length / 4);

            var request = new AiEmbedRequest
            {
                CapabilityCode = capabilityCode,
                Content = text,
                Dimensions = Model.Dimension,
                EstimatedInputTokens = estimatedTokens
            };

            var headers = new Metadata
            {
                { "x-service-id", input.TenantId.HasValue ? TenantServiceId : PlatformServiceId }
            };

            if (input.TenantId.HasValue)
                headers.Add("x-tenant-id", input.TenantId.Value.ToString());

            if (currentUser.UserId.HasValue)
                headers.Add("x-user-id", currentUser.UserId.Value.ToString());

            if (!string.IsNullOrEmpty(currentUser.TraceId))
                headers.Add("x-trace-id", currentUser.TraceId);

            var response = await aiExecutionClient.EmbedAsync(
                request,
                headers,
                deadline: DateTime.UtcNow.AddSeconds(30),
                cancellationToken: cancellationToken);

            var vector = response.Vector.ToArray();

            if (vector.Length != Model.Dimension)
            {
                throw new InvalidOperationException(
                    $"Embedding response dimension mismatch. Expected {Model.Dimension}, got {vector.Length}.");
            }

            if (vector.Any(v => !float.IsFinite(v)))
            {
                throw new InvalidOperationException("Embedding response contains non-finite float values.");
            }

            results.Add(vector);
        }

        return results;
    }
}
