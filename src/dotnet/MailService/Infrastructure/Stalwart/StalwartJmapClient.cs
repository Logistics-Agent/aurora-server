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
        var jmapAccountId = await ResolveJmapAccountIdAsync(mailbox.FullAddress, cancellationToken);

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
            if (!response.IsSuccessStatusCode) return null;

            var result = await response.Content.ReadFromJsonAsync<JmapEmailGetRoot>(cancellationToken: cancellationToken);
            var item = result?.MethodResponses?.FirstOrDefault()?.List?.FirstOrDefault();
            if (item == null) return null;

            var bodyText = item.TextBody?.FirstOrDefault()?.PartId != null && item.BodyValues != null && item.BodyValues.TryGetValue(item.TextBody.First().PartId, out var textVal)
                ? textVal.Value
                : string.Empty;

            var bodyHtml = item.HtmlBody?.FirstOrDefault()?.PartId != null && item.BodyValues != null && item.BodyValues.TryGetValue(item.HtmlBody.First().PartId, out var htmlVal)
                ? htmlVal.Value
                : string.Empty;

            return new JmapEmailDto
            {
                Id = item.Id,
                BlobId = item.BlobId ?? string.Empty,
                ThreadId = item.ThreadId ?? string.Empty,
                Subject = item.Subject ?? string.Empty,
                From = item.From?.FirstOrDefault()?.Email ?? string.Empty,
                To = item.To?.Select(t => t.Email).ToList() ?? new(),
                BodyText = bodyText,
                BodyHtml = bodyHtml,
                ReceivedAt = item.ReceivedAt ?? DateTimeOffset.UtcNow
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
            if (!res.IsSuccessStatusCode) return null;

            var root = await res.Content.ReadFromJsonAsync<JmapQueryResponseRoot>(cancellationToken: cancellationToken);
            var emailId = root?.MethodResponses?.FirstOrDefault()?.Ids?.FirstOrDefault();

            if (!string.IsNullOrEmpty(emailId))
            {
                return await GetEmailDirectAsync(jmapAccountId, emailId, cancellationToken);
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
