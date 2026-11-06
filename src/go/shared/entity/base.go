package entity

import (
	"time"

	"github.com/google/uuid"
)

// BaseEntity chứa khóa chính UUID
type BaseEntity struct {
	ID uuid.UUID `gorm:"column:Id;primaryKey;type:uuid" json:"id"`
}

// AuditableEntity chứa các trường audit thời gian và người tạo/sửa
type AuditableEntity struct {
	BaseEntity
	CreatedAt time.Time  `gorm:"column:CreatedAt;type:timestamp with time zone;not null" json:"created_at"`
	CreatedBy *string    `gorm:"column:CreatedBy;type:text" json:"created_by,omitempty"`
	UpdatedAt *time.Time `gorm:"column:UpdatedAt;type:timestamp with time zone" json:"updated_at,omitempty"`
	UpdatedBy *string    `gorm:"column:UpdatedBy;type:text" json:"updated_by,omitempty"`
}

// TenantAuditableEntity chứa audit fields và TenantID bắt buộc để đảm bảo multi-tenancy
type TenantAuditableEntity struct {
	AuditableEntity
	TenantID uuid.UUID `gorm:"column:TenantId;type:uuid;not null;index" json:"tenant_id"`
}

