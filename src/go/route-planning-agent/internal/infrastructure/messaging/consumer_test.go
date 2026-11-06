package messaging_test

import (
	"encoding/json"
	"testing"
	"time"

	"aurora/route-planning-agent/internal/application/dtos"
	"aurora/route-planning-agent/internal/infrastructure/messaging"

	"github.com/google/uuid"
)

type mockCacheInvalidator struct {
	invalidatedTenant string
	invalidatedRule   string
}

func (m *mockCacheInvalidator) InvalidateCache(tenantIDStr, ruleName string) {
	m.invalidatedTenant = tenantIDStr
	m.invalidatedRule = ruleName
}

func TestRuleConfigConsumerProcessMessage(t *testing.T) {
	invalidator := &mockCacheInvalidator{}
	consumer := messaging.NewRuleConfigConsumer(nil, invalidator, "route.events", nil)

	tenantID := uuid.New()
	event := dtos.TenantRuleConfigChangedEvent{
		TenantID:   tenantID,
		RuleName:   "HeavyWeightRule",
		OccurredAt: time.Now().UTC(),
	}

	body, err := json.Marshal(event)
	if err != nil {
		t.Fatalf("marshal failed: %v", err)
	}

	if err := consumer.ProcessMessage(body); err != nil {
		t.Fatalf("process message failed: %v", err)
	}

	if invalidator.invalidatedTenant != tenantID.String() {
		t.Errorf("expected tenant %s, got %s", tenantID.String(), invalidator.invalidatedTenant)
	}
	if invalidator.invalidatedRule != "HeavyWeightRule" {
		t.Errorf("expected rule HeavyWeightRule, got %s", invalidator.invalidatedRule)
	}
}

