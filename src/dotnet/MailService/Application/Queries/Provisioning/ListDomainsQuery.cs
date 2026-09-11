using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MailService.Domain.Entities;
using MailService.Infrastructure.Persistence;

namespace MailService.Application.Queries.Provisioning;

public record ListDomainsQuery(int PageSize = 100) : IRequest<List<Domain.Entities.Domain>>;

public class ListDomainsQueryHandler : IRequestHandler<ListDomainsQuery, List<Domain.Entities.Domain>>
{
    private readonly MailServiceDbContext _dbContext;

    public ListDomainsQueryHandler(MailServiceDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<Domain.Entities.Domain>> Handle(ListDomainsQuery request, CancellationToken cancellationToken)
    {
        var limit = request.PageSize > 0 ? request.PageSize : 100;
        return await _dbContext.Domains
            .Include(d => d.Mailboxes)
            .OrderByDescending(d => d.CreatedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }
}
