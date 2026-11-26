package persistence

import (
	"context"
	"errors"
	"time"

	"aurora/ai-governance/internal/governance/domain"

	"github.com/google/uuid"
	"gorm.io/gorm"
	"gorm.io/gorm/clause"
)

type txKeyType struct{}

var txKey = txKeyType{}

func getDB(ctx context.Context, fallback *gorm.DB) *gorm.DB {
	if tx, ok := ctx.Value(txKey).(*gorm.DB); ok && tx != nil {
		return tx
	}
	return fallback
}

type UnitOfWork struct {
	db *gorm.DB
}

func NewUnitOfWork(db *gorm.DB) *UnitOfWork {
	return &UnitOfWork{db: db}
}

func (u *UnitOfWork) WithTransaction(ctx context.Context, fn func(txCtx context.Context) error) error {
	if u.db == nil {
		return errors.New("postgres db not initialized")
	}
	return u.db.WithContext(ctx).Transaction(func(tx *gorm.DB) error {
		txCtx := context.WithValue(ctx, txKey, tx)
		return fn(txCtx)
	})
}

type PlanRepository struct {
	db *gorm.DB
}

func NewPlanRepository(db *gorm.DB) *PlanRepository {
	return &PlanRepository{db: db}
}

func (r *PlanRepository) FindByCode(ctx context.Context, code string) (*domain.Plan, error) {
	db := getDB(ctx, r.db)
	if db == nil {
		return nil, errors.New("postgres db not initialized")
	}
	var plan domain.Plan
	err := db.WithContext(ctx).
		Preload("Capabilities").
		Preload("Quotas").
		Where("code = ?", code).
		First(&plan).Error
	if errors.Is(err, gorm.ErrRecordNotFound) {
		return nil, nil
	}
	if err != nil {
		return nil, err
	}
	return &plan, nil
}

type UsageRecordRepository struct {
	db *gorm.DB
}

func NewUsageRecordRepository(db *gorm.DB) *UsageRecordRepository {
	return &UsageRecordRepository{db: db}
}

func (r *UsageRecordRepository) UpsertIncrement(
	ctx context.Context,
	tenantID uuid.UUID,
	metric domain.QuotaMetric,
	period domain.QuotaPeriod,
	periodKey string,
	amount int64,
) error {
	db := getDB(ctx, r.db)
	if db == nil {
		return errors.New("postgres db not initialized")
	}
	now := time.Now().UTC()
	rec := domain.UsageRecord{
		ID:          uuid.New(),
		TenantID:    tenantID,
		QuotaMetric: metric,
		QuotaPeriod: period,
		PeriodKey:   periodKey,
		UsageValue:  amount,
		CreatedAt:   now,
		CreatedBy:   "system",
		UpdatedAt:   &now,
		UpdatedBy:   "system",
		Version:     1,
	}
	return db.WithContext(ctx).
		Clauses(clause.OnConflict{
			Columns: []clause.Column{
				{Name: "tenant_id"},
				{Name: "quota_metric"},
				{Name: "quota_period"},
				{Name: "period_key"},
			},
			DoUpdates: clause.Assignments(map[string]any{
				"usage_value": gorm.Expr("usage_records.usage_value + ?", amount),
				"updated_at":  now,
				"updated_by":  "system",
				"version":     gorm.Expr("COALESCE(usage_records.version, 0) + 1"),
			}),
		}).
		Create(&rec).Error
}

type ProcessedEventRepository struct {
	db *gorm.DB
}

func NewProcessedEventRepository(db *gorm.DB) *ProcessedEventRepository {
	return &ProcessedEventRepository{db: db}
}

func (r *ProcessedEventRepository) ExistsByEventID(ctx context.Context, eventID string) (bool, error) {
	db := getDB(ctx, r.db)
	if db == nil {
		return false, errors.New("postgres db not initialized")
	}
	var count int64
	err := db.WithContext(ctx).
		Model(&domain.ProcessedEvent{}).
		Where("event_id = ?", eventID).
		Count(&count).Error
	return count > 0, err
}

func (r *ProcessedEventRepository) Save(ctx context.Context, event *domain.ProcessedEvent) error {
	db := getDB(ctx, r.db)
	if db == nil {
		return errors.New("postgres db not initialized")
	}
	if event.ID == uuid.Nil {
		event.ID = uuid.New()
	}
	return db.WithContext(ctx).Create(event).Error
}

func (r *ProcessedEventRepository) TryInsert(ctx context.Context, event *domain.ProcessedEvent) (bool, error) {
	db := getDB(ctx, r.db)
	if db == nil {
		return false, errors.New("postgres db not initialized")
	}
	if event.ID == uuid.Nil {
		event.ID = uuid.New()
	}
	res := db.WithContext(ctx).
		Clauses(clause.OnConflict{
			Columns:   []clause.Column{{Name: "event_id"}},
			DoNothing: true,
		}).
		Create(event)
	if res.Error != nil {
		return false, res.Error
	}
	return res.RowsAffected > 0, nil
}
