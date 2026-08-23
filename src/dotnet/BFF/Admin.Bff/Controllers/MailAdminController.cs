using System;
using System.Linq;
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

namespace AdminBff.Controllers;

/// <summary>
/// Quản trị Mail Domain, Mailbox, Alias & Audit (Tenant Admin API).
/// Route: /api/v1/admin/mail — yêu cầu role TENANT_ADMIN + [RequirePermission] module mail.
/// </summary>
[ApiVersion("1.0")]
public class MailAdminController(
    IMailServiceClient mailClient,
    ICurrentUserService currentUser,
    ILogger<MailAdminController> logger) : AdminControllerBase
{
    // ─── Domain Provisioning ──────────────────────────────────────────────────

    [HttpPost("domains")]
    [RequirePermission(PermissionConstants.Modules.Mail, PermissionConstants.Create)]
    public async Task<IActionResult> ProvisionDomain([FromBody] ProvisionDomainRequest body)
    {
        var validator = new ProvisionDomainRequestValidator();
        var validationResult = await validator.ValidateAsync(body, HttpContext.RequestAborted);
        if (!validationResult.IsValid)
        {
            return BadRequest(new { errors = validationResult.Errors.Select(e => e.ErrorMessage) });
        }

        try
        {
            var result = await mailClient.ProvisionDomainAsync(body, HttpContext.RequestAborted);
            logger.LogInformation("Mail domain {DomainName} provisioned for tenant {TenantId} by {UserId}",
                result.DomainName, currentUser.TenantId, currentUser.UserId);

            return Created($"/api/v1/admin/mail/domains/{result.DomainId}", result);
        }
        catch (RpcException ex)
        {
            return ex.ToActionResult();
        }
    }

    // ─── Mailbox Management ───────────────────────────────────────────────────

    [HttpPost("mailboxes")]
    [RequirePermission(PermissionConstants.Modules.Mail, PermissionConstants.Create)]
    public async Task<IActionResult> CreateMailbox([FromBody] CreateMailboxRequest body)
    {
        var validator = new CreateMailboxRequestValidator();
        var validationResult = await validator.ValidateAsync(body, HttpContext.RequestAborted);
        if (!validationResult.IsValid)
        {
            return BadRequest(new { errors = validationResult.Errors.Select(e => e.ErrorMessage) });
        }

        try
        {
            var result = await mailClient.CreateMailboxAsync(body, HttpContext.RequestAborted);
            logger.LogInformation("Mailbox {FullAddress} created for tenant {TenantId} by {UserId}",
                result.FullAddress, currentUser.TenantId, currentUser.UserId);

            return Created($"/api/v1/admin/mail/mailboxes/{result.MailboxId}", result);
        }
        catch (RpcException ex)
        {
            return ex.ToActionResult();
        }
    }

    [HttpPost("aliases")]
    [RequirePermission(PermissionConstants.Modules.Mail, PermissionConstants.Create)]
    public async Task<IActionResult> CreateAlias([FromBody] CreateAliasRequest body)
    {
        var validator = new CreateAliasRequestValidator();
        var validationResult = await validator.ValidateAsync(body, HttpContext.RequestAborted);
        if (!validationResult.IsValid)
        {
            return BadRequest(new { errors = validationResult.Errors.Select(e => e.ErrorMessage) });
        }

        try
        {
            var result = await mailClient.CreateAliasAsync(body, HttpContext.RequestAborted);
            logger.LogInformation("Alias {AliasAddress} created for tenant {TenantId} by {UserId}",
                body.AliasAddress, currentUser.TenantId, currentUser.UserId);

            return Created($"/api/v1/admin/mail/aliases/{result.AliasId}", result);
        }
        catch (RpcException ex)
        {
            return ex.ToActionResult();
        }
    }

    [HttpPost("mailboxes/{id}/reset-password")]
    [RequirePermission(PermissionConstants.Modules.Mail, PermissionConstants.Update)]
    public async Task<IActionResult> ResetPassword([FromRoute] string id)
    {
        try
        {
            var result = await mailClient.ResetPasswordAsync(id, HttpContext.RequestAborted);
            return Ok(result);
        }
        catch (RpcException ex)
        {
            return ex.ToActionResult();
        }
    }

    // ─── Quarantine Administration ────────────────────────────────────────────

    [HttpDelete("quarantine/{id}")]
    [RequirePermission(PermissionConstants.Modules.Mail, PermissionConstants.Delete)]
    public async Task<IActionResult> DeleteQuarantine([FromRoute] string id)
    {
        try
        {
            var result = await mailClient.DeleteQuarantineAsync(id, HttpContext.RequestAborted);
            logger.LogInformation("Quarantine record {QuarantineId} deleted by admin {UserId}", id, currentUser.UserId);
            return Ok(result);
        }
        catch (RpcException ex)
        {
            return ex.ToActionResult();
        }
    }

    // ─── Audit Trail ──────────────────────────────────────────────────────────

    [HttpGet("audit")]
    [RequirePermission(PermissionConstants.Modules.Mail, PermissionConstants.Read)]
    public async Task<IActionResult> GetAuditRecords(
        [FromQuery] string? resourceType = null,
        [FromQuery] string? resourceId = null,
        [FromQuery] int pageSize = 20,
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
