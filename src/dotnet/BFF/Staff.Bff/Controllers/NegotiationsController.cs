using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Asp.Versioning;
using Grpc.Core;
using Microsoft.AspNetCore.Mvc;
using BuildingBlocks.BFF.Attributes;
using BuildingBlocks.BFF.Extensions;
using BuildingBlocks.BFF.Mail.Clients;
using BuildingBlocks.BFF.Mail.Models;
using Negotiation.Grpc;
using Shared.Constants;
using Shared.Security;

namespace StaffBff.Controllers;

/// <summary>
/// Quản lý Bidding/Negotiation và tạo Mail Draft từ AI Suggested Reply (Human-in-the-Loop MVP).
/// Route: /api/v1/negotiations
/// </summary>
[ApiVersion("1.0")]
public class NegotiationsController(
    NegotiationService.NegotiationServiceClient negotiationClient,
    IMailServiceClient mailClient,
    ICurrentUserService currentUser,
    ILogger<NegotiationsController> logger) : StaffControllerBase
{
    /// <summary>
    /// Staff clicks [Create Mail Draft] to turn an approved Negotiation Suggestion into a real threaded Mail Draft.
    /// Inbound mail never auto-creates drafts; this explicit Staff action is mandatory.
    /// </summary>
    [HttpPost("{negotiationId}/mail-draft")]
    [RequirePermission(PermissionConstants.Mail.DraftCreate, "mail:create")]
    public async Task<IActionResult> CreateMailDraftFromNegotiation(
        [FromRoute] string negotiationId,
        [FromBody] CreateNegotiationMailDraftRequest body)
    {
        if (string.IsNullOrWhiteSpace(negotiationId))
        {
            return BadRequest(new { error = "NegotiationId is required." });
        }

        if (string.IsNullOrWhiteSpace(body.MailboxId))
        {
            return BadRequest(new { error = "MailboxId is required." });
        }

        try
        {
            // 1. Fetch persisted validated suggestion from Negotiation Agent via internal gRPC (zero AI regeneration)
            var suggestion = await negotiationClient.GetDraftSuggestionAsync(
                new GetDraftSuggestionRequest { NegotiationSessionId = negotiationId },
                cancellationToken: HttpContext.RequestAborted);

            if (!suggestion.SuggestedReplyAvailable)
            {
                return BadRequest(new { error = "No valid suggestion is available for this negotiation session." });
            }

            // 2. Resolve recipient from original inbound message if available
            var toRecipients = new List<string>();
            if (!string.IsNullOrWhiteSpace(suggestion.SourceMessageId))
            {
                try
                {
                    var sourceMsg = await mailClient.GetProcessedMessageAsync(suggestion.SourceMessageId, HttpContext.RequestAborted);
                    if (!string.IsNullOrWhiteSpace(sourceMsg.SenderAddress))
                    {
                        toRecipients.Add(sourceMsg.SenderAddress);
                    }
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Could not resolve original sender for message {MessageId}", suggestion.SourceMessageId);
                }
            }

            // 3. Construct deterministic server-controlled Draft request
            var idempotencyKey = string.IsNullOrWhiteSpace(body.IdempotencyKey)
                ? $"neg-draft-{currentUser.TenantId}-{negotiationId}"
                : body.IdempotencyKey;

            var draftReq = new CreateDraftRequest(
                MailboxId: body.MailboxId,
                AssignedStaffId: currentUser.UserId?.ToString(),
                Subject: suggestion.Subject,
                Body: suggestion.Body,
                SourceType: "NEGOTIATION",
                SourceId: negotiationId,
                IdempotencyKey: idempotencyKey,
                To: toRecipients.Count > 0 ? toRecipients : null,
                ThreadId: string.IsNullOrWhiteSpace(suggestion.SourceThreadId) ? null : suggestion.SourceThreadId,
                ReplyToMessageId: string.IsNullOrWhiteSpace(suggestion.SourceMessageId) ? null : suggestion.SourceMessageId);

            // 4. Create Draft in MailService (Threaded & linked to source message)
            var draft = await mailClient.CreateDraftAsync(draftReq, HttpContext.RequestAborted);

            logger.LogInformation(
                "Staff {UserId} created mail draft {DraftId} (Existing: {IsExisting}) for Negotiation {NegotiationId} in thread {ThreadId}",
                currentUser.UserId, draft.DraftId, draft.IsExisting, negotiationId, draft.ThreadId);

            return Created($"/api/v1/mail/drafts/{draft.DraftId}", draft);
        }
        catch (RpcException ex)
        {
            return ex.ToActionResult();
        }
    }

    [HttpGet]
    public async Task<IActionResult> ListSessions(
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        try
        {
            var response = await negotiationClient.ListSessionsAsync(
                new ListSessionsRequest
                {
                    Status = status ?? string.Empty,
                    Page = page,
                    PageSize = pageSize
                },
                cancellationToken: HttpContext.RequestAborted);

            return Ok(new
            {
                total = response.Total,
                sessions = response.Sessions.Select(s => new
                {
                    sessionId = s.SessionId,
                    shipmentId = s.ShipmentId,
                    customerId = s.CustomerId,
                    status = s.Status,
                    currentRound = s.CurrentRound,
                    maxRounds = s.MaxRounds,
                    listPrice = s.ListPrice,
                    bottomPrice = s.BottomPrice,
                    currency = s.Currency,
                    lastDecision = s.LastDecision,
                    lastApprovedAmount = s.LastApprovedAmount,
                    createdAt = s.CreatedAt?.ToDateTimeOffset(),
                    updatedAt = s.UpdatedAt?.ToDateTimeOffset()
                })
            });
        }
        catch (RpcException ex)
        {
            return ex.ToActionResult();
        }
    }

    [HttpGet("{negotiationId}")]
    public async Task<IActionResult> GetSessionHistory([FromRoute] string negotiationId)
    {
        if (string.IsNullOrWhiteSpace(negotiationId))
        {
            return BadRequest(new { error = "NegotiationId is required." });
        }

        try
        {
            var history = await negotiationClient.GetSessionHistoryAsync(
                new GetSessionHistoryRequest { SessionId = negotiationId },
                cancellationToken: HttpContext.RequestAborted);

            return Ok(new
            {
                sessionId = history.SessionId,
                tenantId = history.TenantId,
                shipmentId = history.ShipmentId,
                customerId = history.CustomerId,
                status = history.Status,
                currentRound = history.CurrentRound,
                maxRounds = history.MaxRounds,
                listPrice = history.ListPrice,
                bottomPrice = history.BottomPrice,
                currency = history.Currency,
                createdAt = history.CreatedAt?.ToDateTimeOffset(),
                updatedAt = history.UpdatedAt?.ToDateTimeOffset(),
                messages = history.Messages.Select(m => new
                {
                    id = m.Id,
                    sessionId = m.SessionId,
                    round = m.Round,
                    sender = m.Sender,
                    message = m.Message,
                    offerPrice = m.OfferPrice,
                    decision = m.Decision,
                    currency = m.Currency,
                    createdAt = m.CreatedAt?.ToDateTimeOffset()
                })
            });
        }
        catch (RpcException ex)
        {
            return ex.ToActionResult();
        }
    }

    [HttpPost("offers")]
    public async Task<IActionResult> SubmitOffer([FromBody] SubmitNegotiationOfferRequest body)
    {
        if (string.IsNullOrWhiteSpace(body.ShipmentId) || string.IsNullOrWhiteSpace(body.CustomerId) || body.OfferPrice <= 0)
        {
            return BadRequest(new { error = "ShipmentId, CustomerId and positive OfferPrice are required." });
        }

        try
        {
            var result = await negotiationClient.SubmitOfferAsync(
                new SubmitOfferRequest
                {
                    ShipmentId = body.ShipmentId,
                    CustomerId = body.CustomerId,
                    OfferPrice = body.OfferPrice,
                    ListPrice = body.ListPrice ?? 0,
                    BottomPrice = body.BottomPrice ?? 0,
                    CustomerTier = body.CustomerTier ?? "STANDARD",
                    SessionId = body.SessionId ?? string.Empty,
                    SourceMessageId = body.SourceMessageId ?? string.Empty,
                    SourceThreadId = body.SourceThreadId ?? string.Empty
                },
                cancellationToken: HttpContext.RequestAborted);

            return Ok(new
            {
                sessionId = result.SessionId,
                shipmentId = result.ShipmentId,
                round = result.Round,
                decision = result.Decision,
                counterOfferPrice = result.CounterOfferPrice,
                currency = result.Currency,
                aiSpeech = result.AiSpeech,
                status = result.Status,
                createdAt = result.CreatedAt?.ToDateTimeOffset(),
                suggestedReply = result.SuggestedReply is null ? null : new
                {
                    subjectSuggestion = result.SuggestedReply.SubjectSuggestion,
                    body = result.SuggestedReply.Body,
                    language = result.SuggestedReply.Language
                },
                suggestedReplyAvailable = result.SuggestedReplyAvailable,
                aiDraftUsed = result.AiDraftUsed,
                fallbackUsed = result.FallbackUsed
            });
        }
        catch (RpcException ex)
        {
            return ex.ToActionResult();
        }
    }
}

public sealed record SubmitNegotiationOfferRequest(
    string ShipmentId,
    string CustomerId,
    double OfferPrice,
    double? ListPrice,
    double? BottomPrice,
    string? CustomerTier,
    string? SessionId,
    string? SourceMessageId,
    string? SourceThreadId);
