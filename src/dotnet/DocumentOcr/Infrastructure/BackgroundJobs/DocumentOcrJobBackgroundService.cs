using DocumentOcr.Application.Jobs;

namespace DocumentOcr.Infrastructure.BackgroundJobs;

public sealed class DocumentOcrJobBackgroundService(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    DocumentOcrWorkerOptions options,
    ILogger<DocumentOcrJobBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        options.Validate();
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                IReadOnlyList<ClaimedDocumentOcrJob> claimed;
                await using (var scope = scopeFactory.CreateAsyncScope())
                {
                    var store = scope.ServiceProvider.GetRequiredService<IDocumentOcrJobBatchStore>();
                    var recovered = await store.RecoverExpiredAsync(stoppingToken);
                    claimed = await store.ClaimPendingAsync(stoppingToken);
                    if (recovered > 0 || claimed.Count > 0)
                    {
                        logger.LogInformation(
                            "Recovered {RecoveredCount} OCR jobs and claimed {ClaimedCount} jobs.",
                            recovered,
                            claimed.Count);
                    }
                }

                await Task.WhenAll(claimed.Select(job => ProcessWithHeartbeatAsync(job, stoppingToken)));
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Unexpected failure in the Document OCR job worker.");
            }

            try
            {
                await Task.Delay(options.PollingInterval, timeProvider, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    private async Task ProcessWithHeartbeatAsync(
        ClaimedDocumentOcrJob claimed,
        CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Processing OCR job {JobId}, attempt {AttemptId}, with provider {ProviderName}.",
            claimed.JobId,
            claimed.AttemptId,
            claimed.ProviderName);
        await using var processingScope = scopeFactory.CreateAsyncScope();
        var processor = processingScope.ServiceProvider.GetRequiredService<IDocumentOcrJobProcessor>();
        var processing = processor.ProcessClaimedAsync(
            claimed.TenantId,
            claimed.JobId,
            claimed.AttemptId,
            cancellationToken);

        while (!processing.IsCompleted)
        {
            var heartbeatDelay = Task.Delay(options.HeartbeatInterval, timeProvider, cancellationToken);
            if (await Task.WhenAny(processing, heartbeatDelay) == processing)
                break;

            await using var heartbeatScope = scopeFactory.CreateAsyncScope();
            var store = heartbeatScope.ServiceProvider.GetRequiredService<IDocumentOcrJobBatchStore>();
            if (!await store.RenewLeaseAsync(claimed.TenantId, claimed.JobId, cancellationToken))
                break;
        }

        var result = await processing;
        logger.LogInformation(
            "OCR job {JobId}, attempt {AttemptId}, finished with status {Status}.",
            claimed.JobId,
            claimed.AttemptId,
            result?.Status.ToString() ?? "NotFoundOrNoLongerClaimed");
    }
}
