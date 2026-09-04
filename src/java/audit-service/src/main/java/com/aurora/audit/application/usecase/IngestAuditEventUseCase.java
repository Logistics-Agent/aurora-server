package com.aurora.audit.application.usecase;

import com.aurora.audit.infrastructure.messaging.dto.AuditEventDto;
import com.aurora.audit.infrastructure.persistence.SpringDataAuditLogRepository;
import com.aurora.audit.infrastructure.persistence.entity.AuditLogEntity;
import lombok.RequiredArgsConstructor;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.stereotype.Service;

import java.time.Instant;
import java.util.UUID;

@Service
@RequiredArgsConstructor
public class IngestAuditEventUseCase {

    private static final Logger log = LoggerFactory.getLogger(IngestAuditEventUseCase.class);

    private final SpringDataAuditLogRepository auditLogRepository;

    public AuditLogEntity execute(AuditEventDto event) {
        String logId = (event.getEventId() != null && !event.getEventId().isBlank())
                ? event.getEventId()
                : UUID.randomUUID().toString();

        Instant createdAt;
        try {
            createdAt = (event.getTimestamp() != null && !event.getTimestamp().isBlank())
                    ? Instant.parse(event.getTimestamp())
                    : Instant.now();
        } catch (Exception e) {
            createdAt = Instant.now();
        }

        AuditLogEntity entity = new AuditLogEntity(
                logId,
                event.getServiceName() != null ? event.getServiceName() : "SystemCore",
                event.getEventType() != null ? event.getEventType() : "GENERIC_EVENT",
                event.getTenantId(),
                event.getUserId(),
                event.getUserRole() != null ? event.getUserRole() : "SYSTEM",
                event.getResourceId(),
                event.getPayloadJson() != null ? event.getPayloadJson() : "{}",
                event.getIpAddress(),
                createdAt
        );

        AuditLogEntity saved = auditLogRepository.save(entity);
        log.info("Persisted Audit Event: id={}, service={}, eventType={}, userRole={}",
                saved.getId(), saved.getServiceName(), saved.getEventType(), saved.getUserRole());

        return saved;
    }
}
