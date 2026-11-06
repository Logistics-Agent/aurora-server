package repository

import (
	"context"
	"time"

	"aurora/route-planning-agent/internal/domain/entities"

	"github.com/google/uuid"
)

type OutboxRepository interface {
	Save(ctx context.Context, msg *entities.OutboxMessage) error
	GetUnprocessed(ctx context.Context, batchSize int32) ([]entities.OutboxMessage, error)
	MarkProcessed(ctx context.Context, id uuid.UUID, processedAt time.Time) error
	MarkFailed(ctx context.Context, id uuid.UUID, errMessage string) error
}

