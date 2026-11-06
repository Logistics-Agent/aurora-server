package postgres

import (
	"context"
	"errors"
	"fmt"
	"time"

	"aurora/route-planning-agent/internal/domain"
	"aurora/route-planning-agent/internal/domain/entities"
	"aurora/route-planning-agent/internal/domain/enums"
	"aurora/route-planning-agent/internal/domain/repository"
	"aurora/shared/pagination"

	"github.com/google/uuid"
	"gorm.io/gorm"
	"gorm.io/gorm/clause"
)

type riskPolicyRepository struct {
	db *gorm.DB
}

func NewRiskPolicyRepository(db *gorm.DB) repository.RiskPolicyRepository {
	return &riskPolicyRepository{db: db}
}

func (r *riskPolicyRepository) GetConfig(ctx context.Context, tenantID uuid.UUID) (*entities.TenantRiskPolicyConfig, error) {
	db := getDB(ctx, r.db)

	var cfg entities.TenantRiskPolicyConfig
	err := db.Scopes(TenantScope(tenantID)).First(&cfg).Error
	if err != nil {
		if errors.Is(err, gorm.ErrRecordNotFound) {
			return nil, nil // Chưa cấu hình
		}
		return nil, fmt.Errorf("lỗi truy vấn cấu hình rủi ro: %w", err)
	}
	return &cfg, nil
}

func (r *riskPolicyRepository) UpsertConfig(ctx context.Context, cfg *entities.TenantRiskPolicyConfig) error {
	db := getDB(ctx, r.db)

	if cfg.ID == uuid.Nil {
		cfg.ID = uuid.New()
	}
	cfg.UpdatedAt = time.Now().UTC()

	return db.Clauses(clause.OnConflict{
		Columns: []clause.Column{{Name: "TenantId"}},
		DoUpdates: clause.AssignmentColumns([]string{
			"PolicyMode", "ActivePolicyId", "ActivePolicyVersion", "UpdatedAt",
		}),
	}).Create(cfg).Error
}

func (r *riskPolicyRepository) GetPolicyByID(ctx context.Context, tenantID, id uuid.UUID) (*entities.TenantRiskPolicy, error) {
	db := getDB(ctx, r.db)

	var policy entities.TenantRiskPolicy
	err := db.Scopes(TenantScope(tenantID), ActiveOnlyScope()).
		Preload("Rules", func(db *gorm.DB) *gorm.DB {
			return db.Where(`"IsDeleted" = false`)
		}).
		Where(`"Id" = ?`, id).
		First(&policy).Error

	if err != nil {
		if errors.Is(err, gorm.ErrRecordNotFound) {
			return nil, domain.ErrRiskPolicyNotFound
		}
		return nil, fmt.Errorf("lỗi truy vấn chính sách rủi ro: %w", err)
	}

	return &policy, nil
}

func (r *riskPolicyRepository) GetActivePolicy(ctx context.Context, tenantID uuid.UUID, scope string) (*entities.TenantRiskPolicy, error) {
	db := getDB(ctx, r.db)

	var policy entities.TenantRiskPolicy
	err := db.Scopes(TenantScope(tenantID), ActiveOnlyScope()).
		Preload("Rules", func(db *gorm.DB) *gorm.DB {
			return db.Where(`"IsDeleted" = false`)
		}).
		Where(`"Scope" = ? AND "Status" = ?`, scope, int(enums.TenantRiskPolicyStatusActive)).
		First(&policy).Error

	if err != nil {
		if errors.Is(err, gorm.ErrRecordNotFound) {
			return nil, nil
		}
		return nil, fmt.Errorf("lỗi truy vấn chính sách rủi ro active: %w", err)
	}

	return &policy, nil
}

func (r *riskPolicyRepository) GetPolicyByVersion(ctx context.Context, tenantID uuid.UUID, scope string, version int32) (*entities.TenantRiskPolicy, error) {
	db := getDB(ctx, r.db)

	var policy entities.TenantRiskPolicy
	err := db.Scopes(TenantScope(tenantID), ActiveOnlyScope()).
		Preload("Rules", func(db *gorm.DB) *gorm.DB {
			return db.Where(`"IsDeleted" = false`)
		}).
		Where(`"Scope" = ? AND "Version" = ?`, scope, version).
		First(&policy).Error

	if err != nil {
		if errors.Is(err, gorm.ErrRecordNotFound) {
			return nil, nil
		}
		return nil, fmt.Errorf("lỗi truy vấn chính sách theo version: %w", err)
	}

	return &policy, nil
}

func (r *riskPolicyRepository) ListPolicyVersions(ctx context.Context, tenantID uuid.UUID, scope string, page, limit int32) ([]entities.TenantRiskPolicy, int64, error) {
	db := getDB(ctx, r.db)

	page, limit = pagination.NormalizePagination(page, limit)
	offset := pagination.Offset(page, limit)

	query := db.Scopes(TenantScope(tenantID), ActiveOnlyScope()).
		Model(&entities.TenantRiskPolicy{}).
		Where(`"Scope" = ?`, scope)

	var total int64
	if err := query.Count(&total).Error; err != nil {
		return nil, 0, fmt.Errorf("lỗi đếm tổng số policy version: %w", err)
	}

	var policies []entities.TenantRiskPolicy
	err := query.Preload("Rules", func(db *gorm.DB) *gorm.DB {
		return db.Where(`"IsDeleted" = false`)
	}).
		Order(`"Version" DESC`).
		Offset(int(offset)).
		Limit(int(limit)).
		Find(&policies).Error

	if err != nil {
		return nil, 0, fmt.Errorf("lỗi truy vấn danh sách policy: %w", err)
	}

	return policies, total, nil
}

func (r *riskPolicyRepository) CreatePolicyDraft(ctx context.Context, policy *entities.TenantRiskPolicy) error {
	db := getDB(ctx, r.db)

	if policy.ID == uuid.Nil {
		policy.ID = uuid.New()
	}
	if policy.CreatedAt.IsZero() {
		policy.CreatedAt = time.Now().UTC()
	}

	for i := range policy.Rules {
		if policy.Rules[i].ID == uuid.Nil {
			policy.Rules[i].ID = uuid.New()
		}
		policy.Rules[i].PolicyID = policy.ID
		policy.Rules[i].TenantID = policy.TenantID
		if policy.Rules[i].CreatedAt.IsZero() {
			policy.Rules[i].CreatedAt = policy.CreatedAt
		}
	}

	return db.Create(policy).Error
}

func (r *riskPolicyRepository) UpdatePolicyDraft(ctx context.Context, policy *entities.TenantRiskPolicy) error {
	db := getDB(ctx, r.db)

	now := time.Now().UTC()
	policy.UpdatedAt = &now

	return db.Transaction(func(tx *gorm.DB) error {
		if err := tx.Save(policy).Error; err != nil {
			return err
		}

		// Xóa rules cũ và lưu lại rules mới
		if err := tx.Where(`"PolicyId" = ?`, policy.ID).Delete(&entities.TenantRiskRule{}).Error; err != nil {
			return err
		}

		if len(policy.Rules) > 0 {
			for i := range policy.Rules {
				if policy.Rules[i].ID == uuid.Nil {
					policy.Rules[i].ID = uuid.New()
				}
				policy.Rules[i].PolicyID = policy.ID
				policy.Rules[i].TenantID = policy.TenantID
				if policy.Rules[i].CreatedAt.IsZero() {
					policy.Rules[i].CreatedAt = now
				}
			}
			if err := tx.Create(&policy.Rules).Error; err != nil {
				return err
			}
		}

		return nil
	})
}

func (r *riskPolicyRepository) DeletePolicyDraft(ctx context.Context, tenantID, id uuid.UUID) error {
	db := getDB(ctx, r.db)

	now := time.Now().UTC()
	res := db.Model(&entities.TenantRiskPolicy{}).
		Scopes(TenantScope(tenantID), ActiveOnlyScope()).
		Where(`"Id" = ? AND "Status" = ?`, id, int(enums.TenantRiskPolicyStatusDraft)).
		Updates(map[string]any{
			"IsDeleted": true,
			"UpdatedAt": now,
		})

	if res.Error != nil {
		return res.Error
	}
	if res.RowsAffected == 0 {
		return domain.ErrRiskPolicyNotFound
	}

	return nil
}

func (r *riskPolicyRepository) CreateRiskAssessment(ctx context.Context, assessment *entities.RiskAssessment) error {
	db := getDB(ctx, r.db)

	if assessment.ID == uuid.Nil {
		assessment.ID = uuid.New()
	}
	if assessment.CreatedAt.IsZero() {
		assessment.CreatedAt = time.Now().UTC()
	}

	return db.Create(assessment).Error
}

func (r *riskPolicyRepository) GetLatestAssessment(ctx context.Context, tenantID, routeID uuid.UUID) (*entities.RiskAssessment, error) {
	db := getDB(ctx, r.db)

	var assessment entities.RiskAssessment
	err := db.Scopes(TenantScope(tenantID)).
		Where(`"RouteId" = ?`, routeID).
		Order(`"AssessedAt" DESC`).
		First(&assessment).Error

	if err != nil {
		if errors.Is(err, gorm.ErrRecordNotFound) {
			return nil, nil
		}
		return nil, fmt.Errorf("lỗi truy vấn đánh giá rủi ro gần nhất: %w", err)
	}

	return &assessment, nil
}

