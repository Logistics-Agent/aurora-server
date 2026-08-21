using Asp.Versioning;
using BuildingBlocks.BFF.Attributes;
using Grpc.Core;
using Microsoft.AspNetCore.Mvc;
using RoutePlanningAgent.Grpc;
using Shared.Constants;
using Shared.Security;

namespace AdminBff.Controllers;

/// <summary>
/// Cấu hình AI automation policy per-tenant cho Route Planning.
/// Route: /api/v1/admin/ai-configs
/// Policy: Manual | RulesOnly | RulesAndLlm | RulesLlmApproval; Provider: Gemini | AzureOpenAI.
/// </summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/ai-configs")]
public class AiConfigController(
    RoutePlanningService.RoutePlanningServiceClient routeClient,
    ICurrentUserService currentUser,
    ILogger<AiConfigController> logger) : AdminControllerBase
{
    [HttpGet("{feature}")]
    [RequirePermission(PermissionConstants.Modules.RoutePlanning, PermissionConstants.Read)]
    public async Task<IActionResult> GetAiConfig([FromRoute] string feature)
    {
        try
        {
            var response = await routeClient.GetTenantAiConfigAsync(
                new GetTenantAiConfigRequest { Feature = feature });

            return Ok(MapAiConfigResponse(response));
        }
        catch (RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.NotFound)
        {
            return NotFound(new { detail = $"AI config cho feature '{feature}' chưa được cấu hình." });
        }
    }

    [HttpPut("{feature}")]
    [RequirePermission(PermissionConstants.Modules.RoutePlanning, PermissionConstants.Update)]
    public async Task<IActionResult> UpsertAiConfig([FromRoute] string feature, [FromBody] UpsertAiConfigBody body)
    {
        try
        {
            var response = await routeClient.UpsertTenantAiConfigAsync(
                new UpsertTenantAiConfigRequest
                {
                    Feature    = feature,
                    Policy     = body.Policy,
                    AiProvider = body.AiProvider ?? "Gemini",
                    IsActive   = body.IsActive
                });

            logger.LogInformation(
                "TenantAiConfig ({Feature}) upserted: policy={Policy}, provider={Provider} by {AdminId} (tenant {TenantId})",
                feature, body.Policy, body.AiProvider ?? "Gemini", currentUser.UserId, currentUser.TenantId);

            return Ok(MapAiConfigResponse(response));
        }
        catch (RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.InvalidArgument)
        {
            return BadRequest(new { detail = ex.Status.Detail });
        }
    }

    // --- DTOs ---
    public record UpsertAiConfigBody(string Policy, string? AiProvider, bool IsActive);

    private static object MapAiConfigResponse(TenantAiConfigResponse r) => new
    {
        r.Id,
        r.TenantId,
        r.Feature,
        r.Policy,
        r.AiProvider,
        r.IsActive,
        r.UpdatedAt
    };
}
