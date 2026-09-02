using System;
using System.Linq;
using System.Threading.Tasks;
using Asp.Versioning;
using Audit.Grpc;
using Grpc.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace SystemBff.Controllers;

/// <summary>
/// Quản lý tra cứu Audit Logs ở cấp độ System (cross-tenant).
/// Route: /api/v1/system/audit-logs
/// Yêu cầu role: SYSTEM_ADMIN (được bảo vệ bởi SystemControllerBase).
/// </summary>
[ApiVersion("1.0")]
public class AuditLogsController(
    AuditLogService.AuditLogServiceClient auditClient,
    ILogger<AuditLogsController> logger) : SystemControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetSystemAuditLogs(
        [FromQuery] string? serviceName = null,
        [FromQuery] string? eventType = null,
        [FromQuery] string? tenantId = null,
        [FromQuery] string? userId = null,
        [FromQuery] int page = 0,
        [FromQuery] int size = 20,
        [FromQuery] int? limit = null)
    {
        int effectiveLimit = limit ?? size;
        if (effectiveLimit <= 0) effectiveLimit = 20;
        int effectivePage = page >= 0 ? page : 0;

        var request = new GetSystemAuditLogsRequest
        {
            ServiceName = serviceName ?? string.Empty,
            EventType = eventType ?? string.Empty,
            TenantId = tenantId ?? string.Empty,
            UserId = userId ?? string.Empty,
            Page = effectivePage,
            Limit = effectiveLimit
        };

        try
        {
            var response = await auditClient.GetSystemAuditLogsAsync(request);

            var items = response.Logs.Select(MapAuditLogResponse).ToList();

            return Ok(items);
        }
        catch (RpcException ex)
        {
            logger.LogError(ex, "Failed to fetch system audit logs via gRPC: {Status}", ex.Status);
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
