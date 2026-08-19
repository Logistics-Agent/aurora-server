package com.aurora.aigovernance.governance.infrastructure.messaging;

import com.aurora.aigovernance.governance.application.service.QuotaSyncService;
import com.aurora.aigovernance.governance.domain.entity.ProcessedEvent;
import com.aurora.aigovernance.governance.infrastructure.persistence.ProcessedEventRepository;
import com.aurora.aigovernance.shared.domain.AiOperation;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.extension.ExtendWith;
import org.mockito.InjectMocks;
import org.mockito.Mock;
import org.mockito.junit.jupiter.MockitoExtension;

import java.time.Instant;
import java.util.UUID;

import static org.mockito.ArgumentMatchers.any;
import static org.mockito.ArgumentMatchers.eq;
import static org.mockito.Mockito.*;

@ExtendWith(MockitoExtension.class)
public class AiUsageEventConsumerTest {

    @Mock
    private ProcessedEventRepository processedEventRepository;

    @Mock
    private QuotaSyncService quotaSyncService;

    @InjectMocks
    private AiUsageEventConsumer consumer;

    @Test
    public void testFirstTimeEvent_ProcessesAndSavesProcessedEvent() {
        UUID tenantId = UUID.randomUUID();
        AiUsageEvent event = new AiUsageEvent(
                "evt-123",
                tenantId,
                UUID.randomUUID(),
                "regulatory-compliance-rag",
                "compliance.answer",
                AiOperation.GENERATE,
                "GEMINI",
                "gemini-1.5-flash",
                100L,
                50L,
                120L,
                true,
                Instant.now()
        );

        when(processedEventRepository.existsByEventId("evt-123")).thenReturn(false);

        consumer.handleUsageEvent(event);

        verify(processedEventRepository).save(any(ProcessedEvent.class));
        verify(quotaSyncService).recordUsage(eq(tenantId), eq(1L), eq(150L), any(Instant.class));
    }

    @Test
    public void testDuplicateEvent_IsIdempotentlyIgnored() {
        UUID tenantId = UUID.randomUUID();
        AiUsageEvent event = new AiUsageEvent(
                "evt-duplicate",
                tenantId,
                null,
                "regulatory-compliance-rag",
                "compliance.answer",
                AiOperation.GENERATE,
                "GEMINI",
                "gemini-1.5-flash",
                100L,
                50L,
                120L,
                true,
                Instant.now()
        );

        when(processedEventRepository.existsByEventId("evt-duplicate")).thenReturn(true);

        consumer.handleUsageEvent(event);

        verify(processedEventRepository, never()).save(any());
        verifyNoInteractions(quotaSyncService);
    }
}
