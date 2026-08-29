using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Shared.Security;
using MailService.Infrastructure.Persistence;
using MailService.Domain.Entities;

namespace MailService.Application.Queries.Threads;

public record GetThreadAssignmentHistoryQuery(Guid ThreadId) : IRequest<List<ThreadAssignmentHistory>>;

public class GetThreadAssignmentHistoryQueryHandler : IRequestHandler<GetThreadAssignmentHistoryQuery, List<ThreadAssignmentHistory>>
{
    private readonly MailServiceDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;

    public GetThreadAssignmentHistoryQueryHandler(MailServiceDbContext dbContext, ICurrentUserService currentUserService)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
    }

    public async Task<List<ThreadAssignmentHistory>> Handle(GetThreadAssignmentHistoryQuery request, CancellationToken cancellationToken)
    {
        Guid tenantId = _currentUserService.TenantId
            ?? throw new UnauthorizedAccessException("Tenant context is required to query thread assignment history.");

        return await _dbContext.ThreadAssignmentHistories
            .AsNoTracking()
            .Where(h => h.ThreadId == request.ThreadId && h.TenantId == tenantId)
            .OrderByDescending(h => h.CreatedAt)
            .ToListAsync(cancellationToken);
    }
}
