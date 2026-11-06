package repository

import (
	"context"

	"aurora/route-planning-agent/internal/domain/entities"
	"aurora/route-planning-agent/internal/domain/enums"

	"github.com/google/uuid"
)

type RouteRepository interface {
	GetByID(ctx context.Context, tenantID, id uuid.UUID) (*entities.Route, error)
	List(ctx context.Context, tenantID uuid.UUID, page, limit int32, status *enums.RouteStatus) ([]entities.Route, int64, error)
	Create(ctx context.Context, route *entities.Route) error
	Update(ctx context.Context, route *entities.Route) error
	Delete(ctx context.Context, tenantID, id uuid.UUID) error

	// Optimistic concurrency update
	UpdateWithVersion(ctx context.Context, route *entities.Route, expectedVersion int32) error
		
	// Optimization history
	AddOptimizationHistory(ctx context.Context, history *entities.RouteOptimizationHistory) error
}

