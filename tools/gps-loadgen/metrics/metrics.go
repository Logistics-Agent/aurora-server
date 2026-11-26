package metrics

import (
	"fmt"
	"math"
	"sort"
	"sync"
	"sync/atomic"
	"time"
)

type SimulationMetrics struct {
	startTime        time.Time
	stopTime         time.Time
	totalVehicles    int32
	totalEvents      int64
	successfulEvents int64
	failedEvents     int64
	timeoutCount     int64
	retryCount       int64

	latencies []float64
	latMu     sync.Mutex
}

func NewSimulationMetrics() *SimulationMetrics {
	return &SimulationMetrics{
		latencies: make([]float64, 0, 100000),
	}
}

func (m *SimulationMetrics) Start(totalVehicles int) {
	atomic.StoreInt32(&m.totalVehicles, int32(totalVehicles))
	m.startTime = time.Now()
}

func (m *SimulationMetrics) Stop() {
	m.stopTime = time.Now()
}

func (m *SimulationMetrics) RecordSuccess(latencyMs float64) {
	atomic.AddInt64(&m.totalEvents, 1)
	atomic.AddInt64(&m.successfulEvents, 1)

	m.latMu.Lock()
	if len(m.latencies) < 500000 {
		m.latencies = append(m.latencies, latencyMs)
	}
	m.latMu.Unlock()
}

func (m *SimulationMetrics) RecordFailure(isTimeout bool) {
	atomic.AddInt64(&m.totalEvents, 1)
	atomic.AddInt64(&m.failedEvents, 1)
	if isTimeout {
		atomic.AddInt64(&m.timeoutCount, 1)
	}
}

func (m *SimulationMetrics) RecordRetry() {
	atomic.AddInt64(&m.retryCount, 1)
}

func (m *SimulationMetrics) GetPercentiles() (p50, p95, p99 float64) {
	m.latMu.Lock()
	if len(m.latencies) == 0 {
		m.latMu.Unlock()
		return 0, 0, 0
	}
	sorted := make([]float64, len(m.latencies))
	copy(sorted, m.latencies)
	m.latMu.Unlock()

	sort.Float64s(sorted)
	p50 = percentile(sorted, 0.50)
	p95 = percentile(sorted, 0.95)
	p99 = percentile(sorted, 0.99)
	return p50, p95, p99
}

func percentile(sorted []float64, pct float64) float64 {
	if len(sorted) == 0 {
		return 0
	}
	idx := int(math.Ceil(pct*float64(len(sorted)))) - 1
	if idx < 0 {
		idx = 0
	}
	if idx >= len(sorted) {
		idx = len(sorted) - 1
	}
	return sorted[idx]
}

func (m *SimulationMetrics) PrintProgress(activeVehicles int) {
	sent := atomic.LoadInt64(&m.totalEvents)
	succ := atomic.LoadInt64(&m.successfulEvents)
	fail := atomic.LoadInt64(&m.failedEvents)
	totalVehicles := atomic.LoadInt32(&m.totalVehicles)

	elapsedSec := time.Since(m.startTime).Seconds()
	if elapsedSec < 0.1 {
		elapsedSec = 0.1
	}
	rate := float64(sent) / elapsedSec

	fmt.Printf("[Progress %.0fs] Active: %d/%d | Sent: %d | Succ: %d | Fail: %d | Rate: %.1f evt/s\n",
		elapsedSec, activeVehicles, totalVehicles, sent, succ, fail, rate)
}

func (m *SimulationMetrics) PrintSummary() {
	endTime := m.stopTime
	if endTime.IsZero() {
		endTime = time.Now()
	}
	elapsedSec := endTime.Sub(m.startTime).Seconds()
	if elapsedSec < 0.001 {
		elapsedSec = 0.001
	}

	total := atomic.LoadInt64(&m.totalEvents)
	succ := atomic.LoadInt64(&m.successfulEvents)
	fail := atomic.LoadInt64(&m.failedEvents)
	timeouts := atomic.LoadInt64(&m.timeoutCount)
	retries := atomic.LoadInt64(&m.retryCount)
	totalVehicles := atomic.LoadInt32(&m.totalVehicles)

	successRate := 0.0
	if total > 0 {
		successRate = float64(succ) / float64(total) * 100.0
	}
	throughput := float64(succ) / elapsedSec
	p50, p95, p99 := m.GetPercentiles()

	fmt.Println()
	fmt.Println("======================= SIMULATION METRICS REPORT =======================")
	fmt.Printf("Total Vehicles        : %d\n", totalVehicles)
	fmt.Printf("Duration              : %.2fs\n", elapsedSec)
	fmt.Printf("Total Events Sent     : %d\n", total)
	fmt.Printf("Successful Events     : %d\n", succ)
	fmt.Printf("Failed Events         : %d\n", fail)
	fmt.Printf("RPC Timeout Count     : %d\n", timeouts)
	fmt.Printf("Retry Count           : %d\n", retries)
	fmt.Printf("Success Rate          : %.2f%%\n", successRate)
	fmt.Printf("Throughput            : %.2f events/sec\n", throughput)
	fmt.Printf("Client Latency p50    : %.2f ms\n", p50)
	fmt.Printf("Client Latency p95    : %.2f ms\n", p95)
	fmt.Printf("Client Latency p99    : %.2f ms\n", p99)
	fmt.Println("=========================================================================")
	fmt.Println()
}

