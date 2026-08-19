package com.aurora.aigovernance.governance.domain.valueobject;

import com.aurora.aigovernance.governance.domain.enums.*;

import java.util.Set;

/**
 * Immutable governance policy evaluation result.
 * <p>
 * {@code requireApproval} means downstream business side-effect may need human approval —
 * AI inference itself is still allowed to execute.
 */
public record GovernanceDecision(
        boolean allowed,
        DenyReason denyReason,
        String decisionId,
        Set<AiProvider> allowedProviders,
        Set<String> allowedProviderPools,
        ModelTier modelTier,
        int maxTokens,
        AutomationLevel automationLevel,
        boolean requireApproval,
        String policyVersion
) {
    public static GovernanceDecision denied(DenyReason reason, String decisionId) {
        return new GovernanceDecision(
                false, reason, decisionId,
                Set.of(), Set.of(), null, 0,
                null, false, null
        );
    }
}
