using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Shared.Security;
using MailService.Infrastructure.Persistence;
using MailService.Domain.Entities;
using MailService.Domain.Enums;

namespace MailService.Application.Commands.Drafts;

public record CreateDraftMessageCommand(
    Guid MailboxId,
    Guid? AssignedStaffId,
    string Subject,
    string Body,
    DraftSource Source,
    string? SourceType = null,
    string? SourceId = null,
    string? IdempotencyKey = null,
    List<string>? ToRecipients = null,
    Guid? ThreadId = null,
    string? ReplyToMessageId = null) : IRequest<CreateDraftResult>;

public record CreateDraftResult(
    EmailDraft Draft,
    bool IsExisting);

public class CreateDraftMessageCommandHandler : IRequestHandler<CreateDraftMessageCommand, CreateDraftResult>
{
    private readonly MailServiceDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;

    public CreateDraftMessageCommandHandler(MailServiceDbContext dbContext, ICurrentUserService currentUserService)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
    }

    public async Task<CreateDraftResult> Handle(CreateDraftMessageCommand request, CancellationToken cancellationToken)
    {
        Guid tenantId = _currentUserService.TenantId
            ?? throw new UnauthorizedAccessException("Tenant context is required to create an email draft.");

        // 1. Validate Mailbox ownership
        var mailbox = await _dbContext.Mailboxes
            .FirstOrDefaultAsync(m => m.Id == request.MailboxId, cancellationToken);
        if (mailbox == null)
        {
            throw new KeyNotFoundException($"Mailbox '{request.MailboxId}' not found for tenant '{tenantId}'.");
        }

        // 2. Idempotency Check (Duplicate click safety)
        if (!string.IsNullOrWhiteSpace(request.IdempotencyKey))
        {
            var existingDraft = await _dbContext.EmailDrafts
                .FirstOrDefaultAsync(d => d.IdempotencyKey == request.IdempotencyKey, cancellationToken);
            if (existingDraft != null)
            {
                return new CreateDraftResult(existingDraft, IsExisting: true);
            }
        }

        // 3. Thread Resolution & Validation
        EmailThread? thread = null;
        if (request.ThreadId.HasValue)
        {
            thread = await _dbContext.EmailThreads
                .FirstOrDefaultAsync(t => t.Id == request.ThreadId.Value, cancellationToken) 
                    ?? throw new KeyNotFoundException($"Thread '{request.ThreadId.Value}' not found for tenant '{tenantId}'.");
            
            if (thread.MailboxId != request.MailboxId)
            {
                throw new InvalidOperationException($"Thread '{request.ThreadId.Value}' does not belong to mailbox '{request.MailboxId}'.");
            }
        }
        else if (!string.IsNullOrWhiteSpace(request.ReplyToMessageId))
        {
            if (Guid.TryParse(request.ReplyToMessageId, out var parentMsgId))
            {
                var parentMsg = await _dbContext.ProcessedMessages
                    .FirstOrDefaultAsync(p => p.Id == parentMsgId, cancellationToken);
                if (parentMsg != null && parentMsg.ThreadId.HasValue)
                {
                    thread = await _dbContext.EmailThreads
                        .FirstOrDefaultAsync(t => t.Id == parentMsg.ThreadId.Value, cancellationToken);
                }
            }
        }

        // Create new EmailThread if none exists
        if (thread == null)
        {
            thread = new EmailThread
            {
                TenantId = tenantId,
                MailboxId = request.MailboxId,
                Subject = request.Subject,
                Participants = request.ToRecipients?.Distinct().ToList() ?? new List<string>(),
                LastMessageAt = DateTimeOffset.UtcNow,
                MessageCount = 0,
                DraftCount = 1,
                HasUnread = false,
                Snippet = request.Body.Length > 100 ? request.Body.Substring(0, 100) : request.Body,
            };
            _dbContext.EmailThreads.Add(thread);
        }
        else
        {
            thread.DraftCount += 1;
            thread.LastMessageAt = DateTimeOffset.UtcNow;
            if (request.ToRecipients != null)
            {
                foreach (var recipient in request.ToRecipients)
                {
                    if (!thread.Participants.Contains(recipient))
                    {
                        thread.Participants.Add(recipient);
                    }
                }
            }
        }

        // 4. Create EmailDraft entity
        Guid draftRootId = Guid.CreateVersion7();
        var draft = new EmailDraft
        {
            TenantId = tenantId,
            DraftRootId = draftRootId,
            RevisionNumber = 1,
            IsLatestRevision = true,
            Source = request.Source,
            Status = DraftStatus.Draft,
            MailboxId = request.MailboxId,
            AssignedStaffId = request.AssignedStaffId,
            Subject = request.Subject,
            Body = request.Body,
            ContentHash = ComputeContentHash(request.Subject, request.Body),
            ThreadId = thread.Id,
            ReplyToMessageId = request.ReplyToMessageId,
            SourceType = request.SourceType ?? "MANUAL",
            SourceId = request.SourceId,
            IdempotencyKey = request.IdempotencyKey,
            ToRecipients = request.ToRecipients ?? new List<string>(),
            CreatedAt = DateTimeOffset.UtcNow,
        };

        // 5. Audit Log
        var audit = new AuditRecord
        {
            TenantId = tenantId,
            ActorId = _currentUserService.UserId ?? Guid.Empty,
            ActorType = ActorType.Staff,
            Action = "DraftCreated",
            ResourceType = "EmailDraft",
            ResourceId = draft.Id,
            Timestamp = DateTimeOffset.UtcNow,
            Result = "Success",
            DetailJson = JsonSerializer.Serialize(new
            {
                draftId = draft.Id,
                threadId = thread.Id,
                sourceType = draft.SourceType,
                sourceId = draft.SourceId,
                idempotencyKey = draft.IdempotencyKey
            })
        };

        _dbContext.AuditRecords.Add(audit);
        _dbContext.EmailDrafts.Add(draft);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new CreateDraftResult(draft, IsExisting: false);
    }

    private static string ComputeContentHash(string subject, string body)
    {
        string normalized = $"{subject.Trim()}\n{body.Trim()}";
        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}