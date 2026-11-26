package postgres

import (
	"context"
	"fmt"
	"time"

	"aurora/gps-tracking/internal/config"
	"aurora/gps-tracking/internal/domain"

	"gorm.io/driver/postgres"
	"gorm.io/gorm"
	"gorm.io/gorm/logger"
)

func NewDB(cfg config.Config) (*gorm.DB, error) {
	db, err := gorm.Open(postgres.Open(cfg.DatabaseURL), &gorm.Config{
		Logger: logger.Default.LogMode(logger.Silent),
	})
	if err != nil {
		return nil, fmt.Errorf("connect to db failed: %w", err)
	}

	sqlDB, err := db.DB()
	if err != nil {
		return nil, fmt.Errorf("get sql.DB failed: %w", err)
	}

	sqlDB.SetMaxOpenConns(cfg.MaxDBOpenConns)
	sqlDB.SetMaxIdleConns(cfg.MaxDBIdleConns)
	sqlDB.SetConnMaxLifetime(time.Hour)

	// Auto-migrate ensures tables and indexes match if running fresh or existing
	ctx, cancel := context.WithTimeout(context.Background(), 10*time.Second)
	defer cancel()

	if err := sqlDB.PingContext(ctx); err != nil {
		return nil, fmt.Errorf("ping db failed: %w", err)
	}

	// Auto-migrate matching EF Core tables
	if err := db.AutoMigrate(
		&domain.GpsPosition{},
		&domain.CurrentLocation{},
		&domain.VehicleShipmentAssignment{},
		&domain.Geofence{},
		&domain.GeofencePresence{},
		&domain.MonitoringAlert{},
		&domain.ConsumedIntegrationEvent{},
		&domain.ShipmentTrackingState{},
		&domain.OutboxMessage{},
	); err != nil {
		return nil, fmt.Errorf("auto-migrate failed: %w", err)
	}

	return db, nil
}

