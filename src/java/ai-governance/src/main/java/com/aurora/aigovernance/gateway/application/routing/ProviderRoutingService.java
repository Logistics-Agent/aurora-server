package com.aurora.aigovernance.gateway.application.routing;

import com.aurora.aigovernance.gateway.domain.entity.ProviderSlot;
import com.aurora.aigovernance.governance.domain.enums.AiProvider;
import com.aurora.aigovernance.shared.domain.AiOperation;
import com.aurora.aigovernance.shared.domain.TokenBudget;

import java.util.List;
import java.util.Set;

public interface ProviderRoutingService {

    /**
     * Get ordered candidate slots for provider execution.
     *
     * @param allowedProviders    set of authorized providers from governance decision
     * @param allowedProviderPools set of authorized pool codes from governance decision
     * @param operation           GENERATE or EMBED
     * @param callerServiceId     authenticated caller workload identity
     * @param tokenBudget         estimated token budget
     * @return ordered list of candidate slots
     */
    List<ProviderSlot> getCandidates(
            Set<AiProvider> allowedProviders,
            Set<String> allowedProviderPools,
            AiOperation operation,
            String callerServiceId,
            TokenBudget tokenBudget
    );
}
