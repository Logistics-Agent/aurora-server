package rules

import (
	"context"
	"fmt"

	"aurora/route-planning-agent/internal/domain/enums"
	domainrules "aurora/route-planning-agent/internal/domain/rules"
)

type LongDurationRule struct{}

const (
	LongDurationRuleName = "LongDurationRule"
	LongDurationRuleCode = "ROUTE_DURATION_LIMIT"

	DefaultLongDurationMaxMinutes = 480.0
)

func NewLongDurationRule() *LongDurationRule {
	return &LongDurationRule{}
}

func (r *LongDurationRule) Name() string { return LongDurationRuleName }
func (r *LongDurationRule) Code() string { return LongDurationRuleCode }

func (r *LongDurationRule) CanApply(ctx *domainrules.RouteRuleContext) bool {
	return ctx.Route != nil && ctx.Route.EstimatedDurationMinutes > 0
}

func (r *LongDurationRule) Evaluate(ctx context.Context, ruleCtx *domainrules.RouteRuleContext) (*domainrules.RuleResult, error) {
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

	maxDuration := thresholds.Get("maxDurationMinutes", DefaultLongDurationMaxMinutes)

	duration := float64(ruleCtx.Route.EstimatedDurationMinutes)
	if duration > maxDuration {
		return &domainrules.RuleResult{
			RuleName:  r.Name(),
			RuleCode:  r.Code(),
			Passed:    false,
			RiskLevel: enums.RouteRiskLevelHigh,
			Message:   fmt.Sprintf("EstimatedDurationMinutes %d vượt ngưỡng %.0f phút", ruleCtx.Route.EstimatedDurationMinutes, maxDuration),
		}, nil
	}

	return &domainrules.RuleResult{
		RuleName:  r.Name(),
		RuleCode:  r.Code(),
		Passed:    true,
		RiskLevel: enums.RouteRiskLevelLow,
	}, nil
}

