package entities

import (
	"time"

	"aurora/route-planning-agent/internal/domain/enums"
	"aurora/shared/entity"

	"github.com/google/uuid"
)

// RiskAssessment kết quả đánh giá rủi ro tổng hợp cho một phiên bản của tuyến đường
type RiskAssessment struct {
	entity.TenantAuditableEntity

	RouteID            uuid.UUID                `gorm:"column:RouteId;type:uuid;not null;index" json:"route_id"`
	RouteVersion       int32                    `gorm:"column:RouteVersion;type:integer;not null" json:"route_version"`
	PolicyID           string                   `gorm:"column:PolicyId;type:varchar(100);not null" json:"policy_id"`
	PolicyVersion      int32                    `gorm:"column:PolicyVersion;type:integer;not null" json:"policy_version"`
	RiskLevel          enums.RouteRiskLevel     `gorm:"column:RiskLevel;type:integer;not null" json:"risk_level"`
	GovernanceDecision enums.GovernanceDecision `gorm:"column:GovernanceDecision;type:integer;not null" json:"governance_decision"`
	RiskScore          *float64                 `gorm:"column:RiskScore;type:double precision" json:"risk_score,omitempty"`
	ConfidenceScore    *float64                 `gorm:"column:ConfidenceScore;type:double precision" json:"confidence_score,omitempty"`
	MatchedRuleCodes   string                   `gorm:"column:MatchedRuleCodes;type:varchar(1000);not null" json:"matched_rule_codes"`
	Source             string                   `gorm:"column:Source;type:varchar(100);not null" json:"source"`
	ReasonCodes        string                   `gorm:"column:ReasonCodes;type:varchar(500);not null" json:"reason_codes"`
	ReasonDetails      string                   `gorm:"column:ReasonDetails;type:varchar(2000);not null" json:"reason_details"`
	PolicyApplied      string                   `gorm:"column:PolicyApplied;type:varchar(100);not null" json:"policy_applied"`
	PolicySource       enums.RiskPolicySource   `gorm:"column:PolicySource;type:integer;not null" json:"policy_source"`
	AssessedByUserID   uuid.UUID                `gorm:"column:AssessedByUserId;type:uuid;not null" json:"assessed_by_user_id"`
	AssessedAt         time.Time                `gorm:"column:AssessedAt;type:timestamp with time zone;not null" json:"assessed_at"`

	Route *Route `gorm:"foreignKey:RouteID" json:"route,omitempty"`
}

func (RiskAssessment) TableName() string {
	return `"RiskAssessments"`
}

