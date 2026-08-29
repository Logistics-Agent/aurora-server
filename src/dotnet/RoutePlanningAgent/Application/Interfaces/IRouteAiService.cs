using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using RoutePlanningAgent.Application.DTOs.Routes;
using Shared.Rules;

namespace RoutePlanningAgent.Application.Interfaces;

/// <summary>
/// Adapter thuần gọi LLM qua AiGovernance gRPC (không DbContext, không publish event — persistence thuộc về handler).
/// Nhận RouteDto (không phải entity) để tránh JSON cycle khi serialize.
/// </summary>
public interface IRouteAiService
{
    Task<RouteAiResult> GetRecommendationAsync(
        RouteDto route,
        IReadOnlyList<RuleResult> ruleResults,
        ComplianceCheckResultDto? complianceResult,
        CancellationToken ct = default);
}

/// <summary>Kết quả gọi LLM kèm telemetry THẬT (token usage lấy từ response metadata của AiGovernance).</summary>
public record RouteAiResult
{
    public required RouteRecommendationDto Recommendation { get; init; }
    public string Provider { get; init; } = string.Empty;
    public string Model { get; init; } = string.Empty;
    public string PromptVersion { get; init; } = "v2.0";
    public int InputTokens { get; init; }
    public int OutputTokens { get; init; }
    public long LatencyMs { get; init; }
    public bool Success { get; init; }
}
