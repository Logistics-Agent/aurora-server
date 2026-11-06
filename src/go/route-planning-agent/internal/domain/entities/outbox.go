package entities

import (
	"time"

	"github.com/google/uuid"
)

// OutboxMessage lưu trữ sự kiện cần xuất bản theo Transactional Outbox Pattern
type OutboxMessage struct {
	ID          uuid.UUID  `gorm:"column:Id;primaryKey;type:uuid" json:"id"`
	EventType   string     `gorm:"column:EventType;type:varchar(256);not null" json:"event_type"`
	Payload     string     `gorm:"column:Payload;type:text;not null" json:"payload"`
	CreatedAt   time.Time  `gorm:"column:CreatedAt;type:timestamp with time zone;not null;index:idx_outbox_proc_created" json:"created_at"`
	ProcessedAt *time.Time `gorm:"column:ProcessedAt;type:timestamp with time zone;index:idx_outbox_proc_created" json:"processed_at,omitempty"`
	RetryCount  int32      `gorm:"column:RetryCount;type:integer;not null;default:0" json:"retry_count"`
	Error       *string    `gorm:"column:Error;type:text" json:"error,omitempty"`
}

func (OutboxMessage) TableName() string {
	return `"OutboxMessages"`
}

