using System.Linq;
using RoutePlanningAgent.Application.DTOs.Routes;
using RoutePlanningAgent.Domain;
using Route = RoutePlanningAgent.Domain.Route; // tránh nhầm với Microsoft.AspNetCore.Routing.Route

namespace RoutePlanningAgent.Application.Mapping;

/// <summary>
/// Mapper dùng chung Route → RouteDto.
/// Serialize DTO (không phải entity) để tránh JSON cycle (Route.Stops → RouteStop.Route).
/// </summary>
public static class RouteMapper
{
    public static RouteDto ToDto(Route route) => new()
    {
        Id = route.Id,
        TenantId = route.TenantId,
        Name = route.Name,
        Description = route.Description,
        RouteType = route.Type.ToString(),
        Status = route.Status.ToString(),
        RiskLevel = route.RiskLevel.ToString(),
        GovernanceDecision = route.GovernanceDecision.ToString(),
        EstimatedDistanceKm = route.EstimatedDistanceKm,
        EstimatedDurationMinutes = route.EstimatedDurationMinutes,
        MaxWeightKg = route.MaxWeightKg,
        MaxVolumeM3 = route.MaxVolumeM3,
        IsAiGenerated = route.IsAiGenerated,
        OptimizedAt = route.OptimizedAt,
        Version = route.Version,
        CreatedAt = route.CreatedAt,
        Stops = route.Stops.Select(s => new RouteStopDto
        {
            Id = s.Id,
            Sequence = s.Sequence,
            StopType = s.StopType.ToString(),
            LocationName = s.LocationName,
            Address = s.Address,
            Latitude = s.Latitude,
            Longitude = s.Longitude,
            EstimatedArrivalMinutes = s.EstimatedArrivalMinutes,
            ServiceDurationMinutes = s.ServiceDurationMinutes
        }).OrderBy(s => s.Sequence).ToList()
    };

    public static ApprovalRequestDto ToApprovalDto(ApprovalRequest approval) => new()
    {
        Id = approval.Id,
        RouteId = approval.RouteId,
        RouteName = approval.Route?.Name ?? string.Empty,
        Status = approval.Status.ToString(),
        Reason = approval.Reason,
        AiSummary = approval.AiSummary,
        ComplianceSummary = approval.ComplianceSummary,
        RejectionReason = approval.RejectionReason,
        CreatedAt = approval.CreatedAt
    };

    public static RoutePlanningAgent.Application.DTOs.Configs.TenantRiskRuleDto ToRuleDto(TenantRiskRule rule) => new(
        rule.Id,
        rule.PolicyId,
        rule.RuleCode,
        rule.RuleName,
        rule.ThresholdsJson,
        rule.RiskEffect.ToString(),
        rule.IsEnabled,
        rule.SourceReference,
        rule.CreatedAt
    );

    public static RoutePlanningAgent.Application.DTOs.Configs.TenantRiskPolicyDto ToPolicyDto(TenantRiskPolicy policy) => new(
        policy.Id,
        policy.TenantId,
        policy.Name,
        policy.Description,
        policy.Scope,
        policy.Version,
        policy.Status.ToString(),
        policy.Source.ToString(),
        policy.SourceDocumentId,
        policy.SubmittedByUserId,
        policy.SubmittedAt,
        policy.ReviewedByUserId,
        policy.ReviewedAt,
        policy.ReviewerComment,
        policy.PublishedByUserId,
        policy.PublishedAt,
        policy.RejectionReason,
        policy.SupersededAt,
        policy.CreatedAt,
        policy.UpdatedAt,
        policy.Rules?.Select(ToRuleDto).ToList() ?? []
    );
}
