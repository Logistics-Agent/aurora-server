package rules

import (
	"context"

	domainrules "aurora/route-planning-agent/internal/domain/rules"
)

type routeRuleEngine struct {
	rules []domainrules.Rule
}

// NewDefaultRouteRuleEngine khởi tạo Rule Engine với 7 quy tắc tất định mặc định
func NewDefaultRouteRuleEngine() domainrules.RuleEngine {
	return NewRouteRuleEngine([]domainrules.Rule{
		NewHeavyWeightRule(),
		NewLargeVolumeRule(),
		NewRouteStopCountRule(),
		NewOnDemandTypeRule(),
		NewLongDurationRule(),
		NewMinimumStopsRule(),
		NewMultiHubRule(),
	})
}

// NewRouteRuleEngine khởi tạo Rule Engine với danh sách rule tùy chỉnh
func NewRouteRuleEngine(rules []domainrules.Rule) domainrules.RuleEngine {
	return &routeRuleEngine{rules: rules}
}

func (e *routeRuleEngine) EvaluateAll(ctx context.Context, ruleCtx *domainrules.RouteRuleContext) ([]domainrules.RuleResult, error) {
	results := make([]domainrules.RuleResult, 0, len(e.rules))

	for _, rule := range e.rules {
		if rule.CanApply(ruleCtx) {
			res, err := rule.Evaluate(ctx, ruleCtx)
			if err != nil {
				return nil, err
			}
			if res != nil {
				results = append(results, *res)
			}
		}
	}

	return results, nil
}

