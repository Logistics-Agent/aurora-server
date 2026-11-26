package application

import (
	"context"

	"aurora/ai-governance/internal/gateway/domain"
)

type CapacityReservationService struct {
	capacityPort       ProviderCapacityPort
	capacityLimitPolicy *ProviderCapacityLimitPolicy
}

func NewCapacityReservationService(
	capacityPort ProviderCapacityPort,
	capacityLimitPolicy *ProviderCapacityLimitPolicy,
) *CapacityReservationService {
	return &CapacityReservationService{
		capacityPort:        capacityPort,
		capacityLimitPolicy: capacityLimitPolicy,
	}
}

func (s *CapacityReservationService) TryReserve(ctx context.Context, slot *domain.ProviderSlot, requestedTokens int64) (*domain.ProviderReservation, error) {
	effectiveLimits := s.capacityLimitPolicy.EffectiveLimits(slot)
	return s.capacityPort.TryReserve(ctx, slot, effectiveLimits, requestedTokens)
}

func (s *CapacityReservationService) Reconcile(ctx context.Context, reservation *domain.ProviderReservation, actualTokens int64) {
	s.capacityPort.Reconcile(ctx, reservation, actualTokens)
}

func (s *CapacityReservationService) Release(ctx context.Context, reservation *domain.ProviderReservation) {
	s.capacityPort.Release(ctx, reservation)
}

func (s *CapacityReservationService) GetSlotCapacity(ctx context.Context, slot *domain.ProviderSlot) domain.SlotCapacity {
	return s.capacityPort.GetSlotCapacity(ctx, slot)
}

func (s *CapacityReservationService) GetEffectiveLimits(slot *domain.ProviderSlot) domain.ProviderCapacityLimits {
	return s.capacityLimitPolicy.EffectiveLimits(slot)
}
