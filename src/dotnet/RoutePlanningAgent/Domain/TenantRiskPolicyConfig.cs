using System;
using Shared.Entity;
using Shared.Enums;

namespace RoutePlanningAgent.Domain;

public class TenantRiskPolicyConfig : BaseEntity
{
    public Guid TenantId { get; set; }
    public RiskPolicyMode PolicyMode { get; set; } = RiskPolicyMode.UsePlatformDefault;
    public string ActivePolicyId { get; set; } = "platform-default-route-governance";
    public int ActivePolicyVersion { get; set; } = 1;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
