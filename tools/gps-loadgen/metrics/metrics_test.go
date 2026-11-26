package metrics

import (
	"math"
	"testing"
)

func TestMetricsPercentiles(t *testing.T) {
	m := NewSimulationMetrics()
	m.Start(10)

	for i := 1; i <= 100; i++ {
		m.RecordSuccess(float64(i))
	}

	p50, p95, p99 := m.GetPercentiles()
	if math.Abs(p50-50.0) > 0.1 {
		t.Errorf("expected p50 50, got %f", p50)
	}
	if math.Abs(p95-95.0) > 0.1 {
		t.Errorf("expected p95 95, got %f", p95)
	}
	if math.Abs(p99-99.0) > 0.1 {
		t.Errorf("expected p99 99, got %f", p99)
	}
}

func TestMetricsFailureRecording(t *testing.T) {
	m := NewSimulationMetrics()
	m.Start(2)
	m.RecordSuccess(10.0)
	m.RecordFailure(false)
	m.RecordFailure(true)
	m.RecordRetry()
	m.Stop()

	// Calling PrintSummary to ensure no panics
	m.PrintSummary()
}

