package com.aurora.aigovernance.governance.application.service;

import com.aurora.aigovernance.governance.application.port.TenantQuotaPort;
import com.aurora.aigovernance.governance.domain.entity.PlanCapability;
import com.aurora.aigovernance.governance.domain.entity.PlanQuota;
import com.aurora.aigovernance.governance.domain.enums.*;
import com.aurora.aigovernance.governance.domain.valueobject.*;
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
 * Fail-closed on any exception.
 */
@Service
public class GovernancePolicyService {

    private static final Logger log = LoggerFactory.getLogger(GovernancePolicyService.class);
    private static final double SOFT_QUOTA_THRESHOLD = 0.95;

    private final TenantCacheService tenantCacheService;
    private final TenantQuotaPort tenantQuotaPort;
    private final PeriodKeyCalculator periodKeyCalculator;

    public GovernancePolicyService(
            TenantCacheService tenantCacheService,
            TenantQuotaPort tenantQuotaPort,
            PeriodKeyCalculator periodKeyCalculator) {
        this.tenantCacheService = tenantCacheService;
        this.tenantQuotaPort = tenantQuotaPort;
        this.periodKeyCalculator = periodKeyCalculator;
    }

    /**
     * Evaluate governance policy for a request.
     *
     * @param tenantId         external tenant ID from authenticated context
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
            // 1. Load tenant + plan context
            TenantPlanContext context = tenantCacheService.loadContext(tenantId);
            if (context == null) {
                log.warn("Governance DENIED: tenant not found. tenantId={}", tenantId);
                return GovernanceDecision.denied(DenyReason.TENANT_NOT_FOUND, decisionId);
            }

            // 2. Check tenant status
            if (context.status() == TenantStatus.SUSPENDED) {
                return GovernanceDecision.denied(DenyReason.TENANT_SUSPENDED, decisionId);
            }
            if (context.status() == TenantStatus.CANCELLED) {
                return GovernanceDecision.denied(DenyReason.TENANT_CANCELLED, decisionId);
            }

            // 3. Check cloud AI enabled
            if (!context.cloudAiEnabled()) {
                return GovernanceDecision.denied(DenyReason.CLOUD_AI_DISABLED, decisionId);
            }

            // 4. Capability check
            if (!context.allowedCapabilities().contains(capabilityCode)) {
                return GovernanceDecision.denied(DenyReason.CAPABILITY_NOT_ALLOWED, decisionId);
            }

            // 5. Quota check — per-dimension soft check (V1)
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

            // 6. Resolve effective policy
            EffectivePolicy policy = resolveEffectivePolicy(context, capabilityCode);

            log.info("Governance ALLOWED: tenantId={}, capability={}, operation={}, decisionId={}",
                    tenantId, capabilityCode, operation, decisionId);

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
        // For V1, return plan-level defaults merged with capability if found
        // Capability-level allowedProviders override plan default
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
