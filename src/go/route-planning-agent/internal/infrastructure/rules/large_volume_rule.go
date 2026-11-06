package rules

import (
	"context"
	"fmt"

	"aurora/route-planning-agent/internal/domain/enums"
	domainrules "aurora/route-planning-agent/internal/domain/rules"
)

type LargeVolumeRule struct{}

const (
	LargeVolumeRuleName = "LargeVolumeRule"
	LargeVolumeRuleCode = "ROUTE_VOLUME_CAPACITY"

	DefaultLargeVolumeHighRiskM3 = 50.0
	DefaultLargeVolumeApprovalM3 = 40.0
)

func NewLargeVolumeRule() *LargeVolumeRule {
	return &LargeVolumeRule{}
}

func (r *LargeVolumeRule) Name() string { return LargeVolumeRuleName }
func (r *LargeVolumeRule) Code() string { return LargeVolumeRuleCode }

func (r *LargeVolumeRule) CanApply(ctx *domainrules.RouteRuleContext) bool {
	return ctx.Route != nil && ctx.Route.MaxVolumeM3 > 0
}

func (r *LargeVolumeRule) Evaluate(ctx context.Context, ruleCtx *domainrules.RouteRuleContext) (*domainrules.RuleResult, error) {
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

	highRiskM3 := thresholds.Get("maxVolumeM3", DefaultLargeVolumeHighRiskM3)
	approvalM3 := thresholds.Get("requiresApprovalThreshold", DefaultLargeVolumeApprovalM3)

	volume := ruleCtx.Route.MaxVolumeM3
	if volume > highRiskM3 {
		return &domainrules.RuleResult{
			RuleName:                r.Name(),
			RuleCode:                r.Code(),
			Passed:                  false,
			RiskLevel:               enums.RouteRiskLevelHigh,
			Message:                 fmt.Sprintf("MaxVolumeM3 %.2f vượt ngưỡng %.2fm³", volume, highRiskM3),
			RequiresApproval:        volume > approvalM3,
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

