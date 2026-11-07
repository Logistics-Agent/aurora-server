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

type routeRepository struct {
	db *gorm.DB
}

func NewRouteRepository(db *gorm.DB) repository.RouteRepository {
	return &routeRepository{db: db}
}

func (r *routeRepository) GetByID(ctx context.Context, tenantID, id uuid.UUID) (*entities.Route, error) {
	db := getDB(ctx, r.db)

	var route entities.Route
	err := db.Scopes(TenantScope(tenantID), ActiveOnlyScope()).
		Preload("Stops", func(db *gorm.DB) *gorm.DB {
			return db.Order(`"Sequence" ASC`)
		}).
		Where(`"Id" = ?`, id).
		First(&route).Error

	if err != nil {
		if errors.Is(err, gorm.ErrRecordNotFound) {
			return nil, domain.ErrRouteNotFound
		}
		return nil, fmt.Errorf("lỗi truy vấn Route: %w", err)
	}

	return &route, nil
}

func (r *routeRepository) List(ctx context.Context, tenantID uuid.UUID, page, limit int32, status *enums.RouteStatus) ([]entities.Route, int64, error) {
	db := getDB(ctx, r.db)

	page, limit = pagination.NormalizePagination(page, limit)
	offset := pagination.Offset(page, limit)

	query := db.Scopes(TenantScope(tenantID), ActiveOnlyScope()).Model(&entities.Route{})
	if status != nil {
		query = query.Where(`"Status" = ?`, int(*status))
	}

	var total int64
	if err := query.Count(&total).Error; err != nil {
		return nil, 0, fmt.Errorf("lỗi đếm tổng số Route: %w", err)
	}

	var routes []entities.Route
	err := query.Preload("Stops", func(db *gorm.DB) *gorm.DB {
		return db.Order(`"Sequence" ASC`)
	}).
		Order(`"CreatedAt" DESC`).
		Offset(int(offset)).
		Limit(int(limit)).
		Find(&routes).Error

	if err != nil {
		return nil, 0, fmt.Errorf("lỗi truy vấn danh sách Route: %w", err)
	}

	return routes, total, nil
}

func (r *routeRepository) Create(ctx context.Context, route *entities.Route) error {
	db := getDB(ctx, r.db)

	if route.ID == uuid.Nil {
		route.ID = uuid.New()
	}
	if route.CreatedAt.IsZero() {
		route.CreatedAt = time.Now().UTC()
	}

	// Đảm bảo Id và RouteId cho các Stops
	for i := range route.Stops {
		if route.Stops[i].ID == uuid.Nil {
			route.Stops[i].ID = uuid.New()
		}
		route.Stops[i].RouteID = route.ID
	}

	return db.Create(route).Error
}

func (r *routeRepository) Update(ctx context.Context, route *entities.Route) error {
	db := getDB(ctx, r.db)

	now := time.Now().UTC()
	route.UpdatedAt = &now

	return db.Transaction(func(tx *gorm.DB) error {
		// 1. Cập nhật thông tin route
		if err := tx.Save(route).Error; err != nil {
			return err
		}

		// 2. Xóa các stops cũ và insert lại các stops mới
		if err := tx.Where(`"RouteId" = ?`, route.ID).Delete(&entities.RouteStop{}).Error; err != nil {
			return err
		}

		if len(route.Stops) > 0 {
			for i := range route.Stops {
				if route.Stops[i].ID == uuid.Nil {
					route.Stops[i].ID = uuid.New()
				}
				route.Stops[i].RouteID = route.ID
			}
			if err := tx.Create(&route.Stops).Error; err != nil {
				return err
			}
		}

		return nil
	})
}

func (r *routeRepository) UpdateWithVersion(ctx context.Context, route *entities.Route, expectedVersion int32) error {
	db := getDB(ctx, r.db)

	now := time.Now().UTC()
	route.UpdatedAt = &now
	newVersion := expectedVersion + 1

	return db.Transaction(func(tx *gorm.DB) error {
		res := tx.Model(&entities.Route{}).
			Scopes(TenantScope(route.TenantID)).
			Where(`"Id" = ? AND "Version" = ?`, route.ID, expectedVersion).
			Updates(map[string]any{
				"Name":                     route.Name,
				"Description":              route.Description,
				"Type":                     route.Type,
				"Status":                   route.Status,
				"RiskLevel":                route.RiskLevel,
				"GovernanceDecision":       route.GovernanceDecision,
				"LastAssessedAt":           route.LastAssessedAt,
				"EstimatedDistanceKm":      route.EstimatedDistanceKm,
				"EstimatedDurationMinutes": route.EstimatedDurationMinutes,
				"MaxWeightKg":              route.MaxWeightKg,
				"MaxVolumeM3":              route.MaxVolumeM3,
				"IsAiGenerated":            route.IsAiGenerated,
				"OptimizedAt":              route.OptimizedAt,
				"Version":                  newVersion,
				"UpdatedAt":                now,
				"UpdatedBy":                route.UpdatedBy,
			})

		if res.Error != nil {
			return res.Error
		}

		if res.RowsAffected == 0 {
			return domain.ErrOptimisticLock
		}

		if len(route.Stops) > 0 {
			if err := tx.Where(`"RouteId" = ?`, route.ID).Delete(&entities.RouteStop{}).Error; err != nil {
				return err
			}
			for i := range route.Stops {
				if route.Stops[i].ID == uuid.Nil {
					route.Stops[i].ID = uuid.New()
				}
				route.Stops[i].RouteID = route.ID
			}
			if err := tx.Create(&route.Stops).Error; err != nil {
				return err
			}
		}

		route.Version = newVersion
		return nil
	})
}

func (r *routeRepository) Delete(ctx context.Context, tenantID, id uuid.UUID) error {
	db := getDB(ctx, r.db)

	now := time.Now().UTC()
	res := db.Model(&entities.Route{}).
		Scopes(TenantScope(tenantID), ActiveOnlyScope()).
		Where(`"Id" = ?`, id).
		Updates(map[string]any{
			"IsDeleted": true,
			"UpdatedAt": now,
		})

	if res.Error != nil {
		return res.Error
	}
	if res.RowsAffected == 0 {
		return domain.ErrRouteNotFound
	}

	return nil
}

func (r *routeRepository) AddOptimizationHistory(ctx context.Context, history *entities.RouteOptimizationHistory) error {
	db := getDB(ctx, r.db)
	if history.ID == uuid.Nil {
		history.ID = uuid.New()
	}
	if history.CreatedAt.IsZero() {
		history.CreatedAt = time.Now().UTC()
	}
	return db.Create(history).Error
}

