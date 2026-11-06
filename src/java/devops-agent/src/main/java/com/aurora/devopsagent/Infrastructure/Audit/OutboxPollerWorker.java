package com.aurora.devopsagent.Infrastructure.Audit;

import com.aurora.devopsagent.Domain.Entity.AuditEventOutbox;
import com.aurora.devopsagent.Infrastructure.Persistence.AuditEventOutboxJpaRepository;
import com.aurora.devopsagent.Infrastructure.Metrics.DevOpsMetrics;
import net.javacrumbs.shedlock.spring.annotation.SchedulerLock;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.amqp.rabbit.core.RabbitTemplate;
import org.springframework.amqp.core.MessageProperties;
import org.springframework.scheduling.annotation.Scheduled;
import org.springframework.stereotype.Component;
import org.springframework.transaction.annotation.Transactional;

import java.time.Instant;
import java.util.List;

/**
 * OutboxPollerWorker: Multi-pod resilient outbox processor using ShedLock + SELECT FOR UPDATE SKIP LOCKED.
 */
@Component
public class OutboxPollerWorker {

    private static final Logger log = LoggerFactory.getLogger(OutboxPollerWorker.class);
    private static final String AUDIT_EXCHANGE = "devops.audit.events";
    private static final String ROUTING_KEY_PREFIX = "devops.audit.";
    private static final String NOTIFICATION_EXCHANGE = "devops.events.exchange";
    private static final String NOTIFICATION_ROUTING_KEY = "devops.alert.incident.detected";

    private final AuditEventOutboxJpaRepository outboxRepository;
    private final RabbitTemplate rabbitTemplate;
    private final DevOpsMetrics metrics;

    public OutboxPollerWorker(AuditEventOutboxJpaRepository outboxRepository, RabbitTemplate rabbitTemplate, DevOpsMetrics metrics) {
        this.outboxRepository = outboxRepository;
        this.rabbitTemplate = rabbitTemplate;
        this.metrics = metrics;
    }

    @Scheduled(fixedDelayString = "${devops.outbox.poll-interval-ms:5000}")
    @SchedulerLock(name = "outbox_poller_lock", lockAtMostFor = "30s", lockAtLeastFor = "1s")
    @Transactional
    public void processOutboxEvents() {
        // Multi-pod row locking via SELECT FOR UPDATE SKIP LOCKED
        List<AuditEventOutbox> pending = outboxRepository.findAndLockPendingBatch(50);
        if (pending.isEmpty()) {
            return;
        }

        log.debug("Found {} pending audit outbox records locked for processing.", pending.size());

        for (AuditEventOutbox event : pending) {
            try {
                boolean notificationEvent = event.getActionType() == com.aurora.devopsagent.Domain.Enums.AuditActionType.INCIDENT_DETECTED;
                String exchange = notificationEvent ? NOTIFICATION_EXCHANGE : AUDIT_EXCHANGE;
                String routingKey = notificationEvent
                        ? NOTIFICATION_ROUTING_KEY
                        : ROUTING_KEY_PREFIX + event.getActionType().name().toLowerCase();
                rabbitTemplate.convertAndSend(exchange, routingKey, event.getPayloadJson(), message -> {
                    message.getMessageProperties().setCorrelationId(event.getCorrelationId());
                    message.getMessageProperties().setHeader("action_type", event.getActionType().name());
                    message.getMessageProperties().setHeader("actor", event.getActor());
                    if (notificationEvent) {
                        message.getMessageProperties().setContentType(MessageProperties.CONTENT_TYPE_JSON);
                    }
                    return message;
                });

                event.setProcessed(true);
                event.setProcessedAt(Instant.now());
                outboxRepository.save(event);

                log.debug("Successfully published outbox event id={}", event.getId());
            } catch (Exception e) {
                metrics.incrementOutboxPublishFailures();
                log.error("Failed to publish outbox event id={}: {}", event.getId(), e.getMessage());
                event.setRetryCount(event.getRetryCount() + 1);
                event.setLastError(e.getMessage());
                outboxRepository.save(event);
            }
        }
    }
}
