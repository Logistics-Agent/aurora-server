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
)

type approvalRepository struct {
	db *gorm.DB
}

func NewApprovalRepository(db *gorm.DB) repository.ApprovalRepository {
	return &approvalRepository{db: db}
}

func (r *approvalRepository) GetByID(ctx context.Context, tenantID, id uuid.UUID) (*entities.ApprovalRequest, error) {
	db := getDB(ctx, r.db)

	var req entities.ApprovalRequest
	err := db.Scopes(TenantScope(tenantID)).
		Preload("Route").
		Where(`"Id" = ?`, id).
		First(&req).Error

	if err != nil {
		if errors.Is(err, gorm.ErrRecordNotFound) {
			return nil, domain.ErrApprovalNotFound
		}
		return nil, fmt.Errorf("lỗi truy vấn ApprovalRequest: %w", err)
	}

	return &req, nil
}

func (r *approvalRepository) GetPendingByRouteID(ctx context.Context, tenantID, routeID uuid.UUID) (*entities.ApprovalRequest, error) {
	db := getDB(ctx, r.db)

	var req entities.ApprovalRequest
	err := db.Scopes(TenantScope(tenantID)).
		Where(`"RouteId" = ? AND "Status" = ?`, routeID, int(enums.ApprovalStatusPending)).
		Order(`"CreatedAt" DESC`).
		First(&req).Error

	if err != nil {
		if errors.Is(err, gorm.ErrRecordNotFound) {
			return nil, nil // Chưa có ticket pending
		}
		return nil, fmt.Errorf("lỗi kiểm tra ticket pending: %w", err)
	}

	return &req, nil
}

func (r *approvalRepository) GetValidApproved(
	ctx context.Context,
	tenantID, routeID uuid.UUID,
	routeVersion int32,
	policyID string,
	policyVersion int32,
) (*entities.ApprovalRequest, error) {
	db := getDB(ctx, r.db)

	var req entities.ApprovalRequest
	err := db.Scopes(TenantScope(tenantID)).
		Where(`"RouteId" = ? AND "Status" = ? AND "RouteVersion" = ? AND "PolicyId" = ? AND "PolicyVersion" = ?`,
			routeID, int(enums.ApprovalStatusApproved), routeVersion, policyID, policyVersion).
		Order(`"ReviewedAt" DESC, "CreatedAt" DESC`).
		First(&req).Error

	if err != nil {
		if errors.Is(err, gorm.ErrRecordNotFound) {
			return nil, nil
		}
		return nil, fmt.Errorf("lỗi kiểm tra phê duyệt hợp lệ: %w", err)
	}

	return &req, nil
}

func (r *approvalRepository) ListPending(ctx context.Context, tenantID uuid.UUID, page, limit int32) ([]entities.ApprovalRequest, int64, error) {
	db := getDB(ctx, r.db)

	page, limit = pagination.NormalizePagination(page, limit)
	offset := pagination.Offset(page, limit)

	query := db.Scopes(TenantScope(tenantID)).
		Model(&entities.ApprovalRequest{}).
		Where(`"Status" = ?`, int(enums.ApprovalStatusPending))

	var total int64
	if err := query.Count(&total).Error; err != nil {
		return nil, 0, fmt.Errorf("lỗi đếm tổng số ApprovalRequest pending: %w", err)
	}

	var approvals []entities.ApprovalRequest
	err := query.Preload("Route").
		Order(`"CreatedAt" DESC`).
		Offset(int(offset)).
		Limit(int(limit)).
		Find(&approvals).Error

	if err != nil {
		return nil, 0, fmt.Errorf("lỗi truy vấn danh sách ApprovalRequest: %w", err)
	}

	return approvals, total, nil
}

func (r *approvalRepository) Create(ctx context.Context, req *entities.ApprovalRequest) error {
	db := getDB(ctx, r.db)

	if req.ID == uuid.Nil {
		req.ID = uuid.New()
	}
	if req.CreatedAt.IsZero() {
		req.CreatedAt = time.Now().UTC()
	}

	return db.Create(req).Error
}

func (r *approvalRepository) Update(ctx context.Context, req *entities.ApprovalRequest) error {
	db := getDB(ctx, r.db)

	now := time.Now().UTC()
	req.UpdatedAt = &now

	return db.Save(req).Error
}

func (r *approvalRepository) CreateDecisionAuditLog(ctx context.Context, log *entities.RouteDecisionAuditLog) error {
	db := getDB(ctx, r.db)

	if log.ID == uuid.Nil {
		log.ID = uuid.New()
	}
	if log.CreatedAt.IsZero() {
		log.CreatedAt = time.Now().UTC()
	}

	return db.Create(log).Error
}

