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

public record ListAliasesQuery(Guid? DomainId = null, int PageSize = 100) : IRequest<List<Alias>>;

public class ListAliasesQueryHandler : IRequestHandler<ListAliasesQuery, List<Alias>>
{
    private readonly MailServiceDbContext _dbContext;

    public ListAliasesQueryHandler(MailServiceDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<Alias>> Handle(ListAliasesQuery request, CancellationToken cancellationToken)
    {
        var limit = request.PageSize > 0 ? request.PageSize : 100;
        var query = _dbContext.Aliases
            .Include(a => a.Domain)
            .AsQueryable();

        if (request.DomainId.HasValue && request.DomainId.Value != Guid.Empty)
        {
            query = query.Where(a => a.DomainId == request.DomainId.Value);
        }

        return await query
            .OrderByDescending(a => a.CreatedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }
}
