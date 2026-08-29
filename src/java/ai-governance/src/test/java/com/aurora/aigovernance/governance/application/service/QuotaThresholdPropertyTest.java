package com.aurora.aigovernance.governance.application.service;

import com.aurora.aigovernance.governance.application.port.TenantPlanResolver;
import com.aurora.aigovernance.governance.application.port.TenantQuotaPort;
import com.aurora.aigovernance.governance.domain.entity.Plan;
import com.aurora.aigovernance.governance.domain.entity.PlanCapability;
import com.aurora.aigovernance.governance.domain.entity.PlanQuota;
import com.aurora.aigovernance.governance.domain.enums.AiProvider;
import com.aurora.aigovernance.governance.domain.enums.QuotaMetric;
import com.aurora.aigovernance.governance.domain.enums.QuotaPeriod;
import com.aurora.aigovernance.governance.domain.enums.TenantStatus;
import com.aurora.aigovernance.governance.domain.valueobject.GovernanceDecision;
import com.aurora.aigovernance.governance.domain.valueobject.PeriodKey;
import com.aurora.aigovernance.governance.domain.valueobject.TenantPlanResult;
import com.aurora.aigovernance.governance.infrastructure.persistence.PlanRepository;
import com.aurora.aigovernance.shared.domain.AiOperation;
import com.aurora.aigovernance.shared.domain.TokenBudget;
import net.jqwik.api.*;

import java.util.List;
import java.util.Optional;
import java.util.UUID;

import static org.mockito.ArgumentMatchers.any;
import static org.mockito.ArgumentMatchers.eq;
import static org.mockito.Mockito.mock;
import static org.mockito.Mockito.when;

public class QuotaThresholdPropertyTest {

    @Property
    public boolean softQuotaThresholdEnforcesNinetyFivePercentRule(
            @ForAll("validLimits") long limit,
            @ForAll("validUsages") long currentUsage) {

        TenantPlanResolver planResolver = mock(TenantPlanResolver.class);
        PlanRepository planRepository = mock(PlanRepository.class);
        TenantQuotaPort quotaPort = mock(TenantQuotaPort.class);
        PeriodKeyCalculator calculator = new PeriodKeyCalculator();

        GovernancePolicyService policyService = new GovernancePolicyService(
                planResolver, planRepository, quotaPort, calculator
        );

        UUID tenantId = UUID.randomUUID();
        TenantPlanResult.TenantPlanInfo info = new TenantPlanResult.TenantPlanInfo(
                tenantId, "STANDARD", TenantStatus.ACTIVE, true
        );
        when(planResolver.resolve(tenantId)).thenReturn(new TenantPlanResult.Success(info));

        Plan plan = mock(Plan.class);
        when(plan.getCode()).thenReturn("STANDARD");
        when(plan.isCloudAiEnabled()).thenReturn(true);
        when(plan.getDefaultProvider()).thenReturn(AiProvider.GEMINI);

        PlanQuota quota = mock(PlanQuota.class);
        when(quota.getQuotaMetric()).thenReturn(QuotaMetric.REQUESTS);
        when(quota.getQuotaPeriod()).thenReturn(QuotaPeriod.MINUTE);
        when(quota.getLimitValue()).thenReturn(limit);
        when(plan.getQuotas()).thenReturn(List.of(quota));

        PlanCapability cap = mock(PlanCapability.class);
        when(cap.getCapabilityCode()).thenReturn("compliance.answer");
        when(cap.getAllowedProviders()).thenReturn("GEMINI");
        when(plan.getCapabilities()).thenReturn(List.of(cap));

        when(planRepository.findByCode("STANDARD")).thenReturn(Optional.of(plan));
        when(quotaPort.getCurrentUsage(eq(tenantId), eq(QuotaMetric.REQUESTS), eq(QuotaPeriod.MINUTE), any(PeriodKey.class)))
                .thenReturn(currentUsage);

        GovernanceDecision decision = policyService.evaluate(
                tenantId, "service-a", "compliance.answer",
                AiOperation.GENERATE, new TokenBudget(100, 100)
        );

        long threshold = (long) (limit * 0.95);
        long projected = currentUsage + 1; // 1 request increment

        if (projected > threshold) {
            return !decision.allowed();
        } else {
            return decision.allowed();
        }
    }

    @Provide
    Arbitrary<Long> validLimits() {
        return Arbitraries.longs().between(10L, 1_000_000L);
    }

    @Provide
    Arbitrary<Long> validUsages() {
        return Arbitraries.longs().between(0L, 1_200_000L);
    }
}
