package persistence

import (
	"context"
	"errors"

	"aurora/ai-governance/internal/gateway/domain"
	govdomain "aurora/ai-governance/internal/governance/domain"
	"aurora/ai-governance/internal/shared"

	"gorm.io/gorm"
)

type ProviderSlotRepository struct {
	db *gorm.DB
}

func NewProviderSlotRepository(db *gorm.DB) *ProviderSlotRepository {
	return &ProviderSlotRepository{db: db}
}

func (r *ProviderSlotRepository) FindActiveCandidateSlots(
	ctx context.Context,
	poolCodes []string,
	providers []govdomain.AiProvider,
	operation shared.AiOperation,
) ([]*domain.ProviderSlot, error) {
	if r.db == nil {
		return nil, errors.New("postgres db not initialized")
	}
	if len(poolCodes) == 0 || len(providers) == 0 {
		return nil, nil
	}

	var slots []*domain.ProviderSlot
	err := r.db.WithContext(ctx).
		Joins("JOIN provider_pools ON provider_pools.id = provider_slots.pool_id").
		Where("provider_pools.code IN ?", poolCodes).
		Where("provider_slots.provider IN ?", providers).
		Where("provider_slots.operation = ?", operation).
		Where("provider_slots.enabled = ?", true).
		Preload("Pool").
		Order("provider_slots.priority ASC").
		Find(&slots).Error
	if err != nil {
		return nil, err
	}
	return slots, nil
}

func (r *ProviderSlotRepository) SaveCooldown(ctx context.Context, slot *domain.ProviderSlot) error {
	if r.db == nil {
		return errors.New("postgres db not initialized")
	}
	return r.db.WithContext(ctx).
		Model(&domain.ProviderSlot{}).
		Where("id = ?", slot.ID).
		Update("cooldown_until", slot.CooldownUntil).Error
}

type ServiceProviderPoolPolicyRepository struct {
	db *gorm.DB
}

func NewServiceProviderPoolPolicyRepository(db *gorm.DB) *ServiceProviderPoolPolicyRepository {
	return &ServiceProviderPoolPolicyRepository{db: db}
}

func (r *ServiceProviderPoolPolicyRepository) FindByServiceIDOrderByPriorityAsc(
	ctx context.Context,
	serviceID string,
) ([]*domain.ServiceProviderPoolPolicy, error) {
	if r.db == nil {
		return nil, errors.New("postgres db not initialized")
	}
	var policies []*domain.ServiceProviderPoolPolicy
	err := r.db.WithContext(ctx).
		Preload("Pool").
		Where("service_id = ?", serviceID).
		Order("priority ASC").
		Find(&policies).Error
	if err != nil {
		return nil, err
	}
	return policies, nil
}
