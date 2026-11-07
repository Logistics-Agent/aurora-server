package services

import (
	"context"
	"encoding/json"
	"sync"

	domainrules "aurora/route-planning-agent/internal/domain/rules"
	"aurora/route-planning-agent/internal/domain/repository"

	"github.com/google/uuid"
)

type TenantRuleConfigService struct {
	repo  repository.TenantRuleConfigRepository
	cache sync.Map // map[string]*domainrules.TenantRuleThresholds (key: tenantID:ruleName)
}

func NewTenantRuleConfigService(repo repository.TenantRuleConfigRepository) *TenantRuleConfigService {
	return &TenantRuleConfigService{
		repo: repo,
	}
}

func (s *TenantRuleConfigService) GetThresholds(ctx context.Context, tenantID uuid.UUID, ruleName string) (*domainrules.TenantRuleThresholds, error) {
	cacheKey := tenantID.String() + ":" + ruleName
	if val, ok := s.cache.Load(cacheKey); ok {
		return val.(*domainrules.TenantRuleThresholds), nil
	}

	cfg, err := s.repo.GetByRuleName(ctx, tenantID, ruleName)
	if err != nil {
		return nil, err
	}

	thresholds := &domainrules.TenantRuleThresholds{
		IsEnabled: true,
		Values:    make(map[string]float64),
	}

	if cfg != nil {
		thresholds.IsEnabled = cfg.IsEnabled
		if cfg.ThresholdsJSON != "" {
			var vals map[string]float64
			if err := json.Unmarshal([]byte(cfg.ThresholdsJSON), &vals); err == nil {
				thresholds.Values = vals
			}
		}
	}

	s.cache.Store(cacheKey, thresholds)
	return thresholds, nil
}

func (s *TenantRuleConfigService) InvalidateCache(tenantIDStr, ruleName string) {
	if ruleName != "" {
		s.cache.Delete(tenantIDStr + ":" + ruleName)
	} else {
		// Xóa toàn bộ key của tenant này
		s.cache.Range(func(key, value any) bool {
			if k, ok := key.(string); ok && len(k) > len(tenantIDStr) && k[:len(tenantIDStr)] == tenantIDStr {
				s.cache.Delete(k)
			}
			return true
		})
	}
}

var _ domainrules.ITenantRuleConfigService = (*TenantRuleConfigService)(nil)

