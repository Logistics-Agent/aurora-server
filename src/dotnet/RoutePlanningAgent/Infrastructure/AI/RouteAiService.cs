using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using AiGovernance.Grpc;
using RoutePlanningAgent.Application.DTOs.Routes;
using RoutePlanningAgent.Application.Interfaces;
using RoutePlanningAgent.Application.Options;
using Shared.Rules;

namespace RoutePlanningAgent.Infrastructure.AI;

/// <summary>
/// Gọi LLM thông qua dịch vụ tập trung AI Governance (AiExecutionService.Generate) — KHÔNG gọi trực tiếp LLM provider.
/// Toàn bộ cấu hình AI, quota, routing, provider và model thuộc quyền quản trị của AiGovernance.
/// Token usage và provider metadata được lấy trực tiếp từ phản hồi của AI Governance.
/// </summary>
public class RouteAiService(
    AiExecutionService.AiExecutionServiceClient aiExecutionClient,
    IOptions<RoutePlanningOptions> options,
    ILogger<RouteAiService> logger)
    : IRouteAiService
{
    private const string PromptVersion = "v2.0";

    private const string SystemPrompt =
        """
        Bạn là chuyên gia phân tích rủi ro logistics cho hệ thống lập kế hoạch tuyến vận chuyển.
        Nhiệm vụ: phân tích tuyến đường (JSON), các vi phạm rule engine và ngữ cảnh tuân thủ (nếu có),
        sau đó trả về khuyến nghị.

        BẮT BUỘC trả về DUY NHẤT một JSON object đúng schema sau, không thêm văn bản nào khác:
        {
          "summary": "<tóm tắt phân tích rủi ro và khuyến nghị, tiếng Việt, tối đa 500 ký tự>",
          "confidence": <số 0.0-1.0>,
          "suggestions": ["<đề xuất cải thiện 1>", "<đề xuất 2>", ...]
        }
        """;

    public async Task<RouteAiResult> GetRecommendationAsync(
        RouteDto route,
        IReadOnlyList<RuleResult> ruleResults,
        ComplianceCheckResultDto? complianceResult,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();

        var prompt = $"""
            {SystemPrompt}

            [ROUTE]
            {JsonSerializer.Serialize(route)}

            [RULE_VIOLATIONS]
            {JsonSerializer.Serialize(ruleResults)}

            [COMPLIANCE_CONTEXT]
            {complianceResult?.MergedContext ?? "(không có)"}
            """;

        string llmResponse = string.Empty;
        var inputTokens = 0L;
        var outputTokens = 0L;
        var model = string.Empty;
        var usedProvider = string.Empty;
        var success = true;

        try
        {
            var capability = !string.IsNullOrWhiteSpace(options.Value.CapabilityCode)
                ? options.Value.CapabilityCode
                : "route.plan";

            var request = new AiGenerateRequest
            {
                CapabilityCode = capability,
                Prompt = prompt,
                MaxOutputTokens = 1000
            };

            var response = await aiExecutionClient.GenerateAsync(request, cancellationToken: ct);

            llmResponse = response.Content ?? string.Empty;
            inputTokens = response.InputTokens;
            outputTokens = response.OutputTokens;
            model = response.Model ?? string.Empty;
            usedProvider = response.Provider ?? string.Empty;
        }
        catch (Exception ex)
        {
            success = false;
            logger.LogError(ex, "AI Governance Generate call failed for route {RouteId}", route.Id);
        }

        sw.Stop();

        var recommendation = success
            ? ParseLlmResponse(llmResponse, route.Id, complianceResult)
            : BuildFallback(route.Id, ruleResults, complianceResult);

        return new RouteAiResult
        {
            Recommendation = recommendation,
            Provider = usedProvider,
            Model = model,
            PromptVersion = PromptVersion,
            InputTokens = (int)inputTokens,
            OutputTokens = (int)outputTokens,
            LatencyMs = sw.ElapsedMilliseconds,
            Success = success
        };
    }

    internal static RouteRecommendationDto ParseLlmResponse(
        string llmResponse, Guid routeId, ComplianceCheckResultDto? complianceResult)
    {
        var summary = llmResponse;
        var suggestions = new List<string>();
        double confidence = 0.5;

        try
        {
            // Bóc code fence nếu LLM trả ```json ... ```
            var json = llmResponse.Trim();
            if (json.StartsWith("```"))
            {
                var firstNewline = json.IndexOf('\n');
                var lastFence = json.LastIndexOf("```", StringComparison.Ordinal);
                if (firstNewline >= 0 && lastFence > firstNewline)
                    json = json[(firstNewline + 1)..lastFence].Trim();
            }

            if (json.StartsWith('{'))
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                if (root.TryGetProperty("summary", out var sProp)) summary = sProp.GetString() ?? summary;
                if (root.TryGetProperty("confidence", out var cProp) && cProp.ValueKind == JsonValueKind.Number)
                    confidence = cProp.GetDouble();
                if (root.TryGetProperty("suggestions", out var sugProp) && sugProp.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in sugProp.EnumerateArray())
                    {
                        var s = item.GetString();
                        if (!string.IsNullOrWhiteSpace(s)) suggestions.Add(s);
                    }
                }
            }
        }
        catch (JsonException)
        {
            // LLM trả text tự do — dùng nguyên văn làm summary
        }

        return new RouteRecommendationDto
        {
            RouteId = routeId,
            RiskLevel = complianceResult is { HasViolations: true } ? "High" : "Medium",
            AutomationDecision = "ExecutedByAi",
            RecommendationSource = "AI",
            Summary = summary,
            Suggestions = suggestions,
            ConfidenceScore = confidence,
            ApplicableRegulations = complianceResult?.ApplicableRegulations ?? []
        };
    }

    /// <summary>Fallback khi LLM lỗi — degrade về kết quả rule-based, KHÔNG giả vờ là AI.</summary>
    private static RouteRecommendationDto BuildFallback(
        Guid routeId, IReadOnlyList<RuleResult> ruleResults, ComplianceCheckResultDto? complianceResult)
    {
        var failed = new List<string>();
        foreach (var r in ruleResults)
        {
            if (!r.Passed && !string.IsNullOrWhiteSpace(r.Message)) failed.Add(r.Message);
        }

        return new RouteRecommendationDto
        {
            RouteId = routeId,
            RiskLevel = complianceResult is { HasViolations: true } ? "High" : "Medium",
            AutomationDecision = "ExecutedByRules",
            RecommendationSource = "Rules",
            Summary = failed.Count > 0
                ? $"AiGovernance không khả dụng — kết quả rule engine: {string.Join("; ", failed)}"
                : "AiGovernance không khả dụng — rule engine không phát hiện vi phạm.",
            Suggestions = [],
            ConfidenceScore = null,
            ApplicableRegulations = complianceResult?.ApplicableRegulations ?? []
        };
    }
}
