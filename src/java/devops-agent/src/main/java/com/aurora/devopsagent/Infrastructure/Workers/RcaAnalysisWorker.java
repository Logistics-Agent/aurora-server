package com.aurora.devopsagent.Infrastructure.Workers;

import com.aurora.devopsagent.Application.Services.RcaOrchestratorService;
import com.aurora.devopsagent.Domain.Entity.Incident;
import com.aurora.devopsagent.Domain.Enums.AuditActionType;
import com.aurora.devopsagent.Domain.Enums.IncidentStatus;
import com.aurora.devopsagent.Infrastructure.Audit.AuditEventOutboxService;
import com.aurora.devopsagent.Infrastructure.Metrics.DevOpsMetrics;
import com.aurora.devopsagent.Infrastructure.Persistence.IncidentJpaRepository;
import com.aurora.devopsagent.Infrastructure.Persistence.IncidentWorkerLockingRepository;
import net.javacrumbs.shedlock.spring.annotation.SchedulerLock;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.beans.factory.annotation.Value;
import org.springframework.scheduling.annotation.Scheduled;
import org.springframework.stereotype.Component;
import org.springframework.transaction.annotation.Transactional;

import java.util.List;

/** Claims and analyzes one newly ingested incident at a time across agent replicas. */
@Component
public class RcaAnalysisWorker {

    private static final Logger log = LoggerFactory.getLogger(RcaAnalysisWorker.class);

    private final IncidentWorkerLockingRepository lockingRepository;
    private final IncidentJpaRepository incidentRepository;
    private final RcaOrchestratorService rcaOrchestrator;
    private final AuditEventOutboxService outboxService;
    private final DevOpsMetrics metrics;
    private final int batchSize;

    public RcaAnalysisWorker(
            IncidentWorkerLockingRepository lockingRepository,
            IncidentJpaRepository incidentRepository,
            RcaOrchestratorService rcaOrchestrator,
            AuditEventOutboxService outboxService,
            DevOpsMetrics metrics,
            @Value("${devops.rca.batch-size:1}") int batchSize) {
        this.lockingRepository = lockingRepository;
        this.incidentRepository = incidentRepository;
        this.rcaOrchestrator = rcaOrchestrator;
        this.outboxService = outboxService;
        this.metrics = metrics;
        this.batchSize = Math.max(1, batchSize);
    }

    @Scheduled(fixedDelayString = "${devops.rca.poll-interval-ms:5000}")
    @SchedulerLock(name = "rca_analysis_worker_lock", lockAtMostFor = "2m", lockAtLeastFor = "1s")
    @Transactional
    public void processPendingIncidents() {
        List<Incident> pending = lockingRepository.findAndLockIncidentsByStatus(IncidentStatus.NEW, batchSize);
        for (Incident incident : pending) {
            try {
                rcaOrchestrator.executeRca(incident, incident.getEvidenceJson());
            } catch (RuntimeException exception) {
                metrics.incrementRcaFailures();
                log.error("RCA failed for incident correlationId={}: {}", incident.getCorrelationId(), exception.getMessage());
                try {
                    if (incident.getStatus() == IncidentStatus.CONTEXT_READY) {
                        incident.transitionTo(IncidentStatus.AI_ANALYSIS);
                    }
                    if (incident.getStatus() == IncidentStatus.AI_ANALYSIS) {
                        incident.transitionTo(IncidentStatus.AI_ANALYSIS_FAILED);
                    }
                    incidentRepository.save(incident);
                    outboxService.enqueue(
                            incident.getCorrelationId(), incident.getId(), AuditActionType.RCA_ANALYSIS_FAILED,
                            "{\"reason\":\"RCA execution failed; inspect sanitized service logs\"}");
                } catch (RuntimeException stateException) {
                    log.error("Could not persist RCA failure state for correlationId={}: {}",
                            incident.getCorrelationId(), stateException.getMessage());
                }
            }
        }
    }
}
