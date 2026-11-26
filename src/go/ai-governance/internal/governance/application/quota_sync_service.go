package application

import (
	"context"
	"log/slog"
	"time"

	"aurora/ai-governance/internal/governance/domain"

	"github.com/google/uuid"
)

type QuotaSyncService struct {
	quotaStore          QuotaUsageStore
	usageRecordRepo     UsageRecordRepository
	periodKeyCalculator *PeriodKeyCalculator
	logger              *slog.Logger
}

func NewQuotaSyncService(
	quotaStore QuotaUsageStore,
	usageRecordRepo UsageRecordRepository,
	periodKeyCalculator *PeriodKeyCalculator,
	logger *slog.Logger,
) *QuotaSyncService {
	if logger == nil {
		logger = slog.Default()
	}
	return &QuotaSyncService{
		quotaStore:          quotaStore,
		usageRecordRepo:     usageRecordRepo,
		periodKeyCalculator: periodKeyCalculator,
		logger:              logger,
	}
}

func (s *QuotaSyncService) RecordUsage(ctx context.Context, tenantID uuid.UUID, requestCount, totalTokens int64, ts time.Time) error {
	if requestCount > 0 {
		if err := s.recordDimension(ctx, tenantID, domain.QuotaMetricRequests, domain.QuotaPeriodMinute, requestCount, ts); err != nil {
			return err
		}
		if err := s.recordDimension(ctx, tenantID, domain.QuotaMetricRequests, domain.QuotaPeriodDay, requestCount, ts); err != nil {
			return err
		}
	}
	if totalTokens > 0 {
		if err := s.recordDimension(ctx, tenantID, domain.QuotaMetricTokens, domain.QuotaPeriodMinute, totalTokens, ts); err != nil {
			return err
		}
		if err := s.recordDimension(ctx, tenantID, domain.QuotaMetricTokens, domain.QuotaPeriodMonth, totalTokens, ts); err != nil {
			return err
		}
	}
	return nil
}

func (s *QuotaSyncService) recordDimension(ctx context.Context, tenantID uuid.UUID, metric domain.QuotaMetric, period domain.QuotaPeriod, amount int64, ts time.Time) error {
	periodKey := s.periodKeyCalculator.Calculate(period, ts)

	if s.quotaStore != nil {
		s.quotaStore.IncrementUsage(ctx, tenantID, metric, period, periodKey, amount)
	}

	if err := s.usageRecordRepo.UpsertIncrement(ctx, tenantID, metric, period, periodKey.Value, amount); err != nil {
		return err
	}

	s.logger.Debug("Recorded usage for tenant",
		slog.String("tenantId", tenantID.String()),
		slog.String("metric", string(metric)),
		slog.String("period", string(period)),
		slog.String("key", periodKey.Value),
		slog.Int64("amount", amount))
	return nil
}
