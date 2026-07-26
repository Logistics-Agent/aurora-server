using System;
using System.Collections.Generic;

namespace RoutePlanningAgent.Application.DTOs.Routes;

public record RouteRecommendationDto
{
    public Guid RouteId { get; init; }
    public string RiskLevel { get; init; } = string.Empty;
    public string AutomationDecision { get; init; } = string.Empty;
    public string RecommendationSource { get; init; } = string.Empty;
    public string Summary { get; init; } = string.Empty;
    public List<string> Suggestions { get; init; } = [];
    public double? ConfidenceScore { get; init; }
    public Guid? ApprovalRequestId { get; init; }
    public List<string> ApplicableRegulations { get; init; } = [];
}
