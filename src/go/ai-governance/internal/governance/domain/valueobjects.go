package domain

import (
	"fmt"

	"github.com/google/uuid"
)

type PeriodKey struct {
	Value string
}

type QuotaKey struct {
	TenantID  string
	Metric    QuotaMetric
	Period    QuotaPeriod
	PeriodKey PeriodKey
}

func (qk QuotaKey) ToRedisKey() string {
	return fmt.Sprintf("ai:tenant-quota:%s:%s:%s:%s",
		qk.TenantID, string(qk.Metric), string(qk.Period), qk.PeriodKey.Value)
}

type QuotaDefinition struct {
	Metric QuotaMetric
	Period QuotaPeriod
	Limit  int64
}

type EffectivePolicy struct {
	AllowedProviders     map[AiProvider]struct{}
	AllowedProviderPools map[string]struct{}
	ModelTier            ModelTier
	MaxTokens            int32
	AutomationLevel      AutomationLevel
	RequireApproval      bool
}

type GovernanceDecision struct {
	Allowed              bool
	DenyReason           *DenyReason
	DecisionID           string
	AllowedProviders     map[AiProvider]struct{}
	AllowedProviderPools map[string]struct{}
	ModelTier            ModelTier
	MaxTokens            int32
	AutomationLevel      AutomationLevel
	RequireApproval      bool
	PolicyVersion        string
}

func DeniedDecision(reason DenyReason, decisionID string) GovernanceDecision {
	return GovernanceDecision{
		Allowed:    false,
		DenyReason: &reason,
		DecisionID: decisionID,
	}
}

type TenantPlanContext struct {
	TenantID             *uuid.UUID
	Status               TenantStatus
	CloudAiEnabled       bool
	PlanCode             string
	DefaultProvider      AiProvider
	Quotas               []QuotaDefinition
	AllowedCapabilities  map[string]struct{}
	AllowedProviders     map[AiProvider]struct{}
	AllowedProviderPools map[string]struct{}
}

type TenantPlanInfo struct {
	TenantID       uuid.UUID
	PlanCode       string
	Status         TenantStatus
	CloudAiEnabled bool
}

type TenantPlanKind string

const (
	TenantPlanSuccess          TenantPlanKind = "SUCCESS"
	TenantPlanNotFound         TenantPlanKind = "NOT_FOUND"
	TenantPlanSuspended        TenantPlanKind = "SUSPENDED"
	TenantPlanIamUnavailable   TenantPlanKind = "IAM_UNAVAILABLE"
	TenantPlanNotConfigured    TenantPlanKind = "PLAN_NOT_CONFIGURED"
)

type TenantPlanResult struct {
	Kind     TenantPlanKind
	Info     *TenantPlanInfo
	Reason   string
	PlanCode string
}
