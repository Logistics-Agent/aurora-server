using System.Threading.Tasks;
using Asp.Versioning;
using Grpc.Core;
using Microsoft.AspNetCore.Mvc;
using BuildingBlocks.BFF.Attributes;
using BuildingBlocks.BFF.Extensions;
using BuildingBlocks.BFF.Mail.Clients;
using BuildingBlocks.BFF.Mail.Models;
using BuildingBlocks.BFF.Mail.Validation;
using Shared.Constants;
using Shared.Security;

namespace StaffBff.Controllers;

/// <summary>
/// Quản lý Email & Hộp thư (Staff Mail API).
/// Route: /api/v1/mail — phân quyền chi tiết bằng [RequirePermission] module mail.
/// </summary>
[ApiVersion("1.0")]
public class MailController(
    IMailServiceClient mailClient,
    ICurrentUserService currentUser,
    ILogger<MailController> logger) : StaffControllerBase
{
    // ─── Drafts ───────────────────────────────────────────────────────────────

    [HttpPost("drafts")]
    [RequirePermission(PermissionConstants.Modules.Mail, PermissionConstants.Create)]
    public async Task<IActionResult> CreateDraft([FromBody] CreateDraftRequest body)
    {
        var validator = new CreateDraftRequestValidator();
        var validationResult = await validator.ValidateAsync(body, HttpContext.RequestAborted);
        if (!validationResult.IsValid)
        {
            return BadRequest(new { errors = validationResult.Errors.Select(e => e.ErrorMessage) });
        }

        try
        {
            var draft = await mailClient.CreateDraftAsync(body, HttpContext.RequestAborted);
            logger.LogInformation("Draft {DraftId} created by user {UserId} in tenant {TenantId}",
                draft.DraftId, currentUser.UserId, currentUser.TenantId);

            return Created($"/api/v1/mail/drafts/{draft.DraftId}", draft);
        }
        catch (RpcException ex)
        {
            return ex.ToActionResult();
        }
    }

    [HttpGet("drafts")]
    [RequirePermission(PermissionConstants.Modules.Mail, PermissionConstants.Read)]
    public async Task<IActionResult> ListDrafts(
        [FromQuery] string? mailboxId = null,
        [FromQuery] string? status = null,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? pageToken = null)
    {
        try
        {
            var boundedPageSize = Math.Clamp(pageSize, 1, 100);
            var result = await mailClient.ListDraftsAsync(mailboxId, status, boundedPageSize, pageToken, HttpContext.RequestAborted);
            return Ok(result);
        }
        catch (RpcException ex)
        {
            return ex.ToActionResult();
        }
    }

    [HttpGet("drafts/{id}")]
    [RequirePermission(PermissionConstants.Modules.Mail, PermissionConstants.Read)]
    public async Task<IActionResult> GetDraft([FromRoute] string id)
    {
        try
        {
            var draft = await mailClient.GetDraftAsync(id, HttpContext.RequestAborted);
            return Ok(draft);
        }
        catch (RpcException ex)
        {
            return ex.ToActionResult();
        }
    }

    // ─── Outbound Send ────────────────────────────────────────────────────────

    [HttpPost("messages/outbound")]
    [RequirePermission(PermissionConstants.Modules.Mail, PermissionConstants.Send)]
    public async Task<IActionResult> SubmitOutboundMessage([FromBody] SubmitOutboundMessageRequest body)
    {
        var validator = new SubmitOutboundMessageRequestValidator();
        var validationResult = await validator.ValidateAsync(body, HttpContext.RequestAborted);
        if (!validationResult.IsValid)
        {
            return BadRequest(new { errors = validationResult.Errors.Select(e => e.ErrorMessage) });
        }

        try
        {
            var result = await mailClient.SubmitOutboundMessageAsync(body, HttpContext.RequestAborted);
            logger.LogInformation("Outbound message submitted: {ProcessedMessageId} (QueueId: {QueueId}) by user {UserId}",
                result.ProcessedMessageId, result.StalwartQueueId, currentUser.UserId);

            return Ok(result);
        }
        catch (RpcException ex)
        {
            return ex.ToActionResult();
        }
    }

    // ─── Processed Messages ───────────────────────────────────────────────────

    [HttpGet("messages")]
    [RequirePermission(PermissionConstants.Modules.Mail, PermissionConstants.Read)]
    public async Task<IActionResult> ListProcessedMessages(
        [FromQuery] string? direction = null,
        [FromQuery] string? emailCategory = null,
        [FromQuery] string? pipelineStatus = null,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? pageToken = null)
    {
        try
        {
            var boundedPageSize = Math.Clamp(pageSize, 1, 100);
            var result = await mailClient.ListProcessedMessagesAsync(direction, emailCategory, pipelineStatus, boundedPageSize, pageToken, HttpContext.RequestAborted);
            return Ok(result);
        }
        catch (RpcException ex)
        {
            return ex.ToActionResult();
        }
    }

    [HttpGet("messages/{id}")]
    [RequirePermission(PermissionConstants.Modules.Mail, PermissionConstants.Read)]
    public async Task<IActionResult> GetProcessedMessage([FromRoute] string id)
    {
        try
        {
            var message = await mailClient.GetProcessedMessageAsync(id, HttpContext.RequestAborted);
            return Ok(message);
        }
        catch (RpcException ex)
        {
            return ex.ToActionResult();
        }
    }

    // ─── Quarantine Operations ────────────────────────────────────────────────

    [HttpGet("quarantine")]
    [RequirePermission(PermissionConstants.Modules.Mail, PermissionConstants.Read)]
    public async Task<IActionResult> ListQuarantineRecords(
        [FromQuery] string? status = null,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? pageToken = null)
    {
        try
        {
            var boundedPageSize = Math.Clamp(pageSize, 1, 100);
            var result = await mailClient.ListQuarantineRecordsAsync(status, boundedPageSize, pageToken, HttpContext.RequestAborted);
            return Ok(result);
        }
        catch (RpcException ex)
        {
            return ex.ToActionResult();
        }
    }

    [HttpGet("quarantine/{id}")]
    [RequirePermission(PermissionConstants.Modules.Mail, PermissionConstants.Read)]
    public async Task<IActionResult> GetQuarantineRecord([FromRoute] string id)
    {
        try
        {
            var record = await mailClient.GetQuarantineRecordAsync(id, HttpContext.RequestAborted);
            return Ok(record);
        }
        catch (RpcException ex)
        {
            return ex.ToActionResult();
        }
    }

    [HttpPost("quarantine/{id}/release")]
    [RequirePermission(PermissionConstants.Modules.Mail, PermissionConstants.Release)]
    public async Task<IActionResult> ReleaseQuarantine([FromRoute] string id)
    {
        try
        {
            var result = await mailClient.ReleaseQuarantineAsync(id, HttpContext.RequestAborted);
            logger.LogInformation("Quarantine record {QuarantineId} released by user {UserId}", id, currentUser.UserId);
            return Ok(result);
        }
        catch (RpcException ex)
        {
            return ex.ToActionResult();
        }
    }
}
