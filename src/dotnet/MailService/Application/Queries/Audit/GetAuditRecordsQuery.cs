using MediatR;
using Microsoft.EntityFrameworkCore;
using MailService.Domain.Entities;
using MailService.Infrastructure.Persistence;

namespace MailService.Application.Queries.Audit;

public record GetAuditRecordsQuery(Guid? ResourceId, int PageSize) : IRequest<List<AuditRecord>>;

public class GetAuditRecordsQueryHandler : IRequestHandler<GetAuditRecordsQuery, List<AuditRecord>>
{
    private readonly MailServiceDbContext _dbContext;

    public GetAuditRecordsQueryHandler(MailServiceDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<AuditRecord>> Handle(GetAuditRecordsQuery request, CancellationToken cancellationToken)
    {
        var query = _dbContext.AuditRecords.AsNoTracking();

        if (request.ResourceId.HasValue && request.ResourceId.Value != Guid.Empty)
        {
            query = query.Where(a => a.ResourceId == request.ResourceId.Value);
        }

        return await query.OrderByDescending(a => a.Timestamp)
            .Take(Math.Min(request.PageSize > 0 ? request.PageSize : 20, 100))
            .ToListAsync(cancellationToken);
    }
}
