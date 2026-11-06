package rules

import (
	"context"

	"aurora/route-planning-agent/internal/domain/entities"
	"aurora/route-planning-agent/internal/domain/enums"

	"github.com/google/uuid"
)

// RuleResult kết quả đánh giá từ một Business Rule
type RuleResult struct {
	RuleName                string               `json:"rule_name"`
	RuleCode                string               `json:"rule_code"`
	RiskLevel               enums.RouteRiskLevel `json:"risk_level"`
	Passed                  bool                 `json:"passed"`
	Message                 string               `json:"message,omitempty"`
	RequiresApproval        bool                 `json:"requires_approval"`
	RequiresComplianceCheck bool                 `json:"requires_compliance_check"`
}

// TenantRuleThresholds ngưỡng cấu hình của Rule cho một tenant
type TenantRuleThresholds struct {
	IsEnabled bool               `json:"is_enabled"`
	Values    map[string]float64 `json:"values"`
}

// Get lấy giá trị ngưỡng theo key hoặc trả về giá trị mặc định nếu không tồn tại
func (t *TenantRuleThresholds) Get(key string, defaultValue float64) float64 {
	if t == nil || t.Values == nil {
		return defaultValue
	}
	if val, ok := t.Values[key]; ok {
		return val
	}
	return defaultValue
}

// ITenantRuleConfigService interface trừu tượng lấy cấu hình ngưỡng của tenant
type ITenantRuleConfigService interface {
	GetThresholds(ctx context.Context, tenantID uuid.UUID, ruleName string) (*TenantRuleThresholds, error)
}

// RouteRuleContext ngữ cảnh đầu vào cho các rules đánh giá tuyến đường
type RouteRuleContext struct {
	Route             *entities.Route
	TenantID          uuid.UUID
	RuleConfigService ITenantRuleConfigService
}

// Rule interface cho mỗi quy tắc nghiệp vụ
type Rule interface {
	Name() string
	Code() string
	CanApply(ctx *RouteRuleContext) bool
	Evaluate(ctx context.Context, ruleCtx *RouteRuleContext) (*RuleResult, error)
}

// RuleEngine interface điều phối thực thi các rules
type RuleEngine interface {
	EvaluateAll(ctx context.Context, ruleCtx *RouteRuleContext) ([]RuleResult, error)
}

