package domain

import (
	"time"

	"github.com/google/uuid"
)

type Plan struct {
	ID              uuid.UUID        `gorm:"type:uuid;primaryKey;column:id"`
	Code            string           `gorm:"column:code;uniqueIndex;size:50;not null"`
	Name            string           `gorm:"column:name;size:100;not null"`
	DefaultProvider *AiProvider      `gorm:"column:default_provider;size:30"`
	CloudAiEnabled  bool             `gorm:"column:cloud_ai_enabled;not null;default:true"`
	CreatedAt       time.Time        `gorm:"column:created_at;not null"`
	CreatedBy       string           `gorm:"column:created_by;size:100"`
	UpdatedAt       *time.Time       `gorm:"column:updated_at"`
	UpdatedBy       string           `gorm:"column:updated_by;size:100"`
	Version         int64            `gorm:"column:version"`
	Capabilities    []PlanCapability `gorm:"foreignKey:PlanID;references:ID"`
	Quotas          []PlanQuota      `gorm:"foreignKey:PlanID;references:ID"`
}

func (Plan) TableName() string { return "plans" }

type PlanCapability struct {
	ID               uuid.UUID  `gorm:"type:uuid;primaryKey;column:id"`
	PlanID           uuid.UUID  `gorm:"type:uuid;column:plan_id;not null"`
	CapabilityCode   string     `gorm:"column:capability_code;size:100;not null"`
	AllowedProviders string     `gorm:"column:allowed_providers;size:200"`
	ModelTier        string     `gorm:"column:model_tier;size:20"`
	MaxTokens        *int32     `gorm:"column:max_tokens"`
	AutomationLevel  string     `gorm:"column:automation_level;size:30"`
	RequireApproval  bool       `gorm:"column:require_approval;default:false"`
	CreatedAt        time.Time  `gorm:"column:created_at;not null"`
	CreatedBy        string     `gorm:"column:created_by;size:100"`
	UpdatedAt        *time.Time `gorm:"column:updated_at"`
	UpdatedBy        string     `gorm:"column:updated_by;size:100"`
	Version          int64      `gorm:"column:version"`
}

func (PlanCapability) TableName() string { return "plan_capabilities" }

type PlanQuota struct {
	PlanID      uuid.UUID   `gorm:"type:uuid;primaryKey;column:plan_id"`
	QuotaMetric QuotaMetric `gorm:"primaryKey;column:quota_metric;size:20"`
	QuotaPeriod QuotaPeriod `gorm:"primaryKey;column:quota_period;size:20"`
	LimitValue  int64       `gorm:"column:limit_value;not null"`
}

func (PlanQuota) TableName() string { return "plan_quotas" }

type Tenant struct {
	ID               uuid.UUID    `gorm:"type:uuid;primaryKey;column:id"`
	ExternalTenantID uuid.UUID    `gorm:"type:uuid;column:external_tenant_id;uniqueIndex;not null"`
	PlanID           uuid.UUID    `gorm:"type:uuid;column:plan_id;not null"`
	Status           TenantStatus `gorm:"column:status;size:20;not null;default:ACTIVE"`
	CloudAiEnabled   bool         `gorm:"column:cloud_ai_enabled;not null;default:true"`
	CreatedAt        time.Time    `gorm:"column:created_at;not null"`
	CreatedBy        string       `gorm:"column:created_by;size:100"`
	UpdatedAt        *time.Time   `gorm:"column:updated_at"`
	UpdatedBy        string       `gorm:"column:updated_by;size:100"`
	Version          int64        `gorm:"column:version"`
}

func (Tenant) TableName() string { return "tenants" }

type UsageRecord struct {
	ID          uuid.UUID   `gorm:"type:uuid;primaryKey;column:id"`
	TenantID    uuid.UUID   `gorm:"type:uuid;column:tenant_id;not null"`
	QuotaMetric QuotaMetric `gorm:"column:quota_metric;size:20;not null"`
	QuotaPeriod QuotaPeriod `gorm:"column:quota_period;size:20;not null"`
	PeriodKey   string      `gorm:"column:period_key;size:30;not null"`
	UsageValue  int64       `gorm:"column:usage_value;not null;default:0"`
	CreatedAt   time.Time   `gorm:"column:created_at;not null"`
	CreatedBy   string      `gorm:"column:created_by;size:100"`
	UpdatedAt   *time.Time  `gorm:"column:updated_at"`
	UpdatedBy   string      `gorm:"column:updated_by;size:100"`
	Version     int64       `gorm:"column:version"`
}

func (UsageRecord) TableName() string { return "usage_records" }

type ProcessedEvent struct {
	ID          uuid.UUID `gorm:"type:uuid;primaryKey;column:id"`
	EventID     string    `gorm:"column:event_id;size:100;uniqueIndex;not null"`
	ProcessedAt time.Time `gorm:"column:processed_at;not null"`
	Version     int64     `gorm:"column:version"`
}

func (ProcessedEvent) TableName() string { return "processed_events" }
