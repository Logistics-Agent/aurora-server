using System;
using RoutePlanningAgent.Domain;
using RoutePlanningAgent.Application.Interfaces;

namespace RoutePlanningAgent.Infrastructure.Rules;

public record RouteRuleContext(
    Route Route,
    Guid TenantId,
    ITenantRuleConfigService RuleConfigService
);
