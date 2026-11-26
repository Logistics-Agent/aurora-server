package application

import (
	"math"

	"aurora/ai-governance/internal/gateway/domain"
)

type ProviderCapacityLimitPolicy struct {
	headroomRatio float64
}

func NewProviderCapacityLimitPolicy(headroomRatio float64) *ProviderCapacityLimitPolicy {
	clamped := math.Max(0.0, math.Min(0.9, headroomRatio))
	return &ProviderCapacityLimitPolicy{headroomRatio: clamped}
}

func (p *ProviderCapacityLimitPolicy) EffectiveLimits(slot *domain.ProviderSlot) domain.ProviderCapacityLimits {
	factor := 1.0 - p.headroomRatio
	effectiveRpm := int64(math.Floor(float64(slot.RpmLimit) * factor))
	effectiveTpm := int64(math.Floor(float64(slot.TpmLimit) * factor))
	effectiveRpd := int64(math.Floor(float64(slot.RpdLimit) * factor))

	if slot.RpmLimit > 0 && effectiveRpm < 1 {
		effectiveRpm = 1
	}
	if slot.TpmLimit > 0 && effectiveTpm < 1 {
		effectiveTpm = 1
	}
	if slot.RpdLimit > 0 && effectiveRpd < 1 {
		effectiveRpd = 1
	}

	return domain.ProviderCapacityLimits{
		RpmLimit: effectiveRpm,
		TpmLimit: effectiveTpm,
		RpdLimit: effectiveRpd,
	}
}

func (p *ProviderCapacityLimitPolicy) HeadroomRatio() float64 {
	return p.headroomRatio
}
