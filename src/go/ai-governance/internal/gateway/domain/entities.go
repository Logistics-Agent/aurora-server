package domain

import (
	"time"

	govdomain "aurora/ai-governance/internal/governance/domain"
	"aurora/ai-governance/internal/shared"

	"github.com/google/uuid"
)

type ProviderPool struct {
	ID        uuid.UUID  `gorm:"type:uuid;primaryKey;column:id"`
	Code      string     `gorm:"column:code;uniqueIndex;size:50;not null"`
	Name      string     `gorm:"column:name;size:100;not null"`
	CreatedAt time.Time  `gorm:"column:created_at;not null"`
	CreatedBy string     `gorm:"column:created_by;size:100"`
	UpdatedAt *time.Time `gorm:"column:updated_at"`
	UpdatedBy string     `gorm:"column:updated_by;size:100"`
	Version   int64      `gorm:"column:version"`
}

func (ProviderPool) TableName() string { return "provider_pools" }

type ProviderSlot struct {
	ID            uuid.UUID            `gorm:"type:uuid;primaryKey;column:id"`
	PoolID        uuid.UUID            `gorm:"type:uuid;column:pool_id;not null"`
	Pool          *ProviderPool        `gorm:"foreignKey:PoolID;references:ID"`
	Provider      govdomain.AiProvider `gorm:"column:provider;size:30;not null"`
	Operation     shared.AiOperation   `gorm:"column:operation;size:20;not null"`
	SlotAlias     string               `gorm:"column:slot_alias;uniqueIndex;size:80;not null"`
	ProjectID     string               `gorm:"column:project_id;size:100"`
	SecretRef     string               `gorm:"column:secret_ref;size:200;not null"`
	ModelName     string               `gorm:"column:model_name;size:100;not null"`
	RpmLimit      int                  `gorm:"column:rpm_limit;not null"`
	TpmLimit      int                  `gorm:"column:tpm_limit;not null"`
	RpdLimit      int                  `gorm:"column:rpd_limit;not null"`
	Priority      int                  `gorm:"column:priority;not null;default:1"`
	Weight        int                  `gorm:"column:weight;not null;default:1"`
	Enabled       bool                 `gorm:"column:enabled;not null"`
	CooldownUntil *time.Time           `gorm:"column:cooldown_until"`
	CreatedAt     time.Time            `gorm:"column:created_at;not null"`
	CreatedBy     string               `gorm:"column:created_by;size:100"`
	UpdatedAt     *time.Time           `gorm:"column:updated_at"`
	UpdatedBy     string               `gorm:"column:updated_by;size:100"`
	Version       int64                `gorm:"column:version"`
}

func (ProviderSlot) TableName() string { return "provider_slots" }

func (s *ProviderSlot) IsInCooldown(now time.Time) bool {
	return s.CooldownUntil != nil && s.CooldownUntil.After(now)
}

type ServiceProviderPoolPolicy struct {
	ID        uuid.UUID     `gorm:"type:uuid;primaryKey;column:id"`
	ServiceID string        `gorm:"column:service_id;size:100;not null"`
	PoolID    uuid.UUID     `gorm:"type:uuid;column:pool_id;not null"`
	Pool      *ProviderPool `gorm:"foreignKey:PoolID;references:ID"`
	Priority  int           `gorm:"column:priority;not null;default:1"`
	CreatedAt time.Time     `gorm:"column:created_at;not null"`
	CreatedBy string        `gorm:"column:created_by;size:100"`
	UpdatedAt *time.Time    `gorm:"column:updated_at"`
	UpdatedBy string        `gorm:"column:updated_by;size:100"`
	Version   int64         `gorm:"column:version"`
}

func (ServiceProviderPoolPolicy) TableName() string { return "service_provider_pool_policies" }
