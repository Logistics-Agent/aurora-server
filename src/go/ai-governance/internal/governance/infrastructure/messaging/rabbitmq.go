package messaging

import (
	"context"
	"encoding/json"
	"log/slog"
	"time"

	"aurora/ai-governance/internal/governance/application"
	"aurora/ai-governance/internal/governance/domain"
	"aurora/ai-governance/internal/shared"
	"aurora/shared/rabbitmq"

	"github.com/google/uuid"
	amqp "github.com/rabbitmq/amqp091-go"
)

const (
	ExchangeAiGovernance   = "exchange.ai-governance"
	QueuePolicyAudit       = "queue.ai-governance.policy-audit"
	QueueUsageEvents       = "queue.ai-governance.usage-events"
	RoutingKeyPolicyAudit  = "ai.policy.audit"
	RoutingKeyUsageEvents  = "ai.usage.recorded"
)

type AiUsageEvent struct {
	EventID         string             `json:"eventId"`
	TenantID        *uuid.UUID         `json:"tenantId,omitempty"`
	UserID          *uuid.UUID         `json:"userId,omitempty"`
	CallerServiceID string             `json:"callerServiceId"`
	CapabilityCode  string             `json:"capabilityCode"`
	Operation       shared.AiOperation `json:"operation"`
	Provider        string             `json:"provider"`
	Model           string             `json:"model"`
	InputTokens     int64              `json:"inputTokens"`
	OutputTokens    int64              `json:"outputTokens"`
	DurationMs      int64              `json:"durationMs"`
	Success         bool               `json:"success"`
	Timestamp       time.Time          `json:"timestamp"`
}

type RabbitMqPolicyAuditPublisher struct {
	client *rabbitmq.Client
	logger *slog.Logger
}

func NewRabbitMqPolicyAuditPublisher(client *rabbitmq.Client, logger *slog.Logger) *RabbitMqPolicyAuditPublisher {
	if logger == nil {
		logger = slog.Default()
	}
	return &RabbitMqPolicyAuditPublisher{
		client: client,
		logger: logger,
	}
}

func (p *RabbitMqPolicyAuditPublisher) PublishPolicyDecision(ctx context.Context, event application.PolicyAuditEvent) {
	if p.client == nil {
		return
	}
	body, err := json.Marshal(event)
	if err != nil {
		p.logger.Error("Failed to marshal policy audit event", slog.String("decisionId", event.DecisionID), slog.Any("error", err))
		return
	}
	if err := p.client.PublishTopic(ctx, ExchangeAiGovernance, RoutingKeyPolicyAudit, body, "application/json", event.DecisionID, nil); err != nil {
		p.logger.Error("Failed to publish policy audit event", slog.String("decisionId", event.DecisionID), slog.Any("error", err))
		return
	}
	p.logger.Debug("Published policy audit event", slog.String("decisionId", event.DecisionID))
}

type UsageEventPublisher struct {
	client *rabbitmq.Client
	logger *slog.Logger
}

func NewUsageEventPublisher(client *rabbitmq.Client, logger *slog.Logger) *UsageEventPublisher {
	if logger == nil {
		logger = slog.Default()
	}
	return &UsageEventPublisher{client: client, logger: logger}
}

func (p *UsageEventPublisher) PublishUsageEvent(ctx context.Context, event AiUsageEvent) {
	if p.client == nil {
		return
	}
	body, err := json.Marshal(event)
	if err != nil {
		p.logger.Warn("Failed to marshal AI usage event", slog.Any("error", err))
		return
	}
	if err := p.client.PublishTopic(ctx, ExchangeAiGovernance, RoutingKeyUsageEvents, body, "application/json", event.EventID, nil); err != nil {
		p.logger.Warn("Failed to publish AI usage event", slog.Any("error", err))
	}
}

type AiUsageEventConsumer struct {
	client         *rabbitmq.Client
	processedRepo  application.ProcessedEventRepository
	quotaSync      *application.QuotaSyncService
	logger         *slog.Logger
}

func NewAiUsageEventConsumer(
	client *rabbitmq.Client,
	processedRepo application.ProcessedEventRepository,
	quotaSync *application.QuotaSyncService,
	logger *slog.Logger,
) *AiUsageEventConsumer {
	if logger == nil {
		logger = slog.Default()
	}
	return &AiUsageEventConsumer{
		client:        client,
		processedRepo: processedRepo,
		quotaSync:     quotaSync,
		logger:        logger,
	}
}

func (c *AiUsageEventConsumer) Start(ctx context.Context) error {
	if c.client == nil {
		return nil
	}
	// Ensure audit queue binding also exists
	if err := c.client.DeclareTopicExchange(ExchangeAiGovernance); err != nil {
		return err
	}
	return c.client.SubscribeTopic(ctx, QueueUsageEvents, ExchangeAiGovernance, RoutingKeyUsageEvents, func(d amqp.Delivery) {
		var evt AiUsageEvent
		if err := json.Unmarshal(d.Body, &evt); err != nil {
			c.logger.Warn("Invalid AI usage event payload", slog.Any("error", err))
			_ = d.Nack(false, false)
			return
		}
		if err := c.HandleUsageEvent(ctx, evt); err != nil {
			c.logger.Error("Failed handling AI usage event", slog.String("eventId", evt.EventID), slog.Any("error", err))
			_ = d.Nack(false, true)
			return
		}
		_ = d.Ack(false)
	})
}

func (c *AiUsageEventConsumer) HandleUsageEvent(ctx context.Context, event AiUsageEvent) error {
	exists, err := c.processedRepo.ExistsByEventID(ctx, event.EventID)
	if err != nil {
		return err
	}
	if exists {
		c.logger.Info("Duplicate event ignored", slog.String("eventId", event.EventID))
		return nil
	}

	processed := &domain.ProcessedEvent{
		ID:          uuid.New(),
		EventID:     event.EventID,
		ProcessedAt: time.Now().UTC(),
		Version:     1,
	}
	if err := c.processedRepo.Save(ctx, processed); err != nil {
		return err
	}

	if event.Success && event.TenantID != nil && *event.TenantID != uuid.Nil {
		totalTokens := event.InputTokens + event.OutputTokens
		ts := event.Timestamp
		if ts.IsZero() {
			ts = time.Now().UTC()
		}
		return c.quotaSync.RecordUsage(ctx, *event.TenantID, 1, totalTokens, ts)
	}
	return nil
}
