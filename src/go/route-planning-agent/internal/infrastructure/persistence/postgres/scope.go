package postgres

import (
	"aurora/route-planning-agent/internal/domain"

	"github.com/google/uuid"
	"gorm.io/gorm"
)

func TenantScope(tenantID uuid.UUID) func(db *gorm.DB) *gorm.DB {
	return func(db *gorm.DB) *gorm.DB {
		if tenantID == uuid.Nil {
			_ = db.AddError(domain.ErrTenantRequired)
			return db.Where("1 = 0")
		}
		return db.Where(`"TenantId" = ?`, tenantID)
	}
}

// ActiveOnlyScope lọc các bản ghi chưa bị xóa mềm
func ActiveOnlyScope() func(db *gorm.DB) *gorm.DB {
	return func(db *gorm.DB) *gorm.DB {
		return db.Where(`"IsDeleted" = false`)
	}
}

