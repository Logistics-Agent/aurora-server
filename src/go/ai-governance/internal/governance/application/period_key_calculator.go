package application

import (
	"time"

	"aurora/ai-governance/internal/governance/domain"
)

type PeriodKeyCalculator struct{}

func NewPeriodKeyCalculator() *PeriodKeyCalculator {
	return &PeriodKeyCalculator{}
}

func (c *PeriodKeyCalculator) Calculate(period domain.QuotaPeriod, now time.Time) domain.PeriodKey {
	utc := now.UTC()
	switch period {
	case domain.QuotaPeriodMinute:
		return domain.PeriodKey{Value: utc.Format("2006-01-02T15:04")}
	case domain.QuotaPeriodDay:
		return domain.PeriodKey{Value: utc.Format("2006-01-02")}
	case domain.QuotaPeriodMonth:
		return domain.PeriodKey{Value: utc.Format("2006-01")}
	default:
		return domain.PeriodKey{Value: utc.Format("2006-01-02")}
	}
}

func (c *PeriodKeyCalculator) CalculateNow(period domain.QuotaPeriod) domain.PeriodKey {
	return c.Calculate(period, time.Now().UTC())
}
