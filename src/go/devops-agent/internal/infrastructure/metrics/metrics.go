package metrics

import (
	"time"

	"github.com/prometheus/client_golang/prometheus"
	"github.com/prometheus/client_golang/prometheus/promauto"
)

type DevOpsMetrics struct {
	AlertsIngested        prometheus.Counter
	AlertsDeduplicated    prometheus.Counter
	AntiflappingTriggered prometheus.Counter
	RcaRequests           prometheus.Counter
	RcaFailures           prometheus.Counter
	RagFallback           prometheus.Counter
	ActionsExecuted       prometheus.Counter
	ActionsBlocked        prometheus.Counter
	ApprovalTimeouts      prometheus.Counter
	OutboxPublishFailures prometheus.Counter
	RcaDuration           prometheus.Histogram
}

func NewDevOpsMetrics(reg prometheus.Registerer) *DevOpsMetrics {
	if reg == nil {
		reg = prometheus.DefaultRegisterer
	}
	factory := promauto.With(reg)
	return &DevOpsMetrics{
		AlertsIngested: factory.NewCounter(prometheus.CounterOpts{
			Name: "devops_alerts_ingested_total",
			Help: "Total number of ingested alerts",
		}),
		AlertsDeduplicated: factory.NewCounter(prometheus.CounterOpts{
			Name: "devops_alerts_deduplicated_total",
			Help: "Total number of alerts deduplicated",
		}),
		AntiflappingTriggered: factory.NewCounter(prometheus.CounterOpts{
			Name: "devops_antiflapping_triggered_total",
			Help: "Total number of anti-flapping escalations triggered",
		}),
		RcaRequests: factory.NewCounter(prometheus.CounterOpts{
			Name: "devops_rca_requests_total",
			Help: "Total number of RCA requests initiated",
		}),
		RcaFailures: factory.NewCounter(prometheus.CounterOpts{
			Name: "devops_rca_failures_total",
			Help: "Total number of RCA analysis failures",
		}),
		RagFallback: factory.NewCounter(prometheus.CounterOpts{
			Name: "devops_rag_fallback_total",
			Help: "Total number of DevOps-RAG fallbacks due to unavailability",
		}),
		ActionsExecuted: factory.NewCounter(prometheus.CounterOpts{
			Name: "devops_actions_executed_total",
			Help: "Total number of remediation actions executed",
		}),
		ActionsBlocked: factory.NewCounter(prometheus.CounterOpts{
			Name: "devops_actions_blocked_total",
			Help: "Total number of remediation actions blocked by policy or validation",
		}),
		ApprovalTimeouts: factory.NewCounter(prometheus.CounterOpts{
			Name: "devops_approval_timeouts_total",
			Help: "Total number of approval timeouts expired",
		}),
		OutboxPublishFailures: factory.NewCounter(prometheus.CounterOpts{
			Name: "devops_outbox_publish_failures_total",
			Help: "Total number of outbox message publish failures",
		}),
		RcaDuration: factory.NewHistogram(prometheus.HistogramOpts{
			Name:    "devops_rca_duration_seconds",
			Help:    "End-to-end duration of successful RCA analyses",
			Buckets: prometheus.DefBuckets,
		}),
	}
}

func (m *DevOpsMetrics) IncrementAlertsIngested()        { if m != nil { m.AlertsIngested.Inc() } }
func (m *DevOpsMetrics) IncrementAlertsDeduplicated()    { if m != nil { m.AlertsDeduplicated.Inc() } }
func (m *DevOpsMetrics) IncrementAntiflappingTriggered() { if m != nil { m.AntiflappingTriggered.Inc() } }
func (m *DevOpsMetrics) IncrementRcaRequests()           { if m != nil { m.RcaRequests.Inc() } }
func (m *DevOpsMetrics) IncrementRcaFailures()           { if m != nil { m.RcaFailures.Inc() } }
func (m *DevOpsMetrics) IncrementRagFallback()           { if m != nil { m.RagFallback.Inc() } }
func (m *DevOpsMetrics) IncrementActionsExecuted()       { if m != nil { m.ActionsExecuted.Inc() } }
func (m *DevOpsMetrics) IncrementActionsBlocked()        { if m != nil { m.ActionsBlocked.Inc() } }
func (m *DevOpsMetrics) IncrementApprovalTimeouts()      { if m != nil { m.ApprovalTimeouts.Inc() } }
func (m *DevOpsMetrics) IncrementOutboxPublishFailures() { if m != nil { m.OutboxPublishFailures.Inc() } }
func (m *DevOpsMetrics) RecordRcaDuration(d time.Duration) {
	if m != nil {
		m.RcaDuration.Observe(d.Seconds())
	}
}
