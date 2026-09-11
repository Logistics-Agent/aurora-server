using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Asp.Versioning;
using BuildingBlocks.BFF.Attributes;
using BuildingBlocks.BFF.Extensions;
using Grpc.Core;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
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
    private static readonly List<DefaultRuleDefinition> DefaultRules =
    [
        new("HeavyWeightRule", "ROUTE_WEIGHT_CAPACITY", "Maximum cargo weight", "Routes exceeding this cargo weight are flagged as high-risk and require elevated approval.", "kg", 22000, 1000, 60000, 500, true, "threshold", "maxWeightKg"),
        new("LargeVolumeRule", "ROUTE_VOLUME_CAPACITY", "Maximum cargo volume", "Shipments exceeding this volumetric threshold are flagged for specialized vehicle assignment.", "m³", 80, 10, 300, 5, true, "threshold", "maxVolumeM3"),
        new("RouteStopCountRule", "ROUTE_STOP_COUNT", "Maximum waypoint count", "Routes with more stops than this threshold are reviewed for complexity and driver hour compliance.", "stops", 12, 2, 50, 1, true, "threshold", "maxStops"),
        new("LongDurationRule", "ROUTE_DURATION_LIMIT", "Maximum route duration", "Routes projected to exceed this duration require manager approval and may trigger driver change planning.", "hours", 10, 1, 72, 0.5, true, "threshold", "maxDurationHours"),
        new("MinimumStopsRule", "ROUTE_MIN_STOPS", "Minimum stops required", "Routes below this stop count are flagged as potentially incomplete or misrouted.", "stops", 2, 1, 10, 1, false, "threshold", "minStops"),
        new("MultiHubRule", "ROUTE_MULTI_HUB", "Multi-hub requirement", "When enabled, routes spanning more than one logistics hub require cross-hub coordination approval.", "", 1, 0, 1, 1, true, "boolean", "requiresApproval"),
        new("OnDemandTypeRule", "ROUTE_ON_DEMAND", "On-demand / urgent risk trigger", "Urgent and on-demand shipment types automatically inherit an elevated risk score, triggering expedited review.", "", 1, 0, 1, 1, true, "boolean", "elevateRisk")
    ];

    [HttpGet]
    [RequirePermission(PermissionConstants.RoutePlanning.PolicyManage, "routing:read")]
    public async Task<IActionResult> ListRuleConfigs([FromQuery] int page = 1, [FromQuery] int limit = 20)
    {
        try
        {
            var response = await routeClient.ListTenantRuleConfigsAsync(
                new ListTenantRuleConfigsRequest { Page = 1, Limit = 100 });

            var dbConfigsMap = response.Configs
                .GroupBy(c => c.RuleName, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            var items = new List<object>();

            // 1. Merge default rules with DB configs
            foreach (var def in DefaultRules)
            {
                if (dbConfigsMap.TryGetValue(def.Name, out var dbConfig))
                {
                    items.Add(MapRuleConfigResponse(dbConfig));
                    dbConfigsMap.Remove(def.Name);
                }
                else
                {
                    items.Add(new
                    {
                        id = def.Name,
                        tenantId = currentUser.TenantId,
                        ruleName = def.Name,
                        ruleCode = def.Code,
                        label = def.Label,
                        description = def.Description,
                        unit = def.Unit,
                        value = def.DefaultValue,
                        min = def.Min,
                        max = def.Max,
                        step = def.Step,
                        type = def.Type,
                        thresholdKey = def.ThresholdKey,
                        isEnabled = def.DefaultEnabled,
                        thresholds = new Dictionary<string, double> { { def.ThresholdKey, def.DefaultValue } },
                        updatedAt = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")
                    });
                }
            }

            // 2. Append any extra custom rules from DB that are not in DefaultRules
            foreach (var extra in dbConfigsMap.Values)
            {
                items.Add(MapRuleConfigResponse(extra));
            }

            return Ok(new
            {
                Items = items,
                Page = 1,
                Limit = items.Count,
                TotalItems = items.Count
            });
        }
        catch (RpcException ex)
        {
            logger.LogWarning(ex, "gRPC error fetching tenant rule configs, falling back to default rules catalog: {Detail}", ex.Status.Detail);
            return Ok(new
            {
                Items = GetDefaultRuleResponses(),
                Page = 1,
                Limit = DefaultRules.Count,
                TotalItems = DefaultRules.Count
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error in ListRuleConfigs");
            return StatusCode(500, new { detail = ex.Message });
        }
    }

    [HttpPost]
    [RequirePermission(PermissionConstants.RoutePlanning.PolicyManage, "routing:create", "routing:update")]
    public async Task<IActionResult> CreateRuleConfig([FromBody] RuleConfigPayload body)
    {
        if (string.IsNullOrWhiteSpace(body.RuleName))
        {
            return BadRequest(new { detail = "ruleName is required." });
        }

        return await ProcessUpsertRuleConfig(body.RuleName, body);
    }

    [HttpPost("{ruleName}")]
    [RequirePermission(PermissionConstants.RoutePlanning.PolicyManage, "routing:create", "routing:update")]
    public async Task<IActionResult> CreateOrUpdateRuleConfigByRoute([FromRoute] string ruleName, [FromBody] RuleConfigPayload body)
    {
        return await ProcessUpsertRuleConfig(ruleName, body);
    }

    [HttpPut("{ruleName}")]
    [RequirePermission(PermissionConstants.RoutePlanning.PolicyManage, "routing:update")]
    public async Task<IActionResult> UpsertRuleConfig([FromRoute] string ruleName, [FromBody] RuleConfigPayload body)
    {
        return await ProcessUpsertRuleConfig(ruleName, body);
    }

    [HttpPatch("{ruleName}")]
    [HttpPatch("{ruleName}/status")]
    [RequirePermission(PermissionConstants.RoutePlanning.PolicyManage, "routing:update")]
    public async Task<IActionResult> PatchRuleStatus([FromRoute] string ruleName, [FromBody] RuleConfigPayload body)
    {
        return await ProcessUpsertRuleConfig(ruleName, body);
    }

    [HttpDelete("{ruleName}")]
    [RequirePermission(PermissionConstants.RoutePlanning.PolicyManage, "routing:delete", "routing:update")]
    public async Task<IActionResult> DeleteRuleConfig([FromRoute] string ruleName)
    {
        // Deactivate rule for tenant
        return await ProcessUpsertRuleConfig(ruleName, new RuleConfigPayload
        {
            RuleName = ruleName,
            IsEnabled = false
        });
    }

    [HttpPost("{ruleName}/toggle")]
    [RequirePermission(PermissionConstants.RoutePlanning.PolicyManage, "routing:update")]
    public async Task<IActionResult> ToggleRuleStatus([FromRoute] string ruleName, [FromBody] ToggleRulePayload? body)
    {
        var targetEnabled = body?.IsEnabled;
        return await ProcessUpsertRuleConfig(ruleName, new RuleConfigPayload
        {
            RuleName = ruleName,
            IsEnabled = targetEnabled
        });
    }

    private async Task<IActionResult> ProcessUpsertRuleConfig(string ruleName, RuleConfigPayload body)
    {
        try
        {
            var def = DefaultRules.FirstOrDefault(d => string.Equals(d.Name, ruleName, StringComparison.OrdinalIgnoreCase));

            var isEnabled = body.IsEnabled ?? def?.DefaultEnabled ?? true;
            var request = new UpsertTenantRuleConfigRequest
            {
                RuleName = def?.Name ?? ruleName,
                IsEnabled = isEnabled
            };

            // Populate thresholds
            if (body.Thresholds != null && body.Thresholds.Count > 0)
            {
                foreach (var (key, value) in body.Thresholds)
                {
                    if (value < 0)
                    {
                        return BadRequest(new { detail = $"Threshold '{key}' cannot be negative ({value})." });
                    }
                    request.Thresholds[key] = value;
                }
            }
            else if (body.Value.HasValue)
            {
                if (body.Value.Value < 0)
                {
                    return BadRequest(new { detail = $"Threshold value cannot be negative ({body.Value.Value})." });
                }
                var key = !string.IsNullOrWhiteSpace(body.ThresholdKey) ? body.ThresholdKey : (def?.ThresholdKey ?? "threshold");
                request.Thresholds[key] = body.Value.Value;
            }
            else if (def != null)
            {
                request.Thresholds[def.ThresholdKey] = def.DefaultValue;
            }

            var response = await routeClient.UpsertTenantRuleConfigAsync(request);

            logger.LogInformation(
                "TenantRuleConfig ({RuleName}) saved: enabled={IsEnabled} by {AdminId} (tenant {TenantId})",
                request.RuleName, request.IsEnabled, currentUser.UserId, currentUser.TenantId);

            return Ok(MapRuleConfigResponse(response));
        }
        catch (RpcException ex)
        {
            logger.LogWarning(ex, "gRPC error in UpsertRuleConfig for {RuleName}: {Detail}", ruleName, ex.Status.Detail);
            return ex.ToActionResult();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error in UpsertRuleConfig for {RuleName}", ruleName);
            return StatusCode(500, new { detail = ex.Message });
        }
    }

    // --- DTOs ---
    public class RuleConfigPayload
    {
        public string? Id { get; set; }
        public string? TenantId { get; set; }
        public string? RuleName { get; set; }
        public bool? IsEnabled { get; set; }
        public Dictionary<string, double>? Thresholds { get; set; }
        public double? Value { get; set; }
        public string? ThresholdKey { get; set; }
    }

    public class ToggleRulePayload
    {
        public bool? IsEnabled { get; set; }
    }

    private record DefaultRuleDefinition(
        string Name,
        string Code,
        string Label,
        string Description,
        string Unit,
        double DefaultValue,
        double Min,
        double Max,
        double Step,
        bool DefaultEnabled,
        string Type,
        string ThresholdKey);

    private IEnumerable<object> GetDefaultRuleResponses()
    {
        return DefaultRules.Select(d => new
        {
            id = d.Name,
            tenantId = currentUser.TenantId,
            ruleName = d.Name,
            ruleCode = d.Code,
            label = d.Label,
            description = d.Description,
            unit = d.Unit,
            value = d.DefaultValue,
            min = d.Min,
            max = d.Max,
            step = d.Step,
            type = d.Type,
            thresholdKey = d.ThresholdKey,
            isEnabled = d.DefaultEnabled,
            thresholds = new Dictionary<string, double> { { d.ThresholdKey, d.DefaultValue } },
            updatedAt = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")
        });
    }

    private static object MapRuleConfigResponse(TenantRuleConfigResponse r)
    {
        var def = DefaultRules.FirstOrDefault(d => string.Equals(d.Name, r.RuleName, StringComparison.OrdinalIgnoreCase));
        var thresholdVal = def != null && r.Thresholds.TryGetValue(def.ThresholdKey, out var val)
            ? val
            : (r.Thresholds.Count > 0 ? r.Thresholds.Values.FirstOrDefault() : (def?.DefaultValue ?? 0));

        var thresholds = r.Thresholds.ToDictionary(kv => kv.Key, kv => kv.Value);
        if (thresholds.Count == 0 && def != null)
        {
            thresholds[def.ThresholdKey] = def.DefaultValue;
        }

        return new
        {
            id = r.Id,
            tenantId = r.TenantId,
            ruleName = r.RuleName,
            ruleCode = def?.Code ?? r.RuleName,
            label = def?.Label ?? r.RuleName,
            description = def?.Description ?? string.Empty,
            unit = def?.Unit ?? string.Empty,
            value = thresholdVal,
            min = def?.Min ?? 0,
            max = def?.Max ?? 100000,
            step = def?.Step ?? 1,
            type = def?.Type ?? "threshold",
            thresholdKey = def?.ThresholdKey ?? "threshold",
            isEnabled = r.IsEnabled,
            thresholds,
            updatedAt = r.UpdatedAt
        };
    }
}
