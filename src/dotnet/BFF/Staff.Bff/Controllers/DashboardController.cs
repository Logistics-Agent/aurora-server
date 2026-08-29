using Asp.Versioning;
using BuildingBlocks.BFF.Attributes;
using Grpc.Core;
using IamTenant.Grpc;
using Microsoft.AspNetCore.Mvc;
using RoutePlanningAgent.Grpc;
using Shared.Constants;
using Shared.Security;

namespace StaffBff.Controllers;

/// <summary>
/// Dashboard tổng hợp cho Staff: profile + các route đang hoạt động.
/// Aggregation: gọi IamTenant + RoutePlanningAgent SONG SONG (Task.WhenAll).
/// </summary>
[ApiVersion("1.0")]
public class DashboardController(
    IamService.IamServiceClient iamClient,
    RoutePlanningService.RoutePlanningServiceClient routeClient,
    ICurrentUserService currentUser,
    ILogger<DashboardController> logger) : StaffControllerBase
{
    [HttpGet("summary")]
    [RequirePermission(PermissionConstants.RoutePlanning.Read)]
    public async Task<IActionResult> GetDashboardSummary()
    {
        try
        {
            var userId = currentUser.UserId.ToString()!;

            var userTask = iamClient.GetUserAsync(new GetUserRequest { Id = userId }).ResponseAsync;

            // Tenant isolation do gRPC metadata (x-tenant-id) + global query filter đảm nhiệm
            var routesTask = routeClient.ListRoutesAsync(new ListRoutesRequest
            {
                Page   = 1,
                Limit  = 5,
                Status = "Active"
            }).ResponseAsync;

            await Task.WhenAll(userTask, routesTask);

            var userResponse = await userTask;
            var routesResponse = await routesTask;

            var dashboardData = new DashboardSummaryDto
            {
                UserProfile = new UserProfileDto(
                    userResponse.FirstName,
                    userResponse.LastName,
                    userResponse.Email),

                ActiveRoutesCount = routesResponse.TotalItems,

                RecentActiveRoutes = routesResponse.Routes.Select(r => new RouteShortDto(
                    r.Id,
                    r.Stops.OrderBy(s => s.Sequence).FirstOrDefault()?.LocationName ?? string.Empty,
                    r.Stops.OrderBy(s => s.Sequence).LastOrDefault()?.LocationName ?? string.Empty,
                    r.Status)).ToList()
            };

            logger.LogInformation("Aggregated dashboard data for user {UserId}", userId);

            return Ok(dashboardData);
        }
        catch (RpcException ex)
        {
            logger.LogError(ex, "gRPC Error aggregating dashboard data");
            return StatusCode(StatusCodes.Status500InternalServerError, new { detail = "Internal server error connecting to services." });
        }
    }

    // --- Aggregated DTOs ---
    public record DashboardSummaryDto
    {
        public required UserProfileDto UserProfile { get; init; }
        public int ActiveRoutesCount { get; init; }
        public required List<RouteShortDto> RecentActiveRoutes { get; init; }
    }

    public record UserProfileDto(string FirstName, string LastName, string Email);
    public record RouteShortDto(string RouteId, string Origin, string Destination, string Status);
}
