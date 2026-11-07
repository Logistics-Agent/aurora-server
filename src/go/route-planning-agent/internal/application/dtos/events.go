package dtos

import (
	"time"

	"github.com/google/uuid"
)

type RouteCreatedEvent struct {
	RouteID         uuid.UUID `json:"RouteId"`
	TenantID        uuid.UUID `json:"TenantId"`
	RouteName       string    `json:"RouteName"`
	RouteType       string    `json:"RouteType"`
	RiskLevel       string    `json:"RiskLevel"`
	CreatedByUserID uuid.UUID `json:"CreatedByUserId"`
}

type RouteUpdatedEvent struct {
	RouteID         uuid.UUID `json:"RouteId"`
	TenantID        uuid.UUID `json:"TenantId"`
	RouteName       string    `json:"RouteName"`
	Version         int32     `json:"Version"`
	UpdatedByUserID uuid.UUID `json:"UpdatedByUserId"`
}

type RouteDeletedEvent struct {
	RouteID         uuid.UUID `json:"RouteId"`
	TenantID        uuid.UUID `json:"TenantId"`
	DeletedByUserID uuid.UUID `json:"DeletedByUserId"`
}

type RouteStatusChangedEvent struct {
	RouteID         uuid.UUID `json:"RouteId"`
	TenantID        uuid.UUID `json:"TenantId"`
	OldStatus       string    `json:"OldStatus"`
	NewStatus       string    `json:"NewStatus"`
	ChangedByUserID uuid.UUID `json:"ChangedByUserId"`
}

type RouteOptimizedEvent struct {
	RouteID              uuid.UUID `json:"RouteId"`
	TenantID             uuid.UUID `json:"TenantId"`
	Provider             string    `json:"Provider"`
	Model                string    `json:"Model"`
	TotalDistanceKm      float64   `json:"TotalDistanceKm"`
	TotalDurationMinutes int32     `json:"TotalDurationMinutes"`
}

type RouteApprovalRequestedEvent struct {
	ApprovalRequestID uuid.UUID `json:"ApprovalRequestId"`
	RouteID           uuid.UUID `json:"RouteId"`
	TenantID          uuid.UUID `json:"TenantId"`
	Reason            string    `json:"Reason"`
	AiSummary         string    `json:"AiSummary"`
}

type RouteApprovedEvent struct {
	ApprovalRequestID uuid.UUID `json:"ApprovalRequestId"`
	RouteID           uuid.UUID `json:"RouteId"`
	TenantID          uuid.UUID `json:"TenantId"`
	ReviewerUserID    uuid.UUID `json:"ReviewerUserId"`
	Comment           *string   `json:"Comment,omitempty"`
}

type RouteRejectedEvent struct {
	ApprovalRequestID uuid.UUID `json:"ApprovalRequestId"`
	RouteID           uuid.UUID `json:"RouteId"`
	TenantID          uuid.UUID `json:"TenantId"`
	ReviewerUserID    uuid.UUID `json:"ReviewerUserId"`
	Reason            string    `json:"Reason"`
	Comment           *string   `json:"Comment,omitempty"`
}

type RouteRiskEvaluatedEvent struct {
	RouteID            uuid.UUID `json:"RouteId"`
	RouteVersion       int32     `json:"RouteVersion"`
	TenantID           uuid.UUID `json:"TenantId"`
	RiskLevel          string    `json:"RiskLevel"`
	GovernanceDecision string    `json:"GovernanceDecision"`
	PolicyID           string    `json:"PolicyId"`
	PolicyVersion      int32     `json:"PolicyVersion"`
	PolicySource       string    `json:"PolicySource"`
	MatchedRuleCodes   []string  `json:"MatchedRuleCodes"`
	Source             string    `json:"Source"`
	EvaluatedByUserID  uuid.UUID `json:"EvaluatedByUserId"`
}

type TenantRiskPolicyCreatedEvent struct {
	PolicyID        uuid.UUID `json:"PolicyId"`
	TenantID        uuid.UUID `json:"TenantId"`
	Scope           string    `json:"Scope"`
	Version         int32     `json:"Version"`
	Source          string    `json:"Source"`
	CreatedByUserID uuid.UUID `json:"CreatedByUserId"`
}

type TenantRiskPolicySubmittedEvent struct {
	PolicyID          uuid.UUID `json:"PolicyId"`
	TenantID          uuid.UUID `json:"TenantId"`
	Scope             string    `json:"Scope"`
	Version           int32     `json:"Version"`
	SubmittedByUserID uuid.UUID `json:"SubmittedByUserId"`
}

type TenantRiskPolicyRejectedEvent struct {
	PolicyID          uuid.UUID `json:"PolicyId"`
	TenantID          uuid.UUID `json:"TenantId"`
	Scope             string    `json:"Scope"`
	Version           int32     `json:"Version"`
	ReviewedByUserID  uuid.UUID `json:"ReviewedByUserId"`
	RejectionReason   string    `json:"RejectionReason"`
}

type TenantRiskPolicyPublishedEvent struct {
	PolicyID           uuid.UUID  `json:"PolicyId"`
	TenantID           uuid.UUID  `json:"TenantId"`
	Scope              string     `json:"Scope"`
	Version            int32      `json:"Version"`
	PublishedByUserID  uuid.UUID  `json:"PublishedByUserId"`
	SupersededPolicyID *uuid.UUID `json:"SupersededPolicyId,omitempty"`
	SupersededVersion  *int32     `json:"SupersededVersion,omitempty"`
}

type TenantRiskPolicySupersededEvent struct {
	PolicyID             uuid.UUID `json:"PolicyId"`
	TenantID             uuid.UUID `json:"TenantId"`
	Scope                string    `json:"Scope"`
	Version              int32     `json:"Version"`
	SupersededByPolicyID uuid.UUID `json:"SupersededByPolicyId"`
	SupersededByVersion  int32     `json:"SupersededByVersion"`
}

type TenantAiConfigChangedEvent struct {
	TenantID uuid.UUID `json:"TenantId"`
	Feature  string    `json:"Feature"`
}

type TenantRuleConfigChangedEvent struct {
	TenantID uuid.UUID `json:"TenantId"`
	RuleName string    `json:"RuleName"`
}

type AiUsageEvent struct {
	TenantID      uuid.UUID `json:"TenantId"`
	ServiceName   string    `json:"ServiceName"`
	Feature       string    `json:"Feature"`
	Provider      string    `json:"Provider"`
	Model         string    `json:"Model"`
	PromptVersion string    `json:"PromptVersion"`
	InputTokens   int32     `json:"InputTokens"`
	OutputTokens  int32     `json:"OutputTokens"`
	LatencyMs     int64     `json:"LatencyMs"`
	Success       bool      `json:"Success"`
	OccurredAt    time.Time `json:"OccurredAt"`
}
