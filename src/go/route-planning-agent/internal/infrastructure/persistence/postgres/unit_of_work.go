package postgres

import (
	"context"

	"aurora/route-planning-agent/internal/domain/repository"

	"gorm.io/gorm"
)

type txContextKey struct{}

// WithTx lưu *gorm.DB transaction vào context
func WithTx(ctx context.Context, tx *gorm.DB) context.Context {
	return context.WithValue(ctx, txContextKey{}, tx)
}

// GetTx lấy *gorm.DB transaction từ context nếu có
func GetTx(ctx context.Context) *gorm.DB {
	if tx, ok := ctx.Value(txContextKey{}).(*gorm.DB); ok {
		return tx
	}
	return nil
}

type unitOfWork struct {
	db *gorm.DB
}

func NewUnitOfWork(db *gorm.DB) repository.UnitOfWork {
	return &unitOfWork{db: db}
}

func (u *unitOfWork) DoInTransaction(ctx context.Context, fn func(ctx context.Context) error) error {
	// Nếu đã nằm trong transaction sẵn thì tái sử dụng
	if GetTx(ctx) != nil {
		return fn(ctx)
	}

	return u.db.WithContext(ctx).Transaction(func(tx *gorm.DB) error {
		txCtx := WithTx(ctx, tx)
		return fn(txCtx)
	})
}

// getDB trả về tx từ context nếu có, ngược lại trả về defaultDB
func getDB(ctx context.Context, defaultDB *gorm.DB) *gorm.DB {
	if tx := GetTx(ctx); tx != nil {
		return tx.WithContext(ctx)
	}
	return defaultDB.WithContext(ctx)
}

