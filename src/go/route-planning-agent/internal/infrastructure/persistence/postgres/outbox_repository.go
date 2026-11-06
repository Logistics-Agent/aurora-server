package postgres

import (
	"context"
	"time"

	"aurora/route-planning-agent/internal/domain/entities"
	"aurora/route-planning-agent/internal/domain/repository"

	"github.com/google/uuid"
	"gorm.io/gorm"
)

type outboxRepository struct {
	db *gorm.DB
}

func NewOutboxRepository(db *gorm.DB) repository.OutboxRepository {
	return &outboxRepository{db: db}
}

func (r *outboxRepository) Save(ctx context.Context, msg *entities.OutboxMessage) error {
	db := getDB(ctx, r.db)

	if msg.ID == uuid.Nil {
		msg.ID = uuid.New()
	}
	if msg.CreatedAt.IsZero() {
		msg.CreatedAt = time.Now().UTC()
	}

	return db.Create(msg).Error
}

func (r *outboxRepository) GetUnprocessed(ctx context.Context, batchSize int32) ([]entities.OutboxMessage, error) {
	db := getDB(ctx, r.db)

	if batchSize <= 0 {
		batchSize = 20
	}

	var messages []entities.OutboxMessage
	err := db.Where(`"ProcessedAt" IS NULL AND "RetryCount" < 5`).
		Order(`"CreatedAt" ASC`).
		Limit(int(batchSize)).
		Find(&messages).Error

	if err != nil {
		return nil, err
	}

	return messages, nil
}

func (r *outboxRepository) MarkProcessed(ctx context.Context, id uuid.UUID, processedAt time.Time) error {
	db := getDB(ctx, r.db)

	return db.Model(&entities.OutboxMessage{}).
		Where(`"Id" = ?`, id).
		Updates(map[string]any{
			"ProcessedAt": processedAt,
			"Error":       nil,
		}).Error
}

func (r *outboxRepository) MarkFailed(ctx context.Context, id uuid.UUID, errMessage string) error {
	db := getDB(ctx, r.db)

	return db.Model(&entities.OutboxMessage{}).
		Where(`"Id" = ?`, id).
		Updates(map[string]any{
			"RetryCount": gorm.Expr(`"RetryCount" + 1`),
			"Error":      errMessage,
		}).Error
}

