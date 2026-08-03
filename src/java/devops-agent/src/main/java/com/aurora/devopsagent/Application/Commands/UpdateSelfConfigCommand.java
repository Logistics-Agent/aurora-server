package com.aurora.devopsagent.Application.Commands;

import com.aurora.devopsagent.Domain.Entity.DevOpsAgentSelfConfig;
import com.aurora.devopsagent.Infrastructure.Persistence.SelfConfigJpaRepository;
import com.aurora.shared.exception.DomainExceptions;
import com.aurora.shared.security.CurrentUserContext;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

import java.math.BigDecimal;
import java.util.List;

public record UpdateSelfConfigCommand(
        String modelProvider,
        String modelName,
        String apiEndpoint,
        int maxTokensPerRequest,
        BigDecimal alertThresholdUsdPerDay
) {}

@Service
public class UpdateSelfConfigCommandHandler {

    private static final Logger log = LoggerFactory.getLogger(UpdateSelfConfigCommandHandler.class);

    private final SelfConfigJpaRepository selfConfigRepository;

    public UpdateSelfConfigCommandHandler(SelfConfigJpaRepository selfConfigRepository) {
        this.selfConfigRepository = selfConfigRepository;
    }

    @Transactional
    public DevOpsAgentSelfConfig handle(UpdateSelfConfigCommand command) {
        CurrentUserContext context = CurrentUserContext.getCurrent();
        if (context == null || !context.isSystemAdmin()) {
            throw new DomainExceptions.ForbiddenException("Only SYSTEM_ADMIN can update SelfConfig.");
        }

        List<DevOpsAgentSelfConfig> configs = selfConfigRepository.findAll();
        DevOpsAgentSelfConfig config = configs.isEmpty() ? new DevOpsAgentSelfConfig() : configs.get(0);

        config.setModelProvider(command.modelProvider());
        config.setModelName(command.modelName());
        config.setApiEndpoint(command.apiEndpoint());
        config.setMaxTokensPerRequest(command.maxTokensPerRequest());
        config.setAlertThresholdUsdPerDay(command.alertThresholdUsdPerDay());

        DevOpsAgentSelfConfig saved = selfConfigRepository.save(config);
        log.info("Updated SelfConfig: provider='{}', model='{}'", saved.getModelProvider(), saved.getModelName());
        return saved;
    }
}
