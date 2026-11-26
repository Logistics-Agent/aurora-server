package application

import (
	"context"

	"aurora/ai-governance/internal/governance/domain"

	"github.com/google/uuid"
)

type PolicyAuditEvent struct {
	DecisionID      string  `json:"decisionId"`
	TenantID        string  `json:"tenantId"`
	CallerServiceID string  `json:"callerServiceId"`
	CapabilityCode  string  `json:"capabilityCode"`
	Operation       string  `json:"operation"`
	Allowed         bool    `json:"allowed"`
	DenyReason      *string `json:"denyReason,omitempty"`
	PolicyVersion   string  `json:"policyVersion,omitempty"`
	TimestampMillis int64   `json:"timestampMillis"`
}

type PolicyAuditPort interface {
	PublishPolicyDecision(ctx context.Context, event PolicyAuditEvent)
}

type TenantPlanResolver interface {
	Resolve(ctx context.Context, tenantID uuid.UUID) domain.TenantPlanResult
}

type TenantQuotaPort interface {
	GetCurrentUsage(ctx context.Context, tenantID uuid.UUID, metric domain.QuotaMetric, period domain.QuotaPeriod, periodKey domain.PeriodKey) int64
}

type QuotaUsageStore interface {
	IncrementUsage(ctx context.Context, tenantID uuid.UUID, metric domain.QuotaMetric, period domain.QuotaPeriod, periodKey domain.PeriodKey, amount int64)
}

type PlanRepository interface {
	FindByCode(ctx context.Context, code string) (*domain.Plan, error)
}

type UsageRecordRepository interface {
	UpsertIncrement(ctx context.Context, tenantID uuid.UUID, metric domain.QuotaMetric, period domain.QuotaPeriod, periodKey string, amount int64) error
}

type ProcessedEventRepository interface {
	ExistsByEventID(ctx context.Context, eventID string) (bool, error)
	Save(ctx context.Context, event *domain.ProcessedEvent) error
	TryInsert(ctx context.Context, event *domain.ProcessedEvent) (bool, error)
}

type TransactionRunner interface {
	WithTransaction(ctx context.Context, fn func(txCtx context.Context) error) error
}
