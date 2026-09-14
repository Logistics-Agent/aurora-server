using System;
using System.Threading.Tasks;
using Asp.Versioning;
using Grpc.Core;
using Microsoft.AspNetCore.Mvc;
using BuildingBlocks.BFF.Attributes;
using BuildingBlocks.BFF.Extensions;
using BuildingBlocks.BFF.Mail.Clients;
using BuildingBlocks.BFF.Mail.Models;
using Shared.Constants;
using Shared.Security;

namespace SystemBff.Controllers;

/// <summary>
/// Quản trị Hệ thống Email & Dead-Letter (System Admin API).
/// Route: /api/v1/system/mail — chỉ dành cho SYSTEM_ADMIN (role gate ở SystemControllerBase).
/// </summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/system/mail")]
public class MailSystemController(
    IMailServiceClient mailClient,
    ICurrentUserService currentUser,
    ILogger<MailSystemController> logger) : SystemControllerBase
{

    [HttpPost("dead-letter/{id}/requeue")]
    [RequirePermission(PermissionConstants.Mail.SystemManage)]
    public async Task<IActionResult> RequeueDeadLetter([FromRoute] string id)
    {
        if (!Guid.TryParse(id, out _))
        {
            return BadRequest(new { detail = "Invalid ProcessedMessageId GUID format." });
        }

        try
        {
            var result = await mailClient.RequeueDeadLetterAsync(id, HttpContext.RequestAborted);
            logger.LogInformation("Dead-letter message {ProcessedMessageId} requeued by System Admin {UserId}",
                id, currentUser.UserId);

            return Ok(result);
        }
        catch (RpcException ex)
        {
            return ex.ToActionResult();
        }
    }

    [HttpGet("dead-letter")]
    [HttpGet("dead-letters")]
    public async Task<IActionResult> ListDeadLetters(
        [FromQuery] int pageSize = 50,
        [FromQuery] string? pageToken = null)
    {
        try
        {
            var boundedPageSize = Math.Clamp(pageSize, 1, 100);
            var result = await mailClient.ListProcessedMessagesAsync(
                direction: null,
                emailCategory: null,
                pipelineStatus: "DEAD_LETTER",
                pageSize: boundedPageSize,
                nextPageToken: pageToken,
                cancellationToken: HttpContext.RequestAborted);

            var messagesList = result?.Messages != null ? (object)result.Messages : System.Array.Empty<object>();
            return Ok(new
            {
                items = messagesList,
                messages = messagesList,
                nextPageToken = result?.NextPageToken ?? string.Empty
            });
        }
        catch (RpcException ex)
        {
            logger.LogWarning(ex, "gRPC error in ListDeadLetters, returning empty list: {Detail}", ex.Status.Detail);
            return Ok(new { items = System.Array.Empty<object>(), messages = System.Array.Empty<object>(), nextPageToken = string.Empty });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error in ListDeadLetters, returning empty list");
            return Ok(new { items = System.Array.Empty<object>(), messages = System.Array.Empty<object>(), nextPageToken = string.Empty });
        }
    }

    [HttpGet("audit")]
    public async Task<IActionResult> GetAuditRecords(
        [FromQuery] string? resourceType = null,
        [FromQuery] string? resourceId = null,
        [FromQuery] int pageSize = 50,
        [FromQuery] string? pageToken = null)
    {
        try
        {
            var boundedPageSize = Math.Clamp(pageSize, 1, 100);
            var result = await mailClient.GetAuditRecordsAsync(resourceType, resourceId, boundedPageSize, pageToken, HttpContext.RequestAborted);
            return Ok(result);
        }
        catch (RpcException ex)
        {
            return ex.ToActionResult();
        }
    }
}
