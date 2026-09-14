using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
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

    public async Task<string> ResolveOrCreateStalwartDomainIdAsync(string domainName, CancellationToken cancellationToken = default)
    {
        var normalizedDomain = domainName.Trim().ToLowerInvariant();

        // 1. Check existing domain via x:Domain/query
        var queryPayload = new
        {
            @using = new[] { "urn:ietf:params:jmap:core", "urn:stalwart:jmap" },
            methodCalls = new object[]
            {
                new object[]
                {
                    "x:Domain/query",
                    new
                    {
                        filter = new { name = normalizedDomain }
                    },
                    "q1"
                }
            }
        };

        try
        {
            var queryRes = await _httpClient.PostAsJsonAsync("/jmap", queryPayload, cancellationToken);
            if (queryRes.IsSuccessStatusCode)
            {
                var queryRoot = await queryRes.Content.ReadFromJsonAsync<JmapQueryResponseRoot>(cancellationToken: cancellationToken);
                var existingId = queryRoot?.MethodResponses?.FirstOrDefault()?.Ids?.FirstOrDefault();
                if (!string.IsNullOrEmpty(existingId))
                {
                    return existingId;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to query domain {Domain} on Stalwart JMAP", normalizedDomain);
        }

        // 2. Create domain via x:Domain/set with required fields
        var setPayload = new
        {
            @using = new[] { "urn:ietf:params:jmap:core", "urn:stalwart:jmap" },
            methodCalls = new object[]
            {
                new object[]
                {
                    "x:Domain/set",
                    new
                    {
                        create = new Dictionary<string, object>
                        {
                            ["dom1"] = new
                            {
                                name = normalizedDomain,
                                description = $"Aurora Managed Domain: {normalizedDomain}",
                                aliases = new Dictionary<string, object>(),
                                certificateManagement = new { @type = "Manual" },
                                dkimManagement = new { @type = "Automatic" },
                                dnsManagement = new { @type = "Manual" },
                                subAddressing = new { @type = "Enabled" }
                            }
                        }
                    },
                    "s1"
                }
            }
        };

        try
        {
            var setRes = await _httpClient.PostAsJsonAsync("/jmap", setPayload, cancellationToken);
            if (setRes.IsSuccessStatusCode)
            {
                var setRoot = await setRes.Content.ReadFromJsonAsync<JmapSetResponseRoot>(cancellationToken: cancellationToken);
                var createdId = setRoot?.MethodResponses?.FirstOrDefault()?.Created?.FirstOrDefault().Value.Id;
                if (!string.IsNullOrEmpty(createdId))
                {
                    return createdId;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create domain {Domain} on Stalwart JMAP", normalizedDomain);
        }

        return normalizedDomain;
    }

    public async Task<ProvisionResult> ProvisionAccountAsync(
        string localPart, 
        string stalwartDomainId, 
        string? displayName = null, 
        CancellationToken cancellationToken = default)
    {
        var normalizedLocalPart = localPart.Trim().ToLowerInvariant();

        var jmapPayload = new
        {
            @using = new[] { "urn:ietf:params:jmap:core", "urn:stalwart:jmap" },
            methodCalls = new object[]
            {
                new object[]
                {
                    "x:Account/set",
                    new
                    {
                        create = new Dictionary<string, object>
                        {
                            ["new1"] = new
                            {
                                @type = "User",
                                name = normalizedLocalPart,
                                domainId = stalwartDomainId,
                                description = displayName ?? $"Aurora Mailbox: {normalizedLocalPart}",
                                aliases = new Dictionary<string, object>(),
                                credentials = new Dictionary<string, object>(),
                                memberGroupIds = new Dictionary<string, object>(),
                                roles = new { @type = "User" },
                                permissions = new { @type = "Inherit" },
                                quotas = new Dictionary<string, object>(),
                                encryptionAtRest = new { @type = "Disabled" }
                            }
                        }
                    },
                    "c1"
                }
            }
        };

        try
        {
            var response = await _httpClient.PostAsJsonAsync("/jmap", jmapPayload, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return ProvisionResult.Failed($"HTTP {(int)response.StatusCode}: {response.ReasonPhrase}");
            }

            var root = await response.Content.ReadFromJsonAsync<JmapAccountSetResponseRoot>(cancellationToken: cancellationToken);
            var methodResponse = root?.MethodResponses?.FirstOrDefault();
            var created = methodResponse?.Created?.FirstOrDefault();

            if (created.HasValue && !string.IsNullOrEmpty(created.Value.Value.Id))
            {
                return ProvisionResult.Success(created.Value.Value.Id);
            }

            var notCreated = methodResponse?.NotCreated?.FirstOrDefault();
            var errorDesc = notCreated?.Value.Description ?? notCreated?.Value.Type ?? "JMAP x:Account/set rejected creation";
            return ProvisionResult.Failed(errorDesc);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to call x:Account/set for localPart {LocalPart}", normalizedLocalPart);
            return ProvisionResult.Failed(ex.Message);
        }
    }

    public async Task<string?> FindExistingAccountIdAsync(
        string localPart, 
        string stalwartDomainId, 
        CancellationToken cancellationToken = default)
    {
        var normalizedLocalPart = localPart.Trim().ToLowerInvariant();

        var jmapPayload = new
        {
            @using = new[] { "urn:ietf:params:jmap:core", "urn:stalwart:jmap" },
            methodCalls = new object[]
            {
                new object[]
                {
                    "x:Account/query",
                    new
                    {
                        filter = new
                        {
                            name = normalizedLocalPart,
                            domainId = stalwartDomainId
                        }
                    },
                    "q1"
                }
            }
        };

        try
        {
            var response = await _httpClient.PostAsJsonAsync("/jmap", jmapPayload, cancellationToken);
            if (!response.IsSuccessStatusCode) return null;

            var root = await response.Content.ReadFromJsonAsync<JmapQueryResponseRoot>(cancellationToken: cancellationToken);
            return root?.MethodResponses?.FirstOrDefault()?.Ids?.FirstOrDefault();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to query existing account for {LocalPart} in domain {DomainId}", normalizedLocalPart, stalwartDomainId);
            return null;
        }
    }

    public async Task<bool> ProvisionAccountAsync(string fullAddress, CancellationToken cancellationToken = default)
    {
        var parts = fullAddress.Split('@');
        var localPart = parts[0];
        var domainName = parts.Length > 1 ? parts[1] : "e-verland.site";

        var domainId = await ResolveOrCreateStalwartDomainIdAsync(domainName, cancellationToken);
        var result = await ProvisionAccountAsync(localPart, domainId, null, cancellationToken);
        return result.IsSuccess;
    }

    public async Task<bool> RegisterDomainAsync(string domainName, CancellationToken cancellationToken = default)
    {
        try
        {
            var domainId = await ResolveOrCreateStalwartDomainIdAsync(domainName, cancellationToken);
            return !string.IsNullOrEmpty(domainId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "RegisterDomainAsync failed for {Domain}", domainName);
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
            _logger.LogWarning(ex, "Stalwart DKIM generation failed for {Domain}", domainName);
        }

        return string.Empty;
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
            _logger.LogWarning(ex, "Stalwart create alias failed for {Alias}", aliasAddress);
            return false;
        }
    }

    public async Task<bool> DeleteAliasAsync(string aliasAddress, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.DeleteAsync($"/api/management/aliases/{Uri.EscapeDataString(aliasAddress)}", cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Stalwart delete alias failed for {Alias}", aliasAddress);
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
            _logger.LogWarning(ex, "Stalwart GetMessageEml failed for {MessageId}", messageId);
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
            _logger.LogWarning(ex, "Stalwart quarantine release failed for {MessageId}", messageId);
            return false;
        }
    }

    private record StalwartDkimResponse(string TxtRecord);
}

public record JmapQueryResponseRoot
{
    [JsonPropertyName("methodResponses")]
    public List<JmapQueryMethodResponse>? MethodResponses { get; init; }
}

public record JmapQueryMethodResponse
{
    [JsonPropertyName("ids")]
    public List<string>? Ids { get; init; }
}

public record JmapSetResponseRoot
{
    [JsonPropertyName("methodResponses")]
    public List<JmapSetMethodResponse>? MethodResponses { get; init; }
}

public record JmapSetMethodResponse
{
    [JsonPropertyName("created")]
    public Dictionary<string, JmapCreatedItem>? Created { get; init; }
}

public record JmapCreatedItem
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;
}

public record JmapAccountSetResponseRoot
{
    [JsonPropertyName("methodResponses")]
    public List<JmapAccountSetMethodResponse>? MethodResponses { get; init; }
}

public record JmapAccountSetMethodResponse
{
    [JsonPropertyName("created")]
    public Dictionary<string, JmapCreatedItem>? Created { get; init; }

    [JsonPropertyName("notCreated")]
    public Dictionary<string, JmapSetErrorItem>? NotCreated { get; init; }
}

public record JmapSetErrorItem
{
    [JsonPropertyName("type")]
    public string? Type { get; init; }

    [JsonPropertyName("description")]
    public string? Description { get; init; }
}
