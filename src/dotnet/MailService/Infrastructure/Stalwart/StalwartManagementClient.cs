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
            var rawJson = await queryRes.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogInformation("x:Domain/query {Domain} response: {Response}", normalizedDomain, rawJson);

            if (queryRes.IsSuccessStatusCode)
            {
                using var doc = JsonDocument.Parse(rawJson);
                if (doc.RootElement.TryGetProperty("methodResponses", out var methodResponses) && methodResponses.GetArrayLength() > 0)
                {
                    var firstCall = methodResponses[0];
                    if (firstCall.GetArrayLength() >= 2)
                    {
                        var methodName = firstCall[0].GetString();
                        var payload = firstCall[1];

                        if (methodName == "error")
                        {
                            var errType = payload.TryGetProperty("type", out var t) ? t.GetString() : "unknown";
                            var errDesc = payload.TryGetProperty("description", out var d) ? d.GetString() : null;
                            _logger.LogWarning("Stalwart JMAP x:Domain/query error: {Type} - {Description}", errType, errDesc);
                        }
                        else if (payload.TryGetProperty("ids", out var ids))
                        {
                            if (ids.GetArrayLength() > 0)
                            {
                                return ids[0].GetString()
                                    ?? throw new InvalidOperationException($"Domain id was null for domain '{normalizedDomain}'.");
                            }
                        }
                    }
                }
            }
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            _logger.LogWarning(ex, "Failed to query domain {Domain} on Stalwart JMAP", normalizedDomain);
        }

        // 2. Create domain via x:Domain/set with required fields
        var domainObj = new Dictionary<string, object>
        {
            ["@type"] = "Domain",
            ["name"] = normalizedDomain,
            ["description"] = $"Aurora Managed Domain: {normalizedDomain}",
            ["aliases"] = new Dictionary<string, object>(),
            ["certificateManagement"] = new Dictionary<string, object> { ["@type"] = "Manual" },
            ["dkimManagement"] = new Dictionary<string, object> { ["@type"] = "Automatic" },
            ["dnsManagement"] = new Dictionary<string, object> { ["@type"] = "Manual" },
            ["subAddressing"] = new Dictionary<string, object> { ["@type"] = "Enabled" }
        };

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
                            ["dom1"] = domainObj
                        }
                    },
                    "s1"
                }
            }
        };

        try
        {
            var setRes = await _httpClient.PostAsJsonAsync("/jmap", setPayload, cancellationToken);
            var rawJson = await setRes.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogInformation("x:Domain/set {Domain} response: {Response}", normalizedDomain, rawJson);

            if (setRes.IsSuccessStatusCode)
            {
                using var doc = JsonDocument.Parse(rawJson);
                if (doc.RootElement.TryGetProperty("methodResponses", out var methodResponses) && methodResponses.GetArrayLength() > 0)
                {
                    var firstCall = methodResponses[0];
                    if (firstCall.GetArrayLength() >= 2)
                    {
                        var methodName = firstCall[0].GetString();
                        var payload = firstCall[1];

                        if (methodName == "error")
                        {
                            var errType = payload.TryGetProperty("type", out var t) ? t.GetString() : "unknown";
                            var errDesc = payload.TryGetProperty("description", out var d) ? d.GetString() : null;
                            _logger.LogWarning("Stalwart JMAP x:Domain/set error: {Type} - {Description}", errType, errDesc);
                        }
                        else if (payload.TryGetProperty("created", out var created))
                        {
                            foreach (var property in created.EnumerateObject())
                            {
                                if (property.Value.TryGetProperty("id", out var idElement))
                                {
                                    var id = idElement.GetString();
                                    if (!string.IsNullOrWhiteSpace(id))
                                    {
                                        return id;
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            _logger.LogError(ex, "Failed to create domain {Domain} on Stalwart JMAP", normalizedDomain);
        }

        throw new InvalidOperationException($"Failed to resolve or create Stalwart domain ID for '{normalizedDomain}'.");
    }

    public async Task<ProvisionResult> ProvisionAccountAsync(
        string localPart, 
        string stalwartDomainId, 
        string? displayName = null, 
        CancellationToken cancellationToken = default)
    {
        var normalizedLocalPart = localPart.Trim().ToLowerInvariant();

        var accountObj = new Dictionary<string, object>
        {
            ["@type"] = "User",
            ["name"] = normalizedLocalPart,
            ["domainId"] = stalwartDomainId,
            ["description"] = displayName ?? $"Aurora Mailbox: {normalizedLocalPart}",
            ["aliases"] = new Dictionary<string, object>(),
            ["credentials"] = new Dictionary<string, object>(),
            ["memberGroupIds"] = new Dictionary<string, object>(),
            ["roles"] = new Dictionary<string, object> { ["@type"] = "User" },
            ["permissions"] = new Dictionary<string, object> { ["@type"] = "Inherit" },
            ["quotas"] = new Dictionary<string, object>(),
            ["encryptionAtRest"] = new Dictionary<string, object> { ["@type"] = "Disabled" }
        };

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
                            ["new1"] = accountObj
                        }
                    },
                    "c1"
                }
            }
        };

        try
        {
            var response = await _httpClient.PostAsJsonAsync("/jmap", jmapPayload, cancellationToken);
            var rawJson = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogDebug("JMAP x:Account/set response: {RawJson}", rawJson);

            if (!response.IsSuccessStatusCode)
            {
                return ProvisionResult.Failed($"HTTP {(int)response.StatusCode}: {response.ReasonPhrase} - {rawJson}");
            }

            using var doc = JsonDocument.Parse(rawJson);
            if (!doc.RootElement.TryGetProperty("methodResponses", out var methodResponses) || methodResponses.GetArrayLength() == 0)
            {
                return ProvisionResult.Failed("Empty methodResponses from Stalwart JMAP");
            }

            var firstCall = methodResponses[0];
            if (firstCall.GetArrayLength() < 2)
            {
                return ProvisionResult.Failed("Malformed JMAP response tuple");
            }

            var methodName = firstCall[0].GetString();
            var payload = firstCall[1];

            if (methodName == "error")
            {
                var errType = payload.TryGetProperty("type", out var t) ? t.GetString() : "unknown";
                var errDesc = payload.TryGetProperty("description", out var d) ? d.GetString() : null;
                return ProvisionResult.Failed($"Stalwart JMAP error: {errType} - {errDesc}");
            }

            if (payload.TryGetProperty("created", out var created))
            {
                foreach (var item in created.EnumerateObject())
                {
                    if (item.Value.TryGetProperty("id", out var idProp) && !string.IsNullOrEmpty(idProp.GetString()))
                    {
                        return ProvisionResult.Success(idProp.GetString()!);
                    }
                }
            }

            if (payload.TryGetProperty("notCreated", out var notCreated))
            {
                foreach (var item in notCreated.EnumerateObject())
                {
                    var errorType = item.Value.TryGetProperty("type", out var t) ? t.GetString() : "unknown";
                    var errorDesc = item.Value.TryGetProperty("description", out var d) ? d.GetString() : errorType;
                    return ProvisionResult.Failed(errorDesc ?? "Creation rejected");
                }
            }

            return ProvisionResult.Failed("Stalwart did not return created account ID");
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
                            name = normalizedLocalPart
                        }
                    },
                    "q1"
                }
            }
        };

        try
        {
            var response = await _httpClient.PostAsJsonAsync("/jmap", jmapPayload, cancellationToken);
            var rawJson = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogDebug("JMAP x:Account/query response: {RawJson}", rawJson);

            if (!response.IsSuccessStatusCode) return null;

            using var doc = JsonDocument.Parse(rawJson);
            if (doc.RootElement.TryGetProperty("methodResponses", out var methodResponses) && methodResponses.GetArrayLength() > 0)
            {
                var firstCall = methodResponses[0];
                if (firstCall.GetArrayLength() >= 2)
                {
                    var methodName = firstCall[0].GetString();
                    var payload = firstCall[1];

                    if (methodName == "error")
                    {
                        var errType = payload.TryGetProperty("type", out var t) ? t.GetString() : "unknown";
                        var errDesc = payload.TryGetProperty("description", out var d) ? d.GetString() : null;
                        _logger.LogWarning("Stalwart JMAP x:Account/query error: {Type} - {Description}", errType, errDesc);
                        return null;
                    }

                    if (payload.TryGetProperty("ids", out var ids) && ids.GetArrayLength() > 0)
                    {
                        return ids[0].GetString();
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to query existing account for {LocalPart} in domain {DomainId}", normalizedLocalPart, stalwartDomainId);
            return null;
        }

        return null;
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
