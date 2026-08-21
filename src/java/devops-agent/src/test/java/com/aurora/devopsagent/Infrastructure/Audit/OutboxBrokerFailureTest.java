package com.aurora.devopsagent.Infrastructure.Audit;

import com.aurora.devopsagent.Domain.Entity.AuditEventOutbox;
import com.aurora.devopsagent.Domain.Enums.AuditActionType;
import com.aurora.devopsagent.Infrastructure.Persistence.AuditEventOutboxJpaRepository;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.springframework.amqp.AmqpException;
import org.springframework.amqp.core.MessagePostProcessor;
import org.springframework.amqp.rabbit.core.RabbitTemplate;

import java.util.List;

import static org.junit.jupiter.api.Assertions.*;
import static org.mockito.ArgumentMatchers.*;
import static org.mockito.Mockito.*;

class OutboxBrokerFailureTest {

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
    @DisplayName("When RabbitMQ publish throws AmqpException, record remains unprocessed, retryCount increments, lastError is populated")
    void testOutboxBrokerFailureHandling() {
        AuditEventOutbox event = new AuditEventOutbox();
        event.setCorrelationId("corr-fail-outbox");
        event.setActionType(AuditActionType.INCIDENT_CREATED);
        event.setActor("SYSTEM");
        event.setPayloadJson("{\"incidentId\":\"123\"}");
        event.setProcessed(false);
        event.setRetryCount(0);

        when(outboxRepository.findAndLockPendingBatch(anyInt())).thenReturn(List.of(event));

        doThrow(new AmqpException("Broker connection refused"))
                .when(rabbitTemplate)
                .convertAndSend(anyString(), anyString(), any(Object.class), any(MessagePostProcessor.class));

        worker.processOutboxEvents();

        assertFalse(event.isProcessed());
        assertEquals(1, event.getRetryCount());
        assertNotNull(event.getLastError());
        assertTrue(event.getLastError().contains("Broker connection refused"));

        verify(outboxRepository, times(1)).save(event);
    }
}
