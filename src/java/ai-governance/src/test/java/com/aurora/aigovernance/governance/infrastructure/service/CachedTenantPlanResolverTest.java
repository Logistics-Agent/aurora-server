package com.aurora.aigovernance.governance.infrastructure.service;

import com.aurora.aigovernance.governance.application.port.TenantPlanResolver;
import com.aurora.aigovernance.governance.application.port.TenantQuotaPort;
import com.aurora.aigovernance.governance.application.service.GovernancePolicyService;
import com.aurora.aigovernance.governance.application.service.PeriodKeyCalculator;
import com.aurora.aigovernance.governance.domain.entity.Plan;
import com.aurora.aigovernance.governance.domain.entity.PlanCapability;
import com.aurora.aigovernance.governance.domain.enums.AiProvider;
import com.aurora.aigovernance.governance.domain.enums.DenyReason;
import com.aurora.aigovernance.governance.domain.enums.TenantStatus;
import com.aurora.aigovernance.governance.domain.valueobject.GovernanceDecision;
import com.aurora.aigovernance.governance.domain.valueobject.TenantPlanResult;
import com.aurora.aigovernance.governance.infrastructure.persistence.PlanRepository;
import com.aurora.aigovernance.shared.domain.AiOperation;
import com.aurora.aigovernance.shared.domain.TokenBudget;
import com.fasterxml.jackson.databind.ObjectMapper;
import com.aurora.common.grpc.PlanType;
import iam.IamServiceGrpc;
import iam.IamTenant;
import io.grpc.Status;
import io.grpc.StatusRuntimeException;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.mockito.ArgumentCaptor;
import org.springframework.data.redis.RedisConnectionFailureException;
import org.springframework.data.redis.core.StringRedisTemplate;
import org.springframework.data.redis.core.ValueOperations;

import java.time.Duration;
import java.util.*;

import static org.junit.jupiter.api.Assertions.*;
import static org.mockito.ArgumentMatchers.*;
import static org.mockito.Mockito.*;

public class CachedTenantPlanResolverTest {

    private StringRedisTemplate redisTemplate;
    private ValueOperations<String, String> valueOperations;
    private ObjectMapper objectMapper;
    private IamServiceGrpc.IamServiceBlockingStub iamClient;
    private CachedTenantPlanResolver resolver;

    private PlanRepository planRepository;
    private TenantQuotaPort tenantQuotaPort;
    private PeriodKeyCalculator periodKeyCalculator;
    private GovernancePolicyService governancePolicyService;

    private static final UUID TEST_TENANT_ID = UUID.fromString("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static final String CACHE_KEY = "ai-governance:tenant-plan:" + TEST_TENANT_ID;

    @BeforeEach
    void setUp() {
        redisTemplate = mock(StringRedisTemplate.class);
        valueOperations = mock(ValueOperations.class);
        when(redisTemplate.opsForValue()).thenReturn(valueOperations);

        objectMapper = new ObjectMapper();
        iamClient = mock(IamServiceGrpc.IamServiceBlockingStub.class);
        when(iamClient.withDeadlineAfter(anyLong(), any())).thenReturn(iamClient);

        resolver = new CachedTenantPlanResolver(
                redisTemplate,
                objectMapper,
                iamClient,
                Duration.ofHours(1)
        );

        planRepository = mock(PlanRepository.class);
        tenantQuotaPort = mock(TenantQuotaPort.class);
        periodKeyCalculator = mock(PeriodKeyCalculator.class);

        governancePolicyService = new GovernancePolicyService(
                resolver,
                planRepository,
                tenantQuotaPort,
                periodKeyCalculator
        );
    }

    @Test
    @DisplayName("1. CacheHit_DoesNotCallIamTenant: Valid cached entry returns immediately without invoking gRPC")
    void cacheHit_DoesNotCallIamTenant() throws Exception {
        CachedTenantPlanResolver.TenantPlanCacheEntry entry =
                new CachedTenantPlanResolver.TenantPlanCacheEntry("STANDARD", "ACTIVE", true);
        String json = objectMapper.writeValueAsString(entry);

        when(valueOperations.get(CACHE_KEY)).thenReturn(json);

        TenantPlanResult result = resolver.resolve(TEST_TENANT_ID);

        assertInstanceOf(TenantPlanResult.Success.class, result);
        TenantPlanResult.Success success = (TenantPlanResult.Success) result;
        assertEquals(TEST_TENANT_ID, success.info().tenantId());
        assertEquals("STANDARD", success.info().planCode());
        assertEquals(TenantStatus.ACTIVE, success.info().status());
        assertTrue(success.info().cloudAiEnabled());

        verify(iamClient, never()).getTenant(any());
    }

    @Test
    @DisplayName("2. CacheMiss_CallsIamTenantAndCachesPlan: Miss invokes IamTenant and stores plan in Redis")
    void cacheMiss_CallsIamTenantAndCachesPlan() {
        when(valueOperations.get(CACHE_KEY)).thenReturn(null);

        IamTenant.TenantResponse response = IamTenant.TenantResponse.newBuilder()
                .setId(TEST_TENANT_ID.toString())
                .setPlanType(com.aurora.common.grpc.PlanType.ENTERPRISE)
                .setStatus(com.aurora.common.grpc.TenantStatus.TENANT_STATUS_ACTIVE)
                .build();

        when(iamClient.getTenant(any())).thenReturn(response);

        TenantPlanResult result = resolver.resolve(TEST_TENANT_ID);

        assertInstanceOf(TenantPlanResult.Success.class, result);
        TenantPlanResult.Success success = (TenantPlanResult.Success) result;
        assertEquals("ENTERPRISE", success.info().planCode());
        assertEquals(TenantStatus.ACTIVE, success.info().status());

        verify(iamClient, times(1)).getTenant(argThat(req -> req.getId().equals(TEST_TENANT_ID.toString())));
        verify(valueOperations, times(1)).set(eq(CACHE_KEY), anyString(), eq(Duration.ofHours(1)));
    }

    @Test
    @DisplayName("3. CacheExpired_RefreshesPlan: Expired cache (null in Redis) fetches fresh state from IamTenant")
    void cacheExpired_RefreshesPlan() {
        // Cache expired -> null
        when(valueOperations.get(CACHE_KEY)).thenReturn(null);

        IamTenant.TenantResponse response = IamTenant.TenantResponse.newBuilder()
                .setId(TEST_TENANT_ID.toString())
                .setPlanType(com.aurora.common.grpc.PlanType.STANDARD)
                .setStatus(com.aurora.common.grpc.TenantStatus.TENANT_STATUS_ACTIVE)
                .build();

        when(iamClient.getTenant(any())).thenReturn(response);

        TenantPlanResult result = resolver.resolve(TEST_TENANT_ID);

        assertInstanceOf(TenantPlanResult.Success.class, result);
        TenantPlanResult.Success success = (TenantPlanResult.Success) result;
        assertEquals("STANDARD", success.info().planCode());
        verify(iamClient, times(1)).getTenant(any());
        verify(valueOperations, times(1)).set(eq(CACHE_KEY), anyString(), eq(Duration.ofHours(1)));
    }

    @Test
    @DisplayName("4. RedisUnavailable_FallsBackToIamTenant: Redis connection failure triggers graceful fallback to IamTenant")
    void redisUnavailable_FallsBackToIamTenant() {
        when(valueOperations.get(CACHE_KEY)).thenThrow(new RedisConnectionFailureException("Redis connection refused"));

        IamTenant.TenantResponse response = IamTenant.TenantResponse.newBuilder()
                .setId(TEST_TENANT_ID.toString())
                .setPlanType(com.aurora.common.grpc.PlanType.ENTERPRISE)
                .setStatus(com.aurora.common.grpc.TenantStatus.TENANT_STATUS_ACTIVE)
                .build();

        when(iamClient.getTenant(any())).thenReturn(response);

        TenantPlanResult result = resolver.resolve(TEST_TENANT_ID);

        assertInstanceOf(TenantPlanResult.Success.class, result);
        TenantPlanResult.Success success = (TenantPlanResult.Success) result;
        assertEquals("ENTERPRISE", success.info().planCode());
        verify(iamClient, times(1)).getTenant(any());
    }

    @Test
    @DisplayName("5. IamTenantUnavailableAndCacheMiss_FailsClosed: When both cache misses and IamTenant fails, returns typed error and fails closed")
    void iamTenantUnavailableAndCacheMiss_FailsClosed() {
        when(valueOperations.get(CACHE_KEY)).thenReturn(null);
        when(iamClient.getTenant(any())).thenThrow(new StatusRuntimeException(Status.UNAVAILABLE.withDescription("Service Down")));

        TenantPlanResult result = resolver.resolve(TEST_TENANT_ID);

        assertInstanceOf(TenantPlanResult.IamUnavailable.class, result);
        TenantPlanResult.IamUnavailable unavailable = (TenantPlanResult.IamUnavailable) result;
        assertTrue(unavailable.reason().contains("Service Down"));

        // Evaluate in GovernancePolicyService -> Must DENY fail-closed
        GovernanceDecision decision = governancePolicyService.evaluate(
                TEST_TENANT_ID,
                "customer-assistant",
                "assistant.general",
                AiOperation.GENERATE,
                new TokenBudget(100, 100)
        );

        assertFalse(decision.allowed());
        assertEquals(DenyReason.POLICY_ERROR, decision.denyReason());
    }

    @Test
    @DisplayName("6. InternalServiceContext_DoesNotResolveTenantPlan: Internal platform workload (devops-agent) bypasses TenantPlanResolver")
    void internalServiceContext_DoesNotResolveTenantPlan() {
        Plan enterprisePlan = mock(Plan.class);
        when(enterprisePlan.getCode()).thenReturn("ENTERPRISE");
        when(enterprisePlan.isCloudAiEnabled()).thenReturn(true);
        when(enterprisePlan.getDefaultProvider()).thenReturn(AiProvider.AZURE_OPENAI);
        when(enterprisePlan.getQuotas()).thenReturn(Collections.emptyList());

        PlanCapability cap = mock(PlanCapability.class);
        when(cap.getCapabilityCode()).thenReturn("devops.diagnose");
        when(cap.getAllowedProviders()).thenReturn("AZURE_OPENAI,GEMINI");
        when(enterprisePlan.getCapabilities()).thenReturn(List.of(cap));

        when(planRepository.findByCode("ENTERPRISE")).thenReturn(Optional.of(enterprisePlan));

        // Evaluate for internal service without tenantId
        GovernanceDecision decision = governancePolicyService.evaluate(
                null, // No tenantId for internal platform service
                "devops-agent",
                "devops.diagnose",
                AiOperation.GENERATE,
                new TokenBudget(500, 500)
        );

        assertTrue(decision.allowed());
        assertNull(decision.denyReason());
        // Verify TenantPlanResolver / IamTenant was NEVER called
        verify(valueOperations, never()).get(any());
        verify(iamClient, never()).getTenant(any());
    }

    @Test
    @DisplayName("7. TenantServiceMissingTenantId_RejectedWithoutBypass: Non-internal caller missing tenantId is rejected")
    void tenantServiceMissingTenantId_RejectedWithoutBypass() {
        GovernanceDecision decision = governancePolicyService.evaluate(
                null,
                "customer-assistant",
                "assistant.general",
                AiOperation.GENERATE,
                new TokenBudget(100, 100)
        );

        assertFalse(decision.allowed());
        assertEquals(DenyReason.TENANT_NOT_FOUND, decision.denyReason());
        verify(iamClient, never()).getTenant(any());
    }
}
