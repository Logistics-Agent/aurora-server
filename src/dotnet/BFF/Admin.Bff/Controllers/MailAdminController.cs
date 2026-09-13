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
[Route("api/v{version:apiVersion}/admin/mail")]
[Route("api/v{version:apiVersion}/admin/[controller]")]
public class MailAdminController(
    IMailServiceClient mailClient,
    ICurrentUserService currentUser,
    ILogger<MailAdminController> logger) : AdminControllerBase
{
    // ─── Domain Provisioning ──────────────────────────────────────────────────

    [HttpPost("domains")]
    [RequirePermission(PermissionConstants.Mail.DomainManage, "mail:create")]
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
            logger.LogWarning(ex, "gRPC error in ProvisionDomain: {Detail}", ex.Status.Detail);
            return ex.ToActionResult();
        }
    }

    [HttpPost("domains/{domainId}/verify")]
    [RequirePermission(PermissionConstants.Mail.DomainManage, "mail:update")]
    public async Task<IActionResult> VerifyDomain(string domainId)
    {
        if (!Guid.TryParse(domainId, out _)) return BadRequest(new { message = "Invalid domain ID." });
        try
        {
            var result = await mailClient.VerifyDomainAsync(domainId, HttpContext.RequestAborted);
            logger.LogInformation("Mail domain {DomainId} verification completed with status {Status} for tenant {TenantId}", domainId, result.Status, currentUser.TenantId);
            return Ok(result);
        }
        catch (RpcException ex)
        {
            logger.LogWarning(ex, "gRPC error in VerifyDomain: {Detail}", ex.Status.Detail);
            return ex.ToActionResult();
        }
    }

    // ─── Mailbox Management ──────────────────────────────────────────────────

    [HttpPost("mailboxes")]
    [RequirePermission(PermissionConstants.Mail.MailboxManage, "mail:create")]
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
            logger.LogWarning(ex, "gRPC error in CreateMailbox: {Detail}", ex.Status.Detail);
            return ex.ToActionResult();
        }
    }

    [HttpPost("aliases")]
    [RequirePermission(PermissionConstants.Mail.MailboxManage, "mail:create")]
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
            logger.LogWarning(ex, "gRPC error in CreateAlias: {Detail}", ex.Status.Detail);
            return ex.ToActionResult();
        }
    }

    [HttpDelete("aliases/{id}")]
    [RequirePermission(PermissionConstants.Mail.MailboxManage, "mail:delete", "mail:manage")]
    public async Task<IActionResult> DeleteAlias([FromRoute] string id)
    {
        try
        {
            var result = await mailClient.DeleteAliasAsync(id, HttpContext.RequestAborted);
            logger.LogInformation("Alias {AliasId} deleted for tenant {TenantId} by {UserId}",
                id, currentUser.TenantId, currentUser.UserId);
            return Ok(result);
        }
        catch (RpcException ex)
        {
            logger.LogWarning(ex, "gRPC error in DeleteAlias: {Detail}", ex.Status.Detail);
            return ex.ToActionResult();
        }
    }

    [HttpPost("mailboxes/{id}/reset-password")]
    [RequirePermission(PermissionConstants.Mail.MailboxManage, "mail:update")]
    public async Task<IActionResult> ResetPassword([FromRoute] string id)
    {
        try
        {
            var result = await mailClient.ResetPasswordAsync(id, HttpContext.RequestAborted);
            return Ok(result);
        }
        catch (RpcException ex)
        {
            logger.LogWarning(ex, "gRPC error in ResetPassword: {Detail}", ex.Status.Detail);
            return ex.ToActionResult();
        }
    }

    [HttpGet("domains")]
    [RequirePermission(PermissionConstants.Mail.DomainManage, "mail:read")]
    public async Task<IActionResult> ListDomains(
        [FromQuery] int pageSize = 100,
        [FromQuery] string? pageToken = null)
    {
        try
        {
            var boundedPageSize = Math.Clamp(pageSize, 1, 100);
            var result = await mailClient.ListDomainsAsync(boundedPageSize, pageToken, HttpContext.RequestAborted);
            return Ok(result);
        }
        catch (RpcException ex)
        {
            logger.LogWarning(ex, "gRPC error in ListDomains, returning empty list: {Detail}", ex.Status.Detail);
            return Ok(new { domains = Array.Empty<object>(), nextPageToken = string.Empty });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error in ListDomains, returning empty list");
            return Ok(new { domains = Array.Empty<object>(), nextPageToken = string.Empty });
        }
    }

    [HttpGet("mailboxes")]
    [RequirePermission(PermissionConstants.Mail.MailboxManage, "mail:read")]
    public async Task<IActionResult> ListMailboxes(
        [FromQuery] string? domainId = null,
        [FromQuery] int pageSize = 100,
        [FromQuery] string? pageToken = null)
    {
        try
        {
            var boundedPageSize = Math.Clamp(pageSize, 1, 100);
            var result = await mailClient.ListMailboxesAsync(domainId, boundedPageSize, pageToken, HttpContext.RequestAborted);
            return Ok(result);
        }
        catch (RpcException ex)
        {
            logger.LogWarning(ex, "gRPC error in ListMailboxes, returning empty list: {Detail}", ex.Status.Detail);
            return Ok(new { mailboxes = Array.Empty<object>(), nextPageToken = string.Empty });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error in ListMailboxes, returning empty list");
            return Ok(new { mailboxes = Array.Empty<object>(), nextPageToken = string.Empty });
        }
    }

    [HttpGet("aliases")]
    [RequirePermission(PermissionConstants.Mail.MailboxManage, "mail:read")]
    public async Task<IActionResult> ListAliases(
        [FromQuery] string? domainId = null,
        [FromQuery] int pageSize = 100,
        [FromQuery] string? pageToken = null)
    {
        try
        {
            var boundedPageSize = Math.Clamp(pageSize, 1, 100);
            var result = await mailClient.ListAliasesAsync(domainId, boundedPageSize, pageToken, HttpContext.RequestAborted);
            return Ok(result);
        }
        catch (RpcException ex)
        {
            logger.LogWarning(ex, "gRPC error in ListAliases, returning empty list: {Detail}", ex.Status.Detail);
            return Ok(new { aliases = Array.Empty<object>(), nextPageToken = string.Empty });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error in ListAliases, returning empty list");
            return Ok(new { aliases = Array.Empty<object>(), nextPageToken = string.Empty });
        }
    }

    // ─── Quarantine Administration ────────────────────────────────────────────

    [HttpGet("quarantine")]
    [RequirePermission(PermissionConstants.Mail.QuarantineRead, "mail:read")]
    public async Task<IActionResult> ListQuarantine(
        [FromQuery] string? status = null,
        [FromQuery] int pageSize = 50,
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
            logger.LogWarning(ex, "gRPC error in ListQuarantine, returning empty list: {Detail}", ex.Status.Detail);
            return Ok(new { records = Array.Empty<object>(), nextPageToken = string.Empty });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error in ListQuarantine, returning empty list");
            return Ok(new { records = Array.Empty<object>(), nextPageToken = string.Empty });
        }
    }

    [HttpDelete("quarantine/{id}")]
    [RequirePermission(PermissionConstants.Mail.QuarantineDelete, "mail:delete")]
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
    [RequirePermission(PermissionConstants.Mail.AuditRead, "mail:read")]
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
            logger.LogWarning(ex, "gRPC error querying mail audit records, returning empty list: {Detail}", ex.Status.Detail);
            return Ok(new { records = Array.Empty<object>(), nextPageToken = string.Empty });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error querying mail audit records, returning empty list");
            return Ok(new { records = Array.Empty<object>(), nextPageToken = string.Empty });
        }
    }
}
