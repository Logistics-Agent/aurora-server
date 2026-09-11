using System;
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
    [RequirePermission(PermissionConstants.RoutePlanning.PolicyManage, "routing:read")]
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
            // Return sensible default for tenant if not created yet
            return Ok(new
            {
                id = $"default-{feature}",
                tenantId = currentUser.TenantId,
                feature,
                policy = "RulesAndLlm",
                aiProvider = "Gemini",
                isActive = true,
                updatedAt = DateTime.UtcNow.ToString("yyyy-MM-dd")
            });
        }
        catch (RpcException ex)
        {
            logger.LogWarning(ex, "gRPC error fetching AI config for feature {Feature}: {Detail}", feature, ex.Status.Detail);
            return Ok(new
            {
                id = $"default-{feature}",
                tenantId = currentUser.TenantId,
                feature,
                policy = "RulesAndLlm",
                aiProvider = "Gemini",
                isActive = true,
                updatedAt = DateTime.UtcNow.ToString("yyyy-MM-dd")
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error in GetAiConfig for feature {Feature}", feature);
            return StatusCode(500, new { detail = ex.Message });
        }
    }

    [HttpPut("{feature}")]
    [RequirePermission(PermissionConstants.RoutePlanning.PolicyManage, "routing:update")]
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
        catch (RpcException ex)
        {
            logger.LogWarning(ex, "gRPC error in UpsertAiConfig for feature {Feature}: {Detail}", feature, ex.Status.Detail);
            return ex.ToActionResult();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error in UpsertAiConfig for feature {Feature}", feature);
            return StatusCode(500, new { detail = ex.Message });
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
