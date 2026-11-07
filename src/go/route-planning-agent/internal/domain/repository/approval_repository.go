package repository

import (
	"context"

	"aurora/route-planning-agent/internal/domain/entities"

	"github.com/google/uuid"
)

type ApprovalRepository interface {
	GetByID(ctx context.Context, tenantID, id uuid.UUID) (*entities.ApprovalRequest, error)
	GetPendingByRouteID(ctx context.Context, tenantID, routeID uuid.UUID) (*entities.ApprovalRequest, error)
	GetValidApproved(ctx context.Context, tenantID, routeID uuid.UUID, routeVersion int32, policyID string, policyVersion int32) (*entities.ApprovalRequest, error)
	ListPending(ctx context.Context, tenantID uuid.UUID, page, limit int32) ([]entities.ApprovalRequest, int64, error)
	Create(ctx context.Context, req *entities.ApprovalRequest) error
	Update(ctx context.Context, req *entities.ApprovalRequest) error

	// Decision Audit Log
	CreateDecisionAuditLog(ctx context.Context, log *entities.RouteDecisionAuditLog) error
}

