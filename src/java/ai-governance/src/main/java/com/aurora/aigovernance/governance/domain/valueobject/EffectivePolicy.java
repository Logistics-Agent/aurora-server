package com.aurora.aigovernance.governance.domain.valueobject;

import com.aurora.aigovernance.governance.domain.enums.AiProvider;
import com.aurora.aigovernance.governance.domain.enums.AutomationLevel;
import com.aurora.aigovernance.governance.domain.enums.ModelTier;

import java.util.Set;

/**
 * Resolved effective policy for a specific capability after plan/capability hierarchy merge.
 */
public record EffectivePolicy(
        Set<AiProvider> allowedProviders,
        Set<String> allowedProviderPools,
        ModelTier modelTier,
        int maxTokens,
        AutomationLevel automationLevel,
        boolean requireApproval
) {}
