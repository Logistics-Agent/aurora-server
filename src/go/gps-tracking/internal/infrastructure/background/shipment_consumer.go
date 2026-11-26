package background

import (
	"context"
	"encoding/json"
	"errors"
	"fmt"
	"log/slog"
	"strings"
	"time"

	"aurora/gps-tracking/internal/domain"
	"aurora/shared/rabbitmq"

	"github.com/google/uuid"
	amqp "github.com/rabbitmq/amqp091-go"
	"gorm.io/gorm"
)

type ShipmentConsumer struct {
	db     *gorm.DB
	rabbit *rabbitmq.Client
	logger *slog.Logger
}

func NewShipmentConsumer(db *gorm.DB, rabbit *rabbitmq.Client, logger *slog.Logger) *ShipmentConsumer {
	if logger == nil {
		logger = slog.Default()
	}
	return &ShipmentConsumer{
		db:     db,
		rabbit: rabbit,
		logger: logger,
	}
}

type baseShipmentEvent struct {
	EventId         uuid.UUID `json:"eventId"`
	ContractVersion int       `json:"contractVersion"`
	ShipmentId      uuid.UUID `json:"shipmentId"`
	TenantId        uuid.UUID `json:"tenantId"`
}

type routeAssignedPayload struct {
	baseShipmentEvent
	ShipmentNumber string    `json:"shipmentNumber"`
	RouteId        string    `json:"routeId"`
	VehicleId      string    `json:"vehicleId"`
	AssignedAt     time.Time `json:"assignedAt"`
}

type shipmentCancelledPayload struct {
	baseShipmentEvent
	Reason      string    `json:"reason"`
	CancelledAt time.Time `json:"cancelledAt"`
}

type shipmentCompletedPayload struct {
	baseShipmentEvent
	ShipmentNumber string    `json:"shipmentNumber"`
	CurrentStatus  string    `json:"currentStatus"`
	CompletedAt    time.Time `json:"completedAt"`
}

func (c *ShipmentConsumer) Start(ctx context.Context) error {
	if c.rabbit == nil {
		c.logger.Warn("RabbitMQ client is nil, skipping shipment consumer start")
		return nil
	}

	queueName := "gps-tracking-shipment-events"
	exchanges := []string{
		"Shipment.Contracts.Events:RouteAssignedEvent",
		"Shipment.Contracts.Events:ShipmentCancelledEvent",
		"Shipment.Contracts.Events:ShipmentCompletedEvent",
	}

	for _, ex := range exchanges {
		err := c.rabbit.Subscribe(ctx, queueName, ex, "", func(d amqp.Delivery) {
			if err := c.handleDelivery(ctx, d); err != nil {
				c.logger.Error("Failed to handle shipment event message",
					slog.String("exchange", d.Exchange),
					slog.Any("error", err),
				)
				_ = d.Nack(false, true) // Requeue
			} else {
				_ = d.Ack(false)
			}
		})
		if err != nil {
			return fmt.Errorf("failed to subscribe to exchange %s: %w", ex, err)
		}
	}

	c.logger.Info("GPS Shipment Consumer subscribed to shipment lifecycle events")
	return nil
}

func (c *ShipmentConsumer) handleDelivery(ctx context.Context, d amqp.Delivery) error {
	var base baseShipmentEvent
	if err := json.Unmarshal(d.Body, &base); err != nil {
		return fmt.Errorf("failed to parse event envelope: %w", err)
	}

	if base.EventId == uuid.Nil || base.TenantId == uuid.Nil || base.ShipmentId == uuid.Nil {
		return errors.New("missing required event identity fields")
	}

	eventType := d.Exchange
	if idx := strings.LastIndex(eventType, ":"); idx != -1 {
		eventType = eventType[idx+1:]
	}

	// 1. Check idempotency / consumed events
	var consumed domain.ConsumedIntegrationEvent
	err := c.db.WithContext(ctx).
		Where(`"SourceEventType" = ? AND "SourceEventId" = ?`, eventType, base.EventId).
		First(&consumed).Error
	if err == nil {
		// Already processed
		return nil
	} else if !errors.Is(err, gorm.ErrRecordNotFound) {
		return fmt.Errorf("idempotency check failed: %w", err)
	}

	switch eventType {
	case "RouteAssignedEvent":
		var ev routeAssignedPayload
		if err := json.Unmarshal(d.Body, &ev); err != nil {
			return fmt.Errorf("failed to unmarshal RouteAssignedEvent: %w", err)
		}
		return c.processRouteAssigned(ctx, ev, eventType)

	case "ShipmentCancelledEvent":
		var ev shipmentCancelledPayload
		if err := json.Unmarshal(d.Body, &ev); err != nil {
			return fmt.Errorf("failed to unmarshal ShipmentCancelledEvent: %w", err)
		}
		return c.processTerminal(ctx, base, ev.CancelledAt, eventType)

	case "ShipmentCompletedEvent":
		var ev shipmentCompletedPayload
		if err := json.Unmarshal(d.Body, &ev); err != nil {
			return fmt.Errorf("failed to unmarshal ShipmentCompletedEvent: %w", err)
		}
		return c.processTerminal(ctx, base, ev.CompletedAt, eventType)

	default:
		c.logger.Warn("Unknown shipment event type, ignoring", slog.String("eventType", eventType))
		return nil
	}
}

func (c *ShipmentConsumer) processRouteAssigned(ctx context.Context, ev routeAssignedPayload, eventType string) error {
	vehicleId := strings.TrimSpace(ev.VehicleId)
	routeId := strings.TrimSpace(ev.RouteId)
	if vehicleId == "" || routeId == "" || ev.AssignedAt.IsZero() {
		return errors.New("route assignment requires vehicle, route, and assigned time")
	}

	now := time.Now().UTC()

	return c.db.WithContext(ctx).Transaction(func(tx *gorm.DB) error {
		// 1. Update ShipmentTrackingState
		var state domain.ShipmentTrackingState
		stateErr := tx.Where(`"TenantId" = ? AND "ShipmentId" = ?`, ev.TenantId, ev.ShipmentId).First(&state).Error

		shouldProject := false
		if errors.Is(stateErr, gorm.ErrRecordNotFound) {
			shouldProject = true
			state = domain.ShipmentTrackingState{
				Id:          uuid.New(),
				TenantId:    ev.TenantId,
				ShipmentId:  ev.ShipmentId,
				IsClosed:    false,
				LastEventAt: ev.AssignedAt,
				CreatedAt:   ev.AssignedAt,
			}
			if err := tx.Create(&state).Error; err != nil {
				return err
			}
		} else if stateErr == nil {
			if !state.IsClosed && ev.AssignedAt.After(state.LastEventAt) {
				shouldProject = true
				state.LastEventAt = ev.AssignedAt
				state.UpdatedAt = &ev.AssignedAt
				if err := tx.Save(&state).Error; err != nil {
					return err
				}
			}
		} else {
			return stateErr
		}

		// 2. Project VehicleShipmentAssignment
		if shouldProject {
			var active []domain.VehicleShipmentAssignment
			if err := tx.Where(`"TenantId" = ? AND "EndedAt" IS NULL AND ("VehicleId" = ? OR "ShipmentId" = ?)`,
				ev.TenantId, vehicleId, ev.ShipmentId).Find(&active).Error; err != nil {
				return err
			}

			exactExists := false
			for _, assignment := range active {
				if assignment.AssignedAt.Before(ev.AssignedAt) || assignment.AssignedAt.Equal(ev.AssignedAt) {
					if err := tx.Model(&domain.VehicleShipmentAssignment{}).
						Where(`"Id" = ?`, assignment.Id).
						Update("EndedAt", ev.AssignedAt).Error; err != nil {
						return err
					}
				}
				if assignment.VehicleId == vehicleId && assignment.ShipmentId == ev.ShipmentId && assignment.RouteId == routeId {
					exactExists = true
				}
			}

			if !exactExists {
				newAssignment := domain.VehicleShipmentAssignment{
					Id:         uuid.New(),
					TenantId:   ev.TenantId,
					ShipmentId: ev.ShipmentId,
					VehicleId:  vehicleId,
					RouteId:    routeId,
					AssignedAt: ev.AssignedAt,
					EndedAt:    nil,
				}
				if err := tx.Create(&newAssignment).Error; err != nil {
					return err
				}
			}
		}

		// 3. Record receipt
		receipt := domain.ConsumedIntegrationEvent{
			Id:              uuid.New(),
			TenantId:        ev.TenantId,
			SourceEventId:   ev.EventId,
			SourceEventType: eventType,
			ContractVersion: ev.ContractVersion,
			ReceivedAt:      now,
			CreatedAt:       now,
		}
		return tx.Create(&receipt).Error
	})
}

func (c *ShipmentConsumer) processTerminal(ctx context.Context, base baseShipmentEvent, occurredAt time.Time, eventType string) error {
	if occurredAt.IsZero() {
		return errors.New("terminal shipment event time is required")
	}

	now := time.Now().UTC()

	return c.db.WithContext(ctx).Transaction(func(tx *gorm.DB) error {
		// 1. Update ShipmentTrackingState
		var state domain.ShipmentTrackingState
		stateErr := tx.Where(`"TenantId" = ? AND "ShipmentId" = ?`, base.TenantId, base.ShipmentId).First(&state).Error

		if errors.Is(stateErr, gorm.ErrRecordNotFound) {
			state = domain.ShipmentTrackingState{
				Id:          uuid.New(),
				TenantId:    base.TenantId,
				ShipmentId:  base.ShipmentId,
				IsClosed:    true,
				LastEventAt: occurredAt,
				CreatedAt:   occurredAt,
			}
			if err := tx.Create(&state).Error; err != nil {
				return err
			}
		} else if stateErr == nil {
			state.IsClosed = true
			if occurredAt.After(state.LastEventAt) {
				state.LastEventAt = occurredAt
			}
			state.UpdatedAt = &occurredAt
			if err := tx.Save(&state).Error; err != nil {
				return err
			}
		} else {
			return stateErr
		}

		// 2. End active assignments for this shipment
		var active []domain.VehicleShipmentAssignment
		if err := tx.Where(`"TenantId" = ? AND "ShipmentId" = ? AND "EndedAt" IS NULL`,
			base.TenantId, base.ShipmentId).Find(&active).Error; err != nil {
			return err
		}

		for _, assignment := range active {
			endAt := occurredAt
			if occurredAt.Before(assignment.AssignedAt) {
				endAt = assignment.AssignedAt
			}
			if err := tx.Model(&domain.VehicleShipmentAssignment{}).
				Where(`"Id" = ?`, assignment.Id).
				Update("EndedAt", endAt).Error; err != nil {
				return err
			}
		}

		// 3. Record receipt
		receipt := domain.ConsumedIntegrationEvent{
			Id:              uuid.New(),
			TenantId:        base.TenantId,
			SourceEventId:   base.EventId,
			SourceEventType: eventType,
			ContractVersion: base.ContractVersion,
			ReceivedAt:      now,
			CreatedAt:       now,
		}
		return tx.Create(&receipt).Error
	})
}

