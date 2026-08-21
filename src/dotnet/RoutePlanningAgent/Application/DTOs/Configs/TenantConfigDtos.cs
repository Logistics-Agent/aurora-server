using System;
using System.Collections.Generic;

namespace RoutePlanningAgent.Application.DTOs.Configs;

public record TenantAiConfigDto
{
    public Guid Id { get; init; }
    public Guid TenantId { get; init; }
    public string Feature { get; init; } = string.Empty;
    public string Policy { get; init; } = string.Empty;
    public string AiProvider { get; init; } = string.Empty;
    public bool IsActive { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
}

public record TenantRuleConfigDto
{
    public Guid Id { get; init; }
    public Guid TenantId { get; init; }
    public string RuleName { get; init; } = string.Empty;
    public bool IsEnabled { get; init; }
    public Dictionary<string, decimal> Thresholds { get; init; } = [];
    public DateTimeOffset UpdatedAt { get; init; }
}
