using System;
using Shared.Entity;
using RoutePlanningAgent.Domain.Enums;

namespace RoutePlanningAgent.Domain;

public class ApprovalRequest : TenantAuditableEntity
{
    public Guid RouteId { get; set; }
    public int RouteVersion { get; set; } = 1;
    public string PolicyId { get; set; } = "platform-default-route-governance";
    public int PolicyVersion { get; set; } = 1;
    public string Feature { get; set; } = default!;
    public ApprovalStatus Status { get; set; } = ApprovalStatus.Pending;
    public string Reason { get; set; } = default!;
    public string AiSummary { get; set; } = default!;
    public string? ComplianceSummary { get; set; }
    public Guid? ReviewedByUserId { get; set; }
    public DateTimeOffset? ReviewedAt { get; set; }
    public string? ReviewerComment { get; set; }

    /// <summary>Lý do từ chối — bắt buộc khi Status = Rejected.</summary>
    public string? RejectionReason { get; set; }
    public Route Route { get; set; } = default!;
}
