using RegulatoryCompliance.Application.Embeddings;

namespace RegulatoryCompliance.Infrastructure.BackgroundJobs;

public sealed class ComplianceEmbeddingBackgroundService(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    RegulatoryComplianceRuntimeOptions options,
    ILogger<ComplianceEmbeddingBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        options.Validate();
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var processor = scope.ServiceProvider.GetRequiredService<IEmbeddingBatchProcessor>();
                var count = await processor.ProcessPendingAsync(stoppingToken);
                if (count > 0)
                    logger.LogInformation("Generated embeddings for {ChunkCount} regulatory chunks.", count);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Unexpected failure in the regulatory embedding worker.");
            }
            await DelayAsync(stoppingToken);
        }
    }

    private async Task DelayAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(options.EmbeddingPollingInterval, timeProvider, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }
}
