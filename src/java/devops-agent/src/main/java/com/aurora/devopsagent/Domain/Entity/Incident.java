package com.aurora.devopsagent.Domain.Entity;

import com.aurora.devopsagent.Domain.Enums.IncidentStatus;
import com.aurora.devopsagent.Domain.Enums.Severity;
import com.aurora.shared.entity.AuditableEntity;
import jakarta.persistence.*;

import java.math.BigDecimal;
import java.util.Map;
import java.util.Set;
import java.util.UUID;

@Entity
@Table(name = "incidents")
public class Incident extends AuditableEntity {

    private static final Map<IncidentStatus, Set<IncidentStatus>> TRANSITIONS = Map.ofEntries(
        Map.entry(IncidentStatus.NEW,                  Set.of(IncidentStatus.COLLECTING_CONTEXT, IncidentStatus.IGNORED, IncidentStatus.CANCELLED)),
        Map.entry(IncidentStatus.COLLECTING_CONTEXT,   Set.of(IncidentStatus.CONTEXT_READY, IncidentStatus.CONTEXT_BUILD_FAILED, IncidentStatus.CANCELLED)),
        Map.entry(IncidentStatus.CONTEXT_READY,        Set.of(IncidentStatus.RULE_ANALYSIS, IncidentStatus.AI_ANALYSIS, IncidentStatus.ROUTING_FAILED, IncidentStatus.CANCELLED)),
        Map.entry(IncidentStatus.RULE_ANALYSIS,        Set.of(IncidentStatus.RECOMMENDATION_READY, IncidentStatus.AI_ANALYSIS, IncidentStatus.CANCELLED)),
        Map.entry(IncidentStatus.AI_ANALYSIS,          Set.of(IncidentStatus.RECOMMENDATION_READY, IncidentStatus.AI_ANALYSIS_FAILED, IncidentStatus.CANCELLED)),
        Map.entry(IncidentStatus.RECOMMENDATION_READY, Set.of(IncidentStatus.WAITING_APPROVAL, IncidentStatus.APPROVED, IncidentStatus.CANCELLED)),
        Map.entry(IncidentStatus.WAITING_APPROVAL,     Set.of(IncidentStatus.APPROVED, IncidentStatus.CANCELLED, IncidentStatus.RECOMMENDATION_READY)),
        Map.entry(IncidentStatus.APPROVED,             Set.of(IncidentStatus.EXECUTING, IncidentStatus.CANCELLED)),
        Map.entry(IncidentStatus.EXECUTING,            Set.of(IncidentStatus.VERIFYING, IncidentStatus.EXECUTION_FAILED)),
        Map.entry(IncidentStatus.VERIFYING,            Set.of(IncidentStatus.RESOLVED, IncidentStatus.EXECUTION_FAILED)),
        Map.entry(IncidentStatus.RESOLVED,             Set.of(IncidentStatus.CLOSED)),
        Map.entry(IncidentStatus.IGNORED,              Set.of(IncidentStatus.CLOSED)),
        Map.entry(IncidentStatus.CANCELLED,            Set.of(IncidentStatus.CLOSED)),
        Map.entry(IncidentStatus.CONTEXT_BUILD_FAILED, Set.of(IncidentStatus.COLLECTING_CONTEXT, IncidentStatus.CLOSED)),
        Map.entry(IncidentStatus.ROUTING_FAILED,       Set.of(IncidentStatus.CONTEXT_READY, IncidentStatus.CLOSED)),
        Map.entry(IncidentStatus.AI_ANALYSIS_FAILED,   Set.of(IncidentStatus.AI_ANALYSIS, IncidentStatus.CLOSED)),
        Map.entry(IncidentStatus.EXECUTION_FAILED,     Set.of(IncidentStatus.APPROVED, IncidentStatus.CLOSED))
    );

    @Column(name = "correlation_id", nullable = false, unique = true, length = 64)
    private String correlationId;

    @Column(name = "dedup_key", nullable = false, length = 64)
    private String dedupKey;

    @Column(name = "source", nullable = false, length = 50)
    private String source; // azure_monitor / loki

    @Column(name = "error_signature", nullable = false, columnDefinition = "TEXT")
    private String errorSignature;

    @Enumerated(EnumType.STRING)
    @Column(name = "severity", nullable = false)
    private Severity severity;

    @Enumerated(EnumType.STRING)
    @Column(name = "original_severity", nullable = false)
    private Severity originalSeverity;

    @Enumerated(EnumType.STRING)
    @Column(name = "status", nullable = false)
    private IncidentStatus status = IncidentStatus.NEW;

    @Column(name = "flap_count", nullable = false)
    private int flapCount = 0;

    @Column(name = "affected_service", length = 100)
    private String affectedService;

    @Column(name = "affected_tenant_id")
    private UUID affectedTenantId;

    @Column(name = "impact_score", nullable = false, precision = 5, scale = 2)
    private BigDecimal impactScore = BigDecimal.ZERO;

    @Column(name = "rca_root_cause", columnDefinition = "TEXT")
    private String rcaRootCause;

    @Column(name = "rca_recommendation", columnDefinition = "TEXT")
    private String rcaRecommendation;

    @Column(name = "selected_recommendation_id")
    private UUID selectedRecommendationId;

    public Incident() {
        this.status = IncidentStatus.NEW;
    }

    /**
     * Domain state machine transition validation
     */
    public void transitionTo(IncidentStatus target) {
        if (this.status == target) {
            return;
        }
        Set<IncidentStatus> allowed = TRANSITIONS.getOrDefault(this.status, Set.of());
        if (!allowed.contains(target)) {
            throw new IllegalStateException(
                "Invalid transition: " + this.status + " → " + target
            );
        }
        this.status = target;
    }

    /**
     * Escalate severity invariant check (disallow downgrade)
     */
    public void escalateSeverity(Severity newSeverity) {
        if (this.severity != null && newSeverity != null && newSeverity.weight() < this.severity.weight()) {
            throw new IllegalArgumentException("Cannot downgrade severity from " + this.severity + " to " + newSeverity);
        }
        this.severity = newSeverity;
        if (this.originalSeverity == null) {
            this.originalSeverity = newSeverity;
        }
    }

    // ── Getters and Setters ──────────────────────────────

    public String getCorrelationId() {
        return correlationId;
    }

    public void setCorrelationId(String correlationId) {
        this.correlationId = correlationId;
    }

    public String getDedupKey() {
        return dedupKey;
    }

    public void setDedupKey(String dedupKey) {
        this.dedupKey = dedupKey;
    }

    public String getSource() {
        return source;
    }

    public void setSource(String source) {
        this.source = source;
    }

    public String getErrorSignature() {
        return errorSignature;
    }

    public void setErrorSignature(String errorSignature) {
        this.errorSignature = errorSignature;
    }

    public Severity getSeverity() {
        return severity;
    }

    public Severity getOriginalSeverity() {
        return originalSeverity;
    }

    public void setOriginalSeverity(Severity originalSeverity) {
        this.originalSeverity = originalSeverity;
    }

    public IncidentStatus getStatus() {
        return status;
    }

    public int getFlapCount() {
        return flapCount;
    }

    public void setFlapCount(int flapCount) {
        this.flapCount = flapCount;
    }

    public String getAffectedService() {
        return affectedService;
    }

    public void setAffectedService(String affectedService) {
        this.affectedService = affectedService;
    }

    public UUID getAffectedTenantId() {
        return affectedTenantId;
    }

    public void setAffectedTenantId(UUID affectedTenantId) {
        this.affectedTenantId = affectedTenantId;
    }

    public BigDecimal getImpactScore() {
        return impactScore;
    }

    public void setImpactScore(BigDecimal impactScore) {
        this.impactScore = impactScore;
    }

    public String getRcaRootCause() {
        return rcaRootCause;
    }

    public void setRcaRootCause(String rcaRootCause) {
        this.rcaRootCause = rcaRootCause;
    }

    public String getRcaRecommendation() {
        return rcaRecommendation;
    }

    public void setRcaRecommendation(String rcaRecommendation) {
        this.rcaRecommendation = rcaRecommendation;
    }

    public UUID getSelectedRecommendationId() {
        return selectedRecommendationId;
    }

    public void setSelectedRecommendationId(UUID selectedRecommendationId) {
        this.selectedRecommendationId = selectedRecommendationId;
    }
}
