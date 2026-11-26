package application

import (
	"context"
	"time"

	"aurora/ai-governance/internal/gateway/domain"
	govdomain "aurora/ai-governance/internal/governance/domain"
	"aurora/ai-governance/internal/shared"
)

type AiProviderClient interface {
	Generate(ctx context.Context, slot *domain.ProviderSlot, request domain.AiGenerateRequest) (domain.AiGenerateResult, error)
	Embed(ctx context.Context, slot *domain.ProviderSlot, request domain.AiEmbeddingRequest) (domain.AiEmbeddingResult, error)
}

type ProviderCapacityPort interface {
	TryReserve(ctx context.Context, slot *domain.ProviderSlot, effectiveLimits domain.ProviderCapacityLimits, requestedTokens int64) (*domain.ProviderReservation, error)
	Reconcile(ctx context.Context, reservation *domain.ProviderReservation, actualTokens int64)
	Release(ctx context.Context, reservation *domain.ProviderReservation)
	GetSlotCapacity(ctx context.Context, slot *domain.ProviderSlot) domain.SlotCapacity
}

type ProviderRateWindowPolicy interface {
	RpmWindow(slot *domain.ProviderSlot, now time.Time) domain.RateWindow
	TpmWindow(slot *domain.ProviderSlot, now time.Time) domain.RateWindow
	RpdWindow(slot *domain.ProviderSlot, now time.Time) domain.RateWindow
}

type ProviderSlotRepository interface {
	FindActiveCandidateSlots(ctx context.Context, poolCodes []string, providers []govdomain.AiProvider, operation shared.AiOperation) ([]*domain.ProviderSlot, error)
	SaveCooldown(ctx context.Context, slot *domain.ProviderSlot) error
}

type ServiceProviderPoolPolicyRepository interface {
	FindByServiceIDOrderByPriorityAsc(ctx context.Context, serviceID string) ([]*domain.ServiceProviderPoolPolicy, error)
}

type ProviderRoutingService interface {
	GetCandidates(ctx context.Context, allowedProviders map[govdomain.AiProvider]struct{}, allowedProviderPools map[string]struct{}, operation shared.AiOperation, callerServiceID string, tokenBudget shared.TokenBudget) ([]*domain.ProviderSlot, error)
}
