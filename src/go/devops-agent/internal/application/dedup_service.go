package application

import (
	"context"
	"crypto/sha256"
	"encoding/hex"
	"log/slog"
	"strings"

	"github.com/redis/go-redis/v9"
)

const (
	dedupKeyPrefix  = "devops:dedup:"
	dedupTTLSeconds = "300"
)

const dedupLuaScript = `
local key = KEYS[1]
local correlationId = ARGV[1]
local ttl = tonumber(ARGV[2])
local existing = redis.call('GET', key)
if existing then
    return existing
else
    redis.call('SET', key, correlationId, 'EX', ttl)
    return false
end
`

type DedupService struct {
	redisClient *redis.Client
	script      *redis.Script
	logger      *slog.Logger
}

func NewDedupService(redisClient *redis.Client, logger *slog.Logger) *DedupService {
	if logger == nil {
		logger = slog.Default()
	}
	return &DedupService{
		redisClient: redisClient,
		script:      redis.NewScript(dedupLuaScript),
		logger:      logger,
	}
}

func (s *DedupService) ComputeDedupKey(_ string, errorSignature string, _ int64) string {
	normalized := strings.ToLower(strings.TrimSpace(errorSignature))
	sum := sha256.Sum256([]byte(normalized))
	return hex.EncodeToString(sum[:])
}

func (s *DedupService) CheckAndStore(ctx context.Context, dedupKey, correlationID string) (string, bool, error) {
	if s.redisClient == nil {
		return "", false, nil
	}
	redisKey := dedupKeyPrefix + dedupKey
	res, err := s.script.Run(ctx, s.redisClient, []string{redisKey}, correlationID, dedupTTLSeconds).Result()
	if err == redis.Nil {
		s.logger.Debug("New Dedup Key stored atomically in Redis", slog.String("dedupKey", dedupKey), slog.String("correlationId", correlationID))
		return "", false, nil
	}
	if err != nil {
		return "", false, err
	}
	if existing, ok := res.(string); ok && existing != "" {
		s.logger.Info("Duplicate alert detected atomically", slog.String("dedupKey", dedupKey), slog.String("existingCorrelationId", existing))
		return existing, true, nil
	}
	return "", false, nil
}
