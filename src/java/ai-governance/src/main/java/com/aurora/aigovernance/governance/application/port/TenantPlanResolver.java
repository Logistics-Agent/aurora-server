package com.aurora.aigovernance.governance.application.port;

import com.aurora.aigovernance.governance.domain.valueobject.TenantPlanResult;

import java.util.UUID;

/**
 * Port for resolving tenant plan and status from IamTenant service with caching.
 */
public interface TenantPlanResolver {

    /**
     * Resolves the subscription plan and lifecycle status for a given tenant.
     *
     * @param tenantId UUID of the tenant
     * @return typed {@link TenantPlanResult}
     */
    TenantPlanResult resolve(UUID tenantId);
}
