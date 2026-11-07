package postgres

import (
	"context"
	"fmt"
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

	leaseExpiry := time.Now().UTC().Add(60 * time.Second).Unix()
	leaseTag := fmt.Sprintf("CLAIMED:%d:%s", leaseExpiry, uuid.New().String())

	if db.Dialector != nil && db.Dialector.Name() == "postgres" {
		rawSQL := `
			UPDATE "OutboxMessages"
			SET "RetryCount" = "RetryCount" + 1,
			    "Error" = ?
			WHERE "Id" IN (
				SELECT "Id" FROM "OutboxMessages"
				WHERE "ProcessedAt" IS NULL 
				  AND "RetryCount" < 5
				  AND (
				      "Error" IS NULL 
				      OR "Error" NOT LIKE 'CLAIMED:%' 
				      OR (CASE WHEN "Error" LIKE 'CLAIMED:%' THEN split_part("Error", ':', 2)::bigint < extract(epoch from now()) ELSE false END)
				  )
				ORDER BY "CreatedAt" ASC
				LIMIT ?
				FOR UPDATE SKIP LOCKED
			)
			RETURNING "Id", "EventType", "Payload", "CreatedAt", "ProcessedAt", "RetryCount", "Error";
		`
		if err := db.Raw(rawSQL, leaseTag, batchSize).Scan(&messages).Error; err != nil {
			return nil, err
		}
		return messages, nil
	}

	// Fallback cho SQLite / môi trường test
	var ids []uuid.UUID
	err := db.Model(&entities.OutboxMessage{}).
		Where(`"ProcessedAt" IS NULL AND "RetryCount" < 5 AND ("Error" IS NULL OR "Error" NOT LIKE 'CLAIMED:%')`).
		Order(`"CreatedAt" ASC`).
		Limit(int(batchSize)).
		Pluck(`"Id"`, &ids).Error
	if err != nil {
		return nil, err
	}
	if len(ids) == 0 {
		return nil, nil
	}

	if err := db.Model(&entities.OutboxMessage{}).
		Where(`"Id" IN ?`, ids).
		Updates(map[string]any{
			"RetryCount": gorm.Expr(`"RetryCount" + 1`),
			"Error":      leaseTag,
		}).Error; err != nil {
		return nil, err
	}

	err = db.Where(`"Id" IN ?`, ids).Find(&messages).Error
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

	updates := map[string]any{
		"Error": errMessage,
	}
	if db.Dialector == nil || db.Dialector.Name() != "postgres" {
		updates["RetryCount"] = gorm.Expr(`"RetryCount" + 1`)
	}

	return db.Model(&entities.OutboxMessage{}).
		Where(`"Id" = ?`, id).
		Updates(updates).Error
}

