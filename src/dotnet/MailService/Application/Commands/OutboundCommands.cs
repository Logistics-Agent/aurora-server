using MediatR;
using Shared.Security;
using MailService.Application.Interfaces;
using MailService.Application.Pipeline;
using MailService.Domain.Entities;
using MailService.Domain.Enums;
using MailService.Infrastructure.Persistence.Repositories;

namespace MailService.Application.Commands.Outbound;

public record CreateDraftMessageCommand(
    Guid MailboxId,
    Guid? AssignedStaffId,
    string Subject,
    string Body,
    DraftSource Source) : IRequest<EmailDraft>;

public class CreateDraftMessageCommandHandler : IRequestHandler<CreateDraftMessageCommand, EmailDraft>
{
    private readonly IEmailDraftRepository _draftRepository;
    private readonly ICurrentUserService _currentUserService;

    public CreateDraftMessageCommandHandler(IEmailDraftRepository draftRepository, ICurrentUserService currentUserService)
    {
        _draftRepository = draftRepository;
        _currentUserService = currentUserService;
    }

    public async Task<EmailDraft> Handle(CreateDraftMessageCommand request, CancellationToken cancellationToken)
    {
        Guid draftRootId = Guid.CreateVersion7();
        var draft = new EmailDraft
        {
            Id = Guid.CreateVersion7(),
            TenantId = _currentUserService.TenantId ?? Guid.Empty,
            DraftRootId = draftRootId,
            RevisionNumber = 1,
            IsLatestRevision = true,
            Source = request.Source,
            Status = DraftStatus.Draft,
            MailboxId = request.MailboxId,
            AssignedStaffId = request.AssignedStaffId,
            Subject = request.Subject,
            Body = request.Body,
            CreatedAt = DateTimeOffset.UtcNow
        };

        return await _draftRepository.CreateNewDraftAsync(draft, cancellationToken);
    }
}

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

        // Draft Revision Pre-Check (executed entirely within transaction inside repository)
        if (request.DraftRootId.HasValue && request.DraftRootId.Value != Guid.Empty)
        {
            var latest = await _draftRepository.GetLatestRevisionAsync(request.DraftRootId.Value, cancellationToken);
            if (latest != null)
            {
                draftSource = latest.Source;
                string newContentHash = EmailDraftRepository.ComputeContentHash(request.Subject, request.BodyText);

                if (latest.ContentHash == newContentHash)
                {
                    // Match -> Update existing revision status to Sent
                    await _draftRepository.MarkAsSentAsync(request.DraftRootId.Value, cancellationToken);
                    finalDraftRevisionId = latest.Id;
                }
                else
                {
                    // Mismatch (Staff edited content) -> Create new Manual snapshot revision in a SINGLE TRANSACTION
                    var newRevision = await _draftRepository.CreateNextRevisionInTransactionAsync(
                        request.DraftRootId.Value,
                        request.Subject,
                        request.BodyText,
                        DraftSource.Manual,
                        DraftStatus.Sent,
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
            RecipientAddresses = request.RecipientAddresses,
            Subject = request.Subject,
            BodyText = request.BodyText,
            BodyHtml = request.BodyHtml,
            DraftRootId = request.DraftRootId,
            FinalDraftRevisionId = finalDraftRevisionId,
            DraftSource = draftSource
        };

        pipelineContext.Attachments.AddRange(request.Attachments);

        // Dispatch to Outbound Pipeline Runner
        return await _pipelineRunner.RunAsync(pipelineContext, cancellationToken);
    }
}
