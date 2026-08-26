package com.aurora.aigovernance.governance.domain.valueobject;

import com.aurora.aigovernance.governance.domain.enums.TenantStatus;

import java.util.UUID;

/**
 * Typed result from TenantPlanResolver.
 * Avoids returning null on failure and provides explicit failure reasons for governance policy decisions.
 */
public sealed interface TenantPlanResult {

    record TenantPlanInfo(
            UUID tenantId,
            String planCode,
            TenantStatus status,
            boolean cloudAiEnabled
    ) {}

    record Success(TenantPlanInfo info) implements TenantPlanResult {}

    record NotFound(String reason) implements TenantPlanResult {}

    record Suspended(String reason) implements TenantPlanResult {}

    record IamUnavailable(String reason) implements TenantPlanResult {}

    record PlanNotConfigured(String planCode, String reason) implements TenantPlanResult {}
}
