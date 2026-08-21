using MassTransit;
using System;

namespace Shared.Events;

[EntityName("ai_usage_event")]
public record AiUsageEvent
{
    public Guid TenantId { get; init; }
    public string ServiceName { get; init; } = string.Empty;    // "RoutePlanningAgent"
    public string Feature { get; init; } = string.Empty;        // "RouteRecommendation"
    public string Provider { get; init; } = string.Empty;       // "Gemini" | "AzureOpenAI" | "AwsClaude"
    public string Model { get; init; } = string.Empty;
    public string PromptVersion { get; init; } = string.Empty;
    public int InputTokens { get; init; }
    public int OutputTokens { get; init; }
    public long LatencyMs { get; init; }
    public bool Success { get; init; }
    public DateTimeOffset OccurredAt { get; init; }
}
