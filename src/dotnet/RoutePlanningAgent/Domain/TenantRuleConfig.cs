using System;
using Shared.Entity;

namespace RoutePlanningAgent.Domain;

public class TenantRuleConfig : BaseEntity
{
    public Guid TenantId { get; set; }
    public string RuleName { get; set; } = default!;
    public bool IsEnabled { get; set; } = true;
    public string ThresholdsJson { get; set; } = "{}";
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
