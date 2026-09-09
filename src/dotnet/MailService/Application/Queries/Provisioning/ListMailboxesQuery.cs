using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MailService.Domain.Entities;
using MailService.Infrastructure.Persistence;

namespace MailService.Application.Queries.Provisioning;

public record ListMailboxesQuery(Guid? DomainId = null, int PageSize = 100) : IRequest<List<Mailbox>>;

public class ListMailboxesQueryHandler : IRequestHandler<ListMailboxesQuery, List<Mailbox>>
{
    private readonly MailServiceDbContext _dbContext;

    public ListMailboxesQueryHandler(MailServiceDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<Mailbox>> Handle(ListMailboxesQuery request, CancellationToken cancellationToken)
    {
        var limit = request.PageSize > 0 ? request.PageSize : 100;
        var query = _dbContext.Mailboxes
            .Include(m => m.Domain)
            .AsQueryable();

        if (request.DomainId.HasValue && request.DomainId.Value != Guid.Empty)
        {
            query = query.Where(m => m.DomainId == request.DomainId.Value);
        }

        return await query
            .OrderByDescending(m => m.CreatedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }
}
