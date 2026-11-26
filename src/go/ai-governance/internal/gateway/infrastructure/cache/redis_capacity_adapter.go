package cache

import (
	"context"
	_ "embed"
	"fmt"
	"log/slog"
	"strconv"
	"strings"
	"time"

	"aurora/ai-governance/internal/gateway/application"
	"aurora/ai-governance/internal/gateway/domain"
	govdomain "aurora/ai-governance/internal/governance/domain"

	"github.com/google/uuid"
	"github.com/redis/go-redis/v9"
)

//go:embed reserve_capacity.lua
var reserveCapacityLua string

const releaseCapacityLua = `
if redis.call('EXISTS', KEYS[1]) == 1 then
    local val = redis.call('DECRBY', KEYS[1], tonumber(ARGV[1]))
    if val < 0 then
        redis.call('SET', KEYS[1], 0, 'KEEPTTL')
    end
end
return 1
`

const reconcileCapacityLua = `
if redis.call('EXISTS', KEYS[1]) == 1 then
    local val = redis.call('INCRBY', KEYS[1], tonumber(ARGV[1]))
    if val < 0 then
        redis.call('SET', KEYS[1], 0, 'KEEPTTL')
    end
end
return 1
`

type RedisCapacityAdapter struct {
	client             *redis.Client
	rateWindowPolicies map[govdomain.AiProvider]application.ProviderRateWindowPolicy
	reserveScript      *redis.Script
	releaseScript      *redis.Script
	reconcileScript    *redis.Script
	logger             *slog.Logger
}

func NewRedisCapacityAdapter(
	client *redis.Client,
	rateWindowPolicies map[govdomain.AiProvider]application.ProviderRateWindowPolicy,
	logger *slog.Logger,
) *RedisCapacityAdapter {
	if logger == nil {
		logger = slog.Default()
	}
	return &RedisCapacityAdapter{
		client:             client,
		rateWindowPolicies: rateWindowPolicies,
		reserveScript:      redis.NewScript(reserveCapacityLua),
		releaseScript:      redis.NewScript(releaseCapacityLua),
		reconcileScript:    redis.NewScript(reconcileCapacityLua),
		logger:             logger,
	}
}

func (a *RedisCapacityAdapter) TryReserve(
	ctx context.Context,
	slot *domain.ProviderSlot,
	effectiveLimits domain.ProviderCapacityLimits,
	requestedTokens int64,
) (*domain.ProviderReservation, error) {
	if a.client == nil {
		return nil, fmt.Errorf("redis client not initialized")
	}

	now := time.Now().UTC()
	policy := a.resolveWindowPolicy(slot.Provider)

	rpmWin := policy.RpmWindow(slot, now)
	tpmWin := policy.TpmWindow(slot, now)
	rpdWin := policy.RpdWindow(slot, now)

	rpmKey := buildCapacityKey(slot, "rpm", rpmWin.BucketKey)
	tpmKey := buildCapacityKey(slot, "tpm", tpmWin.BucketKey)
	rpdKey := buildCapacityKey(slot, "rpd", rpdWin.BucketKey)

	res, err := a.reserveScript.Run(ctx, a.client,
		[]string{rpmKey, tpmKey, rpdKey},
		effectiveLimits.RpmLimit,
		effectiveLimits.TpmLimit,
		effectiveLimits.RpdLimit,
		requestedTokens,
		rpmWin.TTLSeconds,
		tpmWin.TTLSeconds,
		rpdWin.TTLSeconds,
	).Int64()
	if err != nil {
		a.logger.Error("Redis error executing capacity reservation", slog.String("slot", slot.SlotAlias), slog.Any("error", err))
		return nil, err
	}

	if res == 1 {
		return &domain.ProviderReservation{
			ReservationID:  uuid.NewString(),
			Slot:           slot,
			ReservedTokens: requestedTokens,
			RpmKey:         rpmKey,
			TpmKey:         tpmKey,
			RpdKey:         rpdKey,
		}, nil
	}

	a.logger.Debug("Capacity reservation rejected by Lua script", slog.String("slot", slot.SlotAlias))
	return nil, nil
}

func (a *RedisCapacityAdapter) Reconcile(ctx context.Context, reservation *domain.ProviderReservation, actualTokens int64) {
	if a.client == nil || reservation == nil {
		return
	}
	delta := actualTokens - reservation.ReservedTokens
	if delta == 0 {
		return
	}
	if err := a.reconcileScript.Run(ctx, a.client, []string{reservation.TpmKey}, delta).Err(); err != nil {
		a.logger.Warn("Failed to reconcile TPM capacity", slog.String("slot", reservation.Slot.SlotAlias), slog.Any("error", err))
		return
	}
	a.logger.Debug("Reconciled TPM capacity", slog.String("slot", reservation.Slot.SlotAlias), slog.Int64("delta", delta))
}

func (a *RedisCapacityAdapter) Release(ctx context.Context, reservation *domain.ProviderReservation) {
	if a.client == nil || reservation == nil {
		return
	}
	if err := a.releaseScript.Run(ctx, a.client, []string{reservation.RpmKey}, 1).Err(); err != nil {
		a.logger.Warn("Failed to release RPM reservation", slog.String("reservationId", reservation.ReservationID), slog.Any("error", err))
	}
	if err := a.releaseScript.Run(ctx, a.client, []string{reservation.TpmKey}, reservation.ReservedTokens).Err(); err != nil {
		a.logger.Warn("Failed to release TPM reservation", slog.String("reservationId", reservation.ReservationID), slog.Any("error", err))
	}
	if err := a.releaseScript.Run(ctx, a.client, []string{reservation.RpdKey}, 1).Err(); err != nil {
		a.logger.Warn("Failed to release RPD reservation", slog.String("reservationId", reservation.ReservationID), slog.Any("error", err))
	}
	a.logger.Debug("Released reservation",
		slog.String("reservationId", reservation.ReservationID),
		slog.String("slot", reservation.Slot.SlotAlias))
}

func (a *RedisCapacityAdapter) GetSlotCapacity(ctx context.Context, slot *domain.ProviderSlot) domain.SlotCapacity {
	if a.client == nil {
		return domain.SlotCapacity{}
	}
	now := time.Now().UTC()
	policy := a.resolveWindowPolicy(slot.Provider)

	rpmKey := buildCapacityKey(slot, "rpm", policy.RpmWindow(slot, now).BucketKey)
	tpmKey := buildCapacityKey(slot, "tpm", policy.TpmWindow(slot, now).BucketKey)
	rpdKey := buildCapacityKey(slot, "rpd", policy.RpdWindow(slot, now).BucketKey)

	vals, err := a.client.MGet(ctx, rpmKey, tpmKey, rpdKey).Result()
	if err != nil {
		a.logger.Warn("Failed to get slot capacity", slog.String("slot", slot.SlotAlias), slog.Any("error", err))
		return domain.SlotCapacity{}
	}
	return domain.SlotCapacity{
		CurrentRpm: parseRedisInt64(vals, 0),
		CurrentTpm: parseRedisInt64(vals, 1),
		CurrentRpd: parseRedisInt64(vals, 2),
	}
}

func parseRedisInt64(vals []any, idx int) int64 {
	if idx >= len(vals) || vals[idx] == nil {
		return 0
	}
	switch v := vals[idx].(type) {
	case string:
		n, _ := strconv.ParseInt(v, 10, 64)
		return n
	case []byte:
		n, _ := strconv.ParseInt(string(v), 10, 64)
		return n
	default:
		return 0
	}
}

func buildCapacityKey(slot *domain.ProviderSlot, metric, bucket string) string {
	return fmt.Sprintf("ai:capacity:%s:%s:%s:%s",
		strings.ToLower(string(slot.Provider)),
		slot.SlotAlias,
		metric,
		bucket,
	)
}

func (a *RedisCapacityAdapter) resolveWindowPolicy(provider govdomain.AiProvider) application.ProviderRateWindowPolicy {
	if p, ok := a.rateWindowPolicies[provider]; ok && p != nil {
		return p
	}
	return defaultRateWindowPolicy{}
}

type defaultRateWindowPolicy struct{}

func (defaultRateWindowPolicy) RpmWindow(_ *domain.ProviderSlot, _ time.Time) domain.RateWindow {
	return domain.RateWindow{BucketKey: "rpm:default", TTLSeconds: 70}
}

func (defaultRateWindowPolicy) TpmWindow(_ *domain.ProviderSlot, _ time.Time) domain.RateWindow {
	return domain.RateWindow{BucketKey: "tpm:default", TTLSeconds: 70}
}

func (defaultRateWindowPolicy) RpdWindow(_ *domain.ProviderSlot, _ time.Time) domain.RateWindow {
	return domain.RateWindow{BucketKey: "rpd:default", TTLSeconds: 90000}
}
