package rules

import (
	"context"
	"fmt"

	"aurora/route-planning-agent/internal/domain/enums"
	domainrules "aurora/route-planning-agent/internal/domain/rules"
)

type HeavyWeightRule struct{}

const (
	HeavyWeightRuleName = "HeavyWeightRule"
	HeavyWeightRuleCode = "ROUTE_WEIGHT_CAPACITY"

	DefaultHeavyWeightHighRiskKg = 5000.0
	DefaultHeavyWeightApprovalKg = 3000.0
)

func NewHeavyWeightRule() *HeavyWeightRule {
	return &HeavyWeightRule{}
}

func (r *HeavyWeightRule) Name() string { return HeavyWeightRuleName }
func (r *HeavyWeightRule) Code() string { return HeavyWeightRuleCode }

func (r *HeavyWeightRule) CanApply(ctx *domainrules.RouteRuleContext) bool {
	return ctx.Route != nil && ctx.Route.MaxWeightKg > 0
}

func (r *HeavyWeightRule) Evaluate(ctx context.Context, ruleCtx *domainrules.RouteRuleContext) (*domainrules.RuleResult, error) {
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

	highRiskKg := thresholds.Get("maxWeightKg", DefaultHeavyWeightHighRiskKg)
	approvalKg := thresholds.Get("requiresApprovalThreshold", DefaultHeavyWeightApprovalKg)

	weight := ruleCtx.Route.MaxWeightKg
	if weight > highRiskKg {
		return &domainrules.RuleResult{
			RuleName:                r.Name(),
			RuleCode:                r.Code(),
			Passed:                  false,
			RiskLevel:               enums.RouteRiskLevelHigh,
			Message:                 fmt.Sprintf("MaxWeightKg %.2f vượt ngưỡng %.2fkg", weight, highRiskKg),
			RequiresApproval:        weight > approvalKg,
			RequiresComplianceCheck: true,
		}, nil
	}

	return &domainrules.RuleResult{
		RuleName:  r.Name(),
		RuleCode:  r.Code(),
		Passed:    true,
		RiskLevel: enums.RouteRiskLevelLow,
	}, nil
}

