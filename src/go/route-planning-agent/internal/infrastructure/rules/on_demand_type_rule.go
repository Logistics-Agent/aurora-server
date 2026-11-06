package rules

import (
	"context"

	"aurora/route-planning-agent/internal/domain/enums"
	domainrules "aurora/route-planning-agent/internal/domain/rules"
)

type OnDemandTypeRule struct{}

const (
	OnDemandTypeRuleName = "OnDemandTypeRule"
	OnDemandTypeRuleCode = "ROUTE_ON_DEMAND_OPERATION"
)

func NewOnDemandTypeRule() *OnDemandTypeRule {
	return &OnDemandTypeRule{}
}

func (r *OnDemandTypeRule) Name() string { return OnDemandTypeRuleName }
func (r *OnDemandTypeRule) Code() string { return OnDemandTypeRuleCode }

func (r *OnDemandTypeRule) CanApply(ctx *domainrules.RouteRuleContext) bool {
	return true
}

func (r *OnDemandTypeRule) Evaluate(ctx context.Context, ruleCtx *domainrules.RouteRuleContext) (*domainrules.RuleResult, error) {
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

	if ruleCtx.Route.Type == enums.RouteTypeOnDemand {
		return &domainrules.RuleResult{
			RuleName:  r.Name(),
			RuleCode:  r.Code(),
			Passed:    false,
			RiskLevel: enums.RouteRiskLevelMedium,
			Message:   "Tuyến vận chuyển thuộc loại OnDemand (Yêu cầu tức thì)",
		}, nil
	}

	return &domainrules.RuleResult{
		RuleName:  r.Name(),
		RuleCode:  r.Code(),
		Passed:    true,
		RiskLevel: enums.RouteRiskLevelLow,
	}, nil
}

