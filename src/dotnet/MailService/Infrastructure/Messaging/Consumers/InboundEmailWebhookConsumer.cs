using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MailService.Application.Interfaces.Stalwart;
using MailService.Application.Pipeline;
using MailService.Domain.Entities;
using MailService.Domain.Enums;
using MailService.Infrastructure.Persistence;
using Shared.Events;

namespace MailService.Infrastructure.Messaging.Consumers;

public class InboundEmailWebhookConsumer : IConsumer<InboundEmailWebhookReceivedEvent>
{
    private readonly MailServiceDbContext _dbContext;
    private readonly InboundWebhookEventRepository _eventRepository;
    private readonly IMailboxResolver _mailboxResolver;
    private readonly IStalwartJmapClient _jmapClient;
    private readonly InboundPipelineRunner _pipelineRunner;
    private readonly ILogger<InboundEmailWebhookConsumer> _logger;

    public InboundEmailWebhookConsumer(
        MailServiceDbContext dbContext,
        InboundWebhookEventRepository eventRepository,
        IMailboxResolver mailboxResolver,
        IStalwartJmapClient jmapClient,
        InboundPipelineRunner pipelineRunner,
        ILogger<InboundEmailWebhookConsumer> logger)
    {
        _dbContext = dbContext;
        _eventRepository = eventRepository;
        _mailboxResolver = mailboxResolver;
        _jmapClient = jmapClient;
        _pipelineRunner = pipelineRunner;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<InboundEmailWebhookReceivedEvent> context)
    {
        var msg = context.Message;

        // 1. Atomic Idempotent Event Lock (PostgreSQL Race Condition Safe)
        var (evt, shouldProcess) = await _eventRepository.AcquireEventLockAsync(
            msg.StalwartEventId, 
            msg.EventType, 
            msg.RawPayloadJson, 
            context.CancellationToken);

        if (!shouldProcess)
        {
            _logger.LogInformation("Stalwart event {StalwartEventId} already completed or currently processing. Skipping duplicate execution.", msg.StalwartEventId);
            return;
        }

        try
        {
            // 2. Resolve Target Mailbox (Strict Priority: StalwartAccountId -> Recipient Address)
            MailboxResolutionResult mailbox;
            if (!string.IsNullOrEmpty(msg.AccountId))
            {
                mailbox = await _mailboxResolver.ResolveMailboxByStalwartAccountIdAsync(msg.AccountId, context.CancellationToken);
                if (!mailbox.IsResolved && !string.IsNullOrEmpty(msg.To ?? msg.AccountName))
                {
                    mailbox = await _mailboxResolver.ResolveMailboxByAddressAsync(msg.To ?? msg.AccountName!, context.CancellationToken);
                }
            }
            else
            {
                var targetAddress = msg.To ?? msg.AccountName ?? string.Empty;
                mailbox = await _mailboxResolver.ResolveMailboxByAddressAsync(targetAddress, context.CancellationToken);
            }

            if (!mailbox.IsResolved)
            {
                _logger.LogWarning("Target mailbox for Stalwart event {StalwartEventId} (Account: {Account}, To: {To}) could not be resolved. Marking completed to prevent infinite retry.", 
                    msg.StalwartEventId, msg.AccountName, msg.To);

                await _eventRepository.MarkCompletedAsync(msg.StalwartEventId, context.CancellationToken);
                return;
            }

            // 3. Fetch Full Email Data from Stalwart JMAP
            var emailDto = await _jmapClient.FetchEmailByWebhookDataAsync(mailbox, msg, context.CancellationToken);

            // 4. Resolve or Create Thread
            string subject = emailDto.Subject ?? "(No Subject)";
            string cleanSubject = Regex.Replace(subject, @"^(Re|Fwd|Fw):\s*", "", RegexOptions.IgnoreCase).Trim();
            string snippet = (emailDto.BodyText ?? string.Empty).Length > 200 
                ? (emailDto.BodyText ?? string.Empty)[..200] 
                : (emailDto.BodyText ?? string.Empty);

            var thread = await _dbContext.EmailThreads
                .IgnoreQueryFilters()
                .Where(t => t.TenantId == mailbox.TenantId && t.MailboxId == mailbox.MailboxId && t.Status != ThreadStatus.Resolved)
                .OrderByDescending(t => t.LastMessageAt)
                .FirstOrDefaultAsync(t => t.Subject == subject || t.Subject == cleanSubject, context.CancellationToken);

            if (thread == null)
            {
                thread = new EmailThread
                {
                    TenantId = mailbox.TenantId,
                    MailboxId = mailbox.MailboxId,
                    Subject = subject,
                    Snippet = snippet,
                    Status = ThreadStatus.Unassigned,
                    Priority = Domain.Enums.ThreadPriority.Normal,
                    MessageCount = 1,
                    LastMessageAt = DateTimeOffset.UtcNow,
                    CreatedAt = DateTimeOffset.UtcNow
                };
                if (!string.IsNullOrEmpty(emailDto.From) && !thread.Participants.Contains(emailDto.From))
                {
                    thread.Participants.Add(emailDto.From);
                }
                if (!string.IsNullOrEmpty(mailbox.FullAddress) && !thread.Participants.Contains(mailbox.FullAddress))
                {
                    thread.Participants.Add(mailbox.FullAddress);
                }
                _dbContext.EmailThreads.Add(thread);
                await _dbContext.SaveChangesAsync(context.CancellationToken);
            }
            else
            {
                thread.Snippet = snippet;
                thread.MessageCount++;
                thread.LastMessageAt = DateTimeOffset.UtcNow;
                if (!string.IsNullOrEmpty(emailDto.From) && !thread.Participants.Contains(emailDto.From))
                {
                    thread.Participants.Add(emailDto.From);
                }
                await _dbContext.SaveChangesAsync(context.CancellationToken);
            }

            // 5. Run Inbound Pipeline (ClamAV, AI Phishing, Classification, R2, Database Persistence)
            var pipelineContext = new InboundPipelineContext
            {
                TenantId = mailbox.TenantId,
                SenderAddress = emailDto.From,
                Subject = subject
            };
            if (emailDto.To != null)
            {
                foreach (var to in emailDto.To)
                {
                    pipelineContext.RecipientAddresses.Add(to);
                }
            }

            pipelineContext.ProcessedMessage.TenantId = mailbox.TenantId;
            pipelineContext.ProcessedMessage.MailboxId = mailbox.MailboxId;
            pipelineContext.ProcessedMessage.ThreadId = thread.Id;
            pipelineContext.ProcessedMessage.SourceEventId = msg.StalwartEventId;
            pipelineContext.ProcessedMessage.MessageId = emailDto.Id;
            pipelineContext.ProcessedMessage.SenderAddress = emailDto.From;
            pipelineContext.ProcessedMessage.RecipientAddresses = emailDto.To ?? new();
            pipelineContext.ProcessedMessage.Subject = subject;
            pipelineContext.ProcessedMessage.BodyText = emailDto.BodyText;
            pipelineContext.ProcessedMessage.BodyHtml = emailDto.BodyHtml;
            pipelineContext.ProcessedMessage.ReceivedAt = emailDto.ReceivedAt;

            await _pipelineRunner.RunAsync(pipelineContext, context.CancellationToken);

            // 6. Mark Event Completed Only After Entire Pipeline Has Succeeded
            await _eventRepository.MarkCompletedAsync(msg.StalwartEventId, context.CancellationToken);
            _logger.LogInformation("Successfully processed and recorded inbound email for Stalwart event {StalwartEventId} (Mailbox: {Mailbox}, Thread: {ThreadId})", 
                msg.StalwartEventId, mailbox.FullAddress, thread.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failure in InboundEmailWebhookConsumer for event {StalwartEventId}", msg.StalwartEventId);
            await _eventRepository.MarkFailedAsync(msg.StalwartEventId, ex.Message, context.CancellationToken);
            throw; // Re-throw to trigger MassTransit retry / DLQ
        }
    }
}
