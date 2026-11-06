package com.aurora.devopsagent.Application.Commands;

import com.aurora.devopsagent.Application.Services.AntiFlappingTracker;
import com.aurora.devopsagent.Application.Services.DedupService;
import com.aurora.devopsagent.Application.Services.SeverityClassifier;
import com.aurora.devopsagent.Domain.Entity.Incident;
import com.aurora.devopsagent.Domain.Enums.AuditActionType;
import com.aurora.devopsagent.Domain.Enums.IncidentStatus;
import com.aurora.devopsagent.Domain.Enums.Severity;
import com.aurora.devopsagent.Infrastructure.Audit.AuditEventOutboxService;
import com.aurora.devopsagent.Infrastructure.Metrics.DevOpsMetrics;
import com.aurora.devopsagent.Infrastructure.Persistence.IncidentJpaRepository;
import com.aurora.devopsagent.Infrastructure.Security.RedactionService;
import com.fasterxml.jackson.core.JsonProcessingException;
import com.fasterxml.jackson.databind.ObjectMapper;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

import java.time.Instant;
import java.util.UUID;
import java.util.Map;

public record IngestAlertCommand(
        String source,
        String errorSignature,
        String payloadJson,
        String affectedService,
        String environment
) {

    public record Result(
            boolean duplicated,
            String correlationId,
            String incidentId,
            String status
    ) {}

    @Service
    public static class Handler {

        private static final Logger log = LoggerFactory.getLogger(Handler.class);

        private final IncidentJpaRepository incidentRepository;
        private final DedupService dedupService;
        private final AntiFlappingTracker antiFlappingTracker;
        private final SeverityClassifier severityClassifier;
        private final AuditEventOutboxService outboxService;
        private final DevOpsMetrics metrics;
        private final RedactionService redactionService;
        private final ObjectMapper objectMapper;

        public Handler(
                IncidentJpaRepository incidentRepository,
                DedupService dedupService,
                AntiFlappingTracker antiFlappingTracker,
                SeverityClassifier severityClassifier,
                AuditEventOutboxService outboxService,
                DevOpsMetrics metrics,
                RedactionService redactionService,
                ObjectMapper objectMapper) {
            this.incidentRepository = incidentRepository;
            this.dedupService = dedupService;
            this.antiFlappingTracker = antiFlappingTracker;
            this.severityClassifier = severityClassifier;
            this.outboxService = outboxService;
            this.metrics = metrics;
            this.redactionService = redactionService;
            this.objectMapper = objectMapper;
        }

        @Transactional
        public Result handle(IngestAlertCommand command) {
            metrics.incrementAlertsIngested();
            long nowSeconds = Instant.now().getEpochSecond();
            String dedupKey = dedupService.computeDedupKey(command.source(), command.errorSignature(), nowSeconds);
            String proposedCorrelationId = UUID.randomUUID().toString();

            // Check Dedup
            String existingCorrelationId = dedupService.checkAndStore(dedupKey, proposedCorrelationId);
            if (existingCorrelationId != null) {
                metrics.incrementAlertsDeduplicated();
                // Is Duplicate
                log.info("Alert is duplicate. CorrelationId: {}", existingCorrelationId);
                return new Result(true, existingCorrelationId, "", IncidentStatus.NEW.name());
            }

            // Is New Incident
            var classification = severityClassifier.classify(
                    command.source(), command.errorSignature(), command.affectedService(), command.environment());

            Severity finalSeverity = classification.severity();
            Severity originalSeverity = finalSeverity;

            // Anti-Flapping check for Low severity (escalate to Medium if >= 5 in 10 mins)
            if (finalSeverity == Severity.Low) {
                boolean isFlapping = antiFlappingTracker.recordEventAndCheckFlapping(dedupKey);
                if (isFlapping) {
                    finalSeverity = Severity.Medium; // Escalate
                    metrics.incrementAntiflappingTriggered();
                }
            }

            Incident incident = new Incident();
            incident.setCorrelationId(proposedCorrelationId);
            incident.setDedupKey(dedupKey);
            incident.setSource(command.source());
            incident.setErrorSignature(redactionService.redact(command.errorSignature()));
            String sanitizedEvidence = redactionService.redact(command.payloadJson());
            if (sanitizedEvidence != null && sanitizedEvidence.length() > 131072) {
                sanitizedEvidence = sanitizedEvidence.substring(0, 131072);
            }
            incident.setEvidenceJson(sanitizedEvidence);
            incident.escalateSeverity(finalSeverity);
            incident.setOriginalSeverity(originalSeverity);
            incident.setAffectedService(redactionService.redact(command.affectedService()));
            incident.setImpactScore(classification.impactScore());

            Incident saved = incidentRepository.save(incident);

            try {
                String safeEnvironment = redactionService.redact(command.environment());
                String auditEvent = objectMapper.writeValueAsString(Map.of(
                        "severity", saved.getSeverity().name(),
                        "affectedService", saved.getAffectedService() != null ? saved.getAffectedService() : "unknown"
                ));
                String notificationEvent = objectMapper.writeValueAsString(Map.of(
                        "eventId", saved.getCorrelationId(),
                        "serviceName", saved.getAffectedService() != null ? saved.getAffectedService() : "unknown",
                        "alertTitle", saved.getErrorSignature(),
                        "severity", notificationSeverity(saved.getSeverity()),
                        "details", "Incident correlationId=" + saved.getCorrelationId() + "; source=" + saved.getSource(),
                        "environment", safeEnvironment != null ? safeEnvironment : "unknown",
                        "timestamp", Instant.now().toString()
                ));
                outboxService.enqueue(
                        saved.getCorrelationId(),
                        saved.getId(),
                        AuditActionType.INCIDENT_CREATED,
                        auditEvent
                );
                outboxService.enqueue(
                        saved.getCorrelationId(),
                        saved.getId(),
                        AuditActionType.INCIDENT_DETECTED,
                        notificationEvent
                );
            } catch (JsonProcessingException exception) {
                throw new IllegalStateException("Could not serialize incident notification event", exception);
            }

            log.info("Created new Incident id={}, correlationId={}, severity={}, impactScore={}",
                    saved.getId(), saved.getCorrelationId(), saved.getSeverity(), saved.getImpactScore());

            return new Result(false, saved.getCorrelationId(), saved.getId().toString(), saved.getStatus().name());
        }

        private String notificationSeverity(Severity severity) {
            return switch (severity) {
                case Critical -> "CRITICAL";
                case High, Medium -> "WARNING";
                case Low -> "INFO";
            };
        }
    }
}
