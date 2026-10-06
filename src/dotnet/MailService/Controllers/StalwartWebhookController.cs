using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using MassTransit;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MailService.Infrastructure.Security;
using Shared.Events;

namespace MailService.Controllers;

[ApiController]
[Route("api/v1/mail/stalwart")]
public class StalwartWebhookController : ControllerBase
{
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly IConfiguration _configuration;
    private readonly ILogger<StalwartWebhookController> _logger;

    public StalwartWebhookController(
        IPublishEndpoint publishEndpoint,
        IConfiguration configuration,
        ILogger<StalwartWebhookController> logger)
    {
        _publishEndpoint = publishEndpoint;
        _configuration = configuration;
        _logger = logger;
    }

    [HttpPost("events")]
    public async Task<IActionResult> HandleWebhook()
    {
        // 1. Read Raw Body Bytes
        using var ms = new MemoryStream();
        await Request.Body.CopyToAsync(ms);
        byte[] rawBytes = ms.ToArray();
        string rawJson = Encoding.UTF8.GetString(rawBytes);

        // 2. Constant-time Base64 HMAC-SHA256 Verification
        var secret = _configuration["Stalwart:WebhookSecret"] ?? string.Empty;
        var signature = Request.Headers["X-Signature"].FirstOrDefault() 
                     ?? Request.Headers["X-Stalwart-Signature"].FirstOrDefault();

        if (!string.IsNullOrEmpty(secret) && !WebhookSecurity.VerifyStalwartHmac(rawBytes, signature, secret))
        {
            _logger.LogWarning("Unauthorized Stalwart webhook call: HMAC signature mismatch.");
            return Unauthorized();
        }

        // 3. Deserialize Webhook Envelope
        StalwartWebhookEnvelope? envelope;
        try
        {
            envelope = JsonSerializer.Deserialize<StalwartWebhookEnvelope>(rawBytes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to deserialize Stalwart webhook payload: {RawJson}", rawJson);
            return BadRequest("Invalid JSON payload");
        }

        if (envelope?.Events == null || envelope.Events.Count == 0)
        {
            return Ok(new { status = "empty" });
        }

        _logger.LogInformation("Received {Count} events from Stalwart Webhook. Payload preview: {Preview}", 
            envelope.Events.Count, rawJson.Length > 300 ? rawJson[..300] : rawJson);

        try
        {
            // 4. Publish to Durable RabbitMQ Exchange/Queue
            var ingestEvents = envelope.Events
                .Where(e => e.Type.Contains("ingest", StringComparison.OrdinalIgnoreCase))
                .ToList();

            foreach (var evt in ingestEvents)
            {
                var accountId = evt.Data.AccountId;
                var to = evt.Data.To;
                var messageId = evt.Data.MessageId 
                             ?? (evt.Data.RfcMessageId ?? string.Empty);
                var emailId = evt.Data.EmailId;

                await _publishEndpoint.Publish(new InboundEmailWebhookReceivedEvent
                {
                    StalwartEventId = evt.Id,
                    EventType = evt.Type,
                    AccountName = evt.Data.AccountName,
                    AccountId = accountId,
                    JmapEmailId = emailId,
                    RfcMessageId = evt.Data.RfcMessageId,
                    MessageId = messageId,
                    To = to,
                    From = evt.Data.From,
                    IngestedAt = evt.CreatedAt,
                    RawPayloadJson = JsonSerializer.Serialize(evt)
                });
            }

            // 5. Prompt ACK after RabbitMQ publish succeeds
            return Ok(new { status = "accepted", count = envelope.Events.Count, ingestCount = ingestEvents.Count });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "RabbitMQ publish failed for Stalwart webhook events. Returning 503 to trigger retry.");
            return StatusCode(StatusCodes.Status503ServiceUnavailable, "Ingestion broker unavailable");
        }
    }
}

public record StalwartWebhookEnvelope
{
    [JsonPropertyName("events")]
    public System.Collections.Generic.List<StalwartWebhookEventItem> Events { get; init; } = new();
}

public record StalwartWebhookEventItem
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("createdAt")]
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    [JsonPropertyName("type")]
    public string Type { get; init; } = string.Empty;

    [JsonPropertyName("data")]
    public StalwartWebhookData Data { get; init; } = new();
}

public record StalwartWebhookData
{
    [JsonPropertyName("accountName")]
    public string? AccountName { get; init; }

    [JsonPropertyName("accountId")]
    public JsonElement? AccountIdRaw { get; init; }

    public string? AccountId => AccountIdRaw.HasValue 
        ? (AccountIdRaw.Value.ValueKind == JsonValueKind.Number 
            ? AccountIdRaw.Value.GetInt64().ToString() 
            : AccountIdRaw.Value.GetString()) 
        : null;

    [JsonPropertyName("emailId")]
    public string? EmailId { get; init; }

    [JsonPropertyName("rfcMessageId")]
    public string? RfcMessageId { get; init; }

    [JsonPropertyName("messageId")]
    public string? MessageId { get; init; }

    [JsonPropertyName("to")]
    public JsonElement? ToRaw { get; init; }

    public string? To
    {
        get
        {
            if (!ToRaw.HasValue) return null;
            if (ToRaw.Value.ValueKind == JsonValueKind.String) return ToRaw.Value.GetString();
            if (ToRaw.Value.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in ToRaw.Value.EnumerateArray())
                {
                    var s = item.GetString();
                    if (!string.IsNullOrEmpty(s)) return s;
                }
            }
            return null;
        }
    }

    [JsonPropertyName("from")]
    public string? From { get; init; }

    [JsonPropertyName("documentId")]
    public JsonElement? DocumentId { get; init; }

    [JsonPropertyName("blobId")]
    public string? BlobId { get; init; }

    [JsonExtensionData]
    public System.Collections.Generic.Dictionary<string, JsonElement>? AdditionalFields { get; init; }
}
