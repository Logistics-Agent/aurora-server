using MediatR;
using Microsoft.EntityFrameworkCore;
using MailService.Domain.Entities;
using MailService.Infrastructure.Persistence;

namespace MailService.Application.Queries;

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
            if (Enum.TryParse<Domain.Enums.EmailDirection>(request.Direction, true, out var dir))
            {
                query = query.Where(p => p.Direction == dir);
            }
        }

        return await query.OrderByDescending(p => p.ReceivedAt)
            .Take(Math.Min(request.PageSize > 0 ? request.PageSize : 20, 100))
            .ToListAsync(cancellationToken);
    }
}

public record GetDraftQuery(Guid DraftId) : IRequest<EmailDraft?>;

public class GetDraftQueryHandler : IRequestHandler<GetDraftQuery, EmailDraft?>
{
    private readonly MailServiceDbContext _dbContext;

    public GetDraftQueryHandler(MailServiceDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<EmailDraft?> Handle(GetDraftQuery request, CancellationToken cancellationToken)
    {
        return await _dbContext.EmailDrafts.FirstOrDefaultAsync(d => d.Id == request.DraftId, cancellationToken);
    }
}

public record ListDraftsQuery(Guid? MailboxId, string Status, int PageSize) : IRequest<List<EmailDraft>>;

public class ListDraftsQueryHandler : IRequestHandler<ListDraftsQuery, List<EmailDraft>>
{
    private readonly MailServiceDbContext _dbContext;

    public ListDraftsQueryHandler(MailServiceDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<EmailDraft>> Handle(ListDraftsQuery request, CancellationToken cancellationToken)
    {
        var query = _dbContext.EmailDrafts.AsNoTracking().Where(d => d.IsLatestRevision);

        if (request.MailboxId.HasValue && request.MailboxId != Guid.Empty)
        {
            query = query.Where(d => d.MailboxId == request.MailboxId.Value);
        }

        return await query.OrderByDescending(d => d.CreatedAt)
            .Take(Math.Min(request.PageSize > 0 ? request.PageSize : 20, 100))
            .ToListAsync(cancellationToken);
    }
}

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

        if (!string.IsNullOrEmpty(request.Status) && Enum.TryParse<Domain.Enums.QuarantineStatus>(request.Status, true, out var st))
        {
            query = query.Where(q => q.Status == st);
        }

        return await query.OrderByDescending(q => q.QuarantinedAt)
            .Take(Math.Min(request.PageSize > 0 ? request.PageSize : 20, 100))
            .ToListAsync(cancellationToken);
    }
}
