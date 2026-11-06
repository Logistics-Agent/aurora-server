package dtos

import (
	"time"

	"github.com/google/uuid"
)

// RouteRecommendationDto khuyến nghị và phân tích rủi ro từ AI / Rules
type RouteRecommendationDto struct {
	RouteID               uuid.UUID `json:"route_id"`
	RiskLevel             string    `json:"risk_level"`
	AutomationDecision    string    `json:"automation_decision"`
	RecommendationSource  string    `json:"recommendation_source"`
	Summary               string    `json:"summary"`
	Suggestions           []string  `json:"suggestions"`
	ConfidenceScore       *float64  `json:"confidence_score,omitempty"`
	ApprovalRequestID     *string   `json:"approval_request_id,omitempty"`
	ApplicableRegulations []string  `json:"applicable_regulations"`
}

// RouteAiResult kết quả thực thi và chỉ số token usage từ AiGovernance
type RouteAiResult struct {
	Recommendation RouteRecommendationDto `json:"recommendation"`
	Provider       string                 `json:"provider"`
	Model          string                 `json:"model"`
	PromptVersion  string                 `json:"prompt_version"`
	InputTokens    int32                  `json:"input_tokens"`
	OutputTokens   int32                  `json:"output_tokens"`
	LatencyMs      int64                  `json:"latency_ms"`
	Success        bool                   `json:"success"`
}

// ComplianceCheckResultDto kết quả kiểm tra từ RegulatoryCompliance (RAG)
type ComplianceCheckResultDto struct {
	HasViolations         bool     `json:"has_violations"`
	ViolationSummaries    []string `json:"violation_summaries"`
	DocumentRefs          []string `json:"document_refs"`
	ApplicableRegulations []string `json:"applicable_regulations"`
	MergedContext         string   `json:"merged_context"`
	RequiresHumanApproval bool     `json:"requires_human_approval"`
}

// RouteStopDto thông tin trạm dừng
type RouteStopDto struct {
	ID                     uuid.UUID `json:"id"`
	Sequence               int32     `json:"sequence"`
	StopType               string    `json:"stop_type"`
	LocationName           string    `json:"location_name"`
	Address                string    `json:"address"`
	Latitude               float64   `json:"latitude"`
	Longitude              float64   `json:"longitude"`
	EstimatedArrivalMinutes int32     `json:"estimated_arrival_minutes"`
	ServiceDurationMinutes int32     `json:"service_duration_minutes"`
}

// RouteDto thông tin tuyến đường dùng cho DTO & prompt serialization
type RouteDto struct {
	ID                       uuid.UUID      `json:"id"`
	TenantID                 uuid.UUID      `json:"tenant_id"`
	Name                     string         `json:"name"`
	Description              *string        `json:"description,omitempty"`
	Type                     string         `json:"type"`
	Status                   string         `json:"status"`
	RiskLevel                string         `json:"risk_level"`
	GovernanceDecision       string         `json:"governance_decision"`
	EstimatedDistanceKm      float64        `json:"estimated_distance_km"`
	EstimatedDurationMinutes int32          `json:"estimated_duration_minutes"`
	MaxWeightKg              float64        `json:"max_weight_kg"`
	MaxVolumeM3              float64        `json:"max_volume_m3"`
	IsAiGenerated            bool           `json:"is_ai_generated"`
	Version                  int32          `json:"version"`
	CreatedAt                time.Time      `json:"created_at"`
	Stops                    []RouteStopDto `json:"stops"`
}

