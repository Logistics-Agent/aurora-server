package com.aurora.aigovernance.governance.infrastructure.messaging;

import com.aurora.aigovernance.governance.application.port.PolicyAuditPort;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.amqp.rabbit.core.RabbitTemplate;
import org.springframework.stereotype.Component;

/**
 * Async policy audit publisher using RabbitMQ (best-effort with retry in V1).
 */
@Component
public class RabbitMqPolicyAuditPublisher implements PolicyAuditPort {

    private static final Logger log = LoggerFactory.getLogger(RabbitMqPolicyAuditPublisher.class);

    private final RabbitTemplate rabbitTemplate;

    public RabbitMqPolicyAuditPublisher(RabbitTemplate rabbitTemplate) {
        this.rabbitTemplate = rabbitTemplate;
    }

    @Override
    public void publishPolicyDecision(PolicyAuditEvent event) {
        try {
            rabbitTemplate.convertAndSend(
                    RabbitMqConfig.EXCHANGE_AI_GOVERNANCE,
                    RabbitMqConfig.ROUTING_KEY_POLICY_AUDIT,
                    event
            );
            log.debug("Published policy audit event: decisionId={}", event.decisionId());
        } catch (Exception e) {
            log.error("Failed to publish policy audit event: decisionId={}, error={}", event.decisionId(), e.getMessage());
            // Best effort V1 - do not throw to caller
        }
    }
}
