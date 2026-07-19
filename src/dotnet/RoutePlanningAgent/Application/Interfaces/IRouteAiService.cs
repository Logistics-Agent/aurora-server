using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using RoutePlanningAgent.Application.DTOs.Routes;
using RoutePlanningAgent.Domain;
using Shared.Rules;

namespace RoutePlanningAgent.Application.Interfaces;

public interface IRouteAiService
{
    Task<RouteRecommendationDto> GetRecommendationAsync(
        Route route,
        IReadOnlyList<RuleResult> ruleResults,
        ComplianceCheckResultDto? complianceResult,
        string provider,
        CancellationToken ct = default);
}
