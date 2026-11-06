package rules

import (
	"context"
	"fmt"

	"aurora/route-planning-agent/internal/domain/enums"
	domainrules "aurora/route-planning-agent/internal/domain/rules"
)

type MultiHubRule struct{}

const (
	MultiHubRuleName = "MultiHubRule"
	MultiHubRuleCode = "ROUTE_MULTI_HUB_COMPLEXITY"

	DefaultMultiHubMaxCount = 2.0
)

func NewMultiHubRule() *MultiHubRule {
	return &MultiHubRule{}
}

func (r *MultiHubRule) Name() string { return MultiHubRuleName }
func (r *MultiHubRule) Code() string { return MultiHubRuleCode }

func (r *MultiHubRule) CanApply(ctx *domainrules.RouteRuleContext) bool {
	return true
}

func (r *MultiHubRule) Evaluate(ctx context.Context, ruleCtx *domainrules.RouteRuleContext) (*domainrules.RuleResult, error) {
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

	maxHubCount := thresholds.Get("maxHubCount", DefaultMultiHubMaxCount)

	hubCount := 0
	for _, s := range ruleCtx.Route.Stops {
		if s.StopType == enums.StopTypeHub || s.StopType == enums.StopTypePort || s.StopType == enums.StopTypeCustoms {
			hubCount++
		}
	}

	if float64(hubCount) > maxHubCount {
		return &domainrules.RuleResult{
			RuleName:                r.Name(),
			RuleCode:                r.Code(),
			Passed:                  false,
			RiskLevel:               enums.RouteRiskLevelHigh,
			Message:                 fmt.Sprintf("Số lượng Hub/Port/Customs stops (%d) vượt quá mức tối đa (%.0f)", hubCount, maxHubCount),
			RequiresApproval:        true,
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

