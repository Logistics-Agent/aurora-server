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
}
