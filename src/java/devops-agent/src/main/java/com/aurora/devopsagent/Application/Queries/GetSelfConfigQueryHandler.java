package com.aurora.devopsagent.Application.Queries;

import com.aurora.devopsagent.Domain.Entity.DevOpsAgentSelfConfig;
import com.aurora.devopsagent.Infrastructure.Persistence.SelfConfigJpaRepository;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

import java.math.BigDecimal;
import java.util.List;

@Service
public class GetSelfConfigQueryHandler {

    private final SelfConfigJpaRepository selfConfigRepository;

    public GetSelfConfigQueryHandler(SelfConfigJpaRepository selfConfigRepository) {
        this.selfConfigRepository = selfConfigRepository;
    }

    @Transactional(readOnly = true)
    public DevOpsAgentSelfConfig handle() {
        List<DevOpsAgentSelfConfig> list = selfConfigRepository.findAll();
        if (list.isEmpty()) {
            // Default initial self config for DevOps-Agent.
            // Note: Provider, model, and endpoint are strictly owned and managed by AiGovernanceService.
            // maxTokensPerRequest is caller ceiling preference; AiGovernance remains the final authority.
            DevOpsAgentSelfConfig config = new DevOpsAgentSelfConfig();
            config.setModelProvider(null);
            config.setModelName(null);
            config.setApiEndpoint(null);
            config.setMaxTokensPerRequest(4096);
            config.setAlertThresholdUsdPerDay(new BigDecimal("50.0000"));
            return config;
        }
        return list.get(0);
    }
}
