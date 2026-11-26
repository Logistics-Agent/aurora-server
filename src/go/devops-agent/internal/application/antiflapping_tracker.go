package application

import (
	"context"
	"crypto/sha256"
	"encoding/hex"
	"log/slog"
	"strconv"
	"strings"
	"time"

	"github.com/google/uuid"
	"github.com/redis/go-redis/v9"
)

const (
	flapKeyPrefix     = "devops:flap:"
	tenMinutesMs      = int64(10 * 60 * 1000)
	flappingThreshold = int64(5)
)

const antiFlapLuaScript = `
local key = KEYS[1]
local member = ARGV[1]
local score = tonumber(ARGV[2])
local windowStart = tonumber(ARGV[3])
local ttl = tonumber(ARGV[4])

redis.call('ZADD', key, score, member)
redis.call('ZREMRANGEBYSCORE', key, 0, windowStart)
local count = redis.call('ZCARD', key)
redis.call('EXPIRE', key, ttl)
return count
`

type AntiFlappingTracker struct {
	redisClient *redis.Client
	script      *redis.Script
	logger      *slog.Logger
}

func NewAntiFlappingTracker(redisClient *redis.Client, logger *slog.Logger) *AntiFlappingTracker {
	if logger == nil {
		logger = slog.Default()
	}
	return &AntiFlappingTracker{
		redisClient: redisClient,
		script:      redis.NewScript(antiFlapLuaScript),
		logger:      logger,
	}
}

func (t *AntiFlappingTracker) RecordEventAndCheckFlapping(ctx context.Context, errorSignature string) (bool, error) {
	if t.redisClient == nil {
		return false, nil
	}
	normalized := strings.ToLower(strings.TrimSpace(errorSignature))
	sum := sha256.Sum256([]byte(normalized))
	redisKey := flapKeyPrefix + hex.EncodeToString(sum[:])

	nowMs := time.Now().UTC().UnixMilli()
	windowStartMs := nowMs - tenMinutesMs
	eventID := uuid.NewString()

	count, err := t.script.Run(ctx, t.redisClient, []string{redisKey},
		eventID,
		strconv.FormatInt(nowMs, 10),
		strconv.FormatInt(windowStartMs, 10),
		"600",
	).Int64()
	if err != nil {
		return false, err
	}

	isFlapping := count >= flappingThreshold
	if isFlapping {
		t.logger.Warn("Flapping detected; escalating severity to Medium",
			slog.String("signature", errorSignature), slog.Int64("count", count))
	}
	return isFlapping, nil
}
