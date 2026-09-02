using System;
using System.Linq;
using System.Threading.Tasks;
using Asp.Versioning;
using Audit.Grpc;
using Grpc.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Shared.Security;

namespace AdminBff.Controllers;

/// <summary>
/// Quản lý tra cứu Audit Logs ở cấp độ Tenant Admin.
/// Route: /api/v1/admin/audit-logs
/// Yêu cầu role: TENANT_ADMIN (được bảo vệ bởi AdminControllerBase).
/// Tenant isolation được enforce nghiêm ngặt từ context của current user (JWT).
/// </summary>
[ApiVersion("1.0")]
public class AuditLogsController(
    AuditLogService.AuditLogServiceClient auditClient,
    ICurrentUserService currentUser,
    ILogger<AuditLogsController> logger) : AdminControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAdminAuditLogs(
        [FromQuery] string? userId = null,
        [FromQuery] int page = 0,
        [FromQuery] int size = 20,
        [FromQuery] int? limit = null,
        [FromQuery] string? tenantId = null)
    {
        if (!currentUser.TenantId.HasValue)
        {
            return Forbid();
        }

        string authenticatedTenantId = currentUser.TenantId.Value.ToString();

        // Enforce strict tenant isolation: client cannot pass another tenantId
        if (!string.IsNullOrWhiteSpace(tenantId) &&
            !string.Equals(tenantId.Trim(), authenticatedTenantId, StringComparison.OrdinalIgnoreCase))
        {
            logger.LogWarning(
                "Tenant isolation violation attempt: User {UserId} in tenant {AuthTenant} attempted to query tenant {RequestedTenant}",
                currentUser.UserId, authenticatedTenantId, tenantId);
            return Forbid();
        }

        int effectiveLimit = limit ?? size;
        if (effectiveLimit <= 0) effectiveLimit = 20;
        int effectivePage = page >= 0 ? page : 0;

        var request = new GetAdminAuditLogsRequest
        {
            TenantId = authenticatedTenantId,
            UserId = userId ?? string.Empty,
            Page = effectivePage,
            Limit = effectiveLimit
        };

        try
        {
            var response = await auditClient.GetAdminAuditLogsAsync(request);

            var items = response.Logs.Select(MapAuditLogResponse).ToList();

            return Ok(items);
        }
        catch (RpcException ex)
        {
            logger.LogError(ex, "Failed to fetch admin audit logs via gRPC: {Status}", ex.Status);
            return StatusCode((int)ex.StatusCode switch
            {
                (int)Grpc.Core.StatusCode.InvalidArgument => StatusCodes.Status400BadRequest,
                (int)Grpc.Core.StatusCode.PermissionDenied => StatusCodes.Status403Forbidden,
                (int)Grpc.Core.StatusCode.NotFound => StatusCodes.Status404NotFound,
                _ => StatusCodes.Status502BadGateway
            }, new { detail = ex.Status.Detail });
        }
    }

    private static object MapAuditLogResponse(AuditLogMessage log) => new
    {
        id = log.Id,
        serviceName = log.ServiceName,
        eventType = log.EventType,
        tenantId = log.TenantId,
        userId = log.UserId,
        userRole = log.UserRole,
        resourceId = log.ResourceId,
        payloadJson = log.PayloadJson,
        ipAddress = log.IpAddress,
        createdAt = log.CreatedAt?.ToDateTime()
    };
}
