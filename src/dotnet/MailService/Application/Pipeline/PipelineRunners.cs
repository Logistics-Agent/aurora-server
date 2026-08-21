using Microsoft.Extensions.Logging;
using MailService.Domain.Entities;
using MailService.Domain.Enums;
using MailService.Infrastructure.Persistence;

namespace MailService.Application.Pipeline;

public class InboundPipelineRunner
{
    private readonly IEnumerable<IInboundPipelineStage> _stages;
    private readonly MailServiceDbContext _dbContext;
    private readonly ILogger<InboundPipelineRunner> _logger;

    public InboundPipelineRunner(
        IEnumerable<IInboundPipelineStage> stages,
        MailServiceDbContext dbContext,
        ILogger<InboundPipelineRunner> logger)
    {
        _stages = stages.OrderBy(s => (int)s.StageName);
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<InboundPipelineContext> RunAsync(InboundPipelineContext context, CancellationToken cancellationToken = default)
    {
        context.ProcessedMessage.Id = Guid.CreateVersion7();
        context.ProcessedMessage.PipelineExecutionId = context.ExecutionId.Value;
        context.ProcessedMessage.Direction = EmailDirection.Inbound;
        context.ProcessedMessage.ReceivedAt = DateTimeOffset.UtcNow;
        context.ProcessedMessage.PipelineStatus = PipelineStatus.Running;

        foreach (var stage in _stages)
        {
            try
            {
                var result = await stage.ExecuteAsync(context, cancellationToken);
                context.StageResults.Add(result);

                var checkResult = new SecurityCheckResult
                {
                    Id = Guid.CreateVersion7(),
                    TenantId = context.TenantId,
                    ProcessedMessageId = context.ProcessedMessage.Id,
                    Stage = result.Stage,
                    Result = result.Result,
                    DetailJson = result.DetailJson,
                    DurationMs = result.DurationMs,
                    CreatedAt = DateTimeOffset.UtcNow
                };

                context.ProcessedMessage.SecurityCheckResults.Add(checkResult);

                if (result.ShouldShortCircuit)
                {
                    context.IsQuarantined = true;
                    context.QuarantineReason = result.QuarantineReason ?? "Security policy short-circuit";
                    _logger.LogWarning("Inbound pipeline short-circuited at stage {Stage} for message {MessageId}. Reason: {Reason}",
                        stage.StageName, context.ProcessedMessage.MessageId, context.QuarantineReason);
                    break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in inbound pipeline stage {Stage}", stage.StageName);
                context.StageResults.Add(new StageResult
                {
                    Stage = stage.StageName,
                    Result = "Error",
                    DetailJson = $"{{\"error\":\"{ex.Message}\"}}",
                    DurationMs = 0
                });
            }
        }

        context.ProcessedMessage.ProcessedAt = DateTimeOffset.UtcNow;
        context.ProcessedMessage.PipelineStatus = context.IsQuarantined ? PipelineStatus.Quarantined : PipelineStatus.Delivered;
        context.ProcessedMessage.IsQuarantined = context.IsQuarantined;
        context.ProcessedMessage.SpamScore = context.SpamScore;
        context.ProcessedMessage.PhishingScore = context.PhishingScore;
        context.ProcessedMessage.SenderAddress = context.SenderAddress;
        context.ProcessedMessage.RecipientAddresses = context.RecipientAddresses;
        context.ProcessedMessage.Subject = context.Subject;
        context.ProcessedMessage.TenantId = context.TenantId;

        if (context.IsQuarantined)
        {
            var quarantineRecord = new QuarantineRecord
            {
                Id = Guid.CreateVersion7(),
                TenantId = context.TenantId,
                ProcessedMessageId = context.ProcessedMessage.Id,
                MessageId = context.ProcessedMessage.MessageId,
                QuarantineReason = context.QuarantineReason ?? "Security policy quarantine",
                QuarantinedAt = DateTimeOffset.UtcNow,
                Status = QuarantineStatus.Pending,
                CreatedAt = DateTimeOffset.UtcNow
            };

            _dbContext.QuarantineRecords.Add(quarantineRecord);
        }

        _dbContext.ProcessedMessages.Add(context.ProcessedMessage);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return context;
    }
}

public class OutboundPipelineRunner
{
    private readonly IEnumerable<IOutboundPipelineStage> _stages;
    private readonly MailServiceDbContext _dbContext;
    private readonly ILogger<OutboundPipelineRunner> _logger;

    public OutboundPipelineRunner(
        IEnumerable<IOutboundPipelineStage> stages,
        MailServiceDbContext dbContext,
        ILogger<OutboundPipelineRunner> logger)
    {
        _stages = stages.OrderBy(s => (int)s.StageName);
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<OutboundPipelineContext> RunAsync(OutboundPipelineContext context, CancellationToken cancellationToken = default)
    {
        context.ProcessedMessage.Id = Guid.CreateVersion7();
        context.ProcessedMessage.PipelineExecutionId = context.ExecutionId.Value;
        context.ProcessedMessage.Direction = EmailDirection.Outbound;
        context.ProcessedMessage.ReceivedAt = DateTimeOffset.UtcNow;
        context.ProcessedMessage.PipelineStatus = PipelineStatus.Running;
        context.ProcessedMessage.SenderAddress = context.SenderAddress;
        context.ProcessedMessage.RecipientAddresses = context.RecipientAddresses;
        context.ProcessedMessage.Subject = context.Subject;
        context.ProcessedMessage.DraftSource = context.DraftSource;
        context.ProcessedMessage.FinalDraftRevisionId = context.FinalDraftRevisionId;
        context.ProcessedMessage.TenantId = context.TenantId;

        foreach (var stage in _stages)
        {
            try
            {
                var result = await stage.ExecuteAsync(context, cancellationToken);
                context.StageResults.Add(result);

                var checkResult = new SecurityCheckResult
                {
                    Id = Guid.CreateVersion7(),
                    TenantId = context.TenantId,
                    ProcessedMessageId = context.ProcessedMessage.Id,
                    Stage = result.Stage,
                    Result = result.Result,
                    DetailJson = result.DetailJson,
                    DurationMs = result.DurationMs,
                    CreatedAt = DateTimeOffset.UtcNow
                };

                context.ProcessedMessage.SecurityCheckResults.Add(checkResult);

                if (result.ShouldShortCircuit)
                {
                    _logger.LogWarning("Outbound pipeline rejected at stage {Stage} for message. Reason: {Reason}",
                        stage.StageName, context.RejectionReason);
                    break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in outbound pipeline stage {Stage}", stage.StageName);
                context.IsRejected = true;
                context.RejectionReason = $"Pipeline error: {ex.Message}";
                break;
            }
        }

        context.ProcessedMessage.ProcessedAt = DateTimeOffset.UtcNow;
        context.ProcessedMessage.PipelineStatus = context.IsRejected ? PipelineStatus.Failed : PipelineStatus.Delivered;
        context.ProcessedMessage.StalwartQueueId = context.StalwartQueueId;

        _dbContext.ProcessedMessages.Add(context.ProcessedMessage);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return context;
    }
}
