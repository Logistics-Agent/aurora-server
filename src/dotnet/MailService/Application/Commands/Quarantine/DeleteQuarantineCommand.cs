using MediatR;
using MailService.Domain.Enums;
using MailService.Infrastructure.Persistence;

namespace MailService.Application.Commands.Quarantine;

public record DeleteQuarantineCommand(Guid QuarantineId) : IRequest<bool>;

public class DeleteQuarantineCommandHandler : IRequestHandler<DeleteQuarantineCommand, bool>
{
    private readonly MailServiceDbContext _dbContext;

    public DeleteQuarantineCommandHandler(MailServiceDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<bool> Handle(DeleteQuarantineCommand request, CancellationToken cancellationToken)
    {
        var record = await _dbContext.QuarantineRecords.FindAsync(new object[] { request.QuarantineId }, cancellationToken);
        if (record == null)
        {
            return false;
        }

        record.Status = QuarantineStatus.Deleted;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }
}
