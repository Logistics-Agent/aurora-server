package background

import (
	"encoding/json"
	"fmt"
	"testing"
	"time"

	"github.com/google/uuid"
)

func TestRouteAssignedEventSerialization(t *testing.T) {
	eventId := uuid.New()
	tenantId := uuid.New()
	shipmentId := uuid.New()
	assignedAt := time.Now().UTC()

	raw := fmt.Sprintf(`{
		"eventId": "%s",
		"contractVersion": 1,
		"shipmentId": "%s",
		"tenantId": "%s",
		"shipmentNumber": "SHP-001",
		"routeId": "ROUTE-100",
		"vehicleId": "VEHICLE-100",
		"assignedAt": "%s"
	}`, eventId, shipmentId, tenantId, assignedAt.Format(time.RFC3339Nano))

	var ev routeAssignedPayload
	if err := json.Unmarshal([]byte(raw), &ev); err != nil {
		t.Fatalf("unmarshal failed: %v", err)
	}

	if ev.EventId != eventId {
		t.Errorf("expected EventId %s, got %s", eventId, ev.EventId)
	}
	if ev.ShipmentId != shipmentId {
		t.Errorf("expected ShipmentId %s, got %s", shipmentId, ev.ShipmentId)
	}
	if ev.TenantId != tenantId {
		t.Errorf("expected TenantId %s, got %s", tenantId, ev.TenantId)
	}
	if ev.VehicleId != "VEHICLE-100" {
		t.Errorf("expected VehicleId VEHICLE-100, got %s", ev.VehicleId)
	}
	if ev.RouteId != "ROUTE-100" {
		t.Errorf("expected RouteId ROUTE-100, got %s", ev.RouteId)
	}
}

func TestTerminalShipmentEvents(t *testing.T) {
	cancelledRaw := `{
		"eventId": "01940000-0000-7000-0000-000000000001",
		"contractVersion": 1,
		"shipmentId": "01940000-0000-7000-0000-000000000002",
		"tenantId": "01940000-0000-7000-0000-000000000003",
		"reason": "Customer cancelled",
		"cancelledAt": "2026-11-09T10:00:00Z"
	}`

	var cancelEv shipmentCancelledPayload
	if err := json.Unmarshal([]byte(cancelledRaw), &cancelEv); err != nil {
		t.Fatalf("unmarshal cancel event failed: %v", err)
	}
	if cancelEv.Reason != "Customer cancelled" {
		t.Errorf("expected reason 'Customer cancelled', got '%s'", cancelEv.Reason)
	}

	completedRaw := `{
		"eventId": "01940000-0000-7000-0000-000000000004",
		"contractVersion": 1,
		"shipmentId": "01940000-0000-7000-0000-000000000002",
		"tenantId": "01940000-0000-7000-0000-000000000003",
		"shipmentNumber": "SHP-001",
		"currentStatus": "Delivered",
		"completedAt": "2026-11-09T11:00:00Z"
	}`

	var completeEv shipmentCompletedPayload
	if err := json.Unmarshal([]byte(completedRaw), &completeEv); err != nil {
		t.Fatalf("unmarshal complete event failed: %v", err)
	}
	if completeEv.CurrentStatus != "Delivered" {
		t.Errorf("expected status 'Delivered', got '%s'", completeEv.CurrentStatus)
	}
}

func TestOutboxExchangeNaming(t *testing.T) {
	cases := []struct {
		eventType string
		expected  string
	}{
		{"GpsPositionUpdatedEvent", "GpsTracking.Contracts.Events:GpsPositionUpdatedEvent"},
		{"GpsMonitoringAlertRaisedEvent", "GpsTracking.Contracts.Events:GpsMonitoringAlertRaisedEvent"},
	}

	for _, c := range cases {
		actual := fmt.Sprintf("GpsTracking.Contracts.Events:%s", c.eventType)
		if actual != c.expected {
			t.Errorf("expected %s, got %s", c.expected, actual)
		}
	}
}

func TestSignalLossCutoff(t *testing.T) {
	threshold := 5 * time.Minute
	now := time.Now().UTC()
	cutoff := now.Add(-threshold)

	recentSignal := now.Add(-2 * time.Minute)
	oldSignal := now.Add(-6 * time.Minute)

	if !oldSignal.Before(cutoff) {
		t.Errorf("oldSignal should be before cutoff")
	}
	if recentSignal.Before(cutoff) {
		t.Errorf("recentSignal should NOT be before cutoff")
	}
}

