using MediatR;
using Microsoft.EntityFrameworkCore;
using MailService.Domain.Entities;
using MailService.Infrastructure.Persistence;

namespace MailService.Application.Queries.Messages;

public record GetProcessedMessageQuery(Guid ProcessedMessageId) : IRequest<ProcessedMessage?>;

public class GetProcessedMessageQueryHandler : IRequestHandler<GetProcessedMessageQuery, ProcessedMessage?>
{
    private readonly MailServiceDbContext _dbContext;

    public GetProcessedMessageQueryHandler(MailServiceDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ProcessedMessage?> Handle(GetProcessedMessageQuery request, CancellationToken cancellationToken)
    {
        return await _dbContext.ProcessedMessages
            .Include(p => p.SecurityCheckResults)
            .FirstOrDefaultAsync(p => p.Id == request.ProcessedMessageId, cancellationToken);
    }
}
