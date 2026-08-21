using System.Collections.Generic;
using Shared.Rules;
using RoutePlanningAgent.Application.Interfaces;

namespace RoutePlanningAgent.Infrastructure.Rules;

public class RouteRuleEngine(IEnumerable<IRule<RouteRuleContext>> rules) 
    : RuleEngineBase<RouteRuleContext>(rules), IRouteRuleEngine;
