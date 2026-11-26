package application

import (
	"context"
	"log/slog"
	"strings"
	"time"

	"aurora/ai-governance/internal/governance/domain"
	"aurora/ai-governance/internal/shared"

	"github.com/google/uuid"
)

const softQuotaThreshold = 0.95

var trustedInternalCapabilities = map[string]map[string]struct{}{
	"devops-agent": {
		"devops.diagnose": {},
		"devops.rca":      {},
	},
	"regulatory-compliance-platform-rag": {
		"compliance.embed": {},
	},
}

type GovernancePolicyService struct {
	tenantPlanResolver  TenantPlanResolver
	planRepo            PlanRepository
	tenantQuotaPort     TenantQuotaPort
	periodKeyCalculator *PeriodKeyCalculator
	logger              *slog.Logger
}

func NewGovernancePolicyService(
	tenantPlanResolver TenantPlanResolver,
	planRepo PlanRepository,
	tenantQuotaPort TenantQuotaPort,
	periodKeyCalculator *PeriodKeyCalculator,
	logger *slog.Logger,
) *GovernancePolicyService {
	if logger == nil {
		logger = slog.Default()
	}
	return &GovernancePolicyService{
		tenantPlanResolver:  tenantPlanResolver,
		planRepo:            planRepo,
		tenantQuotaPort:     tenantQuotaPort,
		periodKeyCalculator: periodKeyCalculator,
		logger:              logger,
	}
}

func (s *GovernancePolicyService) Evaluate(
	ctx context.Context,
	tenantID *uuid.UUID,
	callerServiceID string,
	capabilityCode string,
	operation shared.AiOperation,
	tokenBudget shared.TokenBudget,
) (decision domain.GovernanceDecision) {
	decisionID := uuid.NewString()

	defer func() {
		if r := recover(); r != nil {
			s.logger.Error("Governance DENIED: panic during policy evaluation", slog.Any("recover", r))
			decision = domain.DeniedDecision(domain.DenyReasonPolicyError, decisionID)
		}
	}()

	var planCtx *domain.TenantPlanContext

	// 1. Internal Platform Service Execution Context Bypass Check
	if isTrustedInternalContext(callerServiceID, capabilityCode) {
		s.logger.Debug("Evaluating trusted internal platform workload",
			slog.String("caller", callerServiceID),
			slog.String("capability", capabilityCode),
			slog.String("decisionId", decisionID))
		var err error
		planCtx, err = s.buildInternalServiceContext(ctx, callerServiceID)
		if err != nil {
			s.logger.Error("Governance DENIED: failed building internal service context", slog.Any("error", err))
			return domain.DeniedDecision(domain.DenyReasonPolicyError, decisionID)
		}
	} else if tenantID == nil || *tenantID == uuid.Nil {
		s.logger.Warn("Governance DENIED: missing required tenantId for non-internal service",
			slog.String("caller", callerServiceID),
			slog.String("capability", capabilityCode))
		return domain.DeniedDecision(domain.DenyReasonTenantNotFound, decisionID)
	} else {
		// 2. Resolve Tenant Plan
		res := s.tenantPlanResolver.Resolve(ctx, *tenantID)
		switch res.Kind {
		case domain.TenantPlanSuccess:
			var err error
			planCtx, err = s.buildTenantPlanContext(ctx, res.Info)
			if err != nil {
				s.logger.Error("Governance DENIED: failed loading plan from repository",
					slog.String("plan", res.Info.PlanCode), slog.Any("error", err))
				return domain.DeniedDecision(domain.DenyReasonPolicyError, decisionID)
			}
			if planCtx == nil {
				s.logger.Error("Governance DENIED: plan not found in PlanRepository",
					slog.String("plan", res.Info.PlanCode), slog.String("tenantId", tenantID.String()))
				return domain.DeniedDecision(domain.DenyReasonCapabilityNotAllowed, decisionID)
			}
		case domain.TenantPlanNotFound:
			s.logger.Warn("Governance DENIED: tenant not found", slog.String("tenantId", tenantID.String()), slog.String("reason", res.Reason))
			return domain.DeniedDecision(domain.DenyReasonTenantNotFound, decisionID)
		case domain.TenantPlanSuspended:
			s.logger.Warn("Governance DENIED: tenant suspended", slog.String("tenantId", tenantID.String()), slog.String("reason", res.Reason))
			return domain.DeniedDecision(domain.DenyReasonTenantSuspended, decisionID)
		case domain.TenantPlanIamUnavailable:
			s.logger.Error("Governance DENIED: fail-closed due to IamTenant unavailability", slog.String("tenantId", tenantID.String()), slog.String("reason", res.Reason))
			return domain.DeniedDecision(domain.DenyReasonPolicyError, decisionID)
		case domain.TenantPlanNotConfigured:
			s.logger.Error("Governance DENIED: plan not configured", slog.String("tenantId", tenantID.String()), slog.String("plan", res.PlanCode))
			return domain.DeniedDecision(domain.DenyReasonCapabilityNotAllowed, decisionID)
		default:
			return domain.DeniedDecision(domain.DenyReasonPolicyError, decisionID)
		}
	}

	// 3. Check tenant status
	if planCtx.Status == domain.TenantStatusSuspended {
		return domain.DeniedDecision(domain.DenyReasonTenantSuspended, decisionID)
	}
	if planCtx.Status == domain.TenantStatusCancelled {
		return domain.DeniedDecision(domain.DenyReasonTenantCancelled, decisionID)
	}

	// 4. Check cloud AI enabled
	if !planCtx.CloudAiEnabled {
		return domain.DeniedDecision(domain.DenyReasonCloudAiDisabled, decisionID)
	}

	// 5. Capability check
	if _, ok := planCtx.AllowedCapabilities[capabilityCode]; !ok {
		s.logger.Info("Governance DENIED: capability not allowed for plan",
			slog.String("capability", capabilityCode), slog.String("plan", planCtx.PlanCode))
		return domain.DeniedDecision(domain.DenyReasonCapabilityNotAllowed, decisionID)
	}

	// 6. Quota check (only for tenant-scoped calls)
	if tenantID != nil && *tenantID != uuid.Nil {
		now := time.Now().UTC()
		for _, quota := range planCtx.Quotas {
			periodKey := s.periodKeyCalculator.Calculate(quota.Period, now)
			currentUsage := s.tenantQuotaPort.GetCurrentUsage(ctx, *tenantID, quota.Metric, quota.Period, periodKey)
			increment := resolveRequestIncrement(quota.Metric, tokenBudget)
			projectedUsage := currentUsage + increment
			threshold := int64(float64(quota.Limit) * softQuotaThreshold)

			if projectedUsage > threshold {
				s.logger.Info("Governance DENIED: quota exceeded",
					slog.String("tenantId", tenantID.String()),
					slog.String("metric", string(quota.Metric)),
					slog.String("period", string(quota.Period)),
					slog.Int64("current", currentUsage),
					slog.Int64("projected", projectedUsage),
					slog.Int64("threshold", threshold))
				return domain.DeniedDecision(domain.DenyReasonQuotaExceeded, decisionID)
			}
		}
	}

	// 7. Resolve effective policy
	policy := resolveEffectivePolicy(planCtx)

	s.logger.Info("Governance ALLOWED",
		slog.String("caller", callerServiceID),
		slog.String("capability", capabilityCode),
		slog.String("operation", string(operation)),
		slog.String("decisionId", decisionID))

	return domain.GovernanceDecision{
		Allowed:              true,
		DecisionID:           decisionID,
		AllowedProviders:     policy.AllowedProviders,
		AllowedProviderPools: policy.AllowedProviderPools,
		ModelTier:            policy.ModelTier,
		MaxTokens:            policy.MaxTokens,
		AutomationLevel:      policy.AutomationLevel,
		RequireApproval:      policy.RequireApproval,
		PolicyVersion:        "v1",
	}
}

func isTrustedInternalContext(callerServiceID, capabilityCode string) bool {
	if callerServiceID == "" {
		return false
	}
	caps, ok := trustedInternalCapabilities[callerServiceID]
	if !ok {
		return false
	}
	_, allowed := caps[capabilityCode]
	return allowed
}

func (s *GovernancePolicyService) buildInternalServiceContext(ctx context.Context, serviceID string) (*domain.TenantPlanContext, error) {
	plan, err := s.planRepo.FindByCode(ctx, "ENTERPRISE")
	if err != nil {
		return nil, err
	}

	allowedCaps := make(map[string]struct{})
	if plan != nil {
		for _, c := range plan.Capabilities {
			allowedCaps[c.CapabilityCode] = struct{}{}
		}
	}
	// Ensure trusted internal capabilities are always present in internal service context
	for capCode := range trustedInternalCapabilities[serviceID] {
		allowedCaps[capCode] = struct{}{}
	}

	return &domain.TenantPlanContext{
		TenantID:             nil,
		Status:               domain.TenantStatusActive,
		CloudAiEnabled:       true,
		PlanCode:             "ENTERPRISE",
		DefaultProvider:      domain.AiProviderAzureOpenAI,
		Quotas:               nil,
		AllowedCapabilities:  allowedCaps,
		AllowedProviders:     map[domain.AiProvider]struct{}{domain.AiProviderAzureOpenAI: {}, domain.AiProviderGemini: {}},
		AllowedProviderPools: map[string]struct{}{"shared-ai": {}},
	}, nil
}

func (s *GovernancePolicyService) buildTenantPlanContext(ctx context.Context, info *domain.TenantPlanInfo) (*domain.TenantPlanContext, error) {
	plan, err := s.planRepo.FindByCode(ctx, info.PlanCode)
	if err != nil {
		return nil, err
	}
	if plan == nil {
		return nil, nil
	}

	quotas := make([]domain.QuotaDefinition, 0, len(plan.Quotas))
	for _, q := range plan.Quotas {
		quotas = append(quotas, domain.QuotaDefinition{
			Metric: q.QuotaMetric,
			Period: q.QuotaPeriod,
			Limit:  q.LimitValue,
		})
	}

	allowedCaps := make(map[string]struct{}, len(plan.Capabilities))
	allowedProviders := make(map[domain.AiProvider]struct{})
	for _, capItem := range plan.Capabilities {
		allowedCaps[capItem.CapabilityCode] = struct{}{}
		if strings.TrimSpace(capItem.AllowedProviders) != "" {
			for _, part := range strings.Split(capItem.AllowedProviders, ",") {
				p := domain.AiProvider(strings.TrimSpace(part))
				if p == domain.AiProviderGemini || p == domain.AiProviderAzureOpenAI {
					allowedProviders[p] = struct{}{}
				}
			}
		}
	}
	if len(allowedProviders) == 0 && plan.DefaultProvider != nil {
		allowedProviders[*plan.DefaultProvider] = struct{}{}
	}

	var defaultProv domain.AiProvider
	if plan.DefaultProvider != nil {
		defaultProv = *plan.DefaultProvider
	}

	tID := info.TenantID
	return &domain.TenantPlanContext{
		TenantID:             &tID,
		Status:               info.Status,
		CloudAiEnabled:       info.CloudAiEnabled && plan.CloudAiEnabled,
		PlanCode:             plan.Code,
		DefaultProvider:      defaultProv,
		Quotas:               quotas,
		AllowedCapabilities:  allowedCaps,
		AllowedProviders:     allowedProviders,
		AllowedProviderPools: map[string]struct{}{"shared-ai": {}},
	}, nil
}

func resolveRequestIncrement(metric domain.QuotaMetric, budget shared.TokenBudget) int64 {
	if metric == domain.QuotaMetricRequests {
		return 1
	}
	return budget.ReservationTokens()
}

func resolveEffectivePolicy(ctx *domain.TenantPlanContext) domain.EffectivePolicy {
	return domain.EffectivePolicy{
		AllowedProviders:     ctx.AllowedProviders,
		AllowedProviderPools: ctx.AllowedProviderPools,
		ModelTier:            domain.ModelTierStandard,
		MaxTokens:            4096,
		AutomationLevel:      domain.AutomationLevelAssisted,
		RequireApproval:      false,
	}
}
