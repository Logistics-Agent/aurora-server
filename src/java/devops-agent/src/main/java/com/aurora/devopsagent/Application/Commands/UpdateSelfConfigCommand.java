package com.aurora.devopsagent.Application.Commands;

import com.aurora.devopsagent.Domain.Entity.DevOpsAgentSelfConfig;
import com.aurora.devopsagent.Domain.Enums.AuditActionType;
import com.aurora.devopsagent.Infrastructure.Audit.AuditEventOutboxService;
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
        @Deprecated String modelProvider,
        @Deprecated String modelName,
        @Deprecated String apiEndpoint,
        int maxTokensPerRequest,
        BigDecimal alertThresholdUsdPerDay
) {

    @Service
    public static class Handler {

        private static final Logger log = LoggerFactory.getLogger(Handler.class);

        private final SelfConfigJpaRepository selfConfigRepository;
        private final AuditEventOutboxService outboxService;

        public Handler(
                SelfConfigJpaRepository selfConfigRepository,
                AuditEventOutboxService outboxService) {
            this.selfConfigRepository = selfConfigRepository;
            this.outboxService = outboxService;
        }

        @Transactional
        public DevOpsAgentSelfConfig handle(UpdateSelfConfigCommand command) {
            CurrentUserContext context = CurrentUserContext.getCurrent();
            if (context == null || !context.isSystemAdmin()) {
                throw new DomainExceptions.ForbiddenException("Only SYSTEM_ADMIN can update SelfConfig.");
            }

            List<DevOpsAgentSelfConfig> configs = selfConfigRepository.findAll();
            DevOpsAgentSelfConfig config = configs.isEmpty() ? new DevOpsAgentSelfConfig() : configs.get(0);

            // Deprecated provider fields - retained for backward DB compatibility, provider managed by AiGovernance
            config.setModelProvider(command.modelProvider());
            config.setModelName(command.modelName());
            config.setApiEndpoint(command.apiEndpoint());
            config.setMaxTokensPerRequest(command.maxTokensPerRequest());
            config.setAlertThresholdUsdPerDay(command.alertThresholdUsdPerDay());

            DevOpsAgentSelfConfig saved = selfConfigRepository.save(config);

            outboxService.enqueue(
                    saved.getId().toString(),
                    null,
                    AuditActionType.SELF_CONFIG_UPDATED,
                    String.format("{\"maxTokens\":\"%d\",\"alertThreshold\":\"%s\"}", saved.getMaxTokensPerRequest(), saved.getAlertThresholdUsdPerDay())
            );

            log.info("Updated SelfConfig: maxTokens='{}', alertThreshold='{}'", saved.getMaxTokensPerRequest(), saved.getAlertThresholdUsdPerDay());
            return saved;
        }
    }
}
