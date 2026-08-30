package com.aurora.audit;

import com.aurora.audit.application.usecase.IngestAuditEventUseCase;
import com.aurora.audit.infrastructure.messaging.dto.AuditEventDto;
import com.aurora.audit.infrastructure.persistence.SpringDataAuditLogRepository;
import com.aurora.audit.infrastructure.persistence.entity.AuditLogEntity;
import org.junit.jupiter.api.Test;
import org.mockito.Mockito;

import java.time.Instant;
import java.util.UUID;

import static org.junit.jupiter.api.Assertions.*;
import static org.mockito.ArgumentMatchers.any;
import static org.mockito.Mockito.when;

class AuditServiceApplicationTests {

    @Test
    void testIngestAuditEventUseCase() {
        SpringDataAuditLogRepository repository = Mockito.mock(SpringDataAuditLogRepository.class);
        IngestAuditEventUseCase useCase = new IngestAuditEventUseCase(repository);

        AuditEventDto dto = new AuditEventDto(
                UUID.randomUUID().toString(),
                "ShipmentWorkflow",
                "SHIPMENT_CREATED",
                "tenant-aurora-01",
                "user-123",
                "SYSTEM",
                "SHIP-999",
                "{\"status\":\"CREATED\"}",
                "127.0.0.1",
                Instant.now().toString()
        );

        when(repository.save(any(AuditLogEntity.class))).thenAnswer(invocation -> invocation.getArgument(0));

        AuditLogEntity result = useCase.execute(dto);

        assertNotNull(result);
        assertEquals("ShipmentWorkflow", result.getServiceName());
        assertEquals("SHIPMENT_CREATED", result.getEventType());
        assertEquals("SYSTEM", result.getUserRole());
        assertEquals("tenant-aurora-01", result.getTenantId());
    }
}
