package outbox_test

import (
	"testing"

	"aurora/route-planning-agent/internal/infrastructure/outbox"
)

func TestMapRoutingKey(t *testing.T) {
	tests := []struct {
		eventType string
		expected  string
	}{
		{"RouteCreatedEvent", "route.created"},
		{"RouteOptimizedEvent", "route.optimized"},
		{"RouteApprovalRequestedEvent", "route.approval.requested"},
		{"TenantRuleConfigChangedEvent", "tenant.rule.config.changed"},
		{"AiUsageEvent", "ai.usage"},
	}

	for _, tt := range tests {
		actual := outbox.MapRoutingKey(tt.eventType)
		if actual != tt.expected {
			t.Errorf("eventType %s: expected %s, got %s", tt.eventType, tt.expected, actual)
		}
	}
}

