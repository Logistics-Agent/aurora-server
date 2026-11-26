package postgres

import (
	"context"
	"errors"
	"strings"
	"time"

	"aurora/devops-agent/internal/domain"
	"aurora/shared/pagination"

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

type IncidentRepository struct{ db *gorm.DB }

func NewIncidentRepository(db *gorm.DB) *IncidentRepository { return &IncidentRepository{db: db} }

func (r *IncidentRepository) Save(ctx context.Context, incident *domain.Incident) error {
	db := getDB(ctx, r.db)
	if db == nil {
		return errors.New("postgres db not initialized")
	}
	return db.WithContext(ctx).Save(incident).Error
}

func (r *IncidentRepository) FindByCorrelationID(ctx context.Context, correlationID string) (*domain.Incident, error) {
	db := getDB(ctx, r.db)
	if db == nil {
		return nil, errors.New("postgres db not initialized")
	}
	var inc domain.Incident
	err := db.WithContext(ctx).Where("correlation_id = ?", correlationID).First(&inc).Error
	if errors.Is(err, gorm.ErrRecordNotFound) {
		return nil, nil
	}
	return &inc, err
}

func (r *IncidentRepository) List(ctx context.Context, severity string, page, size int32) ([]*domain.Incident, int64, error) {
	db := getDB(ctx, r.db)
	if db == nil {
		return nil, 0, errors.New("postgres db not initialized")
	}
	if size <= 0 {
		size = 10
	} else if size > 100 {
		size = 100
	}
	if page < 0 {
		page = 0
	}

	q := db.WithContext(ctx).Model(&domain.Incident{})
	if s := strings.TrimSpace(severity); s != "" {
		switch domain.Severity(s) {
		case domain.SeverityLow, domain.SeverityMedium, domain.SeverityHigh, domain.SeverityCritical:
			q = q.Where("severity = ?", s)
		}
	}

	var total int64
	if err := q.Count(&total).Error; err != nil {
		return nil, 0, err
	}

	var items []*domain.Incident
	err := q.Order("created_at DESC").Offset(int(page * size)).Limit(int(size)).Find(&items).Error
	return items, total, err
}

func (r *IncidentRepository) FindAndLockByStatus(ctx context.Context, status domain.IncidentStatus, limit int) ([]*domain.Incident, error) {
	db := getDB(ctx, r.db)
	if db == nil {
		return nil, errors.New("postgres db not initialized")
	}
	var items []*domain.Incident
	err := db.WithContext(ctx).
		Clauses(clause.Locking{Strength: "UPDATE", Options: "SKIP LOCKED"}).
		Where("status = ?", status).
		Order("created_at ASC").
		Limit(limit).
		Find(&items).Error
	return items, err
}

type RcaAnalysisRepository struct{ db *gorm.DB }

func NewRcaAnalysisRepository(db *gorm.DB) *RcaAnalysisRepository {
	return &RcaAnalysisRepository{db: db}
}

func (r *RcaAnalysisRepository) Save(ctx context.Context, analysis *domain.RcaAnalysis) error {
	db := getDB(ctx, r.db)
	if db == nil {
		return errors.New("postgres db not initialized")
	}
	return db.WithContext(ctx).Save(analysis).Error
}

type ExistingRuleRepository struct{ db *gorm.DB }

func NewExistingRuleRepository(db *gorm.DB) *ExistingRuleRepository {
	return &ExistingRuleRepository{db: db}
}

func (r *ExistingRuleRepository) Save(ctx context.Context, rule *domain.ExistingRule) error {
	db := getDB(ctx, r.db)
	if db == nil {
		return errors.New("postgres db not initialized")
	}
	return db.WithContext(ctx).Save(rule).Error
}

func (r *ExistingRuleRepository) FindByID(ctx context.Context, id uuid.UUID) (*domain.ExistingRule, error) {
	db := getDB(ctx, r.db)
	if db == nil {
		return nil, errors.New("postgres db not initialized")
	}
	var rule domain.ExistingRule
	err := db.WithContext(ctx).Where("id = ?", id).First(&rule).Error
	if errors.Is(err, gorm.ErrRecordNotFound) {
		return nil, nil
	}
	return &rule, err
}

func (r *ExistingRuleRepository) DeleteByID(ctx context.Context, id uuid.UUID) error {
	db := getDB(ctx, r.db)
	if db == nil {
		return errors.New("postgres db not initialized")
	}
	return db.WithContext(ctx).Delete(&domain.ExistingRule{}, "id = ?", id).Error
}

func (r *ExistingRuleRepository) List(ctx context.Context, page, size int32) ([]*domain.ExistingRule, int64, error) {
	db := getDB(ctx, r.db)
	if db == nil {
		return nil, 0, errors.New("postgres db not initialized")
	}
	_, limit := pagination.NormalizePagination(page+1, size)
	if page < 0 {
		page = 0
	}
	var total int64
	if err := db.WithContext(ctx).Model(&domain.ExistingRule{}).Count(&total).Error; err != nil {
		return nil, 0, err
	}
	var items []*domain.ExistingRule
	err := db.WithContext(ctx).Order("created_at DESC").Offset(int(page * limit)).Limit(int(limit)).Find(&items).Error
	return items, total, err
}

type PendingRuleRepository struct{ db *gorm.DB }

func NewPendingRuleRepository(db *gorm.DB) *PendingRuleRepository {
	return &PendingRuleRepository{db: db}
}

func (r *PendingRuleRepository) Save(ctx context.Context, rule *domain.PendingRule) error {
	db := getDB(ctx, r.db)
	if db == nil {
		return errors.New("postgres db not initialized")
	}
	return db.WithContext(ctx).Save(rule).Error
}

func (r *PendingRuleRepository) FindByID(ctx context.Context, id uuid.UUID) (*domain.PendingRule, error) {
	db := getDB(ctx, r.db)
	if db == nil {
		return nil, errors.New("postgres db not initialized")
	}
	var rule domain.PendingRule
	err := db.WithContext(ctx).Where("id = ?", id).First(&rule).Error
	if errors.Is(err, gorm.ErrRecordNotFound) {
		return nil, nil
	}
	return &rule, err
}

func (r *PendingRuleRepository) ListByStatus(ctx context.Context, status string, page, size int32) ([]*domain.PendingRule, int64, error) {
	db := getDB(ctx, r.db)
	if db == nil {
		return nil, 0, errors.New("postgres db not initialized")
	}
	_, limit := pagination.NormalizePagination(page+1, size)
	if page < 0 {
		page = 0
	}
	q := db.WithContext(ctx).Model(&domain.PendingRule{}).Where("status = ?", status)
	var total int64
	if err := q.Count(&total).Error; err != nil {
		return nil, 0, err
	}
	var items []*domain.PendingRule
	err := q.Order("created_at DESC").Offset(int(page * limit)).Limit(int(limit)).Find(&items).Error
	return items, total, err
}

type SelfConfigRepository struct{ db *gorm.DB }

func NewSelfConfigRepository(db *gorm.DB) *SelfConfigRepository {
	return &SelfConfigRepository{db: db}
}

func (r *SelfConfigRepository) FindFirst(ctx context.Context) (*domain.DevOpsAgentSelfConfig, error) {
	db := getDB(ctx, r.db)
	if db == nil {
		return nil, errors.New("postgres db not initialized")
	}
	var cfg domain.DevOpsAgentSelfConfig
	err := db.WithContext(ctx).Order("created_at ASC").First(&cfg).Error
	if errors.Is(err, gorm.ErrRecordNotFound) {
		return nil, nil
	}
	return &cfg, err
}

func (r *SelfConfigRepository) Save(ctx context.Context, cfg *domain.DevOpsAgentSelfConfig) error {
	db := getDB(ctx, r.db)
	if db == nil {
		return errors.New("postgres db not initialized")
	}
	return db.WithContext(ctx).Save(cfg).Error
}

type PrApprovalRecordRepository struct{ db *gorm.DB }

func NewPrApprovalRecordRepository(db *gorm.DB) *PrApprovalRecordRepository {
	return &PrApprovalRecordRepository{db: db}
}

func (r *PrApprovalRecordRepository) FindByStatusAndExpiresAtBefore(ctx context.Context, status domain.ApprovalStatus, before time.Time) ([]*domain.PrApprovalRecord, error) {
	db := getDB(ctx, r.db)
	if db == nil {
		return nil, errors.New("postgres db not initialized")
	}
	var records []*domain.PrApprovalRecord
	err := db.WithContext(ctx).
		Preload("Incident").
		Where("status = ? AND expires_at < ?", status, before).
		Find(&records).Error
	return records, err
}

func (r *PrApprovalRecordRepository) Save(ctx context.Context, record *domain.PrApprovalRecord) error {
	db := getDB(ctx, r.db)
	if db == nil {
		return errors.New("postgres db not initialized")
	}
	return db.WithContext(ctx).Save(record).Error
}

type OutboxRepository struct{ db *gorm.DB }

func NewOutboxRepository(db *gorm.DB) *OutboxRepository { return &OutboxRepository{db: db} }

func (r *OutboxRepository) Enqueue(ctx context.Context, correlationID string, incidentID *uuid.UUID, actionType domain.AuditActionType, actor, payloadJson string) error {
	db := getDB(ctx, r.db)
	if db == nil {
		return errors.New("postgres db not initialized")
	}
	if correlationID == "" {
		correlationID = uuid.NewString()
	}
	if actor == "" {
		actor = "SYSTEM_DEVOPS_AGENT"
	}
	if payloadJson == "" {
		payloadJson = "{}"
	}
	now := time.Now().UTC()
	item := &domain.AuditEventOutbox{
		BaseAuditable: domain.BaseAuditable{
			ID:        uuid.New(),
			CreatedAt: now,
			UpdatedAt: now,
			Version:   0,
		},
		CorrelationID: correlationID,
		IncidentID:    incidentID,
		ActionType:    actionType,
		Actor:         actor,
		PayloadJson:   payloadJson,
		Processed:     false,
		RetryCount:    0,
	}
	return db.WithContext(ctx).Create(item).Error
}

func (r *OutboxRepository) FindAndLockPendingBatch(ctx context.Context, limit int) ([]*domain.AuditEventOutbox, error) {
	db := getDB(ctx, r.db)
	if db == nil {
		return nil, errors.New("postgres db not initialized")
	}
	var items []*domain.AuditEventOutbox
	err := db.WithContext(ctx).
		Clauses(clause.Locking{Strength: "UPDATE", Options: "SKIP LOCKED"}).
		Where("processed = ?", false).
		Order("created_at ASC").
		Limit(limit).
		Find(&items).Error
	return items, err
}

func (r *OutboxRepository) Save(ctx context.Context, item *domain.AuditEventOutbox) error {
	db := getDB(ctx, r.db)
	if db == nil {
		return errors.New("postgres db not initialized")
	}
	return db.WithContext(ctx).Save(item).Error
}
