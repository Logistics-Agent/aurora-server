using System.Collections.Generic;

namespace RoutePlanningAgent.Application.DTOs.Routes;

public record ComplianceCheckResultDto
{
    public bool HasViolations { get; init; }
    public List<string> ViolationSummaries { get; init; } = [];
    public List<string> DocumentRefs { get; init; } = [];
    public List<string> ApplicableRegulations { get; init; } = [];
    public string MergedContext { get; init; } = string.Empty;
    public bool RequiresHumanApproval { get; init; }
}
