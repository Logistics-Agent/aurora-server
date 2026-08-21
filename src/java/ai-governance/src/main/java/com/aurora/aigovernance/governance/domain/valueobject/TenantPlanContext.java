package com.aurora.aigovernance.governance.domain.valueobject;

import com.aurora.aigovernance.governance.domain.enums.AiProvider;
import com.aurora.aigovernance.governance.domain.enums.TenantStatus;

import java.util.List;
import java.util.Set;
import java.util.UUID;

/**
 * Aggregated tenant + plan context loaded for governance evaluation.
 */
public record TenantPlanContext(
        UUID tenantId,
        TenantStatus status,
        boolean cloudAiEnabled,
        String planCode,
        AiProvider defaultProvider,
        List<QuotaDefinition> quotas,
        Set<String> allowedCapabilities,
        Set<AiProvider> allowedProviders,
        Set<String> allowedProviderPools
) {}
