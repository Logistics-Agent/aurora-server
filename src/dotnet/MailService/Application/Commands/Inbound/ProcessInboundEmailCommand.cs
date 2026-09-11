using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MimeKit;
using MailService.Application.Pipeline;
using MailService.Domain.Entities;
using MailService.Domain.Enums;
using MailService.Infrastructure.Persistence;
using ThreadPriority = MailService.Domain.Enums.ThreadPriority;

namespace MailService.Application.Commands.Inbound;

public record ProcessInboundEmailCommand(
    byte[] RawEml,
    string? SenderAddress = null,
    string? RecipientAddress = null,
    string? Source = null
) : IRequest<ProcessInboundEmailResult>;

public record ProcessInboundEmailResult(
    string MessageId,
    string ThreadId,
    string Status,
    bool IsQuarantined,
    string Classification,
    string Subject,
    string MailboxId
);

public class ProcessInboundEmailCommandHandler : IRequestHandler<ProcessInboundEmailCommand, ProcessInboundEmailResult>
{
    private readonly MailServiceDbContext _dbContext;
    private readonly InboundPipelineRunner _pipelineRunner;
    private readonly ILogger<ProcessInboundEmailCommandHandler> _logger;

    public ProcessInboundEmailCommandHandler(
        MailServiceDbContext dbContext,
        InboundPipelineRunner pipelineRunner,
        ILogger<ProcessInboundEmailCommandHandler> logger)
    {
        _dbContext = dbContext;
        _pipelineRunner = pipelineRunner;
        _logger = logger;
    }

    public async Task<ProcessInboundEmailResult> Handle(ProcessInboundEmailCommand request, CancellationToken cancellationToken)
    {
        MimeMessage mimeMessage;
        using (var stream = new MemoryStream(request.RawEml))
        {
            mimeMessage = await MimeMessage.LoadAsync(stream, cancellationToken);
        }

        // 1. Resolve Recipient and Sender
        string recipient = !string.IsNullOrWhiteSpace(request.RecipientAddress)
            ? request.RecipientAddress.Trim().ToLowerInvariant()
            : mimeMessage.To.Mailboxes.FirstOrDefault()?.Address?.Trim().ToLowerInvariant()
              ?? mimeMessage.Cc.Mailboxes.FirstOrDefault()?.Address?.Trim().ToLowerInvariant()
              ?? string.Empty;

        string sender = !string.IsNullOrWhiteSpace(request.SenderAddress)
            ? request.SenderAddress.Trim().ToLowerInvariant()
            : mimeMessage.From.Mailboxes.FirstOrDefault()?.Address?.Trim().ToLowerInvariant()
              ?? "unknown@external.local";

        if (string.IsNullOrWhiteSpace(recipient))
        {
            throw new ArgumentException("Recipient address could not be resolved from inbound email.");
        }

        string recipientDomain = recipient.Split('@').Last().Trim().ToLowerInvariant();

        // 2. Resolve Domain & Tenant (Must IgnoreQueryFilters because webhook runs anonymously before TenantId is resolved)
        var domain = await _dbContext.Domains
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(d => d.DomainName.ToLower() == recipientDomain && d.Status == DomainStatus.Active, cancellationToken)
            ?? await _dbContext.Domains
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(d => d.DomainName.ToLower() == recipientDomain, cancellationToken);

        if (domain == null)
        {
            _logger.LogWarning("Inbound email recipient domain '{Domain}' is not registered in system.", recipientDomain);
            domain = await _dbContext.Domains
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(d => d.Status == DomainStatus.Active, cancellationToken)
                ?? throw new KeyNotFoundException($"Domain '{recipientDomain}' is not recognized for any tenant.");
        }

        Guid tenantId = domain.TenantId;

        // 3. Resolve Mailbox
        var mailbox = await _dbContext.Mailboxes
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(m => m.TenantId == tenantId && m.FullAddress.ToLower() == recipient, cancellationToken);

        if (mailbox == null)
        {
            // Check if recipient is an alias
            var alias = await _dbContext.Aliases
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(a => a.TenantId == tenantId && a.AliasAddress.ToLower() == recipient, cancellationToken);

            if (alias != null && alias.Targets.Count > 0)
            {
                var targetAddress = alias.Targets.First().ToLower();
                mailbox = await _dbContext.Mailboxes
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(m => m.TenantId == tenantId && m.FullAddress.ToLower() == targetAddress, cancellationToken);
            }

            // Fallback to any active mailbox for the domain/tenant
            mailbox ??= await _dbContext.Mailboxes
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(m => m.DomainId == domain.Id, cancellationToken)
                ?? await _dbContext.Mailboxes
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(m => m.TenantId == tenantId, cancellationToken);

            if (mailbox == null)
            {
                // Auto-provision mailbox for recipient address
                var localPart = recipient.Split('@').First();
                mailbox = new Mailbox
                {
                    TenantId = tenantId,
                    DomainId = domain.Id,
                    LocalPart = localPart,
                    FullAddress = $"{localPart}@{domain.DomainName}",
                    Status = MailboxStatus.Active,
                    CreatedAt = DateTimeOffset.UtcNow
                };
                _dbContext.Mailboxes.Add(mailbox);
                await _dbContext.SaveChangesAsync(cancellationToken);
            }
        }

        // 4. Thread Resolution (check In-Reply-To, References, or Subject)
        string subject = mimeMessage.Subject ?? "(No Subject)";
        string cleanSubject = Regex.Replace(subject, @"^(Re|Fwd|Fw):\s*", "", RegexOptions.IgnoreCase).Trim();

        string bodyText = mimeMessage.TextBody ?? string.Empty;
        string bodyHtml = mimeMessage.HtmlBody ?? string.Empty;

        if (string.IsNullOrWhiteSpace(bodyText) && !string.IsNullOrWhiteSpace(bodyHtml))
        {
            bodyText = Regex.Replace(bodyHtml, @"<style.*?</style>", string.Empty, RegexOptions.Singleline | RegexOptions.IgnoreCase);
            bodyText = Regex.Replace(bodyText, @"<script.*?</script>", string.Empty, RegexOptions.Singleline | RegexOptions.IgnoreCase);
            bodyText = Regex.Replace(bodyText, @"<.*?>", string.Empty, RegexOptions.Singleline);
            bodyText = System.Net.WebUtility.HtmlDecode(bodyText).Trim();
        }

        if (string.IsNullOrWhiteSpace(bodyText) && mimeMessage.Body != null)
        {
            if (mimeMessage.Body is TextPart textPart)
            {
                bodyText = textPart.Text;
            }
            else if (mimeMessage.Body is Multipart multipart)
            {
                var textPartFromMulti = multipart.OfType<TextPart>().FirstOrDefault();
                if (textPartFromMulti != null)
                {
                    bodyText = textPartFromMulti.Text;
                }
            }
        }

        string snippet = !string.IsNullOrWhiteSpace(bodyText)
            ? (bodyText.Length > 150 ? bodyText.Substring(0, 150).Trim() : bodyText.Trim())
            : subject;

        EmailThread? thread = null;
        if (!string.IsNullOrEmpty(mimeMessage.InReplyTo))
        {
            var parentMessage = await _dbContext.ProcessedMessages
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(m => m.TenantId == tenantId && m.MessageId == mimeMessage.InReplyTo, cancellationToken);
            if (parentMessage?.ThreadId != null)
            {
                thread = await _dbContext.EmailThreads
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(t => t.Id == parentMessage.ThreadId && t.TenantId == tenantId, cancellationToken);
            }
        }

        if (thread == null)
        {
            thread = await _dbContext.EmailThreads
                .IgnoreQueryFilters()
                .Where(t => t.TenantId == tenantId && t.MailboxId == mailbox.Id && t.Status != ThreadStatus.Resolved)
                .OrderByDescending(t => t.LastMessageAt)
                .FirstOrDefaultAsync(t => t.Subject == subject || t.Subject == cleanSubject, cancellationToken);
        }

        if (thread == null)
        {
            thread = new EmailThread
            {
                TenantId = tenantId,
                MailboxId = mailbox.Id,
                Subject = subject,
                Snippet = snippet,
                Status = ThreadStatus.Unassigned,
                Priority = ThreadPriority.Normal,
                MessageCount = 1,
                LastMessageAt = DateTimeOffset.UtcNow,
                CreatedAt = DateTimeOffset.UtcNow
            };
            if (!string.IsNullOrEmpty(sender) && !thread.Participants.Contains(sender))
            {
                thread.Participants.Add(sender);
            }
            if (!string.IsNullOrEmpty(recipient) && !thread.Participants.Contains(recipient))
            {
                thread.Participants.Add(recipient);
            }
            _dbContext.EmailThreads.Add(thread);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        else
        {
            thread.Snippet = snippet;
            thread.MessageCount++;
            thread.LastMessageAt = DateTimeOffset.UtcNow;
            if (!string.IsNullOrEmpty(sender) && !thread.Participants.Contains(sender))
            {
                thread.Participants.Add(sender);
            }
        }

        // 5. Run Inbound Pipeline
        var context = new InboundPipelineContext
        {
            TenantId = tenantId,
            RawEmlBytes = request.RawEml,
            ParsedMimeMessage = mimeMessage,
            SenderAddress = sender,
            Subject = subject
        };
        context.RecipientAddresses.Add(recipient);
        context.ProcessedMessage.MessageId = mimeMessage.MessageId ?? $"<{Guid.NewGuid():N}@aurora.inbound>";
        context.ProcessedMessage.MailboxId = mailbox.Id;
        context.ProcessedMessage.ThreadId = thread.Id;
        context.ProcessedMessage.BodyText = bodyText;
        context.ProcessedMessage.BodyHtml = bodyHtml;

        var executedContext = await _pipelineRunner.RunAsync(context, cancellationToken);

        _logger.LogInformation("Inbound email {MessageId} processed -> Thread {ThreadId} (Status: {Status}, Category: {Category})",
            executedContext.ProcessedMessage.MessageId, thread.Id, executedContext.ProcessedMessage.PipelineStatus, executedContext.ProcessedMessage.EmailCategory);

        return new ProcessInboundEmailResult(
            executedContext.ProcessedMessage.MessageId,
            thread.Id.ToString(),
            executedContext.ProcessedMessage.PipelineStatus.ToString(),
            executedContext.IsQuarantined,
            executedContext.ProcessedMessage.EmailCategory.ToString(),
            subject,
            mailbox.Id.ToString()
        );
    }
}
