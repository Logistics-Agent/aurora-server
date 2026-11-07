package services

import (
	"context"

	"aurora/route-planning-agent/internal/application/dtos"
	"aurora/route-planning-agent/internal/domain/entities"
	"aurora/route-planning-agent/internal/domain/enums"
	domainrules "aurora/route-planning-agent/internal/domain/rules"

	"github.com/google/uuid"
)

// EffectiveRiskPolicy chính sách rủi ro hiệu lực của tenant
type EffectiveRiskPolicy struct {
	PolicyID       string                                       `json:"policy_id"`
	Version        int32                                        `json:"version"`
	Source         enums.RiskPolicySource                       `json:"source"`
	Scope          string                                       `json:"scope"`
	TenantID       uuid.UUID                                    `json:"tenant_id"`
	RuleThresholds map[string]domainrules.TenantRuleThresholds `json:"rule_thresholds"`
}

// RiskAssessmentResult kết quả thẩm định rủi ro tổng hợp
type RiskAssessmentResult struct {
	RiskLevel        enums.RouteRiskLevel     `json:"risk_level"`
	Decision         enums.GovernanceDecision `json:"decision"`
	Source           string                   `json:"source"`
	PolicyID         string                   `json:"policy_id"`
	PolicyVersion    int32                    `json:"policy_version"`
	PolicySource     enums.RiskPolicySource   `json:"policy_source"`
	MatchedRuleCodes []string                 `json:"matched_rule_codes"`
	ReasonCodes      []string                 `json:"reason_codes"`
	ReasonDetails    string                   `json:"reason_details"`
	ConfidenceScore  *float64                 `json:"confidence_score,omitempty"`
	RuleResults      []domainrules.RuleResult `json:"rule_results,omitempty"`
}

type CacheInvalidator interface {
	InvalidateCache(tenantIDStr, ruleName string)
}

type RouteRiskPolicyProvider interface {
	GetEffectivePolicy(ctx context.Context, tenantID uuid.UUID, scope string) (*EffectiveRiskPolicy, error)
}

type RouteGovernanceService interface {
	AssessRoute(
		ctx context.Context,
		route *entities.Route,
		effectivePolicy *EffectiveRiskPolicy,
		ruleResults []domainrules.RuleResult,
		complianceResult *dtos.ComplianceCheckResultDto,
		aiResult *dtos.RouteAiResult,
	) (*RiskAssessmentResult, error)

	AssessSoftDeleteRisk(
		route *entities.Route,
		effectivePolicy *EffectiveRiskPolicy,
	) *RiskAssessmentResult

	ValidateExecutionAuthorized(
		ctx context.Context,
		route *entities.Route,
		effectivePolicy *EffectiveRiskPolicy,
	) error
}

type ApprovalService interface {
	Create(
		ctx context.Context,
		routeID uuid.UUID,
		reason, aiSummary string,
		complianceSummary *string,
		routeVersion int32,
		policyID string,
		policyVersion int32,
		tenantID uuid.UUID,
	) (*entities.ApprovalRequest, error)

	Approve(
		ctx context.Context,
		approvalID, reviewerUserID, tenantID uuid.UUID,
		comment *string,
	) (*entities.ApprovalRequest, error)

	Reject(
		ctx context.Context,
		approvalID, reviewerUserID, tenantID uuid.UUID,
		reason string,
		comment *string,
	) (*entities.ApprovalRequest, error)
}
