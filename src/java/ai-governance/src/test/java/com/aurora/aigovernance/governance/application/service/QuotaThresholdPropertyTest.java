package com.aurora.aigovernance.governance.application.service;

import com.aurora.aigovernance.governance.application.port.TenantQuotaPort;
import com.aurora.aigovernance.governance.domain.enums.AiProvider;
import com.aurora.aigovernance.governance.domain.enums.QuotaMetric;
import com.aurora.aigovernance.governance.domain.enums.QuotaPeriod;
import com.aurora.aigovernance.governance.domain.enums.TenantStatus;
import com.aurora.aigovernance.governance.domain.valueobject.GovernanceDecision;
import com.aurora.aigovernance.governance.domain.valueobject.PeriodKey;
import com.aurora.aigovernance.governance.domain.valueobject.QuotaDefinition;
import com.aurora.aigovernance.governance.domain.valueobject.TenantPlanContext;
import com.aurora.aigovernance.shared.domain.AiOperation;
import com.aurora.aigovernance.shared.domain.TokenBudget;
import net.jqwik.api.*;

import java.util.List;
import java.util.Set;
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

        TenantCacheService cacheService = mock(TenantCacheService.class);
        TenantQuotaPort quotaPort = mock(TenantQuotaPort.class);
        PeriodKeyCalculator calculator = new PeriodKeyCalculator();

        GovernancePolicyService policyService = new GovernancePolicyService(
                cacheService, quotaPort, calculator
        );

        UUID tenantId = UUID.randomUUID();
        QuotaDefinition quota = new QuotaDefinition(QuotaMetric.REQUESTS, QuotaPeriod.MINUTE, limit);
        TenantPlanContext context = new TenantPlanContext(
                tenantId, TenantStatus.ACTIVE, true, "STANDARD",
                AiProvider.GEMINI, List.of(quota), Set.of("compliance.answer"),
                Set.of(AiProvider.GEMINI), Set.of("shared-ai")
        );

        when(cacheService.loadContext(tenantId)).thenReturn(context);
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
        return Arbitraries.longs().between(10L, 1000L);
    }

    @Provide
    Arbitrary<Long> validUsages() {
        return Arbitraries.longs().between(0L, 1000L);
    }
}
