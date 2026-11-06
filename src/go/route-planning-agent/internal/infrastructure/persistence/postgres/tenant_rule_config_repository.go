package postgres

import (
	"context"
	"errors"
	"fmt"
	"time"

	"aurora/route-planning-agent/internal/domain/entities"
	"aurora/route-planning-agent/internal/domain/repository"
	"aurora/shared/pagination"

	"github.com/google/uuid"
	"gorm.io/gorm"
	"gorm.io/gorm/clause"
)

type tenantRuleConfigRepository struct {
	db *gorm.DB
}

func NewTenantRuleConfigRepository(db *gorm.DB) repository.TenantRuleConfigRepository {
	return &tenantRuleConfigRepository{db: db}
}

func (r *tenantRuleConfigRepository) GetByRuleName(ctx context.Context, tenantID uuid.UUID, ruleName string) (*entities.TenantRuleConfig, error) {
	db := getDB(ctx, r.db)

	var cfg entities.TenantRuleConfig
	err := db.Scopes(TenantScope(tenantID)).
		Where(`"RuleName" = ?`, ruleName).
		First(&cfg).Error

	if err != nil {
		if errors.Is(err, gorm.ErrRecordNotFound) {
			return nil, nil // Chưa có cấu hình riêng
		}
		return nil, fmt.Errorf("lỗi truy vấn cấu hình rule: %w", err)
	}

	return &cfg, nil
}

func (r *tenantRuleConfigRepository) List(ctx context.Context, tenantID uuid.UUID, page, limit int32) ([]entities.TenantRuleConfig, int64, error) {
	db := getDB(ctx, r.db)

	page, limit = pagination.NormalizePagination(page, limit)
	offset := pagination.Offset(page, limit)

	query := db.Scopes(TenantScope(tenantID)).Model(&entities.TenantRuleConfig{})

	var total int64
	if err := query.Count(&total).Error; err != nil {
		return nil, 0, fmt.Errorf("lỗi đếm cấu hình rule: %w", err)
	}

	var configs []entities.TenantRuleConfig
	err := query.Order(`"RuleName" ASC`).
		Offset(int(offset)).
		Limit(int(limit)).
		Find(&configs).Error

	if err != nil {
		return nil, 0, fmt.Errorf("lỗi truy vấn danh sách cấu hình rule: %w", err)
	}

	return configs, total, nil
}

func (r *tenantRuleConfigRepository) ListAll(ctx context.Context, tenantID uuid.UUID) ([]entities.TenantRuleConfig, error) {
	db := getDB(ctx, r.db)

	var configs []entities.TenantRuleConfig
	err := db.Scopes(TenantScope(tenantID)).
		Order(`"RuleName" ASC`).
		Find(&configs).Error

	if err != nil {
		return nil, fmt.Errorf("lỗi truy vấn toàn bộ cấu hình rule: %w", err)
	}

	return configs, nil
}

func (r *tenantRuleConfigRepository) Upsert(ctx context.Context, cfg *entities.TenantRuleConfig) error {
	db := getDB(ctx, r.db)

	if cfg.ID == uuid.Nil {
		cfg.ID = uuid.New()
	}
	cfg.UpdatedAt = time.Now().UTC()

	return db.Clauses(clause.OnConflict{
		Columns: []clause.Column{{Name: "TenantId"}, {Name: "RuleName"}},
		DoUpdates: clause.AssignmentColumns([]string{
			"IsEnabled", "ThresholdsJson", "UpdatedAt",
		}),
	}).Create(cfg).Error
}

