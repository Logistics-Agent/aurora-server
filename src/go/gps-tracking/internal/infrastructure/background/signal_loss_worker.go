package background

import (
	"context"
	"encoding/json"
	"fmt"
	"log/slog"
	"time"

	"aurora/gps-tracking/internal/config"
	"aurora/gps-tracking/internal/domain"

	"github.com/google/uuid"
	"gorm.io/gorm"
)

type SignalLossWorker struct {
	db       *gorm.DB
	cfg      config.Config
	logger   *slog.Logger
	stopChan chan struct{}
}

func NewSignalLossWorker(db *gorm.DB, cfg config.Config, logger *slog.Logger) *SignalLossWorker {
	if logger == nil {
		logger = slog.Default()
	}
	return &SignalLossWorker{
		db:       db,
		cfg:      cfg,
		logger:   logger,
		stopChan: make(chan struct{}),
	}
}

func (w *SignalLossWorker) Start(ctx context.Context) {
	ticker := time.NewTicker(w.cfg.SignalLossInterval)
	defer ticker.Stop()

	w.logger.Info("GPS Signal-Loss Worker started",
		slog.Duration("interval", w.cfg.SignalLossInterval),
		slog.Duration("threshold", w.cfg.SignalLossThreshold),
	)

	for {
		select {
		case <-ctx.Done():
			w.logger.Info("GPS Signal-Loss Worker stopping on context cancellation...")
			return
		case <-w.stopChan:
			w.logger.Info("GPS Signal-Loss Worker stopped")
			return
		case <-ticker.C:
			if count, err := w.Scan(ctx); err != nil {
				w.logger.Warn("Error scanning signal-loss", slog.Any("error", err))
			} else if count > 0 {
				w.logger.Info("Signal-loss alerts raised", slog.Int("count", count))
			}
		}
	}
}

func (w *SignalLossWorker) Stop() {
	close(w.stopChan)
}

func (w *SignalLossWorker) Scan(ctx context.Context) (int, error) {
	now := time.Now().UTC()
	cutoff := now.Add(-w.cfg.SignalLossThreshold)

	// Find active assignments without active SignalLost alerts
	var assignments []domain.VehicleShipmentAssignment
	err := w.db.WithContext(ctx).
		Where(`"EndedAt" IS NULL AND NOT EXISTS (
			SELECT 1 FROM monitoring_alerts m
			WHERE m."TenantId" = vehicle_shipment_assignments."TenantId"
			  AND m."VehicleId" = vehicle_shipment_assignments."VehicleId"
			  AND m."AlertType" = 'SignalLost'
			  AND m."Status" = 'Active'
		)`).
		Limit(w.cfg.SignalLossBatchSize).
		Find(&assignments).Error

	if err != nil {
		return 0, fmt.Errorf("failed to query active assignments: %w", err)
	}

	if len(assignments) == 0 {
		return 0, nil
	}

	raised := 0
	for _, assignment := range assignments {
		var current domain.CurrentLocation
		currErr := w.db.WithContext(ctx).
			Where(`"TenantId" = ? AND "VehicleId" = ?`, assignment.TenantId, assignment.VehicleId).
			First(&current).Error

		lastSignal := assignment.AssignedAt
		var posId *uuid.UUID
		if currErr == nil {
			lastSignal = current.RecordedAt
			posId = &current.PositionId
		}

		if lastSignal.Before(cutoff) || lastSignal.Equal(cutoff) {
			alertErr := w.raiseSignalLostAlert(ctx, assignment, posId, now)
			if alertErr == nil {
				raised++
			} else {
				w.logger.Warn("Failed to raise signal-lost alert",
					slog.String("vehicle_id", assignment.VehicleId),
					slog.Any("error", alertErr),
				)
			}
		}
	}

	return raised, nil
}

func (w *SignalLossWorker) raiseSignalLostAlert(
	ctx context.Context,
	assignment domain.VehicleShipmentAssignment,
	posId *uuid.UUID,
	now time.Time,
) error {
	dedupKey := fmt.Sprintf("SignalLost:%s:", assignment.VehicleId)
	msg := "GPS signal has exceeded the configured silence threshold."
	alertId := uuid.New()

	return w.db.WithContext(ctx).Transaction(func(tx *gorm.DB) error {
		// Double check deduplication inside transaction
		var existing domain.MonitoringAlert
		if err := tx.Where(`"TenantId" = ? AND "DeduplicationKey" = ? AND "Status" = 'Active'`,
			assignment.TenantId, dedupKey).First(&existing).Error; err == nil {
			return nil // Already active
		}

		alert := domain.MonitoringAlert{
			Id:               alertId,
			TenantId:         assignment.TenantId,
			AlertType:        "SignalLost",
			Status:           "Active",
			VehicleId:        assignment.VehicleId,
			ShipmentId:       &assignment.ShipmentId,
			PositionId:       posId,
			DeduplicationKey: dedupKey,
			Message:          msg,
			OccurredAt:       now,
		}
		if err := tx.Create(&alert).Error; err != nil {
			return err
		}

		alertPayload, err := json.Marshal(map[string]interface{}{
			"eventId":         uuid.New(),
			"contractVersion": 1,
			"tenantId":        assignment.TenantId,
			"alertId":         alertId,
			"alertType":       "SignalLost",
			"vehicleId":       assignment.VehicleId,
			"shipmentId":      assignment.ShipmentId,
			"positionId":      posId,
			"message":         msg,
			"occurredAt":      now,
		})
		if err != nil {
			return err
		}

		outbox := domain.OutboxMessage{
			Id:         uuid.New(),
			TenantId:   assignment.TenantId,
			EventId:    uuid.New(),
			EventType:  "GpsMonitoringAlertRaisedEvent",
			Content:    string(alertPayload),
			OccurredAt: now,
			RetryCount: 0,
		}
		return tx.Create(&outbox).Error
	})
}

