package rules

import (
	"context"
	"fmt"

	"aurora/route-planning-agent/internal/domain/enums"
	domainrules "aurora/route-planning-agent/internal/domain/rules"
)

type RouteStopCountRule struct{}

const (
	RouteStopCountRuleName = "RouteStopCountRule"
	RouteStopCountRuleCode = "ROUTE_STOP_COUNT_BOUNDS"

	DefaultRouteStopHighRiskThreshold   = 20.0
	DefaultRouteStopMediumRiskThreshold = 15.0
)

func NewRouteStopCountRule() *RouteStopCountRule {
	return &RouteStopCountRule{}
}

func (r *RouteStopCountRule) Name() string { return RouteStopCountRuleName }
func (r *RouteStopCountRule) Code() string { return RouteStopCountRuleCode }

func (r *RouteStopCountRule) CanApply(ctx *domainrules.RouteRuleContext) bool {
	return true
}

func (r *RouteStopCountRule) Evaluate(ctx context.Context, ruleCtx *domainrules.RouteRuleContext) (*domainrules.RuleResult, error) {
	thresholds, err := ruleCtx.RuleConfigService.GetThresholds(ctx, ruleCtx.TenantID, r.Name())
	if err != nil {
		return nil, err
	}

	if !thresholds.IsEnabled {
		return &domainrules.RuleResult{
			RuleName:  r.Name(),
			RuleCode:  r.Code(),
			Passed:    true,
			RiskLevel: enums.RouteRiskLevelLow,
		}, nil
	}

	highThreshold := thresholds.Get("highRiskThreshold", DefaultRouteStopHighRiskThreshold)
	mediumThreshold := thresholds.Get("mediumRiskThreshold", DefaultRouteStopMediumRiskThreshold)

	stopCount := float64(len(ruleCtx.Route.Stops))

	if stopCount > highThreshold {
		return &domainrules.RuleResult{
			RuleName:  r.Name(),
			RuleCode:  r.Code(),
			Passed:    false,
			RiskLevel: enums.RouteRiskLevelHigh,
			Message:   fmt.Sprintf("Số lượng stops (%d) vượt ngưỡng High Risk (%.0f)", len(ruleCtx.Route.Stops), highThreshold),
		}, nil
	}

	if stopCount > mediumThreshold {
		return &domainrules.RuleResult{
			RuleName:  r.Name(),
			RuleCode:  r.Code(),
			Passed:    false,
			RiskLevel: enums.RouteRiskLevelMedium,
			Message:   fmt.Sprintf("Số lượng stops (%d) vượt ngưỡng Medium Risk (%.0f)", len(ruleCtx.Route.Stops), mediumThreshold),
		}, nil
	}

	return &domainrules.RuleResult{
		RuleName:  r.Name(),
		RuleCode:  r.Code(),
		Passed:    true,
		RiskLevel: enums.RouteRiskLevelLow,
	}, nil
}

