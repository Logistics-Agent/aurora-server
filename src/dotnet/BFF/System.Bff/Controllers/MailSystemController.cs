using System;
using System.Threading.Tasks;
using Asp.Versioning;
using Grpc.Core;
using Microsoft.AspNetCore.Mvc;
using BuildingBlocks.BFF.Extensions;
using BuildingBlocks.BFF.Mail.Clients;
using BuildingBlocks.BFF.Mail.Models;
using Shared.Security;

namespace SystemBff.Controllers;

/// <summary>
/// Quản trị Hệ thống Email & Dead-Letter (System Admin API).
/// Route: /api/v1/system/mail — chỉ dành cho SYSTEM_ADMIN (role gate ở SystemControllerBase).
/// </summary>
[ApiVersion("1.0")]
public class MailSystemController(
    IMailServiceClient mailClient,
    ICurrentUserService currentUser,
    ILogger<MailSystemController> logger) : SystemControllerBase
{
    [HttpPost("dead-letter/{id}/requeue")]
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
