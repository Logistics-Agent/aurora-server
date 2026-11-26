package background

import (
	"context"
	"encoding/json"
	"fmt"
	"log/slog"
	"time"

	"aurora/gps-tracking/internal/config"
	"aurora/gps-tracking/internal/domain"
	"aurora/shared/rabbitmq"

	"gorm.io/gorm"
	"gorm.io/gorm/clause"
)

type OutboxPublisher struct {
	db       *gorm.DB
	rabbit   *rabbitmq.Client
	cfg      config.Config
	logger   *slog.Logger
	stopChan chan struct{}
}

func NewOutboxPublisher(db *gorm.DB, rabbit *rabbitmq.Client, cfg config.Config, logger *slog.Logger) *OutboxPublisher {
	if logger == nil {
		logger = slog.Default()
	}
	return &OutboxPublisher{
		db:       db,
		rabbit:   rabbit,
		cfg:      cfg,
		logger:   logger,
		stopChan: make(chan struct{}),
	}
}

func (p *OutboxPublisher) Start(ctx context.Context) {
	ticker := time.NewTicker(p.cfg.OutboxPollingInterval)
	defer ticker.Stop()

	p.logger.Info("GPS Outbox Publisher started",
		slog.Duration("interval", p.cfg.OutboxPollingInterval),
		slog.Int("batch_size", p.cfg.OutboxBatchSize),
	)

	for {
		select {
		case <-ctx.Done():
			p.logger.Info("GPS Outbox Publisher stopping on context cancellation...")
			return
		case <-p.stopChan:
			p.logger.Info("GPS Outbox Publisher stopped")
			return
		case <-ticker.C:
			if err := p.ProcessBatch(ctx); err != nil {
				p.logger.Warn("Error processing GPS outbox batch", slog.Any("error", err))
			}
		}
	}
}

func (p *OutboxPublisher) Stop() {
	close(p.stopChan)
}

func (p *OutboxPublisher) ProcessBatch(ctx context.Context) error {
	if p.rabbit == nil {
		return nil
	}

	var messages []domain.OutboxMessage
	err := p.db.WithContext(ctx).Transaction(func(tx *gorm.DB) error {
		// Select pending messages with FOR UPDATE SKIP LOCKED
		if err := tx.Clauses(clause.Locking{Strength: "UPDATE", Options: "SKIP LOCKED"}).
			Where(`"ProcessedAt" IS NULL AND "RetryCount" < ?`, p.cfg.OutboxMaxRetries).
			Order(`"OccurredAt" ASC`).
			Limit(p.cfg.OutboxBatchSize).
			Find(&messages).Error; err != nil {
			return err
		}

		if len(messages) == 0 {
			return nil
		}

		now := time.Now().UTC()
		for i := range messages {
			msg := &messages[i]
			exchange := fmt.Sprintf("GpsTracking.Contracts.Events:%s", msg.EventType)

			pubErr := p.rabbit.PublishJSON(ctx, exchange, "", json.RawMessage(msg.Content))
			if pubErr == nil {
				msg.ProcessedAt = &now
				msg.Error = nil
				if err := tx.Model(msg).Select("ProcessedAt", "Error").Updates(map[string]interface{}{
					`"ProcessedAt"`: &now,
					`"Error"`:       nil,
				}).Error; err != nil {
					p.logger.Warn("Failed to mark outbox message processed", slog.String("id", msg.Id.String()), slog.Any("error", err))
				}
			} else {
				msg.RetryCount++
				errMsg := pubErr.Error()
				if len(errMsg) > 2000 {
					errMsg = errMsg[:2000]
				}
				msg.Error = &errMsg
				if err := tx.Model(msg).Select("RetryCount", "Error").Updates(map[string]interface{}{
					`"RetryCount"`: msg.RetryCount,
					`"Error"`:      &errMsg,
				}).Error; err != nil {
					p.logger.Warn("Failed to record outbox message failure", slog.String("id", msg.Id.String()), slog.Any("error", err))
				}
			}
		}
		return nil
	})

	return err
}
