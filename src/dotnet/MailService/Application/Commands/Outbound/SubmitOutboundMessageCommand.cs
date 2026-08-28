using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Shared.Constants;
using Shared.Security;
using MailService.Application.Interfaces.Persistence;
using MailService.Application.Pipeline;
using MailService.Domain.Entities;
using MailService.Domain.Enums;
using MailService.Infrastructure.Persistence;
using MailService.Infrastructure.Persistence.Repositories;

namespace MailService.Application.Commands.Outbound;

public record SubmitOutboundMessageCommand(
    string SenderAddress,
    List<string> RecipientAddresses,
    string Subject,
    string BodyText,
    string BodyHtml,
    List<(string Filename, string ContentType, byte[] Content)> Attachments,
    string IdempotencyKey,
    Guid? DraftRootId,
    Guid? ThreadId = null,
    string? ReplyToMessageId = null) : IRequest<OutboundPipelineContext>;

public class SubmitOutboundMessageCommandHandler : IRequestHandler<SubmitOutboundMessageCommand, OutboundPipelineContext>
{
    private readonly IEmailDraftRepository _draftRepository;
    private readonly OutboundPipelineRunner _pipelineRunner;
    private readonly ICurrentUserService _currentUserService;
    private readonly MailServiceDbContext _dbContext;

    public SubmitOutboundMessageCommandHandler(
        IEmailDraftRepository draftRepository,
        OutboundPipelineRunner pipelineRunner,
        ICurrentUserService currentUserService,
        MailServiceDbContext dbContext)
    {
        _draftRepository = draftRepository;
        _pipelineRunner = pipelineRunner;
        _currentUserService = currentUserService;
        _dbContext = dbContext;
    }

    public async Task<OutboundPipelineContext> Handle(SubmitOutboundMessageCommand request, CancellationToken cancellationToken)
    {
        Guid tenantId = _currentUserService.TenantId ?? Guid.Empty;
        Guid? currentUserId = _currentUserService.UserId;
        Guid? finalDraftRevisionId = null;
        DraftSource draftSource = DraftSource.Manual;
        Guid? resolvedThreadId = request.ThreadId;
        string? resolvedReplyToMessageId = request.ReplyToMessageId;

        // Draft Revision Pre-Check (validate immutable revision state)
        if (request.DraftRootId.HasValue && request.DraftRootId.Value != Guid.Empty)
        {
            var latest = await _draftRepository.GetLatestRevisionAsync(request.DraftRootId.Value, cancellationToken);
            if (latest != null)
            {
                draftSource = latest.Source;
                if (!resolvedThreadId.HasValue && latest.ThreadId.HasValue)
                {
                    resolvedThreadId = latest.ThreadId;
                }
                if (string.IsNullOrEmpty(resolvedReplyToMessageId) && !string.IsNullOrEmpty(latest.ReplyToMessageId))
                {
                    resolvedReplyToMessageId = latest.ReplyToMessageId;
                }

                string newContentHash = EmailDraftRepository.ComputeContentHash(request.Subject, request.BodyText);

                if (latest.ContentHash == newContentHash)
                {
                    // Existing revision content matches exactly
                    finalDraftRevisionId = latest.Id;
                }
                else
                {
                    // Content differs -> Create new revision snapshot in Draft status
                    var newRevision = await _draftRepository.CreateNextRevisionInTransactionAsync(
                        request.DraftRootId.Value,
                        request.Subject,
                        request.BodyText,
                        DraftSource.Manual,
                        DraftStatus.Draft, // Keep as Draft until SMTP succeeds
                        latest.MailboxId,
                        latest.AssignedStaffId,
                        cancellationToken);

                    finalDraftRevisionId = newRevision.Id;
                    draftSource = DraftSource.Manual;
                }
            }
        }

        // Reply-to-Claim / Assignment validation
        if (resolvedThreadId.HasValue && resolvedThreadId.Value != Guid.Empty)
        {
            var thread = await _dbContext.EmailThreads
                .FirstOrDefaultAsync(t => t.Id == resolvedThreadId.Value && t.TenantId == tenantId, cancellationToken);

            if (thread != null)
            {
                if (!thread.PrimaryAssigneeUserId.HasValue)
                {
                    // Implicitly claim unassigned thread on reply
                    if (currentUserId.HasValue)
                    {
                        thread.PrimaryAssigneeUserId = currentUserId.Value;
                        thread.AssignedAt = DateTimeOffset.UtcNow;
                        thread.Status = ThreadStatus.InProgress;
                        thread.Version++;

                        var history = new ThreadAssignmentHistory
                        {
                            ThreadId = thread.Id,
                            TenantId = tenantId,
                            FromUserId = null,
                            ToUserId = currentUserId.Value,
                            Action = ThreadAssignmentAction.Claimed,
                            ActorUserId = currentUserId.Value,
                            Reason = "Implicit claim on reply to unassigned thread"
                        };
                        _dbContext.ThreadAssignmentHistories.Add(history);

                        // Commit assignment immediately so it remains even if SMTP delivery fails
                        await _dbContext.SaveChangesAsync(cancellationToken);
                    }
                }
                else if (currentUserId.HasValue && thread.PrimaryAssigneeUserId.Value != currentUserId.Value)
                {
                    bool hasSupervisoryAccess = _currentUserService.HasPermission(PermissionConstants.Mail.ThreadReassign)
                        || _currentUserService.HasPermission("mail:assign");

                    if (!hasSupervisoryAccess)
                    {
                        throw new InvalidOperationException("THREAD_ASSIGNED_TO_ANOTHER_STAFF");
                    }
                }
            }
        }

        // Prepare Outbound Pipeline Context
        var pipelineContext = new OutboundPipelineContext
        {
            TenantId = tenantId,
            SenderAddress = request.SenderAddress,
            Subject = request.Subject,
            BodyText = request.BodyText,
            BodyHtml = request.BodyHtml,
            DraftRootId = request.DraftRootId,
            FinalDraftRevisionId = finalDraftRevisionId,
            DraftSource = draftSource,
            SentByUserId = currentUserId,
            ThreadId = resolvedThreadId,
            ReplyToMessageId = resolvedReplyToMessageId
        };

        pipelineContext.RecipientAddresses.AddRange(request.RecipientAddresses);
        pipelineContext.Attachments.AddRange(request.Attachments);

        // Dispatch to Outbound Pipeline Runner (Policy -> ClamAV -> AI BEC -> Rate Limit -> SMTP Delivery)
        var resultContext = await _pipelineRunner.RunAsync(pipelineContext, cancellationToken);

        // Draft becomes Sent ONLY after SMTP 2xx acceptance
        if (!resultContext.IsRejected && request.DraftRootId.HasValue && request.DraftRootId.Value != Guid.Empty)
        {
            await _draftRepository.MarkAsSentAsync(request.DraftRootId.Value, cancellationToken);
        }

        return resultContext;
    }
}

