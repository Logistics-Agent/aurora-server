using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Shared.Security;
using MailService.Application.Interfaces.Persistence;
using MailService.Domain.Entities;
using MailService.Domain.Enums;

namespace MailService.Infrastructure.Persistence.Repositories;


public class EmailDraftRepository : IEmailDraftRepository
{
    private readonly MailServiceDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;

    public EmailDraftRepository(MailServiceDbContext dbContext, ICurrentUserService currentUserService)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
    }

    public async Task<EmailDraft?> GetLatestRevisionAsync(Guid draftRootId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.EmailDrafts
            .Where(d => d.DraftRootId == draftRootId && d.IsLatestRevision)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<EmailDraft> CreateNewDraftAsync(EmailDraft draft, CancellationToken cancellationToken = default)
    {
        if (draft.TenantId == Guid.Empty && _currentUserService.TenantId.HasValue)
        {
            draft.TenantId = _currentUserService.TenantId.Value;
        }

        draft.ContentHash = ComputeContentHash(draft.Subject, draft.Body);
        draft.IsLatestRevision = true;

        _dbContext.EmailDrafts.Add(draft);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return draft;
    }

    public async Task<EmailDraft> CreateNextRevisionInTransactionAsync(
        Guid draftRootId,
        string subject,
        string body,
        DraftSource source,
        DraftStatus status,
        Guid mailboxId,
        Guid? assignedStaffId,
        CancellationToken cancellationToken = default)
    {
        // Use explicit execution strategy if retries are configured, or standard transaction
        var strategy = _dbContext.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                // Fetch all existing revisions for this draft root ID (tracking enabled for mutation)
                var existingRevisions = await _dbContext.EmailDrafts
                    .Where(d => d.DraftRootId == draftRootId)
                    .ToListAsync(cancellationToken);

                int nextRevisionNumber = 1;
                Guid? parentRevisionId = null;

                if (existingRevisions.Count > 0)
                {
                    var currentLatest = existingRevisions.FirstOrDefault(r => r.IsLatestRevision)
                                       ?? existingRevisions.OrderByDescending(r => r.RevisionNumber).First();

                    parentRevisionId = currentLatest.Id;
                    nextRevisionNumber = existingRevisions.Max(r => r.RevisionNumber) + 1;

                    // Ensure all previous revisions have IsLatestRevision = false
                    foreach (var rev in existingRevisions)
                    {
                        rev.IsLatestRevision = false;
                    }
                }

                Guid tenantId = _currentUserService.TenantId ?? Guid.Empty;
                if (tenantId == Guid.Empty && existingRevisions.Count > 0)
                {
                    tenantId = existingRevisions.First().TenantId;
                }

                var newRevision = new EmailDraft
                {
                    TenantId = tenantId,
                    DraftRootId = draftRootId,
                    ParentRevisionId = parentRevisionId,
                    RevisionNumber = nextRevisionNumber,
                    IsLatestRevision = true,
                    Source = source,
                    Status = status,
                    MailboxId = mailboxId,
                    AssignedStaffId = assignedStaffId,
                    Subject = subject,
                    Body = body,
                    ContentHash = ComputeContentHash(subject, body),
                    CreatedAt = DateTimeOffset.UtcNow
                };

                _dbContext.EmailDrafts.Add(newRevision);
                await _dbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                return newRevision;
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        });
    }

    public async Task MarkAsSentAsync(Guid draftRootId, CancellationToken cancellationToken = default)
    {
        var latest = await GetLatestRevisionAsync(draftRootId, cancellationToken);
        if (latest != null)
        {
            latest.Status = DraftStatus.Sent;
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    public static string ComputeContentHash(string subject, string body)
    {
        string raw = $"{subject?.Trim()}\n{body?.Trim()}";
        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
