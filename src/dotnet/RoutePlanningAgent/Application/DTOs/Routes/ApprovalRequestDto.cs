using System;

namespace RoutePlanningAgent.Application.DTOs.Routes;

public record ApprovalRequestDto
{
    public Guid Id { get; init; }
    public Guid RouteId { get; init; }
    public string RouteName { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string Reason { get; init; } = string.Empty;
    public string AiSummary { get; init; } = string.Empty;
    public string? ComplianceSummary { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
}
