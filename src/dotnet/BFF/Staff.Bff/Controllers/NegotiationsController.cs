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
}
