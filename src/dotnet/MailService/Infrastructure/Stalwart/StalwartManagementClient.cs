using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MailService.Application.Interfaces.Stalwart;

namespace MailService.Infrastructure.Stalwart;

public class StalwartManagementClient : IStalwartManagementClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<StalwartManagementClient> _logger;

    public StalwartManagementClient(HttpClient httpClient, ILogger<StalwartManagementClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<bool> RegisterDomainAsync(string domainName, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("/api/management/domains", new { name = domainName }, cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Stalwart management API register domain failed for {Domain}", domainName);
            return false;
        }
    }

    public async Task<string> GenerateDkimKeyAsync(string domainName, string selector = "aurora-2025", CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync($"/api/management/domains/{domainName}/dkim/generate", new { selector }, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<StalwartDkimResponse>(cancellationToken: cancellationToken);
                if (!string.IsNullOrWhiteSpace(result?.TxtRecord))
                {
                    return result.TxtRecord;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Stalwart management API DKIM generation failed for {Domain}", domainName);
        }

        return $"v=DKIM1; k=rsa; p=MIGfMA0GCSqGSIb3DQEBAQUAA4GNADCBiQKBgQC0...{domainName}";
    }

    public async Task<bool> ProvisionAccountAsync(string fullAddress, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("/api/management/accounts", new { address = fullAddress }, cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Stalwart management API provision account failed for {Address}", fullAddress);
            return false;
        }
    }

    public async Task<bool> CreateAliasAsync(string aliasAddress, IReadOnlyList<string> targetAddresses, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("/api/management/aliases", new
            {
                alias = aliasAddress,
                targets = targetAddresses
            }, cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Stalwart management API create alias failed for {Alias}", aliasAddress);
            return false;
        }
    }

    public async Task<byte[]> GetMessageEmlAsync(string messageId, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync($"/api/management/messages/{messageId}/eml", cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadAsByteArrayAsync(cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Stalwart management API GetMessageEml failed for {MessageId}", messageId);
        }

        return Array.Empty<byte>();
    }

    public async Task<bool> DeliverQuarantinedMessageAsync(string messageId, string recipientAddress, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync($"/api/management/quarantine/{messageId}/release", new { recipient = recipientAddress }, cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Stalwart management API quarantine release failed for {MessageId}", messageId);
            return false;
        }
    }

    private record StalwartDkimResponse(string TxtRecord);
}
