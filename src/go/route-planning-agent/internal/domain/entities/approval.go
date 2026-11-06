package entities

import (
	"time"

	"aurora/route-planning-agent/internal/domain/enums"
	"aurora/shared/entity"

	"github.com/google/uuid"
)

// ApprovalRequest yêu cầu phê duyệt tuyến đường rủi ro cao từ quản lý
type ApprovalRequest struct {
	entity.TenantAuditableEntity

	RouteID           uuid.UUID            `gorm:"column:RouteId;type:uuid;not null;index" json:"route_id"`
	RouteVersion      int32                `gorm:"column:RouteVersion;type:integer;not null" json:"route_version"`
	PolicyID          string               `gorm:"column:PolicyId;type:varchar(100);not null" json:"policy_id"`
	PolicyVersion     int32                `gorm:"column:PolicyVersion;type:integer;not null" json:"policy_version"`
	Feature           string               `gorm:"column:Feature;type:varchar(100);not null" json:"feature"`
	Status            enums.ApprovalStatus `gorm:"column:Status;type:integer;not null" json:"status"`
	Reason            string               `gorm:"column:Reason;type:varchar(2000);not null" json:"reason"`
	AiSummary         string               `gorm:"column:AiSummary;type:text;not null" json:"ai_summary"`
	ComplianceSummary *string              `gorm:"column:ComplianceSummary;type:text" json:"compliance_summary,omitempty"`
	ReviewedByUserID  *uuid.UUID           `gorm:"column:ReviewedByUserId;type:uuid" json:"reviewed_by_user_id,omitempty"`
	ReviewedAt        *time.Time           `gorm:"column:ReviewedAt;type:timestamp with time zone" json:"reviewed_at,omitempty"`
	ReviewerComment   *string              `gorm:"column:ReviewerComment;type:varchar(1000)" json:"reviewer_comment,omitempty"`
	RejectionReason   *string              `gorm:"column:RejectionReason;type:varchar(1000)" json:"rejection_reason,omitempty"`

	Route *Route `gorm:"foreignKey:RouteID" json:"route,omitempty"`
}

func (ApprovalRequest) TableName() string {
	return `"ApprovalRequests"`
}

// RouteDecisionAuditLog lưu vết quyết định đánh giá rủi ro (Audit Trail bất biến)
type RouteDecisionAuditLog struct {
	entity.TenantAuditableEntity

	RouteID                  uuid.UUID  `gorm:"column:RouteId;type:uuid;not null;index" json:"route_id"`
	RequestedByUserID        uuid.UUID  `gorm:"column:RequestedByUserId;type:uuid;not null" json:"requested_by_user_id"`
	RiskLevel                string     `gorm:"column:RiskLevel;type:varchar(50);not null" json:"risk_level"`
	AutomationDecision       string     `gorm:"column:AutomationDecision;type:varchar(100);not null" json:"automation_decision"`
	RuleResultsJSON          string     `gorm:"column:RuleResultsJson;type:text;not null" json:"rule_results_json"`
	PolicyApplied            string     `gorm:"column:PolicyApplied;type:text;not null" json:"policy_applied"`
	LlmProvider              *string    `gorm:"column:LlmProvider;type:varchar(50)" json:"llm_provider,omitempty"`
	LlmModel                 *string    `gorm:"column:LlmModel;type:varchar(100)" json:"llm_model,omitempty"`
	LlmSummary               *string    `gorm:"column:LlmSummary;type:text" json:"llm_summary,omitempty"`
	ComplianceCheckPerformed bool       `gorm:"column:ComplianceCheckPerformed;type:boolean;not null" json:"compliance_check_performed"`
	ComplianceSummary        *string    `gorm:"column:ComplianceSummary;type:text" json:"compliance_summary,omitempty"`
	ComplianceDocumentRefs   *string    `gorm:"column:ComplianceDocumentRefs;type:text" json:"compliance_document_refs,omitempty"`
	ApprovalRequestID        *uuid.UUID `gorm:"column:ApprovalRequestId;type:uuid" json:"approval_request_id,omitempty"`
}

func (RouteDecisionAuditLog) TableName() string {
	return `"DecisionAuditLogs"`
}

