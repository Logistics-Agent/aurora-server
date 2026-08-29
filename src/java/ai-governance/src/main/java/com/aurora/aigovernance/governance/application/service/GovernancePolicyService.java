package com.aurora.aigovernance.governance.application.service;

import com.aurora.aigovernance.governance.application.port.TenantPlanResolver;
import com.aurora.aigovernance.governance.application.port.TenantQuotaPort;
import com.aurora.aigovernance.governance.domain.entity.Plan;
import com.aurora.aigovernance.governance.domain.entity.PlanCapability;
import com.aurora.aigovernance.governance.domain.enums.*;
import com.aurora.aigovernance.governance.domain.valueobject.*;
import com.aurora.aigovernance.governance.infrastructure.persistence.PlanRepository;
import com.aurora.aigovernance.shared.domain.AiOperation;
import com.aurora.aigovernance.shared.domain.TokenBudget;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.stereotype.Service;

import java.time.Instant;
import java.util.*;
import java.util.stream.Collectors;

/**
 * Governance Policy Decision Point.
 * <p>
 * Evaluates tenant/plan/capability/quota policies and returns a {@link GovernanceDecision}.
 * Resolves tenant subscription plan dynamically via {@link TenantPlanResolver}.
 * Fail-closed on any exception or unresolvable entitlement.
 */
@Service
public class GovernancePolicyService {

    private static final Logger log = LoggerFactory.getLogger(GovernancePolicyService.class);
    private static final double SOFT_QUOTA_THRESHOLD = 0.95;

    private static final Set<String> TRUSTED_INTERNAL_SERVICES = Set.of("devops-agent");
    private static final Set<String> INTERNAL_CAPABILITIES = Set.of("devops.diagnose");

    private final TenantPlanResolver tenantPlanResolver;
    private final PlanRepository planRepository;
    private final TenantQuotaPort tenantQuotaPort;
    private final PeriodKeyCalculator periodKeyCalculator;

    public GovernancePolicyService(
            TenantPlanResolver tenantPlanResolver,
            PlanRepository planRepository,
            TenantQuotaPort tenantQuotaPort,
            PeriodKeyCalculator periodKeyCalculator) {
        this.tenantPlanResolver = tenantPlanResolver;
        this.planRepository = planRepository;
        this.tenantQuotaPort = tenantQuotaPort;
        this.periodKeyCalculator = periodKeyCalculator;
    }

    /**
     * Evaluate governance policy for a request.
     *
     * @param tenantId         external tenant ID from authenticated context (nullable only for internal services)
     * @param callerServiceId  immediate caller workload identity
     * @param capabilityCode   requested AI capability
     * @param operation        GENERATE or EMBED
     * @param tokenBudget      token budget for quota checks
     * @return GovernanceDecision — always returns, never throws for policy denial
     */
    public GovernanceDecision evaluate(
            UUID tenantId,
            String callerServiceId,
            String capabilityCode,
            AiOperation operation,
            TokenBudget tokenBudget) {

        String decisionId = UUID.randomUUID().toString();

        try {
            TenantPlanContext context;

            // 1. Internal Platform Service Execution Context Bypass Check
            if (isTrustedInternalContext(callerServiceId, capabilityCode)) {
                log.debug("Evaluating trusted internal platform workload: caller={}, capability={}, decisionId={}",
                        callerServiceId, capabilityCode, decisionId);
                context = buildInternalServiceContext(callerServiceId);
            } else if (tenantId == null) {
                // Tenant-facing workload missing tenant identity -> REJECT
                log.warn("Governance DENIED: missing required tenantId for non-internal service. caller={}, capability={}",
                        callerServiceId, capabilityCode);
                return GovernanceDecision.denied(DenyReason.TENANT_NOT_FOUND, decisionId);
            } else {
                // 2. Resolve Tenant Plan via TenantPlanResolver (Redis Cache + IamTenant fallback)
                TenantPlanResult result = tenantPlanResolver.resolve(tenantId);

                switch (result) {
                    case TenantPlanResult.Success s -> {
                        context = buildTenantPlanContext(s.info());
                        if (context == null) {
                            log.error("Governance DENIED: plan '{}' configured for tenant {} not found in local PlanRepository",
                                    s.info().planCode(), tenantId);
                            return GovernanceDecision.denied(DenyReason.CAPABILITY_NOT_ALLOWED, decisionId);
                        }
                    }
                    case TenantPlanResult.NotFound nf -> {
                        log.warn("Governance DENIED: tenant not found. tenantId={}, reason={}", tenantId, nf.reason());
                        return GovernanceDecision.denied(DenyReason.TENANT_NOT_FOUND, decisionId);
                    }
                    case TenantPlanResult.Suspended sp -> {
                        log.warn("Governance DENIED: tenant suspended. tenantId={}, reason={}", tenantId, sp.reason());
                        return GovernanceDecision.denied(DenyReason.TENANT_SUSPENDED, decisionId);
                    }
                    case TenantPlanResult.IamUnavailable un -> {
                        log.error("Governance DENIED: fail-closed due to IamTenant unavailability. tenantId={}, reason={}",
                                tenantId, un.reason());
                        return GovernanceDecision.denied(DenyReason.POLICY_ERROR, decisionId);
                    }
                    case TenantPlanResult.PlanNotConfigured pnc -> {
                        log.error("Governance DENIED: plan not configured. tenantId={}, plan={}", tenantId, pnc.planCode());
                        return GovernanceDecision.denied(DenyReason.CAPABILITY_NOT_ALLOWED, decisionId);
                    }
                }
            }

            // 3. Check tenant status
            if (context.status() == TenantStatus.SUSPENDED) {
                return GovernanceDecision.denied(DenyReason.TENANT_SUSPENDED, decisionId);
            }
            if (context.status() == TenantStatus.CANCELLED) {
                return GovernanceDecision.denied(DenyReason.TENANT_CANCELLED, decisionId);
            }

            // 4. Check cloud AI enabled
            if (!context.cloudAiEnabled()) {
                return GovernanceDecision.denied(DenyReason.CLOUD_AI_DISABLED, decisionId);
            }

            // 5. Capability check
            if (!context.allowedCapabilities().contains(capabilityCode)) {
                log.info("Governance DENIED: capability '{}' not allowed for plan '{}'", capabilityCode, context.planCode());
                return GovernanceDecision.denied(DenyReason.CAPABILITY_NOT_ALLOWED, decisionId);
            }

            // 6. Quota check (only applicable for tenant-scoped calls)
            if (tenantId != null) {
                Instant now = Instant.now();
                for (QuotaDefinition quota : context.quotas()) {
                    PeriodKey periodKey = periodKeyCalculator.calculate(quota.period(), now);
                    long currentUsage = tenantQuotaPort.getCurrentUsage(
                            tenantId, quota.metric(), quota.period(), periodKey);

                    long requestIncrement = resolveRequestIncrement(quota.metric(), operation, tokenBudget);
                    long projectedUsage = currentUsage + requestIncrement;
                    long threshold = (long) (quota.limit() * SOFT_QUOTA_THRESHOLD);

                    if (projectedUsage > threshold) {
                        log.info("Governance DENIED: quota exceeded. tenantId={}, metric={}, period={}, " +
                                        "current={}, projected={}, threshold={}",
                                tenantId, quota.metric(), quota.period(),
                                currentUsage, projectedUsage, threshold);
                        return GovernanceDecision.denied(DenyReason.QUOTA_EXCEEDED, decisionId);
                    }
                }
            }

            // 7. Resolve effective policy
            EffectivePolicy policy = resolveEffectivePolicy(context, capabilityCode);

            log.info("Governance ALLOWED: tenantId={}, caller={}, capability={}, operation={}, decisionId={}",
                    tenantId, callerServiceId, capabilityCode, operation, decisionId);

            return new GovernanceDecision(
                    true,
                    null,
                    decisionId,
                    policy.allowedProviders(),
                    policy.allowedProviderPools(),
                    policy.modelTier(),
                    policy.maxTokens(),
                    policy.automationLevel(),
                    policy.requireApproval(),
                    "v1"
            );

        } catch (Exception e) {
            // Fail-closed on any exception
            log.error("Governance DENIED: policy evaluation error. tenantId={}, capability={}",
                    tenantId, capabilityCode, e);
            return GovernanceDecision.denied(DenyReason.POLICY_ERROR, decisionId);
        }
    }

    private boolean isTrustedInternalContext(String callerServiceId, String capabilityCode) {
        return callerServiceId != null &&
                TRUSTED_INTERNAL_SERVICES.contains(callerServiceId) &&
                INTERNAL_CAPABILITIES.contains(capabilityCode);
    }

    private TenantPlanContext buildInternalServiceContext(String serviceId) {
        Optional<Plan> enterprisePlanOpt = planRepository.findByCode("ENTERPRISE");
        Plan plan = enterprisePlanOpt.orElse(null);

        Set<String> allowedCapabilities = plan != null
                ? plan.getCapabilities().stream().map(PlanCapability::getCapabilityCode).collect(Collectors.toSet())
                : INTERNAL_CAPABILITIES;

        Set<AiProvider> allowedProviders = Set.of(AiProvider.AZURE_OPENAI, AiProvider.GEMINI);
        Set<String> allowedPools = Set.of("shared-ai");

        return new TenantPlanContext(
                null,
                TenantStatus.ACTIVE,
                true,
                "ENTERPRISE",
                AiProvider.AZURE_OPENAI,
                Collections.emptyList(),
                allowedCapabilities,
                allowedProviders,
                allowedPools
        );
    }

    private TenantPlanContext buildTenantPlanContext(TenantPlanResult.TenantPlanInfo info) {
        Optional<Plan> planOpt = planRepository.findByCode(info.planCode());
        if (planOpt.isEmpty()) {
            return null;
        }

        Plan plan = planOpt.get();

        List<QuotaDefinition> quotas = plan.getQuotas().stream()
                .map(q -> new QuotaDefinition(q.getQuotaMetric(), q.getQuotaPeriod(), q.getLimitValue()))
                .toList();

        Set<String> allowedCapabilities = plan.getCapabilities().stream()
                .map(PlanCapability::getCapabilityCode)
                .collect(Collectors.toSet());

        Set<AiProvider> allowedProviders = new HashSet<>();
        for (PlanCapability cap : plan.getCapabilities()) {
            if (cap.getAllowedProviders() != null && !cap.getAllowedProviders().isBlank()) {
                String[] providerNames = cap.getAllowedProviders().split(",");
                for (String p : providerNames) {
                    try {
                        allowedProviders.add(AiProvider.valueOf(p.trim()));
                    } catch (IllegalArgumentException ignored) {}
                }
            }
        }
        if (allowedProviders.isEmpty() && plan.getDefaultProvider() != null) {
            allowedProviders.add(plan.getDefaultProvider());
        }

        Set<String> allowedPools = Set.of("shared-ai");

        return new TenantPlanContext(
                info.tenantId(),
                info.status(),
                info.cloudAiEnabled() && plan.isCloudAiEnabled(),
                plan.getCode(),
                plan.getDefaultProvider(),
                quotas,
                allowedCapabilities,
                allowedProviders,
                allowedPools
        );
    }

    /**
     * Resolve the increment for a quota dimension based on operation type.
     */
    private long resolveRequestIncrement(QuotaMetric metric, AiOperation operation, TokenBudget budget) {
        return switch (metric) {
            case REQUESTS -> 1L;
            case TOKENS -> budget.reservationTokens();
        };
    }

    /**
     * Resolve effective policy by merging plan defaults with capability-specific overrides.
     */
    private EffectivePolicy resolveEffectivePolicy(TenantPlanContext context, String capabilityCode) {
        return new EffectivePolicy(
                context.allowedProviders(),
                context.allowedProviderPools(),
                ModelTier.STANDARD,
                4096,
                AutomationLevel.ASSISTED,
                false
        );
    }
}
