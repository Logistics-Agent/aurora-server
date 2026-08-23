using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Shared.Security;
using MailService.Application.Interfaces.Stalwart;
using MailService.Domain.Enums;
using MailService.Infrastructure.Persistence;

namespace MailService.Application.Commands.Quarantine;

public record ReleaseQuarantineCommand(Guid QuarantineId, bool AdminOverride = false) : IRequest<bool>;

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

        // Malware release security rule: Release of malware-infected emails requires explicit Admin authorization
        bool isMalware = !string.IsNullOrEmpty(record.QuarantineReason) &&
                         (record.QuarantineReason.Contains("Malware", StringComparison.OrdinalIgnoreCase) ||
                          record.QuarantineReason.Contains("virus", StringComparison.OrdinalIgnoreCase));

        if (isMalware)
        {
            bool isAdmin = request.AdminOverride ||
                           _currentUserService.RoleIds.Contains("SYSTEM_ADMIN") ||
                           _currentUserService.RoleIds.Contains("TENANT_ADMIN") ||
                           _currentUserService.RoleIds.Contains("Tenant_Admin");

            if (!isAdmin)
            {
                throw new UnauthorizedAccessException("PERMISSION_DENIED: Malware-infected emails cannot be released without System Administrator authorization.");
            }
        }

        record.Status = QuarantineStatus.Released;
        record.ReviewedBy = _currentUserService.UserId;
        record.ReviewedAt = DateTimeOffset.UtcNow;

        // Retrieve recipient address from associated processed message
        var processedMessage = await _dbContext.ProcessedMessages.FindAsync(new object[] { record.ProcessedMessageId }, cancellationToken);
        string targetRecipient = processedMessage?.RecipientAddresses.FirstOrDefault() ?? "inbox@domain.com";

        await _stalwartClient.DeliverQuarantinedMessageAsync(record.MessageId, targetRecipient, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }
}
