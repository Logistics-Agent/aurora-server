package application

import (
	"context"
	"errors"
	"fmt"
	"log/slog"
	"strings"
	"time"

	"aurora/ai-governance/internal/gateway/domain"
	govdomain "aurora/ai-governance/internal/governance/domain"
	"aurora/ai-governance/internal/shared"
)

var (
	ErrProviderCapacityExhausted = errors.New("PROVIDER_CAPACITY_EXHAUSTED: No available provider candidates found")
	ErrProviderExecutionFailed   = errors.New("PROVIDER_EXECUTION_FAILED: All candidates failed to execute")
)

type AiExecutionService struct {
	routingService  ProviderRoutingService
	capacityService *CapacityReservationService
	providerClients map[govdomain.AiProvider]AiProviderClient
	slotRepo        ProviderSlotRepository
	logger          *slog.Logger
}

func NewAiExecutionService(
	routingService ProviderRoutingService,
	capacityService *CapacityReservationService,
	providerClients map[govdomain.AiProvider]AiProviderClient,
	slotRepo ProviderSlotRepository,
	logger *slog.Logger,
) *AiExecutionService {
	if logger == nil {
		logger = slog.Default()
	}
	return &AiExecutionService{
		routingService:  routingService,
		capacityService: capacityService,
		providerClients: providerClients,
		slotRepo:        slotRepo,
		logger:          logger,
	}
}

func (s *AiExecutionService) Generate(
	ctx context.Context,
	decision govdomain.GovernanceDecision,
	request domain.AiGenerateRequest,
	callerServiceID string,
	tokenBudget shared.TokenBudget,
) (domain.AiGenerateResult, error) {
	candidates, err := s.routingService.GetCandidates(
		ctx,
		decision.AllowedProviders,
		decision.AllowedProviderPools,
		shared.AiOperationGenerate,
		callerServiceID,
		tokenBudget,
	)
	if err != nil {
		return domain.AiGenerateResult{}, fmt.Errorf("provider routing failed: %w", err)
	}
	if len(candidates) == 0 {
		s.logger.Error("PROVIDER_CAPACITY_EXHAUSTED: No available candidates", slog.String("serviceId", callerServiceID))
		return domain.AiGenerateResult{}, ErrProviderCapacityExhausted
	}

	var lastErr error
	for _, candidate := range candidates {
		reservation, err := s.capacityService.TryReserve(ctx, candidate, tokenBudget.ReservationTokens())
		if err != nil || reservation == nil {
			s.logger.Info("AI_CAPACITY_RESERVATION_FAILED: Contention on slot, advancing to next candidate",
				slog.String("slot", candidate.SlotAlias))
			continue
		}

		client := s.providerClients[candidate.Provider]
		if client == nil {
			lastErr = fmt.Errorf("no provider client registered for %s", candidate.Provider)
			s.capacityService.Release(ctx, reservation)
			continue
		}

		result, err := client.Generate(ctx, candidate, request)
		if err == nil {
			actualTokens := result.InputTokens + result.OutputTokens
			s.capacityService.Reconcile(ctx, reservation, actualTokens)
			s.logger.Info("AI_EXECUTION_COMPLETED", slog.String("slot", candidate.SlotAlias), slog.Int64("actualTokens", actualTokens))
			return result, nil
		}

		lastErr = err
		s.handleCandidateFailure(ctx, candidate, reservation, err, "generate")
	}

	s.logger.Error("PROVIDER_EXECUTION_FAILED: All candidates exhausted", slog.String("serviceId", callerServiceID))
	if lastErr != nil {
		return domain.AiGenerateResult{}, fmt.Errorf("%w: %v", ErrProviderExecutionFailed, lastErr)
	}
	return domain.AiGenerateResult{}, ErrProviderExecutionFailed
}

func (s *AiExecutionService) Embed(
	ctx context.Context,
	decision govdomain.GovernanceDecision,
	request domain.AiEmbeddingRequest,
	callerServiceID string,
	tokenBudget shared.TokenBudget,
) (domain.AiEmbeddingResult, error) {
	candidates, err := s.routingService.GetCandidates(
		ctx,
		decision.AllowedProviders,
		decision.AllowedProviderPools,
		shared.AiOperationEmbed,
		callerServiceID,
		tokenBudget,
	)
	if err != nil {
		return domain.AiEmbeddingResult{}, fmt.Errorf("provider routing failed: %w", err)
	}
	if len(candidates) == 0 {
		s.logger.Error("PROVIDER_CAPACITY_EXHAUSTED: No embedding candidates", slog.String("serviceId", callerServiceID))
		return domain.AiEmbeddingResult{}, ErrProviderCapacityExhausted
	}

	var lastErr error
	for _, candidate := range candidates {
		reservation, err := s.capacityService.TryReserve(ctx, candidate, tokenBudget.ReservationTokens())
		if err != nil || reservation == nil {
			s.logger.Info("AI_CAPACITY_RESERVATION_FAILED: Contention on embedding slot, advancing",
				slog.String("slot", candidate.SlotAlias))
			continue
		}

		client := s.providerClients[candidate.Provider]
		if client == nil {
			lastErr = fmt.Errorf("no provider client registered for %s", candidate.Provider)
			s.capacityService.Release(ctx, reservation)
			continue
		}

		result, err := client.Embed(ctx, candidate, request)
		if err == nil {
			s.capacityService.Reconcile(ctx, reservation, result.InputTokens)
			s.logger.Info("AI_EMBEDDING_COMPLETED", slog.String("slot", candidate.SlotAlias), slog.Int64("inputTokens", result.InputTokens))
			return result, nil
		}

		lastErr = err
		s.handleCandidateFailure(ctx, candidate, reservation, err, "embed")
	}

	if lastErr != nil {
		return domain.AiEmbeddingResult{}, fmt.Errorf("%w: %v", ErrProviderExecutionFailed, lastErr)
	}
	return domain.AiEmbeddingResult{}, ErrProviderExecutionFailed
}

func (s *AiExecutionService) handleCandidateFailure(
	ctx context.Context,
	candidate *domain.ProviderSlot,
	reservation *domain.ProviderReservation,
	err error,
	op string,
) {
	s.logger.Warn("AI_PROVIDER_FAILED", slog.String("op", op), slog.String("slot", candidate.SlotAlias), slog.Any("error", err))

	if isDefiniteNonConsumingRejection(err) {
		s.logger.Info("AI_CAPACITY_RELEASED: definite non-consumption error", slog.String("slot", candidate.SlotAlias))
		s.capacityService.Release(ctx, reservation)
	} else {
		s.logger.Warn("AI_CAPACITY_UNCERTAIN: keeping reservation until TTL expiry", slog.String("slot", candidate.SlotAlias))
	}

	if strings.Contains(err.Error(), "429") {
		s.applyCooldown(ctx, candidate, 60*time.Second)
	}
	s.logger.Info("AI_PROVIDER_FAILOVER: Attempting next candidate slot...")
}

func isDefiniteNonConsumingRejection(err error) bool {
	if err == nil {
		return false
	}
	msg := strings.ToLower(err.Error())
	return strings.Contains(msg, "429") ||
		strings.Contains(msg, "too many requests") ||
		strings.Contains(msg, "rate limit") ||
		strings.Contains(msg, "400") ||
		strings.Contains(msg, "bad request") ||
		strings.Contains(msg, "401") ||
		strings.Contains(msg, "403") ||
		strings.Contains(msg, "unauthorized") ||
		strings.Contains(msg, "forbidden") ||
		strings.Contains(msg, "secretref cannot be null")
}

func (s *AiExecutionService) applyCooldown(ctx context.Context, slot *domain.ProviderSlot, d time.Duration) {
	if s.slotRepo == nil {
		return
	}
	until := time.Now().UTC().Add(d)
	slot.CooldownUntil = &until
	if err := s.slotRepo.SaveCooldown(ctx, slot); err != nil {
		s.logger.Warn("Failed to persist cooldown for slot", slog.String("slot", slot.SlotAlias), slog.Any("error", err))
		return
	}
	s.logger.Info("Applied cooldown to slot", slog.Duration("duration", d), slog.String("slot", slot.SlotAlias))
}
