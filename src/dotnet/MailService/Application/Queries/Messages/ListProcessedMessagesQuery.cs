using MediatR;
using Microsoft.EntityFrameworkCore;
using MailService.Domain.Entities;
using MailService.Domain.Enums;
using MailService.Infrastructure.Persistence;

namespace MailService.Application.Queries.Messages;

public record ListProcessedMessagesQuery(string Direction, string Category, int PageSize, string? Cursor) : IRequest<List<ProcessedMessage>>;

public class ListProcessedMessagesQueryHandler : IRequestHandler<ListProcessedMessagesQuery, List<ProcessedMessage>>
{
    private readonly MailServiceDbContext _dbContext;

    public ListProcessedMessagesQueryHandler(MailServiceDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<ProcessedMessage>> Handle(ListProcessedMessagesQuery request, CancellationToken cancellationToken)
    {
        var query = _dbContext.ProcessedMessages.AsNoTracking();

        if (!string.IsNullOrEmpty(request.Direction))
        {
            if (Enum.TryParse<EmailDirection>(request.Direction, true, out var dir))
            {
                query = query.Where(p => p.Direction == dir);
            }
        }

        return await query.OrderByDescending(p => p.ReceivedAt)
            .Take(Math.Min(request.PageSize > 0 ? request.PageSize : 20, 100))
            .ToListAsync(cancellationToken);
    }
}
