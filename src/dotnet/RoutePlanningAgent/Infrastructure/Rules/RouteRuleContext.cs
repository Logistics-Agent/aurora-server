using System;
using RoutePlanningAgent.Application.Interfaces;
using Route = RoutePlanningAgent.Domain.Route; // tránh nhầm với Microsoft.AspNetCore.Routing.Route

namespace RoutePlanningAgent.Infrastructure.Rules;

public record RouteRuleContext(
    Route Route,
    Guid TenantId,
    ITenantRuleConfigService RuleConfigService
);
