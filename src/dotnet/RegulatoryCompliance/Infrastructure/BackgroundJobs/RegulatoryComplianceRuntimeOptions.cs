namespace RegulatoryCompliance.Infrastructure.BackgroundJobs;

public sealed class RegulatoryComplianceRuntimeOptions
{
    public const string SectionName = "RegulatoryCompliance";

    public string EmbeddingProvider { get; set; } = "Deterministic";
    public string EmbeddingModel { get; set; } = "deterministic-local";
    public string EmbeddingModelVersion { get; set; } = "1";
    public int EmbeddingDimension { get; set; } = 64;
    public int EmbeddingBatchSize { get; set; } = 64;
    public TimeSpan EmbeddingPollingInterval { get; set; } = TimeSpan.FromSeconds(2);
    public TimeSpan ProviderTimeout { get; set; } = TimeSpan.FromSeconds(30);
    public int RetrievalMaximumTopK { get; set; } = 20;
    public decimal RetrievalMinimumScore { get; set; } = 0.2m;
    public int OutboxBatchSize { get; set; } = 50;
    public int OutboxMaxRetries { get; set; } = 5;
    public TimeSpan OutboxPollingInterval { get; set; } = TimeSpan.FromSeconds(2);

    public void Validate()
    {
        if (!EmbeddingProvider.Equals("Deterministic", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Only the deterministic local embedding provider is registered.");
        if (EmbeddingModel != "deterministic-local" || EmbeddingModelVersion != "1" ||
            EmbeddingDimension != 64)
            throw new InvalidOperationException("Configured embedding identity must match deterministic-local:1/64.");
        if (EmbeddingBatchSize is < 1 or > 64)
            throw new InvalidOperationException("EmbeddingBatchSize must be between 1 and 64.");
        if (EmbeddingPollingInterval <= TimeSpan.Zero || ProviderTimeout <= TimeSpan.Zero)
            throw new InvalidOperationException("Embedding polling interval and provider timeout must be positive.");
        if (RetrievalMaximumTopK is < 1 or > 20 ||
            RetrievalMinimumScore is < 0m or > 1m)
            throw new InvalidOperationException("Retrieval bounds are invalid.");
        if (OutboxBatchSize is < 1 or > 1_000 ||
            OutboxMaxRetries is < 1 or > 100 ||
            OutboxPollingInterval <= TimeSpan.Zero)
            throw new InvalidOperationException("Outbox settings are invalid.");
    }
}
