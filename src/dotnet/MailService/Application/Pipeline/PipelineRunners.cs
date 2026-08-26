using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MailService.Application.Interfaces.Messaging;
using MailService.Application.Interfaces.Storage;
using MailService.Domain.Entities;
using MailService.Domain.Enums;
using MailService.Infrastructure.Persistence;
using Shared.Events;

namespace MailService.Application.Pipeline;

public class InboundPipelineRunner
{
    private readonly IEnumerable<IInboundPipelineStage> _stages;
    private readonly MailServiceDbContext _dbContext;
    private readonly IOutboxWriter _outboxWriter;
    private readonly IR2StorageClient _storageClient;
    private readonly ILogger<InboundPipelineRunner> _logger;

    public InboundPipelineRunner(
        IEnumerable<IInboundPipelineStage> stages,
        MailServiceDbContext dbContext,
        IOutboxWriter outboxWriter,
        IR2StorageClient storageClient,
        ILogger<InboundPipelineRunner> logger)
    {
        _stages = stages.OrderBy(s => (int)s.StageName);
        _dbContext = dbContext;
        _outboxWriter = outboxWriter;
        _storageClient = storageClient;
        _logger = logger;
    }

    public async Task<InboundPipelineContext> RunAsync(InboundPipelineContext context, CancellationToken cancellationToken = default)
    {
        context.ProcessedMessage.PipelineExecutionId = context.ExecutionId.Value;
        context.ProcessedMessage.Direction = EmailDirection.Inbound;
        context.ProcessedMessage.ReceivedAt = DateTimeOffset.UtcNow;
        context.ProcessedMessage.PipelineStatus = PipelineStatus.Running;

        // Durable EML storage before security / quarantine execution
        if (context.RawEmlBytes != null && context.RawEmlBytes.Length > 0)
        {
            string storageKey = await _storageClient.UploadRawEmlAsync(
                context.TenantId,
                context.ProcessedMessage.MessageId,
                EmailDirection.Inbound,
                context.RawEmlBytes,
                cancellationToken);
            context.ProcessedMessage.R2RawEmlPath = storageKey;
        }


        foreach (var stage in _stages)
        {
            try
            {
                var result = await stage.ExecuteAsync(context, cancellationToken);
                context.StageResults.Add(result);

                var checkResult = new SecurityCheckResult
                {
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
                TenantId = context.TenantId,
                ProcessedMessageId = context.ProcessedMessage.Id,
                MessageId = context.ProcessedMessage.MessageId,
                QuarantineReason = context.QuarantineReason ?? "Security policy quarantine",
                QuarantinedAt = DateTimeOffset.UtcNow,
                Status = QuarantineStatus.Pending,
                CreatedAt = DateTimeOffset.UtcNow
            };

            _dbContext.QuarantineRecords.Add(quarantineRecord);

            // Write Outbox Event for Quarantined Email
            await _outboxWriter.WriteAsync(new InboundEmailQuarantinedEvent
            {
                TenantId = context.TenantId,
                MessageId = context.ProcessedMessage.Id,
                SenderEmail = context.SenderAddress,
                Subject = context.Subject,
                Reason = context.QuarantineReason ?? "Security policy quarantine",
                ThreatLevel = "High",
                QuarantinedAt = DateTime.UtcNow
            }, cancellationToken);
        }
        else
        {
            // Write Outbox Event for Received Email
            await _outboxWriter.WriteAsync(new InboundEmailReceivedEvent
            {
                TenantId = context.TenantId,
                MessageId = context.ProcessedMessage.Id,
                SenderEmail = context.SenderAddress,
                RecipientEmails = context.RecipientAddresses,
                Subject = context.Subject,
                Classification = context.ProcessedMessage.EmailCategory.ToString(),
                ReceivedAt = DateTime.UtcNow
            }, cancellationToken);
        }

        _dbContext.ProcessedMessages.Add(context.ProcessedMessage);

        // Atomic commit: ProcessedMessage + SecurityCheckResults + QuarantineRecord + OutboxMessage
        await _dbContext.SaveChangesAsync(cancellationToken);

        return context;
    }
}

public class OutboundPipelineRunner
{
    private readonly IEnumerable<IOutboundPipelineStage> _stages;
    private readonly MailServiceDbContext _dbContext;
    private readonly IOutboxWriter _outboxWriter;
    private readonly ILogger<OutboundPipelineRunner> _logger;

    public OutboundPipelineRunner(
        IEnumerable<IOutboundPipelineStage> stages,
        MailServiceDbContext dbContext,
        IOutboxWriter outboxWriter,
        ILogger<OutboundPipelineRunner> logger)
    {
        _stages = stages.OrderBy(s => (int)s.StageName);
        _dbContext = dbContext;
        _outboxWriter = outboxWriter;
        _logger = logger;
    }

    public async Task<OutboundPipelineContext> RunAsync(OutboundPipelineContext context, CancellationToken cancellationToken = default)
    {
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
        context.ProcessedMessage.SentByUserId = context.SentByUserId;
        context.ProcessedMessage.ThreadId = context.ThreadId;
        context.ProcessedMessage.InReplyTo = context.ReplyToMessageId;
        context.ProcessedMessage.BodyText = context.BodyText;
        context.ProcessedMessage.BodyHtml = context.BodyHtml;

        foreach (var stage in _stages)
        {
            try
            {
                var result = await stage.ExecuteAsync(context, cancellationToken);
                context.StageResults.Add(result);

                var checkResult = new SecurityCheckResult
                {
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
                    context.IsRejected = true;
                    if (string.IsNullOrEmpty(context.RejectionReason))
                    {
                        context.RejectionReason = $"Rejected at stage {stage.StageName}";
                    }
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

        if (context.IsRejected)
        {
            // Write Outbox Event for Outbound Email Rejected
            await _outboxWriter.WriteAsync(new OutboundEmailRejectedEvent
            {
                TenantId = context.TenantId,
                MessageId = context.ProcessedMessage.Id,
                SenderEmail = context.SenderAddress,
                RecipientEmails = context.RecipientAddresses,
                Subject = context.Subject,
                Reason = context.RejectionReason ?? "Policy rejection",
                RejectedAt = DateTime.UtcNow
            }, cancellationToken);
        }

        else
        {
            // Write Outbox Event for Outbound Email Sent
            await _outboxWriter.WriteAsync(new OutboundEmailSentEvent
            {
                TenantId = context.TenantId,
                MessageId = context.ProcessedMessage.Id,
                SenderEmail = context.SenderAddress,
                RecipientEmails = context.RecipientAddresses,
                Subject = context.Subject,
                StalwartQueueId = context.StalwartQueueId ?? string.Empty,
                SentAt = DateTime.UtcNow
            }, cancellationToken);
        }

        _dbContext.ProcessedMessages.Add(context.ProcessedMessage);

        // Atomic commit: ProcessedMessage + SecurityCheckResults + OutboxMessage
        await _dbContext.SaveChangesAsync(cancellationToken);

        return context;
    }
}
