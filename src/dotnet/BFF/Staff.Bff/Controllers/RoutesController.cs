using Asp.Versioning;
using BuildingBlocks.BFF.Attributes;
using Grpc.Core;
using Microsoft.AspNetCore.Mvc;
using RoutePlanningAgent.Grpc;
using Shared.Constants;
using Shared.Security;

namespace StaffBff.Controllers;

/// <summary>
/// Quản lý tuyến vận chuyển (Route Planning Agent).
/// Route: /api/v1/routes — phân quyền chi tiết bằng [RequirePermission] module route_planning.
/// </summary>
[ApiVersion("1.0")]
public class RoutesController(
    RoutePlanningService.RoutePlanningServiceClient routeClient,
    ICurrentUserService currentUser,
    ILogger<RoutesController> logger) : StaffControllerBase
{
    [HttpPost]
    [RequirePermission(PermissionConstants.Modules.RoutePlanning, PermissionConstants.Create)]
    public async Task<IActionResult> CreateRoute([FromBody] CreateRouteBody body)
    {
        try
        {
            var request = new CreateRouteRequest
            {
                Name                     = body.Name,
                Description              = body.Description ?? string.Empty,
                RouteType                = body.RouteType,
                MaxWeightKg              = body.MaxWeightKg,
                MaxVolumeM3              = body.MaxVolumeM3,
                EstimatedDistanceKm      = body.EstimatedDistanceKm ?? 0,
                EstimatedDurationMinutes = body.EstimatedDurationMinutes ?? 0
            };
            request.Stops.AddRange(body.Stops.Select(MapStopInput));

            var response = await routeClient.CreateRouteAsync(request);

            logger.LogInformation(
                "Route '{RouteName}' created in tenant {TenantId} by {UserId}",
                body.Name, currentUser.TenantId, currentUser.UserId);

            return Created($"/api/v1/routes/{response.Id}", MapRouteResponse(response));
        }
        catch (RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.InvalidArgument)
        {
            return BadRequest(new { detail = ex.Status.Detail });
        }
    }

    [HttpGet]
    [RequirePermission(PermissionConstants.Modules.RoutePlanning, PermissionConstants.Read)]
    public async Task<IActionResult> ListRoutes(
        [FromQuery] int page = 1, [FromQuery] int limit = 10, [FromQuery] string? status = null)
    {
        try
        {
            var response = await routeClient.ListRoutesAsync(new ListRoutesRequest
            {
                Page   = page,
                Limit  = limit,
                Status = status ?? string.Empty
            });

            return Ok(new
            {
                Items = response.Routes.Select(MapRouteResponse),
                response.Page,
                response.Limit,
                response.TotalItems,
                response.TotalPages
            });
        }
        catch (RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.InvalidArgument)
        {
            return BadRequest(new { detail = ex.Status.Detail });
        }
    }

    [HttpGet("{id}")]
    [RequirePermission(PermissionConstants.Modules.RoutePlanning, PermissionConstants.Read)]
    public async Task<IActionResult> GetRoute([FromRoute] string id)
    {
        try
        {
            var response = await routeClient.GetRouteAsync(new GetRouteRequest { Id = id });
            return Ok(MapRouteResponse(response));
        }
        catch (RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.NotFound)
        {
            return NotFound(new { detail = $"Route '{id}' not found." });
        }
    }

    [HttpPut("{id}")]
    [RequirePermission(PermissionConstants.Modules.RoutePlanning, PermissionConstants.Update)]
    public async Task<IActionResult> UpdateRoute([FromRoute] string id, [FromBody] UpdateRouteBody body)
    {
        try
        {
            var request = new UpdateRouteRequest
            {
                Id                       = id,
                Name                     = body.Name,
                Description              = body.Description ?? string.Empty,
                RouteType                = body.RouteType,
                MaxWeightKg              = body.MaxWeightKg,
                MaxVolumeM3              = body.MaxVolumeM3,
                EstimatedDistanceKm      = body.EstimatedDistanceKm ?? 0,
                EstimatedDurationMinutes = body.EstimatedDurationMinutes ?? 0
            };
            request.Stops.AddRange(body.Stops.Select(MapStopInput));

            var response = await routeClient.UpdateRouteAsync(request);

            logger.LogInformation(
                "Route {RouteId} updated in tenant {TenantId} by {UserId}",
                id, currentUser.TenantId, currentUser.UserId);

            return Ok(MapRouteResponse(response));
        }
        catch (RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.NotFound)
        {
            return NotFound(new { detail = $"Route '{id}' not found." });
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

    [HttpDelete("{id}")]
    [RequirePermission(PermissionConstants.Modules.RoutePlanning, PermissionConstants.Delete)]
    public async Task<IActionResult> DeleteRoute([FromRoute] string id)
    {
        try
        {
            await routeClient.DeleteRouteAsync(new DeleteRouteRequest { Id = id });

            logger.LogInformation(
                "Route {RouteId} deleted (soft) in tenant {TenantId} by {UserId}",
                id, currentUser.TenantId, currentUser.UserId);

            return NoContent();
        }
        catch (RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.NotFound)
        {
            return NotFound(new { detail = $"Route '{id}' not found." });
        }
        catch (RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.AlreadyExists)
        {
            return Conflict(new { detail = ex.Status.Detail });
        }
    }

    /// <summary>Chuyển trạng thái route theo bảng transition hợp lệ (Draft→Optimizing→Ready→Active→...).</summary>
    [HttpPatch("{id}/status")]
    [RequirePermission(PermissionConstants.Modules.RoutePlanning, PermissionConstants.Update)]
    public async Task<IActionResult> UpdateRouteStatus([FromRoute] string id, [FromBody] UpdateRouteStatusBody body)
    {
        try
        {
            var response = await routeClient.UpdateRouteStatusAsync(new UpdateRouteStatusRequest
            {
                Id        = id,
                NewStatus = body.NewStatus
            });

            logger.LogInformation(
                "Route {RouteId} status changed to {NewStatus} by {UserId}",
                id, body.NewStatus, currentUser.UserId);

            return Ok(MapRouteResponse(response));
        }
        catch (RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.NotFound)
        {
            return NotFound(new { detail = $"Route '{id}' not found." });
        }
        catch (RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.InvalidArgument)
        {
            return BadRequest(new { detail = ex.Status.Detail });
        }
    }

    /// <summary>Tối ưu thứ tự điểm dừng bằng VROOM + OSRM (MLD) — cập nhật ETA/distance/duration thật.</summary>
    [HttpPost("{id}/optimize")]
    [RequirePermission(PermissionConstants.Modules.RoutePlanning, PermissionConstants.Update)]
    public async Task<IActionResult> OptimizeRoute([FromRoute] string id)
    {
        try
        {
            var response = await routeClient.OptimizeRouteAsync(new OptimizeRouteRequest { Id = id });

            logger.LogInformation(
                "Route {RouteId} optimized (VROOM+OSRM) in tenant {TenantId} by {UserId}",
                id, currentUser.TenantId, currentUser.UserId);

            return Ok(MapRouteResponse(response));
        }
        catch (RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.NotFound)
        {
            return NotFound(new { detail = $"Route '{id}' not found." });
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

    /// <summary>Yêu cầu đánh giá/khuyến nghị AI theo automation policy của tenant (rule → compliance → LLM → approval).</summary>
    [HttpPost("{id}/recommendation")]
    [RequirePermission(PermissionConstants.Modules.RoutePlanning, PermissionConstants.Read)]
    public async Task<IActionResult> GetRouteRecommendation([FromRoute] string id)
    {
        try
        {
            var response = await routeClient.GetRouteRecommendationAsync(
                new GetRouteRecommendationRequest { RouteId = id });

            return Ok(new
            {
                response.RouteId,
                response.RiskLevel,
                response.AutomationDecision,
                response.RecommendationSource,
                response.Summary,
                Suggestions = response.Suggestions.ToList(),
                response.ConfidenceScore,
                ApprovalRequestId = string.IsNullOrEmpty(response.ApprovalRequestId) ? null : response.ApprovalRequestId,
                ApplicableRegulations = response.ApplicableRegulations.ToList()
            });
        }
        catch (RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.NotFound)
        {
            return NotFound(new { detail = $"Route '{id}' not found." });
        }
        catch (RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.InvalidArgument)
        {
            return BadRequest(new { detail = ex.Status.Detail });
        }
    }

    // --- DTOs ---
    public record CreateRouteBody(
        string Name,
        string? Description,
        string RouteType,
        double MaxWeightKg,
        double MaxVolumeM3,
        double? EstimatedDistanceKm,
        int? EstimatedDurationMinutes,
        List<RouteStopBody> Stops);

    public record UpdateRouteBody(
        string Name,
        string? Description,
        string RouteType,
        double MaxWeightKg,
        double MaxVolumeM3,
        double? EstimatedDistanceKm,
        int? EstimatedDurationMinutes,
        List<RouteStopBody> Stops);

    public record RouteStopBody(
        int Sequence,
        string StopType,
        string LocationName,
        string Address,
        double Latitude,
        double Longitude,
        int EstimatedArrivalMinutes,
        int ServiceDurationMinutes);

    public record UpdateRouteStatusBody(string NewStatus);

    private static RouteStopInput MapStopInput(RouteStopBody s) => new()
    {
        Sequence                = s.Sequence,
        StopType                = s.StopType,
        LocationName            = s.LocationName,
        Address                 = s.Address,
        Latitude                = s.Latitude,
        Longitude               = s.Longitude,
        EstimatedArrivalMinutes = s.EstimatedArrivalMinutes,
        ServiceDurationMinutes  = s.ServiceDurationMinutes
    };

    internal static object MapRouteResponse(RouteResponse r) => new
    {
        r.Id,
        r.TenantId,
        r.Name,
        r.Description,
        r.RouteType,
        r.Status,
        r.RiskLevel,
        r.EstimatedDistanceKm,
        r.EstimatedDurationMinutes,
        r.MaxWeightKg,
        r.MaxVolumeM3,
        r.IsAiGenerated,
        OptimizedAt = string.IsNullOrEmpty(r.OptimizedAt) ? null : r.OptimizedAt,
        r.Version,
        r.CreatedAt,
        Stops = r.Stops.Select(s => new
        {
            s.Id,
            s.Sequence,
            s.StopType,
            s.LocationName,
            s.Address,
            s.Latitude,
            s.Longitude,
            s.EstimatedArrivalMinutes,
            s.ServiceDurationMinutes
        }).ToList()
    };
}
