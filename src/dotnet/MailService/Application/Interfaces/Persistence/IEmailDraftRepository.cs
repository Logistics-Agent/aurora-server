using MailService.Domain.Entities;
using MailService.Domain.Enums;

namespace MailService.Application.Interfaces.Persistence;

public interface IEmailDraftRepository
{
    Task<EmailDraft?> GetLatestRevisionAsync(Guid draftRootId, CancellationToken cancellationToken = default);
    Task<EmailDraft> CreateNewDraftAsync(EmailDraft draft, CancellationToken cancellationToken = default);
    Task<EmailDraft> CreateNextRevisionInTransactionAsync(Guid draftRootId, string subject, string body, DraftSource source, DraftStatus status, Guid mailboxId, Guid? assignedStaffId, CancellationToken cancellationToken = default);
    Task MarkAsSentAsync(Guid draftRootId, CancellationToken cancellationToken = default);
}
