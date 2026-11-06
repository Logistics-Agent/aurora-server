package outbox

import (
	"context"
	"encoding/json"
	"fmt"
	"log/slog"
	"strings"
	"time"

	"aurora/route-planning-agent/internal/domain/repository"
	"aurora/shared/rabbitmq"
)

// Config cấu hình cho worker quét Outbox
type Config struct {
	PollingInterval time.Duration
	BatchSize       int32
	Exchange        string
}

type Processor struct {
	repo         repository.OutboxRepository
	rabbitClient *rabbitmq.Client
	cfg          Config
	logger       *slog.Logger
}

func NewProcessor(
	repo repository.OutboxRepository,
	rabbitClient *rabbitmq.Client,
	cfg Config,
	logger *slog.Logger,
) *Processor {
	if logger == nil {
		logger = slog.Default()
	}
	if cfg.PollingInterval <= 0 {
		cfg.PollingInterval = 5 * time.Second
	}
	if cfg.BatchSize <= 0 {
		cfg.BatchSize = 50
	}
	if cfg.Exchange == "" {
		cfg.Exchange = "route.events"
	}

	return &Processor{
		repo:         repo,
		rabbitClient: rabbitClient,
		cfg:          cfg,
		logger:       logger,
	}
}

// Start khởi chạy worker chạy nền, dừng an toàn khi context bị hủy
func (p *Processor) Start(ctx context.Context) {
	p.logger.Info("Khởi động Outbox Processor worker",
		slog.Duration("interval", p.cfg.PollingInterval),
		slog.String("exchange", p.cfg.Exchange),
	)

	ticker := time.NewTicker(p.cfg.PollingInterval)
	defer ticker.Stop()

	for {
		select {
		case <-ctx.Done():
			p.logger.Info("Outbox Processor worker đang dừng...")
			return
		case <-ticker.C:
			p.processBatch(ctx)
		}
	}
}

func (p *Processor) processBatch(ctx context.Context) {
	messages, err := p.repo.GetUnprocessed(ctx, p.cfg.BatchSize)
	if err != nil {
		p.logger.Error("Lỗi truy vấn Outbox messages chưa xử lý", slog.Any("error", err))
		return
	}

	if len(messages) == 0 {
		return
	}

	p.logger.Debug("Đang xử lý lô Outbox messages", slog.Int("count", len(messages)))

	for _, msg := range messages {
		routingKey := MapRoutingKey(msg.EventType)

		var payloadMap any
		if err := json.Unmarshal([]byte(msg.Payload), &payloadMap); err != nil {
			// Payload không hợp lệ
			_ = p.repo.MarkFailed(ctx, msg.ID, fmt.Sprintf("invalid json: %v", err))
			continue
		}

		err := p.rabbitClient.PublishJSON(ctx, p.cfg.Exchange, routingKey, payloadMap)
		if err != nil {
			p.logger.Error("Publish event sang RabbitMQ thất bại",
				slog.String("message_id", msg.ID.String()),
				slog.String("event_type", msg.EventType),
				slog.Any("error", err),
			)
			_ = p.repo.MarkFailed(ctx, msg.ID, err.Error())
		} else {
			_ = p.repo.MarkProcessed(ctx, msg.ID, time.Now().UTC())
		}
	}
}

// MapRoutingKey chuyển đổi EventType PascalCase sang routing key dạng kebab/dot
// Ví dụ: RouteOptimizedEvent -> route.optimized
func MapRoutingKey(eventType string) string {
	name := strings.TrimSuffix(eventType, "Event")

	var b strings.Builder
	for i, r := range name {
		if i > 0 && r >= 'A' && r <= 'Z' {
			b.WriteRune('.')
		}
		b.WriteRune(r)
	}

	return strings.ToLower(b.String())
}

