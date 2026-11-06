package dtos

import (
	"time"

	"github.com/google/uuid"
)

type RouteCreatedEvent struct {
	RouteID     uuid.UUID `json:"route_id"`
	TenantID    uuid.UUID `json:"tenant_id"`
	Name        string    `json:"name"`
	Type        string    `json:"type"`
	StopsCount  int       `json:"stops_count"`
	OccurredAt  time.Time `json:"occurred_at"`
}

type RouteUpdatedEvent struct {
	RouteID     uuid.UUID `json:"route_id"`
	TenantID    uuid.UUID `json:"tenant_id"`
	Version     int32     `json:"version"`
	OccurredAt  time.Time `json:"occurred_at"`
}

type RouteDeletedEvent struct {
	RouteID    uuid.UUID `json:"route_id"`
	TenantID   uuid.UUID `json:"tenant_id"`
	OccurredAt time.Time `json:"occurred_at"`
}

type RouteStatusChangedEvent struct {
	RouteID    uuid.UUID `json:"route_id"`
	TenantID   uuid.UUID `json:"tenant_id"`
	OldStatus  string    `json:"old_status"`
	NewStatus  string    `json:"new_status"`
	OccurredAt time.Time `json:"occurred_at"`
}

type RouteOptimizedEvent struct {
	RouteID              uuid.UUID `json:"route_id"`
	TenantID             uuid.UUID `json:"tenant_id"`
	TotalDistanceKm      float64   `json:"total_distance_km"`
	TotalDurationMinutes int32     `json:"total_duration_minutes"`
	OccurredAt           time.Time `json:"occurred_at"`
}

type RouteApprovalRequestedEvent struct {
	ApprovalRequestID uuid.UUID `json:"approval_request_id"`
	RouteID           uuid.UUID `json:"route_id"`
	TenantID          uuid.UUID `json:"tenant_id"`
	Reason            string    `json:"reason"`
	AiSummary         string    `json:"ai_summary"`
	OccurredAt        time.Time `json:"occurred_at"`
}

type TenantRuleConfigChangedEvent struct {
	TenantID   uuid.UUID `json:"tenant_id"`
	RuleName   string    `json:"rule_name"`
	OccurredAt time.Time `json:"occurred_at"`
}

type AiUsageEvent struct {
	RouteID      uuid.UUID `json:"route_id"`
	TenantID     uuid.UUID `json:"tenant_id"`
	Provider     string    `json:"provider"`
	Model        string    `json:"model"`
	InputTokens  int32     `json:"input_tokens"`
	OutputTokens int32     `json:"output_tokens"`
	LatencyMs    int64     `json:"latency_ms"`
	OccurredAt   time.Time `json:"occurred_at"`
}

