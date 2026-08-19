package com.aurora.aigovernance.governance.application.service;

import com.aurora.aigovernance.governance.domain.entity.Plan;
import com.aurora.aigovernance.governance.domain.entity.PlanCapability;
import com.aurora.aigovernance.governance.domain.entity.PlanQuota;
import com.aurora.aigovernance.governance.domain.entity.Tenant;
import com.aurora.aigovernance.governance.domain.enums.AiProvider;
import com.aurora.aigovernance.governance.domain.valueobject.QuotaDefinition;
import com.aurora.aigovernance.governance.domain.valueobject.TenantPlanContext;
import com.aurora.aigovernance.governance.infrastructure.persistence.TenantRepository;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.cache.annotation.CacheEvict;
import org.springframework.cache.annotation.Cacheable;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

import java.util.*;
import java.util.stream.Collectors;

/**
 * Cache-aware service for loading tenant + plan context.
 * <p>
 * Uses Caffeine local cache backed by PostgreSQL.
 */
@Service
public class TenantCacheService {

    private static final Logger log = LoggerFactory.getLogger(TenantCacheService.class);

    private final TenantRepository tenantRepository;

    public TenantCacheService(TenantRepository tenantRepository) {
        this.tenantRepository = tenantRepository;
    }

    /**
     * Load aggregated tenant + plan context. Returns null if tenant not found.
     */
    @Cacheable(value = "tenant-plan-contexts", key = "#tenantId", unless = "#result == null")
    @Transactional(readOnly = true)
    public TenantPlanContext loadContext(UUID tenantId) {
        log.debug("Loading TenantPlanContext from database for tenantId: {}", tenantId);
        Optional<Tenant> tenantOpt = tenantRepository.findByExternalTenantIdWithPlan(tenantId);
        if (tenantOpt.isEmpty()) {
            return null;
        }

        Tenant tenant = tenantOpt.get();
        Plan plan = tenant.getPlan();

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

        // Shared AI pool is default for tenant services
        Set<String> allowedPools = Set.of("shared-ai");

        return new TenantPlanContext(
                tenant.getExternalTenantId(),
                tenant.getStatus(),
                tenant.isCloudAiEnabled() && plan.isCloudAiEnabled(),
                plan.getCode(),
                plan.getDefaultProvider(),
                quotas,
                allowedCapabilities,
                allowedProviders,
                allowedPools
        );
    }

    /**
     * Evict cached context for a tenant (e.g. on plan change event).
     */
    @CacheEvict(value = "tenant-plan-contexts", key = "#tenantId")
    public void evict(UUID tenantId) {
        log.info("Evicted TenantPlanContext cache for tenantId: {}", tenantId);
    }
}
