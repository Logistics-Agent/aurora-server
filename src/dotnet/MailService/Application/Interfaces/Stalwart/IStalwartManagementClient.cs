using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MailService.Application.Interfaces.Stalwart;

public record ProvisionResult(bool IsSuccess, string? StalwartAccountId, string? ErrorMessage)
{
    public static ProvisionResult Success(string accountId) => new(true, accountId, null);
    public static ProvisionResult Failed(string error) => new(false, null, error);
}

public interface IStalwartManagementClient
{
    Task<string> ResolveOrCreateStalwartDomainIdAsync(string domainName, CancellationToken cancellationToken = default);
    Task<ProvisionResult> ProvisionAccountAsync(string localPart, string stalwartDomainId, string? displayName = null, CancellationToken cancellationToken = default);
    Task<string?> FindExistingAccountIdAsync(string localPart, string stalwartDomainId, CancellationToken cancellationToken = default);
    Task<bool> RegisterDomainAsync(string domainName, CancellationToken cancellationToken = default);
    Task<string> GenerateDkimKeyAsync(string domainName, string selector = "aurora-2025", CancellationToken cancellationToken = default);
    Task<bool> ProvisionAccountAsync(string fullAddress, CancellationToken cancellationToken = default);
    Task<bool> CreateAliasAsync(string aliasAddress, IReadOnlyList<string> targetAddresses, CancellationToken cancellationToken = default);
    Task<bool> DeleteAliasAsync(string aliasAddress, CancellationToken cancellationToken = default);
    Task<byte[]> GetMessageEmlAsync(string messageId, CancellationToken cancellationToken = default);
    Task<bool> DeliverQuarantinedMessageAsync(string messageId, string recipientAddress, CancellationToken cancellationToken = default);
}
