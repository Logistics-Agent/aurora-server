using MediatR;
using Shared.Security;
using MailService.Application.Interfaces.Stalwart;
using MailService.Domain.Enums;
using MailService.Infrastructure.Persistence;

namespace MailService.Application.Commands.Quarantine;

public record ReleaseQuarantineCommand(Guid QuarantineId) : IRequest<bool>;

public class ReleaseQuarantineCommandHandler : IRequestHandler<ReleaseQuarantineCommand, bool>
{
    private readonly MailServiceDbContext _dbContext;
    private readonly IStalwartManagementClient _stalwartClient;
    private readonly ICurrentUserService _currentUserService;

    public ReleaseQuarantineCommandHandler(
        MailServiceDbContext dbContext,
        IStalwartManagementClient stalwartClient,
        ICurrentUserService currentUserService)
    {
        _dbContext = dbContext;
        _stalwartClient = stalwartClient;
        _currentUserService = currentUserService;
    }

    public async Task<bool> Handle(ReleaseQuarantineCommand request, CancellationToken cancellationToken)
    {
        var record = await _dbContext.QuarantineRecords.FindAsync(new object[] { request.QuarantineId }, cancellationToken);
        if (record == null || record.Status != QuarantineStatus.Pending)
        {
            return false;
        }

        record.Status = QuarantineStatus.Released;
        record.ReviewedBy = _currentUserService.UserId;
        record.ReviewedAt = DateTimeOffset.UtcNow;

        await _stalwartClient.DeliverQuarantinedMessageAsync(record.MessageId, "recipient@domain.com", cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }
}
