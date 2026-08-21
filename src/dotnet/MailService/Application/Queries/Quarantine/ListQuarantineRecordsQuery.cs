using MediatR;
using Microsoft.EntityFrameworkCore;
using MailService.Domain.Entities;
using MailService.Domain.Enums;
using MailService.Infrastructure.Persistence;

namespace MailService.Application.Queries.Quarantine;

public record ListQuarantineRecordsQuery(string Status, int PageSize) : IRequest<List<QuarantineRecord>>;

public class ListQuarantineRecordsQueryHandler : IRequestHandler<ListQuarantineRecordsQuery, List<QuarantineRecord>>
{
    private readonly MailServiceDbContext _dbContext;

    public ListQuarantineRecordsQueryHandler(MailServiceDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<QuarantineRecord>> Handle(ListQuarantineRecordsQuery request, CancellationToken cancellationToken)
    {
        var query = _dbContext.QuarantineRecords.AsNoTracking();

        if (!string.IsNullOrEmpty(request.Status) && Enum.TryParse<QuarantineStatus>(request.Status, true, out var st))
        {
            query = query.Where(q => q.Status == st);
        }

        return await query.OrderByDescending(q => q.QuarantinedAt)
            .Take(Math.Min(request.PageSize > 0 ? request.PageSize : 20, 100))
            .ToListAsync(cancellationToken);
    }
}
