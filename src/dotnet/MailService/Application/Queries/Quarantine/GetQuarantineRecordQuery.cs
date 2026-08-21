using MediatR;
using Microsoft.EntityFrameworkCore;
using MailService.Domain.Entities;
using MailService.Infrastructure.Persistence;

namespace MailService.Application.Queries.Quarantine;

public record GetQuarantineRecordQuery(Guid QuarantineId) : IRequest<QuarantineRecord?>;

public class GetQuarantineRecordQueryHandler : IRequestHandler<GetQuarantineRecordQuery, QuarantineRecord?>
{
    private readonly MailServiceDbContext _dbContext;

    public GetQuarantineRecordQueryHandler(MailServiceDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<QuarantineRecord?> Handle(GetQuarantineRecordQuery request, CancellationToken cancellationToken)
    {
        return await _dbContext.QuarantineRecords.FirstOrDefaultAsync(q => q.Id == request.QuarantineId, cancellationToken);
    }
}
