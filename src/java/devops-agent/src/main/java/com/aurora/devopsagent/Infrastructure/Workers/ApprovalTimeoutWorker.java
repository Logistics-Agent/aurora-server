package com.aurora.devopsagent.Infrastructure.Workers;

import com.aurora.devopsagent.Domain.Entity.PrApprovalRecord;
import com.aurora.devopsagent.Domain.Enums.ApprovalStatus;
import com.aurora.devopsagent.Domain.Enums.AuditActionType;
import com.aurora.devopsagent.Infrastructure.Audit.AuditEventOutboxService;
import com.aurora.devopsagent.Infrastructure.Persistence.PrApprovalRecordJpaRepository;
import net.javacrumbs.shedlock.spring.annotation.SchedulerLock;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.scheduling.annotation.Scheduled;
import org.springframework.stereotype.Component;
import org.springframework.transaction.annotation.Transactional;

import java.time.Instant;
import java.util.List;

@Component
public class ApprovalTimeoutWorker {

    private static final Logger log = LoggerFactory.getLogger(ApprovalTimeoutWorker.class);

    private final PrApprovalRecordJpaRepository approvalRepository;
    private final AuditEventOutboxService outboxService;

    public ApprovalTimeoutWorker(
            PrApprovalRecordJpaRepository approvalRepository,
            AuditEventOutboxService outboxService) {
        this.approvalRepository = approvalRepository;
        this.outboxService = outboxService;
    }

    @Scheduled(fixedDelayString = "${devops.approval.timeout-check-interval-ms:15000}")
    @SchedulerLock(name = "approval_timeout_worker_lock", lockAtMostFor = "30s", lockAtLeastFor = "2s")
    @Transactional
    public void processExpiredApprovals() {
        Instant now = Instant.now();
        List<PrApprovalRecord> expired = approvalRepository.findByStatusAndExpiresAtBefore(ApprovalStatus.PENDING, now);
        if (expired.isEmpty()) {
            return;
        }

        log.warn("Found {} expired approval requests. Expiring approvals and stopping automation.", expired.size());

        for (PrApprovalRecord record : expired) {
            record.setStatus(ApprovalStatus.EXPIRED);
            approvalRepository.save(record);

            String correlationId = record.getIncident() != null ? record.getIncident().getCorrelationId() : null;
            outboxService.enqueue(
                    correlationId,
                    record.getIncident() != null ? record.getIncident().getId() : null,
                    AuditActionType.APPROVAL_REJECTED,
                    String.format("{\"approvalId\":\"%s\",\"reason\":\"Approval expired due to timeout (%d minutes)\"}",
                            record.getId(), record.getTimeoutMinutes())
            );

            log.info("Approval record id={} marked as EXPIRED.", record.getId());
        }
    }
}
