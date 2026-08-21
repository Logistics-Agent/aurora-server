using MediatR;
using Microsoft.EntityFrameworkCore;
using MailService.Domain.Entities;
using MailService.Infrastructure.Persistence;

namespace MailService.Application.Queries.Drafts;

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
