using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Shared.Security;
using MailService.Application.Interfaces.Persistence;
using MailService.Application.Pipeline;
using MailService.Domain.Entities;
using MailService.Domain.Enums;
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
    Guid? DraftRootId) : IRequest<OutboundPipelineContext>;

public class SubmitOutboundMessageCommandHandler : IRequestHandler<SubmitOutboundMessageCommand, OutboundPipelineContext>
{
    private readonly IEmailDraftRepository _draftRepository;
    private readonly OutboundPipelineRunner _pipelineRunner;
    private readonly ICurrentUserService _currentUserService;

    public SubmitOutboundMessageCommandHandler(
        IEmailDraftRepository draftRepository,
        OutboundPipelineRunner pipelineRunner,
        ICurrentUserService currentUserService)
    {
        _draftRepository = draftRepository;
        _pipelineRunner = pipelineRunner;
        _currentUserService = currentUserService;
    }

    public async Task<OutboundPipelineContext> Handle(SubmitOutboundMessageCommand request, CancellationToken cancellationToken)
    {
        Guid? finalDraftRevisionId = null;
        DraftSource draftSource = DraftSource.Manual;

        // Draft Revision Pre-Check (validate immutable revision state)
        if (request.DraftRootId.HasValue && request.DraftRootId.Value != Guid.Empty)
        {
            var latest = await _draftRepository.GetLatestRevisionAsync(request.DraftRootId.Value, cancellationToken);
            if (latest != null)
            {
                draftSource = latest.Source;
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

        // Prepare Outbound Pipeline Context
        var pipelineContext = new OutboundPipelineContext
        {
            TenantId = _currentUserService.TenantId ?? Guid.Empty,
            SenderAddress = request.SenderAddress,
            Subject = request.Subject,
            BodyText = request.BodyText,
            BodyHtml = request.BodyHtml,
            DraftRootId = request.DraftRootId,
            FinalDraftRevisionId = finalDraftRevisionId,
            DraftSource = draftSource
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
