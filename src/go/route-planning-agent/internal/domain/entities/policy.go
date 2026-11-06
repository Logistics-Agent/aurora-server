package entities

import (
	"time"

	"aurora/route-planning-agent/internal/domain/enums"
	"aurora/shared/entity"

	"github.com/google/uuid"
)

// TenantRiskPolicyConfig cấu hình chế độ policy của tenant (Fail-closed khi Unconfigured)
type TenantRiskPolicyConfig struct {
	ID                  uuid.UUID            `gorm:"column:Id;primaryKey;type:uuid" json:"id"`
	TenantID            uuid.UUID            `gorm:"column:TenantId;type:uuid;not null;uniqueIndex" json:"tenant_id"`
	PolicyMode          enums.RiskPolicyMode `gorm:"column:PolicyMode;type:integer;not null" json:"policy_mode"`
	ActivePolicyID      string               `gorm:"column:ActivePolicyId;type:varchar(100);not null" json:"active_policy_id"`
	ActivePolicyVersion int32                `gorm:"column:ActivePolicyVersion;type:integer;not null" json:"active_policy_version"`
	UpdatedAt           time.Time            `gorm:"column:UpdatedAt;type:timestamp with time zone;not null" json:"updated_at"`
}

func (TenantRiskPolicyConfig) TableName() string {
	return `"TenantRiskPolicyConfigs"`
}

// TenantRiskPolicy chính sách rủi ro có phiên bản của tenant
type TenantRiskPolicy struct {
	entity.TenantAuditableEntity

	Name               string                       `gorm:"column:Name;type:varchar(200);not null" json:"name"`
	Description        *string                      `gorm:"column:Description;type:varchar(1000)" json:"description,omitempty"`
	Scope              string                       `gorm:"column:Scope;type:varchar(100);not null" json:"scope"`
	Version            int32                        `gorm:"column:Version;type:integer;not null" json:"version"`
	Status             enums.TenantRiskPolicyStatus `gorm:"column:Status;type:integer;not null" json:"status"`
	Source             enums.RiskPolicySource       `gorm:"column:Source;type:integer;not null" json:"source"`
	SourceDocumentID   *string                      `gorm:"column:SourceDocumentId;type:varchar(200)" json:"source_document_id,omitempty"`
	SubmittedByUserID  *uuid.UUID                   `gorm:"column:SubmittedByUserId;type:uuid" json:"submitted_by_user_id,omitempty"`
	SubmittedAt        *time.Time                   `gorm:"column:SubmittedAt;type:timestamp with time zone" json:"submitted_at,omitempty"`
	ReviewedByUserID   *uuid.UUID                   `gorm:"column:ReviewedByUserId;type:uuid" json:"reviewed_by_user_id,omitempty"`
	ReviewedAt         *time.Time                   `gorm:"column:ReviewedAt;type:timestamp with time zone" json:"reviewed_at,omitempty"`
	ReviewerComment    *string                      `gorm:"column:ReviewerComment;type:varchar(1000)" json:"reviewer_comment,omitempty"`
	PublishedByUserID  *uuid.UUID                   `gorm:"column:PublishedByUserId;type:uuid" json:"published_by_user_id,omitempty"`
	PublishedAt        *time.Time                   `gorm:"column:PublishedAt;type:timestamp with time zone" json:"published_at,omitempty"`
	RejectionReason    *string                      `gorm:"column:RejectionReason;type:varchar(1000)" json:"rejection_reason,omitempty"`
	SupersededAt       *time.Time                   `gorm:"column:SupersededAt;type:timestamp with time zone" json:"superseded_at,omitempty"`
	IsDeleted          bool                         `gorm:"column:IsDeleted;type:boolean;not null;default:false" json:"is_deleted"`

	Rules []TenantRiskRule `gorm:"foreignKey:PolicyID;constraint:OnDelete:CASCADE" json:"rules,omitempty"`
}

func (TenantRiskPolicy) TableName() string {
	return `"TenantRiskPolicies"`
}

// TenantRiskRule quy tắc rủi ro cụ thể thuộc về một chính sách
type TenantRiskRule struct {
	entity.TenantAuditableEntity

	PolicyID        uuid.UUID            `gorm:"column:PolicyId;type:uuid;not null;index" json:"policy_id"`
	RuleCode        string               `gorm:"column:RuleCode;type:varchar(100);not null" json:"rule_code"`
	RuleName        string               `gorm:"column:RuleName;type:varchar(100);not null" json:"rule_name"`
	ThresholdsJSON  string               `gorm:"column:ThresholdsJson;type:text;not null" json:"thresholds_json"`
	RiskEffect      enums.RouteRiskLevel `gorm:"column:RiskEffect;type:integer;not null" json:"risk_effect"`
	IsEnabled       bool                 `gorm:"column:IsEnabled;type:boolean;not null" json:"is_enabled"`
	SourceReference *string              `gorm:"column:SourceReference;type:varchar(500)" json:"source_reference,omitempty"`
	IsDeleted       bool                 `gorm:"column:IsDeleted;type:boolean;not null;default:false" json:"is_deleted"`

	Policy *TenantRiskPolicy `gorm:"foreignKey:PolicyID" json:"policy,omitempty"`
}

func (TenantRiskRule) TableName() string {
	return `"TenantRiskRules"`
}

// TenantRuleConfig cấu hình ngưỡng trực tiếp của rule theo tenant (RuleEngine)
type TenantRuleConfig struct {
	ID             uuid.UUID `gorm:"column:Id;primaryKey;type:uuid" json:"id"`
	TenantID       uuid.UUID `gorm:"column:TenantId;type:uuid;not null;uniqueIndex:idx_tenant_rule" json:"tenant_id"`
	RuleName       string    `gorm:"column:RuleName;type:varchar(100);not null;uniqueIndex:idx_tenant_rule" json:"rule_name"`
	IsEnabled      bool      `gorm:"column:IsEnabled;type:boolean;not null" json:"is_enabled"`
	ThresholdsJSON string    `gorm:"column:ThresholdsJson;type:text;not null" json:"thresholds_json"`
	UpdatedAt      time.Time `gorm:"column:UpdatedAt;type:timestamp with time zone;not null" json:"updated_at"`
}

func (TenantRuleConfig) TableName() string {
	return `"TenantRuleConfigs"`
}

