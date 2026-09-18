using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MimeKit;
using MailService.Application.Interfaces.Stalwart;
using MailService.Application.Pipeline;
using MailService.Domain.Entities;
using MailService.Domain.Enums;
using MailService.Infrastructure.Persistence;
using MailService.Infrastructure.Security;

namespace MailService.Controllers;

[ApiController]
[Route("api/v1/mail/cloudflare/inbound")]
[Route("api/v1/mail/inbound/cloudflare")]
public class CloudflareInboundController : ControllerBase
{
    private readonly IMailboxResolver _mailboxResolver;
    private readonly MailServiceDbContext _dbContext;
    private readonly InboundPipelineRunner _pipelineRunner;
    private readonly IConfiguration _configuration;
    private readonly ILogger<CloudflareInboundController> _logger;

    public CloudflareInboundController(
        IMailboxResolver mailboxResolver,
        MailServiceDbContext dbContext,
        InboundPipelineRunner pipelineRunner,
        IConfiguration configuration,
        ILogger<CloudflareInboundController> logger)
    {
        _mailboxResolver = mailboxResolver;
        _dbContext = dbContext;
        _pipelineRunner = pipelineRunner;
        _configuration = configuration;
        _logger = logger;
    }

    [HttpGet]
    [HttpGet("/api/v1/mail/inbound")]
    public IActionResult HealthCheck()
    {
        return Ok(new
        {
            status = "healthy",
            service = "mail-service",
            endpoint = "cloudflare-inbound",
            timestamp = DateTimeOffset.UtcNow
        });
    }

    [HttpPost]
    [HttpPost("/api/v1/mail/cloudflare/inbound")]
    [HttpPost("/api/v1/mail/inbound/cloudflare")]
    [DisableRequestSizeLimit]
    [RequestSizeLimit(52_428_800)] // 50MB limit for inbound attachments
    public async Task<IActionResult> HandleInboundEmail(CancellationToken cancellationToken)
    {
        string deliveryId = Guid.NewGuid().ToString("N");
        try
        {
            // 1. Read Raw Body Bytes
            using var ms = new MemoryStream();
            await Request.Body.CopyToAsync(ms, cancellationToken);
            byte[] rawBytes = ms.ToArray();
            string rawBodyString = Encoding.UTF8.GetString(rawBytes);

            // 2. Extract Headers
            var timestampStr = Request.Headers["X-Aurora-Timestamp"].FirstOrDefault();
            var headerDeliveryId = Request.Headers["X-Aurora-Delivery-Id"].FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(headerDeliveryId))
            {
                deliveryId = headerDeliveryId;
            }

            var directSecretHeader = Request.Headers["X-Aurora-Webhook-Secret"].FirstOrDefault()
                                  ?? Request.Headers["X-Webhook-Secret"].FirstOrDefault();

            var signature = Request.Headers["X-Aurora-Signature"].FirstOrDefault()
                         ?? Request.Headers["X-Signature"].FirstOrDefault();

            // 3. Webhook Authentication (Shared Secret Header OR HMAC-SHA256 Signature)
            var secret = _configuration["CloudflareInbound:WebhookSecret"]
                      ?? _configuration["Cloudflare:WebhookSecret"]
                      ?? string.Empty;

            if (!string.IsNullOrEmpty(secret))
            {
                bool isDirectSecretValid = !string.IsNullOrEmpty(directSecretHeader) &&
                                           WebhookSecurity.VerifySharedSecret(directSecretHeader, secret);

                bool isHmacValid = !string.IsNullOrEmpty(signature) &&
                                   WebhookSecurity.VerifyCloudflareHmac(rawBytes, timestampStr, signature, secret);

                if (!isDirectSecretValid && !isHmacValid)
                {
                    _logger.LogWarning("Unauthorized Cloudflare Inbound webhook call: Missing or invalid secret/signature. Timestamp: {Timestamp}", timestampStr);
                    return Unauthorized(new { error = "Invalid signature, missing secret, or expired timestamp" });
                }
            }

            // 4. Check Idempotency (Prevent duplicate processing on Cloudflare retry)
            var existingMsg = await _dbContext.ProcessedMessages
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(m => m.SourceEventId == deliveryId, cancellationToken);

            if (existingMsg != null)
            {
                _logger.LogInformation("Cloudflare inbound delivery {DeliveryId} already processed (idempotent). Returning existing message {MessageId}.",
                    deliveryId, existingMsg.Id);
                return Ok(new
                {
                    status = "accepted",
                    deliveryId = deliveryId,
                    messageId = existingMsg.Id.ToString(),
                    rfcMessageId = existingMsg.MessageId,
                    threadId = existingMsg.ThreadId?.ToString(),
                    idempotent = true
                });
            }

            // 5. Deserialize Payload
            CloudflareInboundPayload? payload;
            try
            {
                payload = JsonSerializer.Deserialize<CloudflareInboundPayload>(rawBytes, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to deserialize Cloudflare inbound payload. Body preview: {Preview}",
                    rawBodyString.Length > 300 ? rawBodyString[..300] : rawBodyString);
                return BadRequest(new { error = "Invalid JSON payload" });
            }

            if (payload == null || string.IsNullOrWhiteSpace(payload.RawEmailBase64))
            {
                return BadRequest(new { error = "rawEmailBase64 is required" });
            }

            if (!string.IsNullOrWhiteSpace(payload.DeliveryId))
            {
                deliveryId = payload.DeliveryId;
            }

            // 6. Decode Base64 Raw EML
            byte[] rawEmlBytes;
            try
            {
                rawEmlBytes = Convert.FromBase64String(payload.RawEmailBase64);
            }
            catch (FormatException)
            {
                _logger.LogError("Invalid base64 payload received for delivery {DeliveryId}", deliveryId);
                return BadRequest(new { error = "Invalid Base64 format in rawEmailBase64" });
            }

            // 7. Parse MIME via MimeKit
            MimeMessage mimeMessage;
            try
            {
                using var emlStream = new MemoryStream(rawEmlBytes);
                mimeMessage = await MimeMessage.LoadAsync(emlStream, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to parse MIME message for delivery {DeliveryId}", deliveryId);
                return BadRequest(new { error = "Malformed RFC822/MIME message" });
            }

            // Extract message attributes
            string senderFrom = mimeMessage.From.Mailboxes.FirstOrDefault()?.Address 
                             ?? payload.From 
                             ?? string.Empty;

            var recipientList = new List<string>();
            foreach (var mb in mimeMessage.To.Mailboxes)
            {
                if (!string.IsNullOrWhiteSpace(mb.Address)) recipientList.Add(mb.Address);
            }
            if (recipientList.Count == 0 && payload.To != null)
            {
                recipientList.AddRange(payload.To.Where(t => !string.IsNullOrWhiteSpace(t)));
            }

            string subject = mimeMessage.Subject ?? "(No Subject)";
            string textBody = mimeMessage.TextBody ?? string.Empty;
            string htmlBody = mimeMessage.HtmlBody ?? string.Empty;

            if (string.IsNullOrWhiteSpace(textBody) && !string.IsNullOrWhiteSpace(htmlBody))
            {
                textBody = Regex.Replace(htmlBody, "<.*?>", string.Empty).Trim();
            }

            string rfcMessageId = mimeMessage.MessageId 
                               ?? $"<cf-{deliveryId}@{senderFrom.Split('@').LastOrDefault() ?? "e-verland.site"}>";

            // 8. Resolve Recipient Mailbox in DB
            MailboxResolutionResult? resolvedMailbox = null;
            foreach (var recipient in recipientList)
            {
                var res = await _mailboxResolver.ResolveMailboxByAddressAsync(recipient, cancellationToken);
                if (res.IsResolved)
                {
                    resolvedMailbox = res;
                    break;
                }
            }

            if (resolvedMailbox == null || !resolvedMailbox.IsResolved)
            {
                _logger.LogWarning("Recipient mailbox could not be resolved for Cloudflare delivery {DeliveryId} (Recipients: {Recipients})",
                    deliveryId, string.Join(", ", recipientList));
                return NotFound(new { error = "Recipient mailbox not configured in Aurora", recipients = recipientList });
            }

            // 9. Resolve or Create Thread
            string cleanSubject = Regex.Replace(subject, @"^(Re|Fwd|Fw):\s*", "", RegexOptions.IgnoreCase).Trim();
            string snippet = !string.IsNullOrWhiteSpace(textBody) 
                ? (textBody.Length > 200 ? textBody[..200] : textBody)
                : subject;

            var thread = await _dbContext.EmailThreads
                .IgnoreQueryFilters()
                .Where(t => t.TenantId == resolvedMailbox.TenantId 
                         && t.MailboxId == resolvedMailbox.MailboxId 
                         && t.Status != ThreadStatus.Resolved)
                .OrderByDescending(t => t.LastMessageAt)
                .FirstOrDefaultAsync(t => t.Subject == subject || t.Subject == cleanSubject, cancellationToken);

            if (thread == null)
            {
                thread = new EmailThread
                {
                    TenantId = resolvedMailbox.TenantId,
                    MailboxId = resolvedMailbox.MailboxId,
                    Subject = subject,
                    Snippet = snippet,
                    Status = ThreadStatus.Unassigned,
                    Priority = Domain.Enums.ThreadPriority.Normal,
                    MessageCount = 1,
                    LastMessageAt = DateTimeOffset.UtcNow,
                    CreatedAt = DateTimeOffset.UtcNow
                };

                if (!string.IsNullOrEmpty(senderFrom) && !thread.Participants.Contains(senderFrom))
                {
                    thread.Participants.Add(senderFrom);
                }
                if (!string.IsNullOrEmpty(resolvedMailbox.FullAddress) && !thread.Participants.Contains(resolvedMailbox.FullAddress))
                {
                    thread.Participants.Add(resolvedMailbox.FullAddress);
                }

                _dbContext.EmailThreads.Add(thread);
                await _dbContext.SaveChangesAsync(cancellationToken);
            }
            else
            {
                thread.Snippet = snippet;
                thread.MessageCount++;
                thread.LastMessageAt = DateTimeOffset.UtcNow;
                if (!string.IsNullOrEmpty(senderFrom) && !thread.Participants.Contains(senderFrom))
                {
                    thread.Participants.Add(senderFrom);
                }
                await _dbContext.SaveChangesAsync(cancellationToken);
            }

            // 10. Run Inbound Pipeline (ClamAV, AI Phishing / Governance, Classification, R2, DB Persistence)
            var pipelineContext = new InboundPipelineContext
            {
                TenantId = resolvedMailbox.TenantId,
                SenderAddress = senderFrom,
                Subject = subject,
                RawEmlBytes = rawEmlBytes,
                ParsedMimeMessage = mimeMessage
            };

            foreach (var to in recipientList)
            {
                pipelineContext.RecipientAddresses.Add(to);
            }

            pipelineContext.ProcessedMessage.TenantId = resolvedMailbox.TenantId;
            pipelineContext.ProcessedMessage.MailboxId = resolvedMailbox.MailboxId;
            pipelineContext.ProcessedMessage.ThreadId = thread.Id;
            pipelineContext.ProcessedMessage.SourceEventId = deliveryId;
            pipelineContext.ProcessedMessage.MessageId = rfcMessageId;
            pipelineContext.ProcessedMessage.SenderAddress = senderFrom;
            pipelineContext.ProcessedMessage.RecipientAddresses = recipientList;
            pipelineContext.ProcessedMessage.Subject = subject;
            pipelineContext.ProcessedMessage.BodyText = textBody;
            pipelineContext.ProcessedMessage.BodyHtml = htmlBody;
            pipelineContext.ProcessedMessage.ReceivedAt = mimeMessage.Date != default ? mimeMessage.Date : DateTimeOffset.UtcNow;
            pipelineContext.ProcessedMessage.InReplyTo = mimeMessage.InReplyTo;
            pipelineContext.ProcessedMessage.References = mimeMessage.References.Count > 0 ? string.Join(" ", mimeMessage.References) : null;

            await _pipelineRunner.RunAsync(pipelineContext, cancellationToken);

            _logger.LogInformation("Inbound source = Cloudflare, DeliveryId = {DeliveryId}, Recipient = {Recipient}, MessageId = {MessageId}, ThreadId = {ThreadId}",
                deliveryId, string.Join(", ", recipientList), rfcMessageId, thread.Id);

            return Ok(new
            {
                status = "accepted",
                deliveryId = deliveryId,
                messageId = pipelineContext.ProcessedMessage.Id.ToString(),
                rfcMessageId = rfcMessageId,
                threadId = thread.Id.ToString()
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing Cloudflare inbound email for delivery {DeliveryId}: {Message}", deliveryId, ex.Message);
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                error = "Inbound pipeline execution failed",
                deliveryId = deliveryId,
                message = ex.Message,
                type = ex.GetType().Name,
                innerError = ex.InnerException?.Message
            });
        }
    }
}

public record CloudflareInboundPayload
{
    [JsonPropertyName("deliveryId")]
    public string? DeliveryId { get; init; }

    [JsonPropertyName("timestamp")]
    public long Timestamp { get; init; }

    [JsonPropertyName("from")]
    public string? From { get; init; }

    [JsonPropertyName("to")]
    public List<string>? To { get; init; }

    [JsonPropertyName("rawEmailBase64")]
    public string RawEmailBase64 { get; init; } = string.Empty;
}
