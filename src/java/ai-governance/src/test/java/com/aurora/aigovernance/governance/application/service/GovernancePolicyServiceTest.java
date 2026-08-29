package com.aurora.aigovernance.governance.application.service;

import com.aurora.aigovernance.governance.application.port.TenantPlanResolver;
import com.aurora.aigovernance.governance.application.port.TenantQuotaPort;
import com.aurora.aigovernance.governance.domain.entity.Plan;
import com.aurora.aigovernance.governance.domain.entity.PlanCapability;
import com.aurora.aigovernance.governance.domain.entity.PlanQuota;
import com.aurora.aigovernance.governance.domain.enums.*;
import com.aurora.aigovernance.governance.domain.valueobject.*;
import com.aurora.aigovernance.governance.infrastructure.persistence.PlanRepository;
import com.aurora.aigovernance.shared.domain.AiOperation;
import com.aurora.aigovernance.shared.domain.TokenBudget;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.extension.ExtendWith;
import org.mockito.Mock;
import org.mockito.junit.jupiter.MockitoExtension;

import java.util.List;
import java.util.Optional;
import java.util.UUID;

import static org.junit.jupiter.api.Assertions.*;
import static org.mockito.ArgumentMatchers.any;
import static org.mockito.ArgumentMatchers.eq;
import static org.mockito.Mockito.mock;
import static org.mockito.Mockito.when;

@ExtendWith(MockitoExtension.class)
public class GovernancePolicyServiceTest {

    @Mock
    private TenantPlanResolver tenantPlanResolver;

    @Mock
    private PlanRepository planRepository;

    @Mock
    private TenantQuotaPort tenantQuotaPort;

    private PeriodKeyCalculator periodKeyCalculator;
    private GovernancePolicyService governancePolicyService;

    private final UUID tenantId = UUID.randomUUID();

    @BeforeEach
    public void setup() {
        periodKeyCalculator = new PeriodKeyCalculator();
        governancePolicyService = new GovernancePolicyService(
                tenantPlanResolver,
                planRepository,
                tenantQuotaPort,
                periodKeyCalculator
        );
    }

    private Plan createStandardPlan(List<PlanQuota> quotas, String capabilities) {
        Plan plan = mock(Plan.class);
        when(plan.getCode()).thenReturn("STANDARD");
        when(plan.isCloudAiEnabled()).thenReturn(true);
        when(plan.getDefaultProvider()).thenReturn(AiProvider.GEMINI);
        when(plan.getQuotas()).thenReturn(quotas != null ? quotas : List.of());

        PlanCapability cap = mock(PlanCapability.class);
        when(cap.getCapabilityCode()).thenReturn(capabilities);
        when(cap.getAllowedProviders()).thenReturn("GEMINI");
        when(plan.getCapabilities()).thenReturn(List.of(cap));

        return plan;
    }

    @Test
    public void testTenantNotFound_ReturnsDenied() {
        when(tenantPlanResolver.resolve(tenantId)).thenReturn(new TenantPlanResult.NotFound("Not found in Iam"));

        GovernanceDecision decision = governancePolicyService.evaluate(
                tenantId, "service-a", "compliance.answer",
                AiOperation.GENERATE, new TokenBudget(100, 100)
        );

        assertFalse(decision.allowed());
        assertEquals(DenyReason.TENANT_NOT_FOUND, decision.denyReason());
    }

    @Test
    public void testTenantSuspended_ReturnsDenied() {
        when(tenantPlanResolver.resolve(tenantId)).thenReturn(new TenantPlanResult.Suspended("Suspended"));

        GovernanceDecision decision = governancePolicyService.evaluate(
                tenantId, "service-a", "compliance.answer",
                AiOperation.GENERATE, new TokenBudget(100, 100)
        );

        assertFalse(decision.allowed());
        assertEquals(DenyReason.TENANT_SUSPENDED, decision.denyReason());
    }

    @Test
    public void testIamUnavailable_FailsClosedWithPolicyError() {
        when(tenantPlanResolver.resolve(tenantId)).thenReturn(new TenantPlanResult.IamUnavailable("gRPC Down"));

        GovernanceDecision decision = governancePolicyService.evaluate(
                tenantId, "service-a", "compliance.answer",
                AiOperation.GENERATE, new TokenBudget(100, 100)
        );

        assertFalse(decision.allowed());
        assertEquals(DenyReason.POLICY_ERROR, decision.denyReason());
    }

    @Test
    public void testCapabilityNotAllowed_ReturnsDenied() {
        TenantPlanResult.TenantPlanInfo info = new TenantPlanResult.TenantPlanInfo(
                tenantId, "STANDARD", TenantStatus.ACTIVE, true
        );
        when(tenantPlanResolver.resolve(tenantId)).thenReturn(new TenantPlanResult.Success(info));

        Plan plan = createStandardPlan(List.of(), "ocr.extract");
        when(planRepository.findByCode("STANDARD")).thenReturn(Optional.of(plan));

        GovernanceDecision decision = governancePolicyService.evaluate(
                tenantId, "service-a", "compliance.answer",
                AiOperation.GENERATE, new TokenBudget(100, 100)
        );

        assertFalse(decision.allowed());
        assertEquals(DenyReason.CAPABILITY_NOT_ALLOWED, decision.denyReason());
    }

    @Test
    public void testQuotaExceeded_ReturnsDenied() {
        TenantPlanResult.TenantPlanInfo info = new TenantPlanResult.TenantPlanInfo(
                tenantId, "STANDARD", TenantStatus.ACTIVE, true
        );
        when(tenantPlanResolver.resolve(tenantId)).thenReturn(new TenantPlanResult.Success(info));

        PlanQuota quota = mock(PlanQuota.class);
        when(quota.getQuotaMetric()).thenReturn(QuotaMetric.REQUESTS);
        when(quota.getQuotaPeriod()).thenReturn(QuotaPeriod.MINUTE);
        when(quota.getLimitValue()).thenReturn(10L);

        Plan plan = createStandardPlan(List.of(quota), "compliance.answer");
        when(planRepository.findByCode("STANDARD")).thenReturn(Optional.of(plan));

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
        TenantPlanResult.TenantPlanInfo info = new TenantPlanResult.TenantPlanInfo(
                tenantId, "STANDARD", TenantStatus.ACTIVE, true
        );
        when(tenantPlanResolver.resolve(tenantId)).thenReturn(new TenantPlanResult.Success(info));

        PlanQuota quota = mock(PlanQuota.class);
        when(quota.getQuotaMetric()).thenReturn(QuotaMetric.REQUESTS);
        when(quota.getQuotaPeriod()).thenReturn(QuotaPeriod.MINUTE);
        when(quota.getLimitValue()).thenReturn(10L);

        Plan plan = createStandardPlan(List.of(quota), "compliance.answer");
        when(planRepository.findByCode("STANDARD")).thenReturn(Optional.of(plan));

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
