package services

import (
	"context"
	"encoding/json"
	"fmt"

	"aurora/route-planning-agent/internal/domain"
	"aurora/route-planning-agent/internal/domain/enums"
	"aurora/route-planning-agent/internal/domain/repository"
	domainrules "aurora/route-planning-agent/internal/domain/rules"

	"github.com/google/uuid"
)

const (
	PlatformDefaultPolicyID = "platform-default-route-governance"
	PlatformDefaultVersion  = 1
)

type riskPolicyProvider struct {
	policyRepo        repository.RiskPolicyRepository
	ruleConfigRepo    repository.TenantRuleConfigRepository
	ruleConfigService domainrules.ITenantRuleConfigService
}

func NewRiskPolicyProvider(
	policyRepo repository.RiskPolicyRepository,
	ruleConfigRepo repository.TenantRuleConfigRepository,
	ruleConfigService domainrules.ITenantRuleConfigService,
) RouteRiskPolicyProvider {
	return &riskPolicyProvider{
		policyRepo:        policyRepo,
		ruleConfigRepo:    ruleConfigRepo,
		ruleConfigService: ruleConfigService,
	}
}

func (p *riskPolicyProvider) GetEffectivePolicy(ctx context.Context, tenantID uuid.UUID, scope string) (*EffectiveRiskPolicy, error) {
	if scope == "" {
		scope = "RoutePlanning"
	}

	config, err := p.policyRepo.GetConfig(ctx, tenantID)
	if err != nil {
		return nil, fmt.Errorf("truy xuất cấu hình chính sách rủi ro của Tenant thất bại: %w", err)
	}

	// 1. UNCONFIGURED -> Kiểm tra nếu có TenantRuleConfigs thì dùng, ngược lại Fail-Closed
	if config == nil {
		ruleConfigs, err := p.ruleConfigRepo.ListAll(ctx, tenantID)
		if err != nil || len(ruleConfigs) == 0 {
			return nil, domain.ErrRiskPolicyNotConfigured
		}

		thresholdsMap := make(map[string]domainrules.TenantRuleThresholds, len(ruleConfigs))
		for _, rc := range ruleConfigs {
			th, err := p.ruleConfigService.GetThresholds(ctx, tenantID, rc.RuleName)
			if err == nil && th != nil {
				thresholdsMap[rc.RuleName] = *th
			}
		}

		return &EffectiveRiskPolicy{
			PolicyID:       fmt.Sprintf("tenant-policy-%s", tenantID),
			Version:        1,
			Source:         enums.RiskPolicySourceTenant,
			Scope:          scope,
			TenantID:       tenantID,
			RuleThresholds: thresholdsMap,
		}, nil
	}

	// 2. USE_PLATFORM_DEFAULT -> Áp dụng Platform Default Policy v1
	if config.PolicyMode == enums.RiskPolicyModeUsePlatformDefault {
		policyID := config.ActivePolicyID
		if policyID == "" {
			policyID = PlatformDefaultPolicyID
		}
		version := config.ActivePolicyVersion
		if version <= 0 {
			version = PlatformDefaultVersion
		}

		return &EffectiveRiskPolicy{
			PolicyID:       policyID,
			Version:        version,
			Source:         enums.RiskPolicySourcePlatformDefault,
			Scope:          scope,
			TenantID:       tenantID,
			RuleThresholds: make(map[string]domainrules.TenantRuleThresholds),
		}, nil
	}

	// 3. USE_CUSTOM_POLICY -> Áp dụng ACTIVE Tenant Policy
	if config.PolicyMode == enums.RiskPolicyModeUseCustomPolicy {
		activePolicy, err := p.policyRepo.GetActivePolicy(ctx, tenantID, scope)
		if err != nil {
			return nil, fmt.Errorf("không thể tải chính sách rủi ro tuỳ chỉnh cho Tenant: %w", err)
		}

		if activePolicy != nil {
			thresholdsMap := make(map[string]domainrules.TenantRuleThresholds, len(activePolicy.Rules))
			for _, rule := range activePolicy.Rules {
				values := make(map[string]float64)
				if rule.ThresholdsJSON != "" {
					_ = json.Unmarshal([]byte(rule.ThresholdsJSON), &values)
				}
				thresholdsMap[rule.RuleName] = domainrules.TenantRuleThresholds{
					IsEnabled: rule.IsEnabled,
					Values:    values,
				}
			}

			return &EffectiveRiskPolicy{
				PolicyID:       activePolicy.ID.String(),
				Version:        activePolicy.Version,
				Source:         activePolicy.Source,
				Scope:          scope,
				TenantID:       tenantID,
				RuleThresholds: thresholdsMap,
			}, nil
		}

		// Backward-compatibility: Kiểm tra legacy TenantRuleConfigs
		ruleConfigs, _ := p.ruleConfigRepo.ListAll(ctx, tenantID)
		if len(ruleConfigs) > 0 {
			thresholdsMap := make(map[string]domainrules.TenantRuleThresholds, len(ruleConfigs))
			for _, rc := range ruleConfigs {
				th, err := p.ruleConfigService.GetThresholds(ctx, tenantID, rc.RuleName)
				if err == nil && th != nil {
					thresholdsMap[rc.RuleName] = *th
				}
			}

			policyID := config.ActivePolicyID
			if policyID == "" {
				policyID = fmt.Sprintf("tenant-policy-%s", tenantID)
			}
			version := config.ActivePolicyVersion
			if version <= 0 {
				version = 1
			}

			return &EffectiveRiskPolicy{
				PolicyID:       policyID,
				Version:        version,
				Source:         enums.RiskPolicySourceTenant,
				Scope:          scope,
				TenantID:       tenantID,
				RuleThresholds: thresholdsMap,
			}, nil
		}

		// Fail-Closed: Đã bật Custom Policy nhưng không có policy active nào
		return nil, fmt.Errorf("không tìm thấy chính sách rủi ro ACTIVE cho Tenant %s (Scope='%s')", tenantID, scope)
	}

	return nil, domain.ErrRiskPolicyNotConfigured
}

