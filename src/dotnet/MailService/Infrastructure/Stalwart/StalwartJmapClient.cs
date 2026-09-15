using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using MailService.Application.Interfaces.Stalwart;
using Shared.Events;

namespace MailService.Infrastructure.Stalwart;

public class StalwartJmapClient : IStalwartJmapClient
{
    private readonly HttpClient _httpClient;
    private readonly IMemoryCache _cache;
    private readonly ILogger<StalwartJmapClient> _logger;

    public StalwartJmapClient(
        HttpClient httpClient,
        IMemoryCache cache,
        ILogger<StalwartJmapClient> logger)
    {
        _httpClient = httpClient;
        _cache = cache;
        _logger = logger;
    }

    public async Task<string> ResolveJmapAccountIdAsync(string accountEmail, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = accountEmail.Trim().ToLowerInvariant();
        var cacheKey = $"jmap_acc:{normalizedEmail}";

        if (_cache.TryGetValue(cacheKey, out string? cachedId) && !string.IsNullOrEmpty(cachedId))
        {
            return cachedId;
        }

        try
        {
            var session = await _httpClient.GetFromJsonAsync<JmapSessionResponse>("/.well-known/jmap", cancellationToken);
            if (session?.Accounts != null)
            {
                foreach (var (accId, accMeta) in session.Accounts)
                {
                    if (string.Equals(accMeta.Name, normalizedEmail, StringComparison.OrdinalIgnoreCase))
                    {
                        _cache.Set(cacheKey, accId, TimeSpan.FromHours(2));
                        return accId;
                    }
                }

                if (session.PrimaryAccounts?.TryGetValue("urn:ietf:params:jmap:mail", out var primaryId) == true && !string.IsNullOrEmpty(primaryId))
                {
                    _cache.Set(cacheKey, primaryId, TimeSpan.FromHours(2));
                    return primaryId;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve JMAP session to resolve account ID for {Account}", normalizedEmail);
        }

        throw new KeyNotFoundException($"No JMAP Account ID found in active session for address: {normalizedEmail}");
    }

    public async Task<JmapEmailDto> FetchEmailByWebhookDataAsync(
        MailboxResolutionResult mailbox, 
        InboundEmailWebhookReceivedEvent evt, 
        CancellationToken cancellationToken = default)
    {
        var jmapAccountId = !string.IsNullOrWhiteSpace(mailbox.StalwartAccountId)
            ? mailbox.StalwartAccountId
            : (!string.IsNullOrWhiteSpace(evt.AccountId) 
                ? evt.AccountId 
                : await ResolveJmapAccountIdAsync(mailbox.FullAddress, cancellationToken));

        // 1. Direct JMAP Email ID if provided by webhook
        if (!string.IsNullOrEmpty(evt.JmapEmailId))
        {
            var direct = await GetEmailDirectAsync(jmapAccountId, evt.JmapEmailId, cancellationToken);
            if (direct != null) return direct;
        }

        // 2. Correlation via RFC Message-ID header query
        var targetRfcId = !string.IsNullOrEmpty(evt.RfcMessageId) 
            ? evt.RfcMessageId 
            : (!string.IsNullOrEmpty(evt.MessageId) && evt.MessageId.Contains('@') ? evt.MessageId : null);

        if (!string.IsNullOrEmpty(targetRfcId))
        {
            var correlated = await this.QueryEmailByRfcMessageIdAsync(jmapAccountId, targetRfcId, cancellationToken);
            if (correlated != null) return correlated;
        }

        // 3. Try direct fetch using MessageId if present
        if (!string.IsNullOrEmpty(evt.MessageId))
        {
            var directFromMsgId = await GetEmailDirectAsync(jmapAccountId, evt.MessageId, cancellationToken);
            if (directFromMsgId != null) return directFromMsgId;
        }

        throw new KeyNotFoundException($"Could not correlate JMAP email for event {evt.StalwartEventId} in mailbox {mailbox.FullAddress}");
    }

    public async Task<JmapEmailDto?> GetEmailDirectAsync(string jmapAccountId, string emailId, CancellationToken cancellationToken = default)
    {
        var jmapRequest = new
        {
            @using = new[] { "urn:ietf:params:jmap:core", "urn:ietf:params:jmap:mail" },
            methodCalls = new object[]
            {
                new object[]
                {
                    "Email/get",
                    new
                    {
                        accountId = jmapAccountId,
                        ids = new[] { emailId },
                        properties = new[] { "id", "blobId", "threadId", "from", "to", "subject", "receivedAt", "bodyValues", "textBody", "htmlBody", "attachments" }
                    },
                    "c1"
                }
            }
        };

        try
        {
            var response = await _httpClient.PostAsJsonAsync("/jmap", jmapRequest, cancellationToken);
            var rawJson = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogDebug("JMAP Email/get response: {RawJson}", rawJson);

            if (!response.IsSuccessStatusCode) return null;

            using var doc = JsonDocument.Parse(rawJson);
            if (!doc.RootElement.TryGetProperty("methodResponses", out var methodResponses) || methodResponses.GetArrayLength() == 0)
            {
                return null;
            }

            var firstCall = methodResponses[0];
            if (firstCall.GetArrayLength() < 2) return null;

            var methodName = firstCall[0].GetString();
            var payload = firstCall[1];

            if (methodName == "error")
            {
                var errType = payload.TryGetProperty("type", out var t) ? t.GetString() : "unknown";
                var errDesc = payload.TryGetProperty("description", out var d) ? d.GetString() : null;
                _logger.LogWarning("Stalwart JMAP Email/get error: {Type} - {Description}", errType, errDesc);
                return null;
            }

            if (!payload.TryGetProperty("list", out var list) || list.GetArrayLength() == 0)
            {
                return null;
            }

            var item = list[0];
            var id = item.TryGetProperty("id", out var idProp) ? idProp.GetString() ?? emailId : emailId;
            var blobId = item.TryGetProperty("blobId", out var bProp) ? bProp.GetString() ?? string.Empty : string.Empty;
            var threadId = item.TryGetProperty("threadId", out var thProp) ? thProp.GetString() ?? string.Empty : string.Empty;
            var subject = item.TryGetProperty("subject", out var sProp) ? sProp.GetString() ?? string.Empty : string.Empty;

            var from = string.Empty;
            if (item.TryGetProperty("from", out var fromArr) && fromArr.GetArrayLength() > 0)
            {
                var firstFrom = fromArr[0];
                if (firstFrom.TryGetProperty("email", out var fromEmail))
                {
                    from = fromEmail.GetString() ?? string.Empty;
                }
            }

            var toList = new List<string>();
            if (item.TryGetProperty("to", out var toArr))
            {
                foreach (var toItem in toArr.EnumerateArray())
                {
                    if (toItem.TryGetProperty("email", out var toEmail) && !string.IsNullOrEmpty(toEmail.GetString()))
                    {
                        toList.Add(toEmail.GetString()!);
                    }
                }
            }

            var receivedAt = DateTimeOffset.UtcNow;
            if (item.TryGetProperty("receivedAt", out var recProp) && recProp.TryGetDateTimeOffset(out var recDate))
            {
                receivedAt = recDate;
            }

            var bodyText = string.Empty;
            var bodyHtml = string.Empty;

            var bodyValues = new Dictionary<string, string>();
            if (item.TryGetProperty("bodyValues", out var bvObj))
            {
                foreach (var bvProp in bvObj.EnumerateObject())
                {
                    if (bvProp.Value.TryGetProperty("value", out var valProp))
                    {
                        bodyValues[bvProp.Name] = valProp.GetString() ?? string.Empty;
                    }
                }
            }

            if (item.TryGetProperty("textBody", out var tbArr) && tbArr.GetArrayLength() > 0)
            {
                if (tbArr[0].TryGetProperty("partId", out var partIdProp))
                {
                    var partId = partIdProp.GetString();
                    if (!string.IsNullOrEmpty(partId) && bodyValues.TryGetValue(partId, out var textVal))
                    {
                        bodyText = textVal;
                    }
                }
            }

            if (item.TryGetProperty("htmlBody", out var hbArr) && hbArr.GetArrayLength() > 0)
            {
                if (hbArr[0].TryGetProperty("partId", out var partIdProp))
                {
                    var partId = partIdProp.GetString();
                    if (!string.IsNullOrEmpty(partId) && bodyValues.TryGetValue(partId, out var htmlVal))
                    {
                        bodyHtml = htmlVal;
                    }
                }
            }

            return new JmapEmailDto
            {
                Id = id,
                BlobId = blobId,
                ThreadId = threadId,
                Subject = subject,
                From = from,
                To = toList,
                BodyText = bodyText,
                BodyHtml = bodyHtml,
                ReceivedAt = receivedAt
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch JMAP email {EmailId} for account {AccountId}", emailId, jmapAccountId);
            return null;
        }
    }

    private async Task<JmapEmailDto?> QueryEmailByRfcMessageIdAsync(string jmapAccountId, string rfcMessageId, CancellationToken cancellationToken)
    {
        var queryPayload = new
        {
            @using = new[] { "urn:ietf:params:jmap:core", "urn:ietf:params:jmap:mail" },
            methodCalls = new object[]
            {
                new object[]
                {
                    "Email/query",
                    new
                    {
                        accountId = jmapAccountId,
                        filter = new { header = new[] { "Message-ID", rfcMessageId } },
                        limit = 1
                    },
                    "q1"
                }
            }
        };

        try
        {
            var res = await _httpClient.PostAsJsonAsync("/jmap", queryPayload, cancellationToken);
            var rawJson = await res.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogDebug("JMAP Email/query response: {RawJson}", rawJson);

            if (!res.IsSuccessStatusCode) return null;

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
                        _logger.LogWarning("Stalwart JMAP Email/query error: {Type} - {Description}", errType, errDesc);
                        return null;
                    }

                    if (payload.TryGetProperty("ids", out var ids) && ids.GetArrayLength() > 0)
                    {
                        var emailId = ids[0].GetString();
                        if (!string.IsNullOrEmpty(emailId))
                        {
                            return await GetEmailDirectAsync(jmapAccountId, emailId, cancellationToken);
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to query email by RFC Message-ID {RfcMessageId}", rfcMessageId);
        }

        return null;
    }
}

public record JmapSessionResponse
{
    [JsonPropertyName("accounts")]
    public Dictionary<string, JmapAccountInfo>? Accounts { get; init; }

    [JsonPropertyName("primaryAccounts")]
    public Dictionary<string, string>? PrimaryAccounts { get; init; }
}

public record JmapAccountInfo
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;
}

public record JmapEmailGetRoot
{
    [JsonPropertyName("methodResponses")]
    public List<JmapEmailGetMethodResponse>? MethodResponses { get; init; }
}

public record JmapEmailGetMethodResponse
{
    [JsonPropertyName("list")]
    public List<JmapEmailItem>? List { get; init; }
}

public record JmapEmailItem
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("blobId")]
    public string? BlobId { get; init; }

    [JsonPropertyName("threadId")]
    public string? ThreadId { get; init; }

    [JsonPropertyName("subject")]
    public string? Subject { get; init; }

    [JsonPropertyName("from")]
    public List<JmapAddressItem>? From { get; init; }

    [JsonPropertyName("to")]
    public List<JmapAddressItem>? To { get; init; }

    [JsonPropertyName("receivedAt")]
    public DateTimeOffset? ReceivedAt { get; init; }

    [JsonPropertyName("textBody")]
    public List<JmapBodyPart>? TextBody { get; init; }

    [JsonPropertyName("htmlBody")]
    public List<JmapBodyPart>? HtmlBody { get; init; }

    [JsonPropertyName("bodyValues")]
    public Dictionary<string, JmapBodyValue>? BodyValues { get; init; }
}

public record JmapAddressItem
{
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("email")]
    public string Email { get; init; } = string.Empty;
}

public record JmapBodyPart
{
    [JsonPropertyName("partId")]
    public string PartId { get; init; } = string.Empty;
}

public record JmapBodyValue
{
    [JsonPropertyName("value")]
    public string Value { get; init; } = string.Empty;
}
