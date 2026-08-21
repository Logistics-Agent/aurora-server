package com.aurora.aigovernance.governance.infrastructure.messaging;

import com.aurora.aigovernance.governance.application.service.QuotaSyncService;
import com.aurora.aigovernance.governance.domain.entity.ProcessedEvent;
import com.aurora.aigovernance.governance.infrastructure.persistence.ProcessedEventRepository;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.amqp.rabbit.annotation.RabbitListener;
import org.springframework.stereotype.Component;
import org.springframework.transaction.annotation.Transactional;

import java.time.OffsetDateTime;
import java.time.ZoneOffset;

/**
 * Consumer for AI execution usage events with idempotency guard.
 */
@Component
public class AiUsageEventConsumer {

    private static final Logger log = LoggerFactory.getLogger(AiUsageEventConsumer.class);

    private final ProcessedEventRepository processedEventRepository;
    private final QuotaSyncService quotaSyncService;

    public AiUsageEventConsumer(
            ProcessedEventRepository processedEventRepository,
            QuotaSyncService quotaSyncService) {
        this.processedEventRepository = processedEventRepository;
        this.quotaSyncService = quotaSyncService;
    }

    @RabbitListener(queues = RabbitMqConfig.QUEUE_USAGE_EVENTS)
    @Transactional
    public void handleUsageEvent(AiUsageEvent event) {
        log.debug("Received AI usage event: {}", event);

        // 1. Idempotency check
        if (processedEventRepository.existsByEventId(event.eventId())) {
            log.info("Duplicate event ignored: {}", event.eventId());
            return;
        }

        // 2. Mark event as processed
        ProcessedEvent processedEvent = new ProcessedEvent();
        processedEvent.setEventId(event.eventId());
        processedEvent.setProcessedAt(OffsetDateTime.now(ZoneOffset.UTC));
        processedEventRepository.save(processedEvent);

        // 3. Record usage in Redis and Postgres if successful
        if (event.success() && event.tenantId() != null) {
            long totalTokens = event.inputTokens() + event.outputTokens();
            quotaSyncService.recordUsage(event.tenantId(), 1L, totalTokens, event.timestamp());
        }
    }
}
