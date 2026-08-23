using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Shared.Security;
using MailService.Application.Interfaces.Persistence;
using MailService.Domain.Entities;
using MailService.Domain.Enums;

namespace MailService.Application.Commands.Drafts;

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
        Guid tenantId = _currentUserService.TenantId
            ?? throw new UnauthorizedAccessException("Tenant context is required to create an email draft.");

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
            CreatedAt = DateTimeOffset.UtcNow
        };

        return await _draftRepository.CreateNewDraftAsync(draft, cancellationToken);
    }
}
