using System;
using System.Threading;
using System.Threading.Tasks;
using MailService.Domain.Entities;

namespace MailService.Application.Interfaces.Stalwart;

public record MailboxResolutionResult(
    bool IsResolved,
    Guid TenantId,
    Guid DomainId,
    Guid MailboxId,
    string FullAddress,
    MailboxType Type,
    Guid? UserId,
    string? StalwartAccountId = null,
    string? FailureReason = null);

public interface IMailboxResolver
{
    Task<MailboxResolutionResult> ResolveMailboxByStalwartAccountIdAsync(string stalwartAccountId, CancellationToken cancellationToken = default);
    Task<MailboxResolutionResult> ResolveMailboxByAddressAsync(string recipientAddress, CancellationToken cancellationToken = default);
}
