package cache

import (
	"context"
	"log/slog"
	"strconv"
	"time"

	"aurora/ai-governance/internal/governance/domain"

	"github.com/google/uuid"
	"github.com/redis/go-redis/v9"
)

type QuotaRedisAdapter struct {
	client *redis.Client
	logger *slog.Logger
}

func NewQuotaRedisAdapter(client *redis.Client, logger *slog.Logger) *QuotaRedisAdapter {
	if logger == nil {
		logger = slog.Default()
	}
	return &QuotaRedisAdapter{
		client: client,
		logger: logger,
	}
}

func (a *QuotaRedisAdapter) GetCurrentUsage(
	ctx context.Context,
	tenantID uuid.UUID,
	metric domain.QuotaMetric,
	period domain.QuotaPeriod,
	periodKey domain.PeriodKey,
) int64 {
	if a.client == nil {
		return 0
	}
	key := domain.QuotaKey{
		TenantID:  tenantID.String(),
		Metric:    metric,
		Period:    period,
		PeriodKey: periodKey,
	}.ToRedisKey()

	val, err := a.client.Get(ctx, key).Result()
	if err == redis.Nil {
		return 0
	}
	if err != nil {
		a.logger.Warn("Failed to read tenant quota from Redis", slog.String("key", key), slog.Any("error", err))
		return 0
	}
	parsed, err := strconv.ParseInt(val, 10, 64)
	if err != nil {
		return 0
	}
	return parsed
}

func (a *QuotaRedisAdapter) IncrementUsage(
	ctx context.Context,
	tenantID uuid.UUID,
	metric domain.QuotaMetric,
	period domain.QuotaPeriod,
	periodKey domain.PeriodKey,
	amount int64,
) {
	if a.client == nil {
		return
	}
	key := domain.QuotaKey{
		TenantID:  tenantID.String(),
		Metric:    metric,
		Period:    period,
		PeriodKey: periodKey,
	}.ToRedisKey()

	newVal, err := a.client.IncrBy(ctx, key, amount).Result()
	if err != nil {
		a.logger.Error("Failed to increment tenant quota in Redis",
			slog.String("key", key), slog.Int64("amount", amount), slog.Any("error", err))
		return
	}
	if newVal == amount {
		_ = a.client.Expire(ctx, key, resolveTTL(period)).Err()
	}
}

func resolveTTL(period domain.QuotaPeriod) time.Duration {
	switch period {
	case domain.QuotaPeriodMinute:
		return 5 * time.Minute
	case domain.QuotaPeriodDay:
		return 48 * time.Hour
	case domain.QuotaPeriodMonth:
		return 35 * 24 * time.Hour
	default:
		return 48 * time.Hour
	}
}
