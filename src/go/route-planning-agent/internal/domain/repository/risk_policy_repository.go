package repository

import (
	"context"

	"aurora/route-planning-agent/internal/domain/entities"

	"github.com/google/uuid"
)

type RiskPolicyRepository interface {
	GetConfig(ctx context.Context, tenantID uuid.UUID) (*entities.TenantRiskPolicyConfig, error)
	UpsertConfig(ctx context.Context, cfg *entities.TenantRiskPolicyConfig) error

	GetPolicyByID(ctx context.Context, tenantID, id uuid.UUID) (*entities.TenantRiskPolicy, error)
	GetActivePolicy(ctx context.Context, tenantID uuid.UUID, scope string) (*entities.TenantRiskPolicy, error)
	GetActivePoliciesByScope(ctx context.Context, tenantID uuid.UUID, scope string) ([]entities.TenantRiskPolicy, error)
	GetMaxVersion(ctx context.Context, tenantID uuid.UUID, scope string) (int32, error)
	GetPolicyByVersion(ctx context.Context, tenantID uuid.UUID, scope string, version int32) (*entities.TenantRiskPolicy, error)
	ListPolicyVersions(ctx context.Context, tenantID uuid.UUID, scope string, page, limit int32) ([]entities.TenantRiskPolicy, int64, error)
	CreatePolicyDraft(ctx context.Context, policy *entities.TenantRiskPolicy) error
	UpdatePolicyDraft(ctx context.Context, policy *entities.TenantRiskPolicy) error
	DeletePolicyDraft(ctx context.Context, tenantID, id uuid.UUID) error

	// Risk Assessment
	CreateRiskAssessment(ctx context.Context, assessment *entities.RiskAssessment) error
	GetLatestAssessment(ctx context.Context, tenantID, routeID uuid.UUID) (*entities.RiskAssessment, error)
}

