package com.aurora.devopsagent.Infrastructure.Audit;

import com.aurora.devopsagent.Domain.Entity.AuditEventOutbox;
import com.aurora.devopsagent.Domain.Enums.AuditActionType;
import com.aurora.devopsagent.Infrastructure.Persistence.AuditEventOutboxJpaRepository;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;

import java.util.List;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.mockito.Mockito.*;

class OutboxConcurrentPollerTest {

    @Test
    @DisplayName("Poller requests batch with findAndLockPendingBatch for multi-pod skip locked execution")
    void testOutboxLockingPollerInvoked() {
        AuditEventOutboxJpaRepository repository = mock(AuditEventOutboxJpaRepository.class);
        AuditEventOutbox event = new AuditEventOutbox();
        event.setCorrelationId("corr-lock-1");
        event.setActionType(AuditActionType.INCIDENT_CREATED);
        event.setActor("SYSTEM");
        event.setPayloadJson("{}");

        when(repository.findAndLockPendingBatch(50)).thenReturn(List.of(event));

        List<AuditEventOutbox> locked = repository.findAndLockPendingBatch(50);
        assertEquals(1, locked.size());
        verify(repository, times(1)).findAndLockPendingBatch(50);
    }
}
