using MediatR;
using Microsoft.EntityFrameworkCore;
using MailService.Domain.Entities;
using MailService.Infrastructure.Persistence;

namespace MailService.Application.Queries.Drafts;

public record ListDraftsQuery(Guid? MailboxId, string Status, int PageSize) : IRequest<List<EmailDraft>>;

public class ListDraftsQueryHandler : IRequestHandler<ListDraftsQuery, List<EmailDraft>>
{
    private readonly MailServiceDbContext _dbContext;

    public ListDraftsQueryHandler(MailServiceDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<EmailDraft>> Handle(ListDraftsQuery request, CancellationToken cancellationToken)
    {
        var query = _dbContext.EmailDrafts.AsNoTracking().Where(d => d.IsLatestRevision);

        if (request.MailboxId.HasValue && request.MailboxId != Guid.Empty)
        {
            query = query.Where(d => d.MailboxId == request.MailboxId.Value);
        }

        return await query.OrderByDescending(d => d.CreatedAt)
            .Take(Math.Min(request.PageSize > 0 ? request.PageSize : 20, 100))
            .ToListAsync(cancellationToken);
    }
}
