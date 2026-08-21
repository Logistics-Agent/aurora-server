namespace MailService.Application.Interfaces.Stalwart;

public interface IStalwartManagementClient
{
    Task<bool> RegisterDomainAsync(string domainName, CancellationToken cancellationToken = default);
    Task<string> GenerateDkimKeyAsync(string domainName, string selector = "aurora-2025", CancellationToken cancellationToken = default);
    Task<bool> ProvisionAccountAsync(string fullAddress, CancellationToken cancellationToken = default);
    Task<byte[]> GetMessageEmlAsync(string messageId, CancellationToken cancellationToken = default);
    Task<bool> DeliverQuarantinedMessageAsync(string messageId, string recipientAddress, CancellationToken cancellationToken = default);
}
