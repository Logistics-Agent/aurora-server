package messaging

import (
	"context"
	"encoding/json"
	"fmt"
	"log/slog"

	"aurora/route-planning-agent/internal/application/dtos"
	"aurora/shared/rabbitmq"

	amqp "github.com/rabbitmq/amqp091-go"
)

type CacheInvalidator interface {
	InvalidateCache(tenantIDStr, ruleName string)
}

type RuleConfigConsumer struct {
	rabbitClient *rabbitmq.Client
	invalidator  CacheInvalidator
	queueName    string
	exchange     string
	logger       *slog.Logger
}

func NewRuleConfigConsumer(
	rabbitClient *rabbitmq.Client,
	invalidator CacheInvalidator,
	exchange string,
	logger *slog.Logger,
) *RuleConfigConsumer {
	if logger == nil {
		logger = slog.Default()
	}
	if exchange == "" {
		exchange = "route.events"
	}

	return &RuleConfigConsumer{
		rabbitClient: rabbitClient,
		invalidator:  invalidator,
		queueName:    "route-planning.rule-config-changed",
		exchange:     exchange,
		logger:       logger,
	}
}

// ProcessMessage giải mã message và xử lý invalidate cache
func (c *RuleConfigConsumer) ProcessMessage(body []byte) error {
	var event dtos.TenantRuleConfigChangedEvent
	if err := json.Unmarshal(body, &event); err != nil {
		return fmt.Errorf("không thể unmarshal TenantRuleConfigChangedEvent: %w", err)
	}

	c.logger.Info("Nhận được TenantRuleConfigChangedEvent",
		slog.String("tenant_id", event.TenantID.String()),
		slog.String("rule_name", event.RuleName),
	)

	if c.invalidator != nil {
		c.invalidator.InvalidateCache(event.TenantID.String(), event.RuleName)
	}

	return nil
}

// HandleDelivery hàm wrapper cho AMQP delivery handler
func (c *RuleConfigConsumer) HandleDelivery(ctx context.Context, d amqp.Delivery) {
	if err := c.ProcessMessage(d.Body); err != nil {
		c.logger.Error("Lỗi xử lý message từ queue", slog.Any("error", err))
		_ = d.Nack(false, true) // Requeue nếu lỗi
		return
	}
	_ = d.Ack(false)
}

