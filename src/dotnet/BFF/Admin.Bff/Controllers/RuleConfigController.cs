using Asp.Versioning;
using BuildingBlocks.BFF.Attributes;
using Grpc.Core;
using Microsoft.AspNetCore.Mvc;
using RoutePlanningAgent.Grpc;
using Shared.Constants;
using Shared.Security;

namespace AdminBff.Controllers;

/// <summary>
/// Cấu hình ngưỡng rule engine per-tenant cho Route Planning.
/// Route: /api/v1/admin/rule-configs
/// RuleName hợp lệ: HeavyWeightRule, LargeVolumeRule, RouteStopCountRule, OnDemandTypeRule,
/// LongDurationRule, MinimumStopsRule, MultiHubRule.
/// </summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/rule-configs")]
public class RuleConfigController(
    RoutePlanningService.RoutePlanningServiceClient routeClient,
    ICurrentUserService currentUser,
    ILogger<RuleConfigController> logger) : AdminControllerBase
{
    [HttpGet]
    [RequirePermission(PermissionConstants.RoutePlanning.PolicyManage)]
    public async Task<IActionResult> ListRuleConfigs([FromQuery] int page = 1, [FromQuery] int limit = 20)
    {
        var response = await routeClient.ListTenantRuleConfigsAsync(
            new ListTenantRuleConfigsRequest { Page = page, Limit = limit });

        return Ok(new
        {
            Items = response.Configs.Select(MapRuleConfigResponse),
            response.Page,
            response.Limit,
            response.TotalItems
        });
    }

    [HttpPut("{ruleName}")]
    [RequirePermission(PermissionConstants.RoutePlanning.PolicyManage)]
    public async Task<IActionResult> UpsertRuleConfig([FromRoute] string ruleName, [FromBody] UpsertRuleConfigBody body)
    {
        try
        {
            var request = new UpsertTenantRuleConfigRequest
            {
                RuleName  = ruleName,
                IsEnabled = body.IsEnabled
            };
            foreach (var (key, value) in body.Thresholds ?? [])
            {
                request.Thresholds[key] = value;
            }

            var response = await routeClient.UpsertTenantRuleConfigAsync(request);

            logger.LogInformation(
                "TenantRuleConfig ({RuleName}) upserted: enabled={IsEnabled} by {AdminId} (tenant {TenantId})",
                ruleName, body.IsEnabled, currentUser.UserId, currentUser.TenantId);

            return Ok(MapRuleConfigResponse(response));
        }
        catch (RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.InvalidArgument)
        {
            return BadRequest(new { detail = ex.Status.Detail });
        }
    }

    // --- DTOs ---
    public record UpsertRuleConfigBody(bool IsEnabled, Dictionary<string, double>? Thresholds);

    private static object MapRuleConfigResponse(TenantRuleConfigResponse r) => new
    {
        r.Id,
        r.TenantId,
        r.RuleName,
        r.IsEnabled,
        Thresholds = r.Thresholds.ToDictionary(kv => kv.Key, kv => kv.Value),
        r.UpdatedAt
    };
}
