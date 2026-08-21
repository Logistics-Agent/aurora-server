package com.aurora.devopsagent.Infrastructure.Audit;

import com.aurora.devopsagent.Domain.Entity.AuditEventOutbox;
import com.aurora.devopsagent.Domain.Enums.AuditActionType;
import com.aurora.devopsagent.Infrastructure.Persistence.AuditEventOutboxJpaRepository;
import com.aurora.shared.security.CurrentUserContext;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Propagation;
import org.springframework.transaction.annotation.Transactional;

import java.util.UUID;

@Service
public class AuditEventOutboxService {

    private static final Logger log = LoggerFactory.getLogger(AuditEventOutboxService.class);

    private final AuditEventOutboxJpaRepository outboxRepository;

    public AuditEventOutboxService(AuditEventOutboxJpaRepository outboxRepository) {
        this.outboxRepository = outboxRepository;
    }

    /**
     * Enqueue an audit event within the caller's existing transaction.
     */
    @Transactional(propagation = Propagation.MANDATORY)
    public AuditEventOutbox enqueue(String correlationId, UUID incidentId, AuditActionType actionType, String payloadJson) {
        CurrentUserContext context = CurrentUserContext.getCurrent();
        String actor = (context != null && context.getUserId() != null)
                ? context.getUserId().toString()
                : "SYSTEM_DEVOPS_AGENT";

        AuditEventOutbox event = new AuditEventOutbox();
        event.setCorrelationId(correlationId != null ? correlationId : UUID.randomUUID().toString());
        event.setIncidentId(incidentId);
        event.setActionType(actionType);
        event.setActor(actor);
        event.setPayloadJson(payloadJson != null ? payloadJson : "{}");
        event.setProcessed(false);
        event.setRetryCount(0);

        AuditEventOutbox saved = outboxRepository.save(event);
        log.debug("Enqueued AuditEventOutbox id={}, actionType={}, correlationId={}",
                saved.getId(), actionType, saved.getCorrelationId());
        return saved;
    }
}
