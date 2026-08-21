package com.aurora.aigovernance.governance.application.service;

import com.aurora.aigovernance.governance.application.port.TenantQuotaPort;
import com.aurora.aigovernance.governance.domain.enums.*;
import com.aurora.aigovernance.governance.domain.valueobject.*;
import com.aurora.aigovernance.shared.domain.AiOperation;
import com.aurora.aigovernance.shared.domain.TokenBudget;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.extension.ExtendWith;
import org.mockito.Mock;
import org.mockito.junit.jupiter.MockitoExtension;

import java.util.List;
import java.util.Set;
import java.util.UUID;

import static org.junit.jupiter.api.Assertions.*;
import static org.mockito.ArgumentMatchers.any;
import static org.mockito.ArgumentMatchers.eq;
import static org.mockito.Mockito.when;

@ExtendWith(MockitoExtension.class)
public class GovernancePolicyServiceTest {

    @Mock
    private TenantCacheService tenantCacheService;

    @Mock
    private TenantQuotaPort tenantQuotaPort;

    private PeriodKeyCalculator periodKeyCalculator;
    private GovernancePolicyService governancePolicyService;

    private final UUID tenantId = UUID.randomUUID();

    @BeforeEach
    public void setup() {
        periodKeyCalculator = new PeriodKeyCalculator();
        governancePolicyService = new GovernancePolicyService(
                tenantCacheService,
                tenantQuotaPort,
                periodKeyCalculator
        );
    }

    @Test
    public void testTenantNotFound_ReturnsDenied() {
        when(tenantCacheService.loadContext(tenantId)).thenReturn(null);

        GovernanceDecision decision = governancePolicyService.evaluate(
                tenantId, "service-a", "compliance.answer",
                AiOperation.GENERATE, new TokenBudget(100, 100)
        );

        assertFalse(decision.allowed());
        assertEquals(DenyReason.TENANT_NOT_FOUND, decision.denyReason());
    }

    @Test
    public void testTenantSuspended_ReturnsDenied() {
        TenantPlanContext context = new TenantPlanContext(
                tenantId, TenantStatus.SUSPENDED, true, "STANDARD",
                AiProvider.GEMINI, List.of(), Set.of("compliance.answer"),
                Set.of(AiProvider.GEMINI), Set.of("shared-ai")
        );
        when(tenantCacheService.loadContext(tenantId)).thenReturn(context);

        GovernanceDecision decision = governancePolicyService.evaluate(
                tenantId, "service-a", "compliance.answer",
                AiOperation.GENERATE, new TokenBudget(100, 100)
        );

        assertFalse(decision.allowed());
        assertEquals(DenyReason.TENANT_SUSPENDED, decision.denyReason());
    }

    @Test
    public void testCloudAiDisabled_ReturnsDenied() {
        TenantPlanContext context = new TenantPlanContext(
                tenantId, TenantStatus.ACTIVE, false, "STANDARD",
                AiProvider.GEMINI, List.of(), Set.of("compliance.answer"),
                Set.of(AiProvider.GEMINI), Set.of("shared-ai")
        );
        when(tenantCacheService.loadContext(tenantId)).thenReturn(context);

        GovernanceDecision decision = governancePolicyService.evaluate(
                tenantId, "service-a", "compliance.answer",
                AiOperation.GENERATE, new TokenBudget(100, 100)
        );

        assertFalse(decision.allowed());
        assertEquals(DenyReason.CLOUD_AI_DISABLED, decision.denyReason());
    }

    @Test
    public void testCapabilityNotAllowed_ReturnsDenied() {
        TenantPlanContext context = new TenantPlanContext(
                tenantId, TenantStatus.ACTIVE, true, "FREE",
                AiProvider.GEMINI, List.of(), Set.of("ocr.extract"),
                Set.of(AiProvider.GEMINI), Set.of("shared-ai")
        );
        when(tenantCacheService.loadContext(tenantId)).thenReturn(context);

        GovernanceDecision decision = governancePolicyService.evaluate(
                tenantId, "service-a", "compliance.answer",
                AiOperation.GENERATE, new TokenBudget(100, 100)
        );

        assertFalse(decision.allowed());
        assertEquals(DenyReason.CAPABILITY_NOT_ALLOWED, decision.denyReason());
    }

    @Test
    public void testQuotaExceeded_ReturnsDenied() {
        QuotaDefinition rpmQuota = new QuotaDefinition(QuotaMetric.REQUESTS, QuotaPeriod.MINUTE, 10);
        TenantPlanContext context = new TenantPlanContext(
                tenantId, TenantStatus.ACTIVE, true, "STANDARD",
                AiProvider.GEMINI, List.of(rpmQuota), Set.of("compliance.answer"),
                Set.of(AiProvider.GEMINI), Set.of("shared-ai")
        );
        when(tenantCacheService.loadContext(tenantId)).thenReturn(context);
        // Current usage is 10 which exceeds 95% of 10 (=9)
        when(tenantQuotaPort.getCurrentUsage(eq(tenantId), eq(QuotaMetric.REQUESTS), eq(QuotaPeriod.MINUTE), any(PeriodKey.class)))
                .thenReturn(10L);

        GovernanceDecision decision = governancePolicyService.evaluate(
                tenantId, "service-a", "compliance.answer",
                AiOperation.GENERATE, new TokenBudget(100, 100)
        );

        assertFalse(decision.allowed());
        assertEquals(DenyReason.QUOTA_EXCEEDED, decision.denyReason());
    }

    @Test
    public void testSuccessfulEvaluation_ReturnsAllowed() {
        QuotaDefinition rpmQuota = new QuotaDefinition(QuotaMetric.REQUESTS, QuotaPeriod.MINUTE, 10);
        TenantPlanContext context = new TenantPlanContext(
                tenantId, TenantStatus.ACTIVE, true, "STANDARD",
                AiProvider.GEMINI, List.of(rpmQuota), Set.of("compliance.answer"),
                Set.of(AiProvider.GEMINI), Set.of("shared-ai")
        );
        when(tenantCacheService.loadContext(tenantId)).thenReturn(context);
        when(tenantQuotaPort.getCurrentUsage(eq(tenantId), eq(QuotaMetric.REQUESTS), eq(QuotaPeriod.MINUTE), any(PeriodKey.class)))
                .thenReturn(0L);

        GovernanceDecision decision = governancePolicyService.evaluate(
                tenantId, "service-a", "compliance.answer",
                AiOperation.GENERATE, new TokenBudget(100, 100)
        );

        assertTrue(decision.allowed());
        assertNull(decision.denyReason());
        assertNotNull(decision.decisionId());
        assertTrue(decision.allowedProviders().contains(AiProvider.GEMINI));
    }
}
