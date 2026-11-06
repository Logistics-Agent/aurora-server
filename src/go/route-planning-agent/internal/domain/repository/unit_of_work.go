package repository

import "context"

type UnitOfWork interface {
	DoInTransaction(ctx context.Context, fn func(ctx context.Context) error) error
}

