package application

import (
	"context"
	"hash/fnv"
	"log/slog"
	"math"
	"sort"
	"sync/atomic"
	"time"

	"aurora/ai-governance/internal/gateway/domain"
	govdomain "aurora/ai-governance/internal/governance/domain"
	"aurora/ai-governance/internal/shared"
)

type slotScore struct {
	priority    int
	utilization float64
}

type ProviderRoutingServiceImpl struct {
	slotRepo          ProviderSlotRepository
	poolPolicyRepo    ServiceProviderPoolPolicyRepository
	capacityService   *CapacityReservationService
	roundRobinCounter atomic.Int64
	logger            *slog.Logger
}

func NewProviderRoutingService(
	slotRepo ProviderSlotRepository,
	poolPolicyRepo ServiceProviderPoolPolicyRepository,
	capacityService *CapacityReservationService,
	logger *slog.Logger,
) *ProviderRoutingServiceImpl {
	if logger == nil {
		logger = slog.Default()
	}
	return &ProviderRoutingServiceImpl{
		slotRepo:        slotRepo,
		poolPolicyRepo:  poolPolicyRepo,
		capacityService: capacityService,
		logger:          logger,
	}
}

func (s *ProviderRoutingServiceImpl) GetCandidates(
	ctx context.Context,
	allowedProviders map[govdomain.AiProvider]struct{},
	allowedProviderPools map[string]struct{},
	operation shared.AiOperation,
	callerServiceID string,
	tokenBudget shared.TokenBudget,
) ([]*domain.ProviderSlot, error) {
	// 1. Resolve effective pools for callerServiceId
	effectivePools := s.resolveEffectivePools(ctx, callerServiceID, allowedProviderPools)
	if len(effectivePools) == 0 || len(allowedProviders) == 0 {
		s.logger.Warn("No candidate pools/providers found", slog.String("serviceId", callerServiceID))
		return nil, nil
	}

	poolList := make([]string, 0, len(effectivePools))
	for p := range effectivePools {
		poolList = append(poolList, p)
	}
	providerList := make([]govdomain.AiProvider, 0, len(allowedProviders))
	for p := range allowedProviders {
		providerList = append(providerList, p)
	}

	// 2. Query active slots
	slots, err := s.slotRepo.FindActiveCandidateSlots(ctx, poolList, providerList, operation)
	if err != nil {
		return nil, err
	}
	if len(slots) == 0 {
		s.logger.Warn("No active slots in DB",
			slog.Any("pools", poolList),
			slog.Any("providers", providerList),
			slog.String("operation", string(operation)))
		return nil, nil
	}

	// 3. Filter out slots in cooldown
	now := time.Now().UTC()
	availableSlots := make([]*domain.ProviderSlot, 0, len(slots))
	for _, slot := range slots {
		if !slot.IsInCooldown(now) {
			availableSlots = append(availableSlots, slot)
		}
	}
	if len(availableSlots) == 0 {
		s.logger.Warn("All candidate slots in cooldown", slog.Any("pools", poolList), slog.Any("providers", providerList))
		return nil, nil
	}

	// 4. Score & sort candidates
	requiredTokens := tokenBudget.ReservationTokens()
	scores := make(map[*domain.ProviderSlot]slotScore, len(availableSlots))
	filtered := make([]*domain.ProviderSlot, 0, len(availableSlots))

	for _, slot := range availableSlots {
		effectiveLimits := s.capacityService.GetEffectiveLimits(slot)
		currentUsage := s.capacityService.GetSlotCapacity(ctx, slot)

		if currentUsage.CurrentRpm >= effectiveLimits.RpmLimit ||
			currentUsage.CurrentRpd >= effectiveLimits.RpdLimit ||
			(currentUsage.CurrentTpm+requiredTokens) > effectiveLimits.TpmLimit {
			s.logger.Debug("Slot skipped in pre-check",
				slog.String("slot", slot.SlotAlias),
				slog.Int64("rpm", currentUsage.CurrentRpm), slog.Int64("rpmLimit", effectiveLimits.RpmLimit),
				slog.Int64("tpm", currentUsage.CurrentTpm+requiredTokens), slog.Int64("tpmLimit", effectiveLimits.TpmLimit),
				slog.Int64("rpd", currentUsage.CurrentRpd), slog.Int64("rpdLimit", effectiveLimits.RpdLimit))
			continue
		}

		rpmUtil := float64(currentUsage.CurrentRpm) / float64(maxInt64(1, effectiveLimits.RpmLimit))
		tpmUtil := float64(currentUsage.CurrentTpm) / float64(maxInt64(1, effectiveLimits.TpmLimit))
		rpdUtil := float64(currentUsage.CurrentRpd) / float64(maxInt64(1, effectiveLimits.RpdLimit))
		maxUtil := math.Max(rpmUtil, math.Max(tpmUtil, rpdUtil))

		scores[slot] = slotScore{priority: slot.Priority, utilization: maxUtil}
		filtered = append(filtered, slot)
	}

	candidatePool := filtered
	if len(candidatePool) == 0 {
		// Fallback to all available slots; Redis Lua script is authoritative
		candidatePool = make([]*domain.ProviderSlot, len(availableSlots))
		copy(candidatePool, availableSlots)
	}

	rrOffset := uint32(s.roundRobinCounter.Add(1) - 1)

	sort.SliceStable(candidatePool, func(i, j int) bool {
		s1, s2 := candidatePool[i], candidatePool[j]
		sc1, ok1 := scores[s1]
		if !ok1 {
			sc1 = slotScore{priority: s1.Priority, utilization: 1.0}
		}
		sc2, ok2 := scores[s2]
		if !ok2 {
			sc2 = slotScore{priority: s2.Priority, utilization: 1.0}
		}

		if sc1.priority != sc2.priority {
			return sc1.priority < sc2.priority
		}
		if sc1.utilization != sc2.utilization {
			return sc1.utilization < sc2.utilization
		}
		h1 := (aliasHash(s1.SlotAlias) + rrOffset) & 0x7FFFFFFF
		h2 := (aliasHash(s2.SlotAlias) + rrOffset) & 0x7FFFFFFF
		return h1 < h2
	})

	return candidatePool, nil
}

func (s *ProviderRoutingServiceImpl) resolveEffectivePools(
	ctx context.Context,
	callerServiceID string,
	allowedProviderPools map[string]struct{},
) map[string]struct{} {
	if s.poolPolicyRepo != nil {
		policies, err := s.poolPolicyRepo.FindByServiceIDOrderByPriorityAsc(ctx, callerServiceID)
		if err == nil && len(policies) > 0 {
			pools := make(map[string]struct{}, len(policies))
			for _, p := range policies {
				if p.Pool != nil && p.Pool.Code != "" {
					pools[p.Pool.Code] = struct{}{}
				}
			}
			if len(pools) > 0 {
				return pools
			}
		}
	}
	return allowedProviderPools
}

func aliasHash(s string) uint32 {
	h := fnv.New32a()
	_, _ = h.Write([]byte(s))
	return h.Sum32()
}

func maxInt64(a, b int64) int64 {
	if a > b {
		return a
	}
	return b
}
