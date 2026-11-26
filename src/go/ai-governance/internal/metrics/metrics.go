package metrics

import (
	"github.com/prometheus/client_golang/prometheus"
	"github.com/prometheus/client_golang/prometheus/promauto"
)

type Metrics struct {
	RequestDuration     *prometheus.HistogramVec
	RequestsFailedTotal *prometheus.CounterVec
	PolicyDeniedTotal   *prometheus.CounterVec
	PolicyAllowedTotal  *prometheus.CounterVec
	GatewayRequestsTotal *prometheus.CounterVec
}

func NewMetrics(reg prometheus.Registerer) *Metrics {
	if reg == nil {
		reg = prometheus.DefaultRegisterer
	}
	factory := promauto.With(reg)
	return &Metrics{
		RequestDuration: factory.NewHistogramVec(prometheus.HistogramOpts{
			Name:    "ai_governance_request_duration_seconds",
			Help:    "Duration of AI governance orchestrated requests",
			Buckets: prometheus.DefBuckets,
		}, []string{"operation", "outcome"}),
		RequestsFailedTotal: factory.NewCounterVec(prometheus.CounterOpts{
			Name: "ai_governance_requests_failed_total",
			Help: "Total number of failed AI governance requests",
		}, []string{"operation"}),
		PolicyDeniedTotal: factory.NewCounterVec(prometheus.CounterOpts{
			Name: "governance_policy_denied_total",
			Help: "Total number of governance policy denials",
		}, []string{"capability", "reason"}),
		PolicyAllowedTotal: factory.NewCounterVec(prometheus.CounterOpts{
			Name: "governance_policy_allowed_total",
			Help: "Total number of governance policy approvals",
		}, []string{"capability", "callerServiceId"}),
		GatewayRequestsTotal: factory.NewCounterVec(prometheus.CounterOpts{
			Name: "ai_gateway_requests_total",
			Help: "Total number of AI gateway provider executions",
		}, []string{"operation", "provider", "callerServiceId", "result"}),
	}
}
