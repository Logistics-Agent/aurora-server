using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using RoutePlanningAgent.Application.DTOs.Routes;
using RoutePlanningAgent.Application.Interfaces;
using Shared.AI;
using Shared.Rules;

namespace RoutePlanningAgent.Infrastructure.AI;

/// <summary>
/// Gọi LLM qua Semantic Kernel IChatCompletionService — KHÔNG dùng plugin stub.
/// Provider hỗ trợ: Gemini (gói Standard, round-robin key pool) và AzureOpenAI (gói Enterprise).
/// Token usage đọc từ response metadata — không bao giờ fabricate.
/// </summary>
public class RouteAiService(
    [FromKeyedServices("Gemini")] IApiKeyPool<string> geminiPool,
    IApiKeyPool<AzureOpenAiKeyEntry> azurePool,
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
        string provider,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();

        var (kernel, model) = BuildKernel(provider);

        var chatHistory = new ChatHistory(SystemPrompt);
        chatHistory.AddUserMessage(
            $"""
            [ROUTE]
            {JsonSerializer.Serialize(route)}

            [RULE_VIOLATIONS]
            {JsonSerializer.Serialize(ruleResults)}

            [COMPLIANCE_CONTEXT]
            {complianceResult?.MergedContext ?? "(không có)"}
            """);

        string llmResponse;
        var inputTokens = 0;
        var outputTokens = 0;
        var success = true;

        try
        {
            var chatService = kernel.GetRequiredService<IChatCompletionService>();
            var content = await chatService.GetChatMessageContentAsync(chatHistory, kernel: kernel, cancellationToken: ct);

            llmResponse = content.Content ?? string.Empty;
            (inputTokens, outputTokens) = ExtractUsage(content.Metadata);

            if (inputTokens == 0 && outputTokens == 0)
                logger.LogWarning("Không đọc được token usage từ {Provider} response metadata", provider);
        }
        catch (Exception ex)
        {
            success = false;
            llmResponse = string.Empty;
            logger.LogError(ex, "LLM call failed (provider={Provider}, model={Model})", provider, model);
        }

        sw.Stop();

        var recommendation = success
            ? ParseLlmResponse(llmResponse, route.Id, complianceResult)
            : BuildFallback(route.Id, ruleResults, complianceResult);

        return new RouteAiResult
        {
            Recommendation = recommendation,
            Provider = provider,
            Model = model,
            PromptVersion = PromptVersion,
            InputTokens = inputTokens,
            OutputTokens = outputTokens,
            LatencyMs = sw.ElapsedMilliseconds,
            Success = success
        };
    }

    private (Kernel Kernel, string Model) BuildKernel(string provider)
    {
        var builder = Kernel.CreateBuilder();

        switch (provider)
        {
            case "AzureOpenAI": // gói Enterprise
                var azureEntry = azurePool.GetNext();
                builder.AddAzureOpenAIChatCompletion(
                    deploymentName: azureEntry.DeploymentName,
                    endpoint: azureEntry.Endpoint,
                    apiKey: azureEntry.ApiKey);
                return (builder.Build(), azureEntry.DeploymentName);

            default: // Gemini — gói Standard
                var geminiKey = geminiPool.GetNext();
                var modelId = options.Value.GeminiModelId;
                builder.AddGoogleAIGeminiChatCompletion(
                    modelId: modelId,
                    apiKey: geminiKey);
                return (builder.Build(), modelId);
        }
    }

    /// <summary>
    /// Đọc token usage thật từ metadata:
    /// - Gemini connector: PromptTokenCount / CandidatesTokenCount
    /// - AzureOpenAI connector: metadata["Usage"] (OpenAI.Chat.ChatTokenUsage) — đọc qua reflection
    ///   để không phụ thuộc cứng vào OpenAI SDK types.
    /// </summary>
    internal static (int Input, int Output) ExtractUsage(IReadOnlyDictionary<string, object?>? metadata)
    {
        if (metadata is null) return (0, 0);

        if (metadata.TryGetValue("PromptTokenCount", out var prompt)
            && metadata.TryGetValue("CandidatesTokenCount", out var candidates))
        {
            return (ToInt(prompt), ToInt(candidates));
        }

        if (metadata.TryGetValue("Usage", out var usage) && usage is not null)
        {
            var type = usage.GetType();
            var input = type.GetProperty("InputTokenCount")?.GetValue(usage)
                        ?? type.GetProperty("PromptTokens")?.GetValue(usage);
            var output = type.GetProperty("OutputTokenCount")?.GetValue(usage)
                         ?? type.GetProperty("CompletionTokens")?.GetValue(usage);
            return (ToInt(input), ToInt(output));
        }

        return (0, 0);

        static int ToInt(object? value) => value is null ? 0 : Convert.ToInt32(value);
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
            AutomationDecision = "ExecutedByLlm",
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
                ? $"LLM không khả dụng — kết quả rule engine: {string.Join("; ", failed)}"
                : "LLM không khả dụng — rule engine không phát hiện vi phạm.",
            Suggestions = [],
            ConfidenceScore = null,
            ApplicableRegulations = complianceResult?.ApplicableRegulations ?? []
        };
    }
}
