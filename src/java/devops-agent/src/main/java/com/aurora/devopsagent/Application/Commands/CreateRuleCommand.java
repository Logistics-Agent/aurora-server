package com.aurora.devopsagent.Application.Commands;

import com.aurora.devopsagent.Domain.Entity.ExistingRule;
import com.aurora.devopsagent.Infrastructure.Audit.AuditEventOutboxService;
import com.aurora.devopsagent.Infrastructure.Persistence.ExistingRuleJpaRepository;
import com.aurora.shared.exception.DomainExceptions;
import com.aurora.shared.security.CurrentUserContext;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

public record CreateRuleCommand(
        String name,
        String errorSignaturePattern,
        String targetService,
        String targetDeployment,
        String actionType,
        String actionParamsJson,
        String scopeConstraintJson
) {

    @Service
    public static class Handler {

        private static final Logger log = LoggerFactory.getLogger(Handler.class);

        private final ExistingRuleJpaRepository ruleRepository;
        private final AuditEventOutboxService outboxService;

        public Handler(
                ExistingRuleJpaRepository ruleRepository,
                AuditEventOutboxService outboxService) {
            this.ruleRepository = ruleRepository;
            this.outboxService = outboxService;
        }

        @Transactional
        public ExistingRule handle(CreateRuleCommand command) {
            CurrentUserContext context = CurrentUserContext.getCurrent();
            if (context == null || !context.isSystemAdmin()) {
                throw new DomainExceptions.ForbiddenException("Only SYSTEM_ADMIN can create rules.");
            }

            ExistingRule rule = new ExistingRule();
            rule.setName(command.name());
            rule.setErrorSignaturePattern(command.errorSignaturePattern());
            rule.setTargetService(command.targetService());
            rule.setTargetDeployment(command.targetDeployment());
            rule.setActionType(command.actionType());
            rule.setActionParamsJson(command.actionParamsJson() != null ? command.actionParamsJson() : "{}");
            rule.setScopeConstraintJson(command.scopeConstraintJson() != null ? command.scopeConstraintJson() : "{}");

            ExistingRule saved = ruleRepository.save(rule);

            outboxService.enqueue(
                    saved.getId().toString(),
                    null,
                    com.aurora.devopsagent.Domain.Enums.AuditActionType.RULE_APPLIED,
                    String.format("{\"ruleId\":\"%s\",\"ruleName\":\"%s\"}", saved.getId(), saved.getName())
            );

            log.info("Created ExistingRule id={}, name='{}', targetService='{}'", saved.getId(), saved.getName(), saved.getTargetService());
            return saved;
        }
    }
}
