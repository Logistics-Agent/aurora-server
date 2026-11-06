package rules

import (
	"context"
	"fmt"

	"aurora/route-planning-agent/internal/domain/enums"
	domainrules "aurora/route-planning-agent/internal/domain/rules"
)

type MinimumStopsRule struct{}

const (
	MinimumStopsRuleName = "MinimumStopsRule"
	MinimumStopsRuleCode = "ROUTE_MINIMUM_STOPS"

	DefaultMinimumStopsMin = 2.0
)

func NewMinimumStopsRule() *MinimumStopsRule {
	return &MinimumStopsRule{}
}

func (r *MinimumStopsRule) Name() string { return MinimumStopsRuleName }
func (r *MinimumStopsRule) Code() string { return MinimumStopsRuleCode }

func (r *MinimumStopsRule) CanApply(ctx *domainrules.RouteRuleContext) bool {
	return true
}

func (r *MinimumStopsRule) Evaluate(ctx context.Context, ruleCtx *domainrules.RouteRuleContext) (*domainrules.RuleResult, error) {
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

	minStops := thresholds.Get("minStops", DefaultMinimumStopsMin)

	stopCount := float64(len(ruleCtx.Route.Stops))
	if stopCount < minStops {
		return &domainrules.RuleResult{
			RuleName:  r.Name(),
			RuleCode:  r.Code(),
			Passed:    false,
			RiskLevel: enums.RouteRiskLevelLow,
			Message:   fmt.Sprintf("Số lượng stops (%d) ít hơn mức tối thiểu yêu cầu (%.0f)", len(ruleCtx.Route.Stops), minStops),
		}, nil
	}

	return &domainrules.RuleResult{
		RuleName:  r.Name(),
		RuleCode:  r.Code(),
		Passed:    true,
		RiskLevel: enums.RouteRiskLevelLow,
	}, nil
}

