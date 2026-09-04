package com.aurora.aigovernance.governance.infrastructure.service;

import java.time.Duration;
import java.util.UUID;
import java.util.concurrent.TimeUnit;

import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.beans.factory.annotation.Value;
import org.springframework.data.redis.core.StringRedisTemplate;
import org.springframework.stereotype.Service;

import com.aurora.aigovernance.governance.application.port.TenantPlanResolver;
import com.aurora.aigovernance.governance.domain.enums.TenantStatus;
import com.aurora.aigovernance.governance.domain.valueobject.TenantPlanResult;
import com.fasterxml.jackson.databind.ObjectMapper;

import iam.IamServiceGrpc;
import iam.IamTenant;
import io.grpc.Status;
import io.grpc.StatusRuntimeException;
import net.devh.boot.grpc.client.inject.GrpcClient;
import org.springframework.beans.factory.annotation.Autowired;
/**
 * Implementation of TenantPlanResolver that caches only {planCode, status, cloudAiEnabled} in Redis
 * with long TTL (default 1 hour).
 * <p>
 * Fault Tolerance:
 * - Cache Hit: Returns cached plan info immediately.
 * - Cache Miss: Fetches from IamTenant gRPC and caches in Redis.
 * - Redis Error: Logs warning and falls back to calling IamTenant gRPC directly.
 * - IamTenant Error + Cache Miss: Fails closed with typed error result.
 */
@Service
public class CachedTenantPlanResolver implements TenantPlanResolver {

    private static final Logger log = LoggerFactory.getLogger(CachedTenantPlanResolver.class);
    private static final String CACHE_KEY_PREFIX = "ai-governance:tenant-plan:";
    private static final long GRPC_TIMEOUT_SECONDS = 5;

    private final StringRedisTemplate redisTemplate;
    private final ObjectMapper objectMapper;
    private final Duration cacheTtl;

    @GrpcClient("iam-service")
    @Autowired
    private IamServiceGrpc.IamServiceBlockingStub iamClient;

    public CachedTenantPlanResolver(
            StringRedisTemplate redisTemplate,
            ObjectMapper objectMapper,
            @Value("${ai-governance.tenant-plan.cache-ttl:PT1H}") Duration cacheTtl) {
        this.redisTemplate = redisTemplate;
        this.objectMapper = objectMapper;
        this.cacheTtl = cacheTtl;
    }

    // Constructor for testing with injected stub
    public CachedTenantPlanResolver(
            StringRedisTemplate redisTemplate,
            ObjectMapper objectMapper,
            IamServiceGrpc.IamServiceBlockingStub iamClient,
            Duration cacheTtl) {
        this.redisTemplate = redisTemplate;
        this.objectMapper = objectMapper;
        this.iamClient = iamClient;
        this.cacheTtl = cacheTtl;
    }

    public record TenantPlanCacheEntry(
            String planCode,
            String status,
            boolean cloudAiEnabled
    ) {}

    @Override
    public TenantPlanResult resolve(UUID tenantId) {
        if (tenantId == null) {
            return new TenantPlanResult.NotFound("Tenant ID is null");
        }

        String cacheKey = CACHE_KEY_PREFIX + tenantId;

        // 1. Try L2 Redis Cache
        try {
            String cachedJson = redisTemplate.opsForValue().get(cacheKey);
            if (cachedJson != null && !cachedJson.isBlank()) {
                TenantPlanCacheEntry entry = objectMapper.readValue(cachedJson, TenantPlanCacheEntry.class);
                TenantStatus tenantStatus = parseTenantStatus(entry.status());
                log.debug("TenantPlan cache HIT for tenantId={}, planCode={}, status={}",
                        tenantId, entry.planCode(), entry.status());

                if (tenantStatus == TenantStatus.SUSPENDED) {
                    return new TenantPlanResult.Suspended("Tenant " + tenantId + " is suspended (from cache)");
                }

                return new TenantPlanResult.Success(new TenantPlanResult.TenantPlanInfo(
                        tenantId,
                        entry.planCode(),
                        tenantStatus,
                        entry.cloudAiEnabled()
                ));
            }
        } catch (Exception e) {
            log.warn("Redis error reading tenant plan cache for tenantId={}: {}. Falling back to IamTenant gRPC.",
                    tenantId, e.getMessage());
        }

        // 2. Cache Miss or Redis Error -> Call IamTenant gRPC
        log.debug("TenantPlan cache MISS for tenantId={}, querying IamTenant service...", tenantId);
        return fetchFromIamTenant(tenantId, cacheKey);
    }

    private TenantPlanResult fetchFromIamTenant(UUID tenantId, String cacheKey) {
        if (iamClient == null) {
            log.error("IamTenant gRPC client is not initialized. Failing closed for tenantId={}", tenantId);
            return new TenantPlanResult.IamUnavailable("IamTenant gRPC client not configured");
        }

        IamTenant.GetTenantRequest request = IamTenant.GetTenantRequest.newBuilder()
                .setId(tenantId.toString())
                .build();

        IamTenant.TenantResponse response;
        try {
            response = iamClient.withDeadlineAfter(GRPC_TIMEOUT_SECONDS, TimeUnit.SECONDS)
                    .getTenant(request);
        } catch (StatusRuntimeException sre) {
            Status.Code code = sre.getStatus().getCode();
            if (code == Status.Code.NOT_FOUND || code == Status.Code.INVALID_ARGUMENT) {
                log.warn("IamTenant reported tenant not found: tenantId={}, code={}", tenantId, code);
                return new TenantPlanResult.NotFound("Tenant not found in IamTenant: " + sre.getStatus().getDescription());
            }

            log.error("IamTenant gRPC call failed for tenantId={}: status={}", tenantId, sre.getStatus());
            return new TenantPlanResult.IamUnavailable("IamTenant gRPC service unavailable: " + sre.getStatus().getDescription());
        } catch (Exception ex) {
            log.error("Unexpected error invoking IamTenant gRPC for tenantId={}", tenantId, ex);
            return new TenantPlanResult.IamUnavailable("Unexpected error calling IamTenant: " + ex.getMessage());
        }

        // Map PlanType enum to plan code
        String planCode = mapPlanTypeToCode(response.getPlanType());
        TenantStatus tenantStatus = mapTenantStatus(response.getStatus());
        boolean cloudAiEnabled = tenantStatus == TenantStatus.ACTIVE;

        // 3. Populate Redis Cache
        try {
            TenantPlanCacheEntry cacheEntry = new TenantPlanCacheEntry(planCode, tenantStatus.name(), cloudAiEnabled);
            String jsonToCache = objectMapper.writeValueAsString(cacheEntry);
            redisTemplate.opsForValue().set(cacheKey, jsonToCache, cacheTtl);
            log.debug("Cached TenantPlan in Redis for tenantId={}, planCode={}, status={}, ttl={}",
                    tenantId, planCode, tenantStatus, cacheTtl);
        } catch (Exception e) {
            log.warn("Failed to write TenantPlan to Redis for tenantId={}: {}", tenantId, e.getMessage());
        }

        if (tenantStatus == TenantStatus.SUSPENDED) {
            return new TenantPlanResult.Suspended("Tenant " + tenantId + " is suspended");
        }

        return new TenantPlanResult.Success(new TenantPlanResult.TenantPlanInfo(
                tenantId,
                planCode,
                tenantStatus,
                cloudAiEnabled
        ));
    }

    private String mapPlanTypeToCode(com.aurora.common.grpc.PlanType planType) {
        return switch (planType) {
            case ENTERPRISE -> "ENTERPRISE";
            case STANDARD, PLAN_TYPE_UNSPECIFIED, UNRECOGNIZED -> "STANDARD";
        };
    }

    private TenantStatus mapTenantStatus(com.aurora.common.grpc.TenantStatus status) {
        return switch (status) {
            case TENANT_STATUS_SUSPENDED -> TenantStatus.SUSPENDED;
            case TENANT_STATUS_ACTIVE, TENANT_STATUS_UNSPECIFIED, UNRECOGNIZED -> TenantStatus.ACTIVE;
        };
    }

    private TenantStatus parseTenantStatus(String statusStr) {
        if (statusStr == null) return TenantStatus.ACTIVE;
        try {
            return TenantStatus.valueOf(statusStr);
        } catch (IllegalArgumentException e) {
            return TenantStatus.ACTIVE;
        }
    }
}
