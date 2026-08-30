package com.aurora.audit.infrastructure.messaging;

import com.aurora.audit.application.usecase.IngestAuditEventUseCase;
import com.aurora.audit.infrastructure.messaging.dto.AuditEventDto;
import lombok.RequiredArgsConstructor;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.amqp.rabbit.annotation.RabbitListener;
import org.springframework.stereotype.Component;

@Component
@RequiredArgsConstructor
public class AuditEventConsumer {

    private static final Logger log = LoggerFactory.getLogger(AuditEventConsumer.class);

    private final IngestAuditEventUseCase ingestAuditEventUseCase;

    @RabbitListener(queues = "${app.rabbitmq.queue.audit-events:aurora.audit.events.queue}")
    public void consumeAuditEvent(AuditEventDto auditEvent) {
        log.info("Received RabbitMQ Audit Event from service '{}': eventType='{}'",
                auditEvent.getServiceName(), auditEvent.getEventType());
        ingestAuditEventUseCase.execute(auditEvent);
    }
}
