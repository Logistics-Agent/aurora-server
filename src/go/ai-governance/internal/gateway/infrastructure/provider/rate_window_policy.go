package provider

import (
	"time"

	"aurora/ai-governance/internal/gateway/domain"
)

type UtcRateWindowPolicy struct{}

func NewUtcRateWindowPolicy() *UtcRateWindowPolicy {
	return &UtcRateWindowPolicy{}
}

func (p *UtcRateWindowPolicy) RpmWindow(_ *domain.ProviderSlot, now time.Time) domain.RateWindow {
	return domain.RateWindow{
		BucketKey:  "rpm:" + now.UTC().Format("200601021504"),
		TTLSeconds: 70,
	}
}

func (p *UtcRateWindowPolicy) TpmWindow(_ *domain.ProviderSlot, now time.Time) domain.RateWindow {
	return domain.RateWindow{
		BucketKey:  "tpm:" + now.UTC().Format("200601021504"),
		TTLSeconds: 70,
	}
}

func (p *UtcRateWindowPolicy) RpdWindow(_ *domain.ProviderSlot, now time.Time) domain.RateWindow {
	return domain.RateWindow{
		BucketKey:  "rpd:" + now.UTC().Format("20060102"),
		TTLSeconds: 90000,
	}
}
