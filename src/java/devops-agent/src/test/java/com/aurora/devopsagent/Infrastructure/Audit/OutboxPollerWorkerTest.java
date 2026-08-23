package com.aurora.devopsagent.Infrastructure.Audit;

import com.aurora.devopsagent.Domain.Entity.AuditEventOutbox;
import com.aurora.devopsagent.Domain.Enums.AuditActionType;
import com.aurora.devopsagent.Infrastructure.Persistence.AuditEventOutboxJpaRepository;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.springframework.amqp.core.MessagePostProcessor;
import org.springframework.amqp.rabbit.core.RabbitTemplate;

import java.util.List;

import static org.junit.jupiter.api.Assertions.assertTrue;
import static org.mockito.ArgumentMatchers.*;
import static org.mockito.Mockito.*;

class OutboxPollerWorkerTest {

    private AuditEventOutboxJpaRepository outboxRepository;
    private RabbitTemplate rabbitTemplate;
    private OutboxPollerWorker worker;

    @BeforeEach
    void setUp() {
        outboxRepository = mock(AuditEventOutboxJpaRepository.class);
        rabbitTemplate = mock(RabbitTemplate.class);
        worker = new OutboxPollerWorker(outboxRepository, rabbitTemplate);
    }

    @Test
    @DisplayName("Poller publishes unprocessed locked events to RabbitMQ and marks them processed")
    void testProcessOutboxEvents() {
        AuditEventOutbox event = new AuditEventOutbox();
        event.setCorrelationId("corr-test-1");
        event.setActionType(AuditActionType.INCIDENT_CREATED);
        event.setActor("SYSTEM");
        event.setPayloadJson("{\"test\":true}");
        event.setProcessed(false);

        when(outboxRepository.findAndLockPendingBatch(anyInt())).thenReturn(List.of(event));

        worker.processOutboxEvents();

        verify(rabbitTemplate, times(1)).convertAndSend(
                eq("devops.audit.events"),
                eq("devops.audit.incident_created"),
                eq("{\"test\":true}"),
                any(MessagePostProcessor.class)
        );

        assertTrue(event.isProcessed());
        verify(outboxRepository, times(1)).save(event);
    }
}
