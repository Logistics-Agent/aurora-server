using DnsClient;
using MailService.Application.Interfaces.Security;

namespace MailService.Infrastructure.Security.Dns;

public class DnsLookupService : IDnsLookupService
{
    private readonly LookupClient _dnsClient;

    public DnsLookupService()
    {
        _dnsClient = new LookupClient();
    }

    public async Task<string?> GetSpfRecordAsync(string domain, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _dnsClient.QueryAsync(domain, QueryType.TXT, cancellationToken: cancellationToken);
            var txtRecord = result.Answers.TxtRecords()
                .FirstOrDefault(r => r.Text.Any(t => t.StartsWith("v=spf1", StringComparison.OrdinalIgnoreCase)));

            return txtRecord?.Text.FirstOrDefault();
        }
        catch
        {
            return null;
        }
    }

    public async Task<string?> GetDkimRecordAsync(string domain, string selector, CancellationToken cancellationToken = default)
    {
        try
        {
            string fqdn = $"{selector}._domainkey.{domain}";
            var result = await _dnsClient.QueryAsync(fqdn, QueryType.TXT, cancellationToken: cancellationToken);
            var txtRecord = result.Answers.TxtRecords().FirstOrDefault();
            return txtRecord?.Text.FirstOrDefault();
        }
        catch
        {
            return null;
        }
    }

    public async Task<string?> GetDmarcRecordAsync(string domain, CancellationToken cancellationToken = default)
    {
        try
        {
            string fqdn = $"_dmarc.{domain}";
            var result = await _dnsClient.QueryAsync(fqdn, QueryType.TXT, cancellationToken: cancellationToken);
            var txtRecord = result.Answers.TxtRecords()
                .FirstOrDefault(r => r.Text.Any(t => t.StartsWith("v=DMARC1", StringComparison.OrdinalIgnoreCase)));

            return txtRecord?.Text.FirstOrDefault();
        }
        catch
        {
            return null;
        }
    }
}
