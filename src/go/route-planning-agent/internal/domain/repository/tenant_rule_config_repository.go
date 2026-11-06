package repository

import (
	"context"

	"aurora/route-planning-agent/internal/domain/entities"

	"github.com/google/uuid"
)

type TenantRuleConfigRepository interface {
	GetByRuleName(ctx context.Context, tenantID uuid.UUID, ruleName string) (*entities.TenantRuleConfig, error)
	List(ctx context.Context, tenantID uuid.UUID, page, limit int32) ([]entities.TenantRuleConfig, int64, error)
	ListAll(ctx context.Context, tenantID uuid.UUID) ([]entities.TenantRuleConfig, error)
	Upsert(ctx context.Context, cfg *entities.TenantRuleConfig) error
}

