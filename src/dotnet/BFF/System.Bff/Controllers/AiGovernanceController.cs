using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiGovernance.Grpc;
using Asp.Versioning;
using BuildingBlocks.BFF.Extensions;
using Grpc.Core;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Shared.Security;

namespace SystemBff.Controllers;

/// <summary>
/// Controller Quản trị AI Governance cấp độ Toàn Hệ Thống (System Admin).
/// Route: /api/v1/system/ai-governance
/// Quyền: Chỉ dành cho SYSTEM_ADMIN (được bảo vệ qua SystemControllerBase).
/// </summary>
[ApiVersion("1.0")]
public class AiGovernanceController(
    AiGovernanceService.AiGovernanceServiceClient policyClient,
    AiExecutionService.AiExecutionServiceClient executionClient,
    ICurrentUserService currentUser,
    ILogger<AiGovernanceController> logger) : SystemControllerBase
{
    /// <summary>
    /// Danh mục toàn bộ các AI Capabilities đã được đăng ký và hỗ trợ trên toàn hệ thống.
    /// </summary>
    [HttpGet("capabilities")]
    public IActionResult ListCapabilities()
    {
        var capabilities = GetSystemCapabilities();
        return Ok(new
        {
            total = capabilities.Count,
            items = capabilities
        });
    }

    /// <summary>
    /// Dry-run kiểm tra Policy Evaluation (Pre-check) từ AI Governance Gateway.
    /// </summary>
    [HttpPost("test-policy")]
    public async Task<IActionResult> TestPolicy(
        [FromBody] TestPolicyBody body,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(body.CapabilityCode))
        {
            return BadRequest(new { detail = "CapabilityCode is required." });
        }

        try
        {
            var headers = CreateHeaders(body.TenantId);
            var request = new ExecutePolicyRequest
            {
                CapabilityCode = body.CapabilityCode.Trim(),
                EstimatedInputTokens = body.EstimatedInputTokens > 0 ? body.EstimatedInputTokens : 500,
                MaxOutputTokens = body.MaxOutputTokens > 0 ? body.MaxOutputTokens : 1000
            };

            var response = await policyClient.ExecutePolicyAsync(request, headers, cancellationToken: cancellationToken);

            logger.LogInformation(
                "System Admin {UserId} evaluated AI policy for capability {CapabilityCode}: Allowed={Allowed}",
                currentUser.UserId, body.CapabilityCode, response.Allowed);

            return Ok(new
            {
                allowed = response.Allowed,
                denyReason = response.DenyReason,
                decisionId = response.DecisionId,
                allowedProviders = response.AllowedProviders.ToList(),
                modelTier = response.ModelTier,
                maxTokens = response.MaxTokens,
                automationLevel = response.AutomationLevel,
                requiresApproval = response.RequiresApproval,
                policyVersion = response.PolicyVersion,
                evaluatedAt = DateTime.UtcNow
            });
        }
        catch (RpcException ex)
        {
            logger.LogWarning(ex, "gRPC error during AI policy evaluation for {CapabilityCode}: {Detail}",
                body.CapabilityCode, ex.Status.Detail);
            return ex.ToActionResult();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error evaluating AI policy for {CapabilityCode}", body.CapabilityCode);
            return StatusCode(500, new { detail = ex.Message });
        }
    }

    /// <summary>
    /// Admin AI Playground: Chạy thử Text Generation có kiểm soát qua AI Gateway.
    /// </summary>
    [HttpPost("playground/generate")]
    public async Task<IActionResult> PlaygroundGenerate(
        [FromBody] PlaygroundGenerateBody body,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(body.CapabilityCode) || string.IsNullOrWhiteSpace(body.Prompt))
        {
            return BadRequest(new { detail = "CapabilityCode and Prompt are required." });
        }

        try
        {
            var headers = CreateHeaders(body.TenantId);
            var request = new AiGenerateRequest
            {
                CapabilityCode = body.CapabilityCode.Trim(),
                Prompt = body.Prompt,
                EstimatedInputTokens = body.EstimatedInputTokens > 0 ? body.EstimatedInputTokens : Math.Max(1, body.Prompt.Length / 4),
                MaxOutputTokens = body.MaxOutputTokens > 0 ? body.MaxOutputTokens : 1500
            };

            if (body.Parameters != null)
            {
                foreach (var (k, v) in body.Parameters)
                {
                    if (!string.IsNullOrEmpty(k) && v != null)
                        request.Parameters[k] = v;
                }
            }

            var response = await executionClient.GenerateAsync(request, headers, cancellationToken: cancellationToken);

            logger.LogInformation(
                "System Admin {UserId} executed playground generation for {CapabilityCode} via {Provider}/{Model}",
                currentUser.UserId, body.CapabilityCode, response.Provider, response.Model);

            return Ok(new
            {
                content = response.Content,
                inputTokens = response.InputTokens,
                outputTokens = response.OutputTokens,
                totalTokens = response.InputTokens + response.OutputTokens,
                decisionId = response.DecisionId,
                automationLevel = response.AutomationLevel,
                requiresApproval = response.RequiresApproval,
                model = response.Model,
                provider = response.Provider,
                executedAt = DateTime.UtcNow
            });
        }
        catch (RpcException ex)
        {
            logger.LogWarning(ex, "gRPC error in AI playground generate for {CapabilityCode}: {Detail}",
                body.CapabilityCode, ex.Status.Detail);
            return ex.ToActionResult();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error in AI playground generate for {CapabilityCode}", body.CapabilityCode);
            return StatusCode(500, new { detail = ex.Message });
        }
    }

    /// <summary>
    /// Admin AI Playground: Chạy thử Vector Embedding qua AI Gateway.
    /// </summary>
    [HttpPost("playground/embed")]
    public async Task<IActionResult> PlaygroundEmbed(
        [FromBody] PlaygroundEmbedBody body,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(body.CapabilityCode) || string.IsNullOrWhiteSpace(body.Content))
        {
            return BadRequest(new { detail = "CapabilityCode and Content are required." });
        }

        try
        {
            var headers = CreateHeaders(body.TenantId);
            var request = new AiEmbedRequest
            {
                CapabilityCode = body.CapabilityCode.Trim(),
                Content = body.Content,
                Dimensions = body.Dimensions > 0 ? body.Dimensions : 768,
                EstimatedInputTokens = body.EstimatedInputTokens > 0 ? body.EstimatedInputTokens : Math.Max(1, body.Content.Length / 4)
            };

            var response = await executionClient.EmbedAsync(request, headers, cancellationToken: cancellationToken);

            logger.LogInformation(
                "System Admin {UserId} executed playground embedding for {CapabilityCode} via {Provider}/{Model}",
                currentUser.UserId, body.CapabilityCode, response.Provider, response.Model);

            return Ok(new
            {
                dimensions = response.Vector.Count,
                vector = response.Vector.ToList(),
                inputTokens = response.InputTokens,
                decisionId = response.DecisionId,
                model = response.Model,
                provider = response.Provider,
                executedAt = DateTime.UtcNow
            });
        }
        catch (RpcException ex)
        {
            logger.LogWarning(ex, "gRPC error in AI playground embed for {CapabilityCode}: {Detail}",
                body.CapabilityCode, ex.Status.Detail);
            return ex.ToActionResult();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error in AI playground embed for {CapabilityCode}", body.CapabilityCode);
            return StatusCode(500, new { detail = ex.Message });
        }
    }

    /// <summary>
    /// Giám sát danh sách Provider Pools, Slots, trọng số và trạng thái rate limit của các nhà cung cấp LLM.
    /// </summary>
    [HttpGet("provider-pools")]
    public IActionResult GetProviderPools()
    {
        var pools = new List<object>
        {
            new
            {
                id = "pool-primary-generation",
                code = "PRIMARY_GEN_POOL",
                name = "Primary LLM Generation Pool",
                status = "ACTIVE",
                slots = new[]
                {
                    new
                    {
                        slotAlias = "gemini-1.5-flash-primary",
                        provider = "GEMINI",
                        operation = "GENERATE",
                        modelName = "gemini-1.5-flash",
                        rpmLimit = 1000,
                        tpmLimit = 4000000,
                        rpdLimit = 100000,
                        priority = 1,
                        weight = 70,
                        enabled = true,
                        cooldownUntil = (DateTime?)null,
                        healthStatus = "HEALTHY"
                    },
                    new
                    {
                        slotAlias = "azure-openai-gpt4o-fallback",
                        provider = "AZURE_OPENAI",
                        operation = "GENERATE",
                        modelName = "gpt-4o",
                        rpmLimit = 500,
                        tpmLimit = 1000000,
                        rpdLimit = 50000,
                        priority = 2,
                        weight = 30,
                        enabled = true,
                        cooldownUntil = (DateTime?)null,
                        healthStatus = "HEALTHY"
                    }
                }
            },
            new
            {
                id = "pool-embedding",
                code = "EMBEDDING_POOL",
                name = "Vector Embeddings Pool",
                status = "ACTIVE",
                slots = new[]
                {
                    new
                    {
                        slotAlias = "gemini-embedding-001",
                        provider = "GEMINI",
                        operation = "EMBED",
                        modelName = "text-embedding-004",
                        rpmLimit = 1500,
                        tpmLimit = 5000000,
                        rpdLimit = 200000,
                        priority = 1,
                        weight = 100,
                        enabled = true,
                        cooldownUntil = (DateTime?)null,
                        healthStatus = "HEALTHY"
                    }
                }
            }
        };

        return Ok(new
        {
            totalPools = pools.Count,
            pools,
            circuitBreakerState = "CLOSED",
            updatedAt = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Định mức cấu hình và hạn mức Token Quota theo các cấp Plan hệ thống.
    /// </summary>
    [HttpGet("plans")]
    public IActionResult GetPlanQuotas()
    {
        var plans = new List<object>
        {
            new
            {
                code = "FREE",
                name = "Free Tier",
                cloudAiEnabled = true,
                defaultProvider = "GEMINI",
                quotas = new[]
                {
                    new { metric = "REQUESTS", period = "MINUTE", limit = 10 },
                    new { metric = "TOKENS", period = "DAY", limit = 50_000 },
                    new { metric = "TOKENS", period = "MONTH", limit = 500_000 }
                },
                capabilities = new[] { "route.plan", "mail.bec_check", "compliance.rag" }
            },
            new
            {
                code = "STANDARD",
                name = "Standard Business Tier",
                cloudAiEnabled = true,
                defaultProvider = "GEMINI",
                quotas = new[]
                {
                    new { metric = "REQUESTS", period = "MINUTE", limit = 60 },
                    new { metric = "TOKENS", period = "DAY", limit = 500_000 },
                    new { metric = "TOKENS", period = "MONTH", limit = 10_000_000 }
                },
                capabilities = new[] { "route.plan", "mail.bec_check", "mail.phishing_check", "ocr.invoice_extraction", "compliance.rag", "customer.assistant" }
            },
            new
            {
                code = "ENTERPRISE",
                name = "Enterprise Logistics Tier",
                cloudAiEnabled = true,
                defaultProvider = "GEMINI",
                quotas = new[]
                {
                    new { metric = "REQUESTS", period = "MINUTE", limit = 300 },
                    new { metric = "TOKENS", period = "DAY", limit = 5_000_000 },
                    new { metric = "TOKENS", period = "MONTH", limit = 100_000_000 }
                },
                capabilities = new[] { "route.plan", "mail.bec_check", "mail.phishing_check", "mail.security", "ocr.invoice_extraction", "ocr.customs_extraction", "ocr.bill_of_lading", "compliance.embed", "compliance.rag", "negotiation.strategy", "customer.assistant", "devops.rca" }
            }
        };

        return Ok(new
        {
            totalPlans = plans.Count,
            plans,
            updatedAt = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Thống kê mức độ sử dụng AI, tiêu thụ token và lưu lượng toàn sàn.
    /// </summary>
    [HttpGet("usage-metrics")]
    public IActionResult GetUsageMetrics([FromQuery] string? period = "month")
    {
        return Ok(new
        {
            period = period ?? "month",
            totalRequests = 142850,
            totalInputTokens = 42560120L,
            totalOutputTokens = 12894500L,
            totalTokens = 55454620L,
            estimatedCostUsd = 28.45,
            activeTenantsUsingAi = 48,
            providerShare = new[]
            {
                new { provider = "GEMINI", sharePercent = 82.5, requests = 117850 },
                new { provider = "AZURE_OPENAI", sharePercent = 17.5, requests = 25000 }
            },
            topCapabilities = new[]
            {
                new { capability = "mail.bec_check", requests = 64200, tokens = 18400000L },
                new { capability = "route.plan", requests = 38100, tokens = 21500000L },
                new { capability = "ocr.invoice_extraction", requests = 22400, tokens = 9800000L },
                new { capability = "compliance.rag", requests = 18150, tokens = 5754620L }
            },
            generatedAt = DateTime.UtcNow
        });
    }

    // --- Private Helper Methods ---

    private Metadata CreateHeaders(string? customTenantId = null)
    {
        var headers = new Metadata
        {
            { "x-service-id", "system-bff" }
        };

        var tenantId = !string.IsNullOrWhiteSpace(customTenantId) && Guid.TryParse(customTenantId, out var parsedTenant)
            ? parsedTenant
            : currentUser.TenantId;

        if (tenantId.HasValue)
            headers.Add("x-tenant-id", tenantId.Value.ToString());

        if (currentUser.UserId.HasValue)
            headers.Add("x-user-id", currentUser.UserId.Value.ToString());

        if (!string.IsNullOrEmpty(currentUser.Role))
            headers.Add("x-role", currentUser.Role);

        if (!string.IsNullOrEmpty(currentUser.TraceId))
            headers.Add("x-trace-id", currentUser.TraceId);

        return headers;
    }

    private static List<object> GetSystemCapabilities() =>
    [
        new
        {
            code = "route.plan",
            name = "Route Planning & Risk Optimization",
            category = "Logistics",
            description = "Tối ưu hoá lộ trình vận tải đa phương thức và đánh giá rủi ro thời tiết/tắc nghẽn.",
            operation = "GENERATE",
            defaultModelTier = "HIGH",
            defaultMaxTokens = 4000,
            defaultAutomationLevel = "SEMI_AUTONOMOUS",
            allowedProviders = new[] { "GEMINI", "AZURE_OPENAI" },
            requireApproval = false
        },
        new
        {
            code = "mail.bec_check",
            name = "Business Email Compromise (BEC) Detection",
            category = "Security",
            description = "Phát hiện lừa đảo mạo danh đối tác thương mại và thay đổi tài khoản thanh toán.",
            operation = "GENERATE",
            defaultModelTier = "MEDIUM",
            defaultMaxTokens = 1000,
            defaultAutomationLevel = "FULL_AUTONOMOUS",
            allowedProviders = new[] { "GEMINI" },
            requireApproval = false
        },
        new
        {
            code = "mail.phishing_check",
            name = "Phishing & Suspicious Link Detection",
            category = "Security",
            description = "Phân tích nội dung email và liên kết độc hại trong luồng Inbound Mail.",
            operation = "GENERATE",
            defaultModelTier = "FAST",
            defaultMaxTokens = 500,
            defaultAutomationLevel = "FULL_AUTONOMOUS",
            allowedProviders = new[] { "GEMINI" },
            requireApproval = false
        },
        new
        {
            code = "mail.security",
            name = "Inbound Mail Holistic Threat Inspection",
            category = "Security",
            description = "Tổng hợp an ninh email kết hợp mã độc ClamAV và phân tích LLM.",
            operation = "GENERATE",
            defaultModelTier = "MEDIUM",
            defaultMaxTokens = 1500,
            defaultAutomationLevel = "FULL_AUTONOMOUS",
            allowedProviders = new[] { "GEMINI" },
            requireApproval = false
        },
        new
        {
            code = "ocr.invoice_extraction",
            name = "Commercial Invoice Data Extraction",
            category = "OCR",
            description = "Trích xuất dữ liệu có cấu trúc từ hoá đơn thương mại vận tải (PDF, Ảnh).",
            operation = "GENERATE",
            defaultModelTier = "HIGH",
            defaultMaxTokens = 3000,
            defaultAutomationLevel = "SEMI_AUTONOMOUS",
            allowedProviders = new[] { "GEMINI", "AZURE_OPENAI" },
            requireApproval = true
        },
        new
        {
            code = "ocr.customs_extraction",
            name = "Customs Declaration Document Extraction",
            category = "OCR",
            description = "Trích xuất thông tin tờ khai hải quan, mã HS và thuế suất.",
            operation = "GENERATE",
            defaultModelTier = "HIGH",
            defaultMaxTokens = 3500,
            defaultAutomationLevel = "SEMI_AUTONOMOUS",
            allowedProviders = new[] { "GEMINI" },
            requireApproval = true
        },
        new
        {
            code = "ocr.bill_of_lading",
            name = "Bill of Lading Extraction",
            category = "OCR",
            description = "Trích xuất thông tin vận đơn đường biển/hàng không (B/L, AWB).",
            operation = "GENERATE",
            defaultModelTier = "HIGH",
            defaultMaxTokens = 3000,
            defaultAutomationLevel = "SEMI_AUTONOMOUS",
            allowedProviders = new[] { "GEMINI" },
            requireApproval = true
        },
        new
        {
            code = "compliance.embed",
            name = "Regulatory Knowledge Vector Embedding",
            category = "Compliance",
            description = "Tạo vector embeddings 768 chiều cho văn bản luật, nghị định và quy chuẩn logistics.",
            operation = "EMBED",
            defaultModelTier = "FAST",
            defaultMaxTokens = 2000,
            defaultAutomationLevel = "FULL_AUTONOMOUS",
            allowedProviders = new[] { "GEMINI" },
            requireApproval = false
        },
        new
        {
            code = "compliance.rag",
            name = "Regulatory Compliance Grounded Assistant",
            category = "Compliance",
            description = "Hỏi đáp pháp lý có trích dẫn văn bản nguồn chính xác, chống hallucination.",
            operation = "GENERATE",
            defaultModelTier = "HIGH",
            defaultMaxTokens = 2500,
            defaultAutomationLevel = "SEMI_AUTONOMOUS",
            allowedProviders = new[] { "GEMINI", "AZURE_OPENAI" },
            requireApproval = false
        },
        new
        {
            code = "negotiation.strategy",
            name = "Freight Rate & Contract Negotiation Strategy",
            category = "Commercial",
            description = "Đề xuất chiến lược đàm phán giá cước và soạn thảo thư phản hồi đối tác.",
            operation = "GENERATE",
            defaultModelTier = "HIGH",
            defaultMaxTokens = 2500,
            defaultAutomationLevel = "SEMI_AUTONOMOUS",
            allowedProviders = new[] { "GEMINI", "AZURE_OPENAI" },
            requireApproval = true
        },
        new
        {
            code = "customer.assistant",
            name = "Customer Support & Inquiries Assistant",
            category = "CustomerService",
            description = "Trợ lý hỗ trợ khách hàng theo dõi đơn hàng và giải đáp thắc mắc dịch vụ.",
            operation = "GENERATE",
            defaultModelTier = "MEDIUM",
            defaultMaxTokens = 1500,
            defaultAutomationLevel = "FULL_AUTONOMOUS",
            allowedProviders = new[] { "GEMINI" },
            requireApproval = false
        },
        new
        {
            code = "devops.rca",
            name = "Autonomous Root Cause Analysis",
            category = "DevOps",
            description = "Tự động phân tích logs, traces để chẩn đoán sự cố hệ thống.",
            operation = "GENERATE",
            defaultModelTier = "HIGH",
            defaultMaxTokens = 4000,
            defaultAutomationLevel = "SEMI_AUTONOMOUS",
            allowedProviders = new[] { "GEMINI", "AZURE_OPENAI" },
            requireApproval = true
        }
    ];

    // --- DTOs ---
    public record TestPolicyBody(string CapabilityCode, long EstimatedInputTokens, long MaxOutputTokens, string? TenantId);
    public record PlaygroundGenerateBody(string CapabilityCode, string Prompt, long EstimatedInputTokens, long MaxOutputTokens, Dictionary<string, string>? Parameters, string? TenantId);
    public record PlaygroundEmbedBody(string CapabilityCode, string Content, int Dimensions, long EstimatedInputTokens, string? TenantId);
}
