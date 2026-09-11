using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Asp.Versioning;
using BuildingBlocks.BFF.Extensions;
using Grpc.Core;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using RoutePlanningAgent.Grpc;
using Shared.Security;

namespace SystemBff.Controllers;

/// <summary>
/// Controller Quản trị Cấu hình AI Automation Policy per-tenant cấp độ Hệ Thống (System Admin).
/// Route: /api/v1/system/ai-configs
/// Quyền: Chỉ dành cho SYSTEM_ADMIN (bảo vệ qua SystemControllerBase).
/// </summary>
[ApiVersion("1.0")]
public class AiConfigController(
    RoutePlanningService.RoutePlanningServiceClient? routeClient,
    ICurrentUserService currentUser,
    ILogger<AiConfigController> logger) : SystemControllerBase
{
    /// <summary>
    /// Danh sách các Module/Tính năng AI hỗ trợ cấu hình Automation Policy.
    /// </summary>
    [HttpGet("features")]
    public IActionResult ListConfigurableFeatures()
    {
        var features = new List<object>
        {
            new
            {
                feature = "route-planning",
                name = "Tối ưu hóa Lộ trình & Đề xuất rủi ro",
                category = "Logistics",
                defaultPolicy = "RulesAndLlm",
                defaultProvider = "Gemini",
                supportedPolicies = new[] { "Manual", "RulesOnly", "RulesAndLlm", "RulesLlmApproval" },
                supportedProviders = new[] { "Gemini", "AzureOpenAI" },
                description = "Chính sách tự động hóa lập kế hoạch lộ trình và gợi ý rủi ro thời tiết/tắc nghẽn."
            },
            new
            {
                feature = "mail-triage",
                name = "Phân loại & Kiểm tra an ninh Email",
                category = "Security",
                defaultPolicy = "RulesAndLlm",
                defaultProvider = "Gemini",
                supportedPolicies = new[] { "Manual", "RulesOnly", "RulesAndLlm" },
                supportedProviders = new[] { "Gemini" },
                description = "Kiểm tra BEC mạo danh, URL phishing và lọc thư rác."
            },
            new
            {
                feature = "customs-ocr",
                name = "Trích xuất Chứng từ Vận tải & Hải quan",
                category = "OCR",
                defaultPolicy = "RulesLlmApproval",
                defaultProvider = "Gemini",
                supportedPolicies = new[] { "Manual", "RulesOnly", "RulesAndLlm", "RulesLlmApproval" },
                supportedProviders = new[] { "Gemini", "AzureOpenAI" },
                description = "Trích xuất tờ khai hải quan, hoá đơn thương mại và vận đơn."
            },
            new
            {
                feature = "compliance-assistant",
                name = "Trợ lý Tra cứu Tuân thủ Pháp lý",
                category = "Compliance",
                defaultPolicy = "RulesAndLlm",
                defaultProvider = "Gemini",
                supportedPolicies = new[] { "Manual", "RulesAndLlm" },
                supportedProviders = new[] { "Gemini", "AzureOpenAI" },
                description = "Trợ lý AI trả lời có trích dẫn điều khoản luật và quy chuẩn quốc tế."
            },
            new
            {
                feature = "negotiation-agent",
                name = "Đề xuất Chiến lược Đàm phán Giá cước",
                category = "Commercial",
                defaultPolicy = "RulesLlmApproval",
                defaultProvider = "Gemini",
                supportedPolicies = new[] { "Manual", "RulesLlmApproval" },
                supportedProviders = new[] { "Gemini", "AzureOpenAI" },
                description = "Tự động phân tích giá thị trường và soạn thảo thư trả giá."
            },
            new
            {
                feature = "customer-assistant",
                name = "Trợ lý Hỗ trợ Khách hàng",
                category = "CustomerService",
                defaultPolicy = "RulesAndLlm",
                defaultProvider = "Gemini",
                supportedPolicies = new[] { "Manual", "RulesAndLlm" },
                supportedProviders = new[] { "Gemini" },
                description = "Tự động tóm tắt ticket, tra cứu vận đơn cho khách hàng."
            }
        };

        return Ok(new
        {
            total = features.Count,
            items = features
        });
    }

    /// <summary>
    /// Lấy cấu hình AI Policy của một Tenant cụ thể cho một tính năng.
    /// </summary>
    [HttpGet("{tenantId}/{feature}")]
    public async Task<IActionResult> GetTenantAiConfig(
        [FromRoute] string tenantId,
        [FromRoute] string feature)
    {
        if (string.IsNullOrWhiteSpace(tenantId) || string.IsNullOrWhiteSpace(feature))
        {
            return BadRequest(new { detail = "TenantId and Feature are required." });
        }

        if (routeClient == null)
        {
            return Ok(GetDefaultTenantConfig(tenantId, feature));
        }

        try
        {
            var headers = CreateHeaders(tenantId);
            var response = await routeClient.GetTenantAiConfigAsync(
                new GetTenantAiConfigRequest { Feature = feature },
                headers);

            return Ok(MapAiConfigResponse(response, tenantId));
        }
        catch (RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.NotFound)
        {
            return Ok(GetDefaultTenantConfig(tenantId, feature));
        }
        catch (RpcException ex)
        {
            logger.LogWarning(ex, "gRPC error fetching AI config for Tenant {TenantId}, Feature {Feature}: {Detail}",
                tenantId, feature, ex.Status.Detail);
            return Ok(GetDefaultTenantConfig(tenantId, feature));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error fetching AI config for Tenant {TenantId}, Feature {Feature}", tenantId, feature);
            return StatusCode(500, new { detail = ex.Message });
        }
    }

    /// <summary>
    /// Ghi đè / Cập nhật cấu hình AI Automation Policy của một Tenant cho một tính năng.
    /// </summary>
    [HttpPut("{tenantId}/{feature}")]
    public async Task<IActionResult> UpsertTenantAiConfig(
        [FromRoute] string tenantId,
        [FromRoute] string feature,
        [FromBody] UpsertTenantAiConfigBody body)
    {
        if (string.IsNullOrWhiteSpace(tenantId) || string.IsNullOrWhiteSpace(feature))
        {
            return BadRequest(new { detail = "TenantId and Feature are required." });
        }

        if (string.IsNullOrWhiteSpace(body.Policy))
        {
            return BadRequest(new { detail = "Policy is required (Manual, RulesOnly, RulesAndLlm, RulesLlmApproval)." });
        }

        if (routeClient == null)
        {
            logger.LogInformation(
                "RouteClient is null, simulating AI config update for Tenant {TenantId}, Feature {Feature}",
                tenantId, feature);

            return Ok(new
            {
                id = $"ai-config-{tenantId}-{feature}",
                tenantId,
                feature,
                policy = body.Policy,
                aiProvider = body.AiProvider ?? "Gemini",
                isActive = body.IsActive,
                updatedAt = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")
            });
        }

        try
        {
            var headers = CreateHeaders(tenantId);
            var response = await routeClient.UpsertTenantAiConfigAsync(
                new UpsertTenantAiConfigRequest
                {
                    Feature = feature,
                    Policy = body.Policy,
                    AiProvider = body.AiProvider ?? "Gemini",
                    IsActive = body.IsActive
                },
                headers);

            logger.LogInformation(
                "Tenant AI Config ({Feature}) updated for Tenant {TenantId} by System Admin {AdminId}: policy={Policy}, provider={Provider}",
                feature, tenantId, currentUser.UserId, body.Policy, body.AiProvider ?? "Gemini");

            return Ok(MapAiConfigResponse(response, tenantId));
        }
        catch (RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.InvalidArgument)
        {
            return BadRequest(new { detail = ex.Status.Detail });
        }
        catch (RpcException ex)
        {
            logger.LogWarning(ex, "gRPC error in UpsertTenantAiConfig for Tenant {TenantId}, Feature {Feature}: {Detail}",
                tenantId, feature, ex.Status.Detail);
            return ex.ToActionResult();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error in UpsertTenantAiConfig for Tenant {TenantId}, Feature {Feature}", tenantId, feature);
            return StatusCode(500, new { detail = ex.Message });
        }
    }

    /// <summary>
    /// Lấy danh sách toàn bộ cấu hình tính năng AI của một Tenant.
    /// </summary>
    [HttpGet("{tenantId}")]
    public async Task<IActionResult> GetAllTenantAiConfigs([FromRoute] string tenantId)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            return BadRequest(new { detail = "TenantId is required." });
        }

        var featureKeys = new[]
        {
            "route-planning",
            "mail-triage",
            "customs-ocr",
            "compliance-assistant",
            "negotiation-agent",
            "customer-assistant"
        };

        var results = new List<object>();

        foreach (var feature in featureKeys)
        {
            if (routeClient != null)
            {
                try
                {
                    var headers = CreateHeaders(tenantId);
                    var response = await routeClient.GetTenantAiConfigAsync(
                        new GetTenantAiConfigRequest { Feature = feature },
                        headers);

                    results.Add(MapAiConfigResponse(response, tenantId));
                    continue;
                }
                catch (Exception)
                {
                    // Fallback to default below
                }
            }

            results.Add(GetDefaultTenantConfig(tenantId, feature));
        }

        return Ok(new
        {
            tenantId,
            total = results.Count,
            items = results
        });
    }

    // --- Private Helper Methods ---

    private Metadata CreateHeaders(string tenantId)
    {
        var headers = new Metadata
        {
            { "x-service-id", "system-bff" },
            { "x-tenant-id", tenantId }
        };

        if (currentUser.UserId.HasValue)
            headers.Add("x-user-id", currentUser.UserId.Value.ToString());

        if (!string.IsNullOrEmpty(currentUser.Role))
            headers.Add("x-role", currentUser.Role);

        if (!string.IsNullOrEmpty(currentUser.TraceId))
            headers.Add("x-trace-id", currentUser.TraceId);

        return headers;
    }

    private static object GetDefaultTenantConfig(string tenantId, string feature) => new
    {
        id = $"default-{tenantId}-{feature}",
        tenantId,
        feature,
        policy = "RulesAndLlm",
        aiProvider = "Gemini",
        isActive = true,
        updatedAt = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")
    };

    private static object MapAiConfigResponse(TenantAiConfigResponse r, string fallbackTenantId) => new
    {
        id = r.Id,
        tenantId = string.IsNullOrEmpty(r.TenantId) ? fallbackTenantId : r.TenantId,
        feature = r.Feature,
        policy = r.Policy,
        aiProvider = r.AiProvider,
        isActive = r.IsActive,
        updatedAt = r.UpdatedAt
    };

    // --- DTOs ---
    public record UpsertTenantAiConfigBody(string Policy, string? AiProvider, bool IsActive);
}
