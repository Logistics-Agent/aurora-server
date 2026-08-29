using Asp.Versioning;
using BuildingBlocks.BFF.Attributes;
using Grpc.Core;
using Microsoft.AspNetCore.Mvc;
using RoutePlanningAgent.Grpc;
using Shared.Constants;
using Shared.Security;

namespace StaffBff.Controllers;

/// <summary>
/// Duyệt/từ chối các approval request của Route Planning (human-in-the-loop).
/// Route: /api/v1/approvals
/// </summary>
[ApiVersion("1.0")]
public class ApprovalsController(
    RoutePlanningService.RoutePlanningServiceClient routeClient,
    ICurrentUserService currentUser,
    ILogger<ApprovalsController> logger) : StaffControllerBase
{
    [HttpGet("pending")]
    [RequirePermission(PermissionConstants.RoutePlanning.ApprovalRead, "route_planning:read")]
    public async Task<IActionResult> ListPendingApprovals([FromQuery] int page = 1, [FromQuery] int limit = 10)
    {
        var response = await routeClient.ListPendingApprovalsAsync(new ListPendingApprovalsRequest
        {
            Page  = page,
            Limit = limit
        });

        return Ok(new
        {
            Items = response.Approvals.Select(MapApprovalResponse),
            response.Page,
            response.Limit,
            response.TotalItems,
            response.TotalPages
        });
    }

    /// <summary>Phê duyệt approval request — route chuyển sang Ready.</summary>
    [HttpPost("{id}/approve")]
    [RequirePermission(PermissionConstants.RoutePlanning.Approve, "route_planning:update")]
    public async Task<IActionResult> Approve([FromRoute] string id, [FromBody] ApproveBody? body)
    {
        try
        {
            var response = await routeClient.ApproveRouteAsync(new ApproveRouteRequest
            {
                ApprovalId = id,
                IsApproved = true,
                Comment    = body?.Comment ?? string.Empty
            });

            logger.LogInformation(
                "Approval {ApprovalId} APPROVED in tenant {TenantId} by {UserId}",
                id, currentUser.TenantId, currentUser.UserId);

            return Ok(MapApprovalResponse(response));
        }
        catch (RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.NotFound)
        {
            return NotFound(new { detail = $"Approval request '{id}' not found." });
        }
        catch (RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.AlreadyExists)
        {
            return Conflict(new { detail = ex.Status.Detail });
        }
    }

    /// <summary>Từ chối approval request — BẮT BUỘC kèm reason; route chuyển sang Cancelled.</summary>
    [HttpPost("{id}/reject")]
    [RequirePermission(PermissionConstants.RoutePlanning.Reject, "route_planning:update")]
    public async Task<IActionResult> Reject([FromRoute] string id, [FromBody] RejectBody body)
    {
        if (string.IsNullOrWhiteSpace(body.Reason))
        {
            return BadRequest(new { detail = "Reason là bắt buộc khi reject approval request." });
        }

        try
        {
            var response = await routeClient.RejectRouteAsync(new RejectRouteRequest
            {
                ApprovalId = id,
                Reason     = body.Reason,
                Comment    = body.Comment ?? string.Empty
            });

            logger.LogInformation(
                "Approval {ApprovalId} REJECTED (reason: {Reason}) in tenant {TenantId} by {UserId}",
                id, body.Reason, currentUser.TenantId, currentUser.UserId);

            return Ok(MapApprovalResponse(response));
        }
        catch (RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.NotFound)
        {
            return NotFound(new { detail = $"Approval request '{id}' not found." });
        }
        catch (RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.AlreadyExists)
        {
            return Conflict(new { detail = ex.Status.Detail });
        }
        catch (RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.InvalidArgument)
        {
            return BadRequest(new { detail = ex.Status.Detail });
        }
    }

    // --- DTOs ---
    public record ApproveBody(string? Comment);
    public record RejectBody(string Reason, string? Comment);

    private static object MapApprovalResponse(ApprovalResponse r) => new
    {
        r.Id,
        r.RouteId,
        r.RouteName,
        r.Status,
        r.Reason,
        r.AiSummary,
        r.ComplianceSummary,
        RejectionReason = string.IsNullOrEmpty(r.RejectionReason) ? null : r.RejectionReason,
        r.CreatedAt
    };
}
