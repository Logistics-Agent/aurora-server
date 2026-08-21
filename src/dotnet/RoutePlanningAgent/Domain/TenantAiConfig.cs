using System;
using Shared.Entity;
using Shared.Enums;

namespace RoutePlanningAgent.Domain;

public class TenantAiConfig : BaseEntity
{
    public Guid TenantId { get; set; }
    public string Feature { get; set; } = default!;
    public AutomationPolicy Policy { get; set; }
    public string AiProvider { get; set; } = "Gemini";
    public bool IsActive { get; set; } = true;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
