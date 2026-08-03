package com.aurora.devopsagent.Application.Queries;

import com.aurora.devopsagent.Domain.Entity.DevOpsAgentSelfConfig;
import com.aurora.devopsagent.Infrastructure.Persistence.SelfConfigJpaRepository;
import com.aurora.shared.exception.DomainExceptions;
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
            // Return default initial self config
            DevOpsAgentSelfConfig config = new DevOpsAgentSelfConfig();
            config.setModelProvider("azure_openai");
            config.setModelName("gpt-4o");
            config.setApiEndpoint("https://azure-openai.aurora.internal");
            config.setMaxTokensPerRequest(4096);
            config.setAlertThresholdUsdPerDay(new BigDecimal("50.0000"));
            return config;
        }
        return list.get(0);
    }
}
