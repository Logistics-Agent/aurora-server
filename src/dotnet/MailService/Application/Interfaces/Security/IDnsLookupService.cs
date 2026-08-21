namespace MailService.Application.Interfaces.Security;

public interface IDnsLookupService
{
    Task<string?> GetSpfRecordAsync(string domain, CancellationToken cancellationToken = default);
    Task<string?> GetDkimRecordAsync(string domain, string selector, CancellationToken cancellationToken = default);
    Task<string?> GetDmarcRecordAsync(string domain, CancellationToken cancellationToken = default);
}
