using System;
using System.Collections.Generic;
using RoutePlanningAgent.Domain.Enums;
using Shared.Entity;
using Shared.Enums;

namespace RoutePlanningAgent.Domain;

public class TenantRiskPolicy : TenantAuditableEntity
{
    public string Name { get; set; } = default!;
    public string? Description { get; set; }
    public string Scope { get; set; } = "RoutePlanning";
    public int Version { get; set; } = 1;
    public TenantRiskPolicyStatus Status { get; set; } = TenantRiskPolicyStatus.Draft;
    public RiskPolicySource Source { get; set; } = RiskPolicySource.Tenant;
    public string? SourceDocumentId { get; set; }

    // Lifecycle Auditing & Approvals
    public Guid? SubmittedByUserId { get; set; }
    public DateTimeOffset? SubmittedAt { get; set; }
    public Guid? ReviewedByUserId { get; set; }
    public DateTimeOffset? ReviewedAt { get; set; }
    public string? ReviewerComment { get; set; }
    public Guid? PublishedByUserId { get; set; }
    public DateTimeOffset? PublishedAt { get; set; }
    public string? RejectionReason { get; set; }
    public DateTimeOffset? SupersededAt { get; set; }
    public bool IsDeleted { get; set; }

    public ICollection<TenantRiskRule> Rules { get; set; } = new List<TenantRiskRule>();
}
