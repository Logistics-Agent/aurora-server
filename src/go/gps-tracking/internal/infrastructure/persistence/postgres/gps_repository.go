package postgres

import (
	"context"
	"encoding/json"
	"errors"
	"fmt"
	"time"

	"aurora/gps-tracking/internal/domain"

	"github.com/google/uuid"
	"gorm.io/gorm"
)

type GpsRepository struct {
	db                 *gorm.DB
	stationarySpeedKph float64
}

func NewGpsRepository(db *gorm.DB, stationarySpeedKph float64) *GpsRepository {
	return &GpsRepository{
		db:                 db,
		stationarySpeedKph: stationarySpeedKph,
	}
}

func (r *GpsRepository) IngestPosition(
	ctx context.Context,
	tenantId uuid.UUID,
	deviceId string,
	vehicleId string,
	externalReadingId string,
	lat float64,
	lng float64,
	speedKph *float64,
	heading *float64,
	accuracy *float64,
	recordedAt time.Time,
) (*domain.GpsPosition, error) {
	now := time.Now().UTC()

	// 1. Deduplication
	var existing domain.GpsPosition
	err := r.db.WithContext(ctx).
		Where(`"TenantId" = ? AND "DeviceId" = ? AND "ExternalReadingId" = ?`, tenantId, deviceId, externalReadingId).
		First(&existing).Error
	if err == nil {
		return &existing, nil
	} else if !errors.Is(err, gorm.ErrRecordNotFound) {
		return nil, fmt.Errorf("dedup check failed: %w", err)
	}

	// 2. Active assignment lookup
	var assignment domain.VehicleShipmentAssignment
	var shipmentId *uuid.UUID
	err = r.db.WithContext(ctx).
		Where(`"TenantId" = ? AND "VehicleId" = ? AND "EndedAt" IS NULL`, tenantId, vehicleId).
		Order(`"AssignedAt" DESC`).
		First(&assignment).Error
	if err == nil {
		shipmentId = &assignment.ShipmentId
	}

	posId := uuid.New()
	position := &domain.GpsPosition{
		Id:                posId,
		TenantId:          tenantId,
		DeviceId:          deviceId,
		VehicleId:         vehicleId,
		ShipmentId:        shipmentId,
		ExternalReadingId: externalReadingId,
		Latitude:          lat,
		Longitude:         lng,
		SpeedKph:          speedKph,
		HeadingDegrees:    heading,
		AccuracyMeters:    accuracy,
		RecordedAt:        recordedAt,
		ReceivedAt:        now,
		CreatedAt:         now,
	}

	err = r.db.WithContext(ctx).Transaction(func(tx *gorm.DB) error {
		// Save position
		if err := tx.Create(position).Error; err != nil {
			return err
		}

		// 3. Current Location snapshot
		var current domain.CurrentLocation
		currErr := tx.Where(`"TenantId" = ? AND "VehicleId" = ?`, tenantId, vehicleId).First(&current).Error

		advancesCurrent := false
		isStationary := speedKph != nil && *speedKph <= r.stationarySpeedKph

		if errors.Is(currErr, gorm.ErrRecordNotFound) {
			advancesCurrent = true
			var statSince *time.Time
			if isStationary {
				statSince = &recordedAt
			}
			newCurrent := domain.CurrentLocation{
				Id:              uuid.New(),
				TenantId:        tenantId,
				PositionId:      position.Id,
				VehicleId:       vehicleId,
				DeviceId:        deviceId,
				ShipmentId:      shipmentId,
				Latitude:        lat,
				Longitude:       lng,
				SpeedKph:        speedKph,
				HeadingDegrees:  heading,
				AccuracyMeters:  accuracy,
				RecordedAt:      recordedAt,
				ReceivedAt:      now,
				IsStationary:    isStationary,
				StationarySince: statSince,
				CreatedAt:       now,
			}
			if err := tx.Create(&newCurrent).Error; err != nil {
				return err
			}
			current = newCurrent
		} else if currErr == nil {
			// Check if this reading advances the current snapshot
			if recordedAt.After(current.RecordedAt) || (recordedAt.Equal(current.RecordedAt) && position.Id.String() > current.PositionId.String()) {
				advancesCurrent = true
				var statSince = current.StationarySince
				if isStationary {
					if statSince == nil {
						statSince = &recordedAt
					}
				} else {
					statSince = nil
				}
				updates := map[string]interface{}{
					`"PositionId"`:      position.Id,
					`"DeviceId"`:        deviceId,
					`"ShipmentId"`:      shipmentId,
					`"Latitude"`:        lat,
					`"Longitude"`:       lng,
					`"SpeedKph"`:        speedKph,
					`"HeadingDegrees"`:  heading,
					`"AccuracyMeters"`:  accuracy,
					`"RecordedAt"`:      recordedAt,
					`"ReceivedAt"`:      now,
					`"IsStationary"`:    isStationary,
					`"StationarySince"`: statSince,
					`"UpdatedAt"`:       &now,
				}
				if err := tx.Model(&domain.CurrentLocation{}).Where(`"Id" = ?`, current.Id).Updates(updates).Error; err != nil {
					return err
				}
			}
		} else {
			return currErr
		}

		// 4. Monitoring evaluation (only if current advanced)
		if advancesCurrent {
			r.evaluateMonitoring(tx, tenantId, position, &current)
		}

		// 5. Outbox Message for GpsPositionUpdatedEvent
		eventPayload, _ := json.Marshal(map[string]interface{}{
			"eventId":         uuid.New(),
			"contractVersion": 1,
			"tenantId":        tenantId,
			"positionId":      position.Id,
			"deviceId":        position.DeviceId,
			"vehicleId":       vehicleId,
			"shipmentId":      position.ShipmentId,
			"latitude":        lat,
			"longitude":       lng,
			"speedKph":        speedKph,
			"headingDegrees":  heading,
			"recordedAt":      recordedAt,
		})
		outbox := domain.OutboxMessage{
			Id:         uuid.New(),
			TenantId:   tenantId,
			EventId:    uuid.New(),
			EventType:  "GpsPositionUpdatedEvent",
			Content:    string(eventPayload),
			OccurredAt: now,
			RetryCount: 0,
		}
		return tx.Create(&outbox).Error
	})

	if err != nil {
		return nil, err
	}
	return position, nil
}

func (r *GpsRepository) evaluateMonitoring(tx *gorm.DB, tenantId uuid.UUID, pos *domain.GpsPosition, current *domain.CurrentLocation) {
	now := time.Now().UTC()

	// 1. Resolve any active SignalLost alert for this vehicle upon new position
	signalLostKey := fmt.Sprintf("SignalLost:%s:", pos.VehicleId)
	_ = tx.Model(&domain.MonitoringAlert{}).
		Where(`"TenantId" = ? AND "DeduplicationKey" = ? AND "Status" = 'Active'`, tenantId, signalLostKey).
		Updates(map[string]interface{}{`"Status"`: "Resolved", `"ResolvedAt"`: &now}).Error

	// 2. Evaluate AbnormalStop
	abnormalStopKey := fmt.Sprintf("AbnormalStop:%s:", pos.VehicleId)
	isStopped := pos.SpeedKph != nil && *pos.SpeedKph <= r.stationarySpeedKph
	// Threshold: 10 minutes (matching standard MonitoringOptions)
	if isStopped && current.StationarySince != nil && pos.ReceivedAt.Sub(*current.StationarySince) >= 10*time.Minute {
		var existingAlert domain.MonitoringAlert
		if tx.Where(`"TenantId" = ? AND "DeduplicationKey" = ? AND "Status" = 'Active'`, tenantId, abnormalStopKey).First(&existingAlert).Error != nil {
			alertId := uuid.New()
			msg := "Vehicle has remained stopped beyond the configured threshold."
			alert := domain.MonitoringAlert{
				Id:               alertId,
				TenantId:         tenantId,
				AlertType:        "AbnormalStop",
				Status:           "Active",
				VehicleId:        pos.VehicleId,
				ShipmentId:       pos.ShipmentId,
				PositionId:       &pos.Id,
				DeduplicationKey: abnormalStopKey,
				Message:          msg,
				OccurredAt:       pos.ReceivedAt,
			}
			if err := tx.Create(&alert).Error; err == nil {
				alertPayload, _ := json.Marshal(map[string]interface{}{
					"eventId":         uuid.New(),
					"contractVersion": 1,
					"tenantId":        tenantId,
					"alertId":         alertId,
					"alertType":       "AbnormalStop",
					"vehicleId":       pos.VehicleId,
					"shipmentId":      pos.ShipmentId,
					"positionId":      pos.Id,
					"message":         msg,
					"occurredAt":      pos.ReceivedAt,
				})
				_ = tx.Create(&domain.OutboxMessage{
					Id:         uuid.New(),
					TenantId:   tenantId,
					EventId:    uuid.New(),
					EventType:  "GpsMonitoringAlertRaisedEvent",
					Content:    string(alertPayload),
					OccurredAt: now,
					RetryCount: 0,
				}).Error
			}
		}
	} else {
		// Resolve AbnormalStop if vehicle is moving or not exceeding threshold
		_ = tx.Model(&domain.MonitoringAlert{}).
			Where(`"TenantId" = ? AND "DeduplicationKey" = ? AND "Status" = 'Active'`, tenantId, abnormalStopKey).
			Updates(map[string]interface{}{`"Status"`: "Resolved", `"ResolvedAt"`: &now}).Error
	}

	// 3. Geofence evaluation with vehicle & shipment filter matching EF Core
	query := tx.Where(`"TenantId" = ? AND "IsActive" = true AND ("VehicleId" IS NULL OR "VehicleId" = ?)`, tenantId, pos.VehicleId)
	if pos.ShipmentId != nil {
		query = query.Where(`("ShipmentId" IS NULL OR "ShipmentId" = ?)`, *pos.ShipmentId)
	} else {
		query = query.Where(`"ShipmentId" IS NULL`)
	}

	var geofences []domain.Geofence
	if err := query.Find(&geofences).Error; err != nil {
		return
	}

	for _, fence := range geofences {
		dist := domain.DistanceMeters(pos.Latitude, pos.Longitude, fence.Latitude, fence.Longitude)
		isInside := dist <= fence.RadiusMeters

		var presence domain.GeofencePresence
		pErr := tx.Where(`"TenantId" = ? AND "GeofenceId" = ? AND "VehicleId" = ?`, tenantId, fence.Id, pos.VehicleId).
			First(&presence).Error

		changed := false
		if errors.Is(pErr, gorm.ErrRecordNotFound) {
			presence = domain.GeofencePresence{
				Id:         uuid.New(),
				TenantId:   tenantId,
				GeofenceId: fence.Id,
				VehicleId:  pos.VehicleId,
				IsInside:   isInside,
				ObservedAt: pos.RecordedAt,
				CreatedAt:  pos.RecordedAt,
			}
			_ = tx.Create(&presence).Error
			changed = isInside
		} else if pErr == nil {
			if !pos.RecordedAt.Before(presence.ObservedAt) {
				if presence.IsInside != isInside {
					changed = true
					presence.IsInside = isInside
				}
				presence.ObservedAt = pos.RecordedAt
				presence.UpdatedAt = &now
				_ = tx.Save(&presence).Error
			}
		}

		if changed {
			activeType := "GeofenceEntered"
			resolvedType := "GeofenceExited"
			msg := fmt.Sprintf("Vehicle entered geofence %s.", fence.Name)
			if !isInside {
				activeType = "GeofenceExited"
				resolvedType = "GeofenceEntered"
				msg = fmt.Sprintf("Vehicle exited geofence %s.", fence.Name)
			}

			resolvedKey := fmt.Sprintf("%s:%s:%s", resolvedType, pos.VehicleId, fence.Id)
			activeKey := fmt.Sprintf("%s:%s:%s", activeType, pos.VehicleId, fence.Id)

			// Resolve opposing alert
			_ = tx.Model(&domain.MonitoringAlert{}).
				Where(`"TenantId" = ? AND "DeduplicationKey" = ? AND "Status" = 'Active'`, tenantId, resolvedKey).
				Updates(map[string]interface{}{`"Status"`: "Resolved", `"ResolvedAt"`: &now}).Error

			// Check if active alert already exists
			var existingAlert domain.MonitoringAlert
			if tx.Where(`"TenantId" = ? AND "DeduplicationKey" = ? AND "Status" = 'Active'`, tenantId, activeKey).First(&existingAlert).Error != nil {
				alertId := uuid.New()
				alert := domain.MonitoringAlert{
					Id:               alertId,
					TenantId:         tenantId,
					AlertType:        activeType,
					Status:           "Active",
					VehicleId:        pos.VehicleId,
					ShipmentId:       pos.ShipmentId,
					GeofenceId:       &fence.Id,
					PositionId:       &pos.Id,
					DeduplicationKey: activeKey,
					Message:          msg,
					OccurredAt:       pos.ReceivedAt,
				}
				if err := tx.Create(&alert).Error; err == nil {
					// Create Outbox message for GpsMonitoringAlertRaisedEvent
					alertPayload, _ := json.Marshal(map[string]interface{}{
						"eventId":         uuid.New(),
						"contractVersion": 1,
						"tenantId":        tenantId,
						"alertId":         alertId,
						"alertType":       activeType,
						"vehicleId":       pos.VehicleId,
						"shipmentId":      pos.ShipmentId,
						"geofenceId":      fence.Id,
						"positionId":      pos.Id,
						"message":         msg,
						"occurredAt":      pos.ReceivedAt,
					})
					_ = tx.Create(&domain.OutboxMessage{
						Id:         uuid.New(),
						TenantId:   tenantId,
						EventId:    uuid.New(),
						EventType:  "GpsMonitoringAlertRaisedEvent",
						Content:    string(alertPayload),
						OccurredAt: now,
						RetryCount: 0,
					}).Error
				}
			}
		}
	}
}

func (r *GpsRepository) GetCurrentLocation(ctx context.Context, tenantId uuid.UUID, vehicleId, shipmentId string) (*domain.CurrentLocation, error) {
	var current domain.CurrentLocation
	query := r.db.WithContext(ctx).Where(`"TenantId" = ?`, tenantId)
	if vehicleId != "" {
		query = query.Where(`"VehicleId" = ?`, vehicleId)
	} else if shipmentId != "" {
		sUUID, err := uuid.Parse(shipmentId)
		if err != nil {
			return nil, err
		}
		query = query.Where(`"ShipmentId" = ?`, sUUID)
	} else {
		return nil, errors.New("must provide vehicle_id or shipment_id")
	}

	if err := query.First(&current).Error; err != nil {
		return nil, err
	}
	return &current, nil
}

func (r *GpsRepository) ListPositionHistory(
	ctx context.Context,
	tenantId uuid.UUID,
	vehicleId, shipmentId string,
	from, to time.Time,
	page, pageSize int,
) ([]domain.GpsPosition, int64, error) {
	var positions []domain.GpsPosition
	var total int64

	query := r.db.WithContext(ctx).Model(&domain.GpsPosition{}).Where(`"TenantId" = ?`, tenantId)
	if vehicleId != "" {
		query = query.Where(`"VehicleId" = ?`, vehicleId)
	} else if shipmentId != "" {
		sUUID, err := uuid.Parse(shipmentId)
		if err != nil {
			return nil, 0, err
		}
		query = query.Where(`"ShipmentId" = ?`, sUUID)
	}

	if !from.IsZero() {
		query = query.Where(`"RecordedAt" >= ?`, from)
	}
	if !to.IsZero() {
		query = query.Where(`"RecordedAt" <= ?`, to)
	}

	if err := query.Count(&total).Error; err != nil {
		return nil, 0, err
	}

	if page < 1 {
		page = 1
	}
	if pageSize < 1 || pageSize > 500 {
		pageSize = 50
	}
	offset := (page - 1) * pageSize

	err := query.Order(`"RecordedAt" DESC`).Limit(pageSize).Offset(offset).Find(&positions).Error
	return positions, total, err
}

func (r *GpsRepository) CreateGeofence(
	ctx context.Context,
	tenantId uuid.UUID,
	name string,
	lat, lng, radius float64,
	shipmentId *uuid.UUID,
	vehicleId *string,
) (*domain.Geofence, error) {
	fence := &domain.Geofence{
		Id:           uuid.New(),
		TenantId:     tenantId,
		Name:         name,
		Latitude:     lat,
		Longitude:    lng,
		RadiusMeters: radius,
		ShipmentId:   shipmentId,
		VehicleId:    vehicleId,
		IsActive:     true,
	}
	err := r.db.WithContext(ctx).Create(fence).Error
	return fence, err
}

func (r *GpsRepository) ListGeofences(ctx context.Context, tenantId uuid.UUID, includeInactive bool) ([]domain.Geofence, error) {
	var list []domain.Geofence
	query := r.db.WithContext(ctx).Where(`"TenantId" = ?`, tenantId)
	if !includeInactive {
		query = query.Where(`"IsActive" = true`)
	}
	err := query.Find(&list).Error
	return list, err
}

func (r *GpsRepository) SetGeofenceActive(ctx context.Context, tenantId, fenceId uuid.UUID, isActive bool) (*domain.Geofence, error) {
	var fence domain.Geofence
	err := r.db.WithContext(ctx).Where(`"TenantId" = ? AND "Id" = ?`, tenantId, fenceId).First(&fence).Error
	if err != nil {
		return nil, err
	}
	fence.IsActive = isActive
	err = r.db.WithContext(ctx).Model(&fence).Update(`"IsActive"`, isActive).Error
	return &fence, err
}

func (r *GpsRepository) ListMonitoringAlerts(ctx context.Context, tenantId uuid.UUID, page, pageSize int) ([]domain.MonitoringAlert, int64, error) {
	var alerts []domain.MonitoringAlert
	var total int64
	query := r.db.WithContext(ctx).Model(&domain.MonitoringAlert{}).Where(`"TenantId" = ?`, tenantId)
	_ = query.Count(&total).Error

	if page < 1 {
		page = 1
	}
	if pageSize < 1 || pageSize > 500 {
		pageSize = 50
	}
	offset := (page - 1) * pageSize

	err := query.Order(`"OccurredAt" DESC`).Limit(pageSize).Offset(offset).Find(&alerts).Error
	return alerts, total, err
}

func (r *GpsRepository) ResolveMonitoringAlert(ctx context.Context, tenantId, alertId uuid.UUID) (*domain.MonitoringAlert, error) {
	var alert domain.MonitoringAlert
	err := r.db.WithContext(ctx).Where(`"TenantId" = ? AND "Id" = ?`, tenantId, alertId).First(&alert).Error
	if err != nil {
		return nil, err
	}
	now := time.Now().UTC()
	alert.Status = "Resolved"
	alert.ResolvedAt = &now
	err = r.db.WithContext(ctx).Model(&alert).Updates(map[string]interface{}{
		`"Status"`:     "Resolved",
		`"ResolvedAt"`: &now,
	}).Error
	return &alert, err
}

