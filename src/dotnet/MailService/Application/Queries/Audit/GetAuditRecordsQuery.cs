using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Shared.Constants;
using Shared.Security;
using MailService.Domain.Entities;
using MailService.Infrastructure.Persistence;

namespace MailService.Application.Queries.Audit;

public record GetAuditRecordsQuery(Guid? ResourceId, int PageSize) : IRequest<List<AuditRecord>>;

public class GetAuditRecordsQueryHandler : IRequestHandler<GetAuditRecordsQuery, List<AuditRecord>>
{
    private readonly MailServiceDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;

    public GetAuditRecordsQueryHandler(MailServiceDbContext dbContext, ICurrentUserService currentUserService)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
    }

    public async Task<List<AuditRecord>> Handle(GetAuditRecordsQuery request, CancellationToken cancellationToken)
    {
        IQueryable<AuditRecord> query;

        if (_currentUserService.IsSystemAdmin())
        {
            // Explicit cross-tenant query for verified System Admin only
            query = _dbContext.AuditRecords.IgnoreQueryFilters().AsNoTracking();
        }
        else
        {
            // Tenant-scoped query — requires valid TenantId
            if (!_currentUserService.TenantId.HasValue || _currentUserService.TenantId.Value == Guid.Empty)
            {
                throw new UnauthorizedAccessException("Tenant context is required to query audit records.");
            }

            query = _dbContext.AuditRecords.AsNoTracking();
        }

        if (request.ResourceId.HasValue && request.ResourceId.Value != Guid.Empty)
        {
            query = query.Where(a => a.ResourceId == request.ResourceId.Value);
        }

        return await query.OrderByDescending(a => a.Timestamp)
            .Take(Math.Min(request.PageSize > 0 ? request.PageSize : 20, 100))
            .ToListAsync(cancellationToken);
    }
}
