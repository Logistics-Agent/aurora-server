package com.aurora.devopsagent.Domain.Entity;

import com.aurora.shared.entity.AuditableEntity;
import jakarta.persistence.Column;
import jakarta.persistence.Entity;
import jakarta.persistence.Table;

import java.math.BigDecimal;
import java.time.OffsetDateTime;

@Entity
@Table(name = "pending_rules")
public class PendingRule extends AuditableEntity {

    @Column(name = "error_signature", nullable = false, columnDefinition = "TEXT")
    private String errorSignature;

    @Column(name = "root_cause_summary", nullable = false, columnDefinition = "TEXT")
    private String rootCauseSummary;

    @Column(name = "proposed_action", nullable = false, columnDefinition = "TEXT")
    private String proposedAction;

    @Column(name = "confidence_score", nullable = false, precision = 5, scale = 4)
    private BigDecimal confidenceScore;

    @Column(name = "source_correlation_id", nullable = false, length = 64)
    private String sourceCorrelationId;

    @Column(name = "status", nullable = false, length = 30)
    private String status; // PENDING_APPROVAL, APPROVED, REJECTED

    @Column(name = "rejection_reason", columnDefinition = "TEXT")
    private String rejectionReason;

    @Column(name = "reviewed_by", length = 100)
    private String reviewedBy;

    @Column(name = "reviewed_at")
    private OffsetDateTime reviewedAt;

    public String getErrorSignature() {
        return errorSignature;
    }

    public void setErrorSignature(String errorSignature) {
        this.errorSignature = errorSignature;
    }

    public String getRootCauseSummary() {
        return rootCauseSummary;
    }

    public void setRootCauseSummary(String rootCauseSummary) {
        this.rootCauseSummary = rootCauseSummary;
    }

    public String getProposedAction() {
        return proposedAction;
    }

    public void setProposedAction(String proposedAction) {
        this.proposedAction = proposedAction;
    }

    public BigDecimal getConfidenceScore() {
        return confidenceScore;
    }

    public void setConfidenceScore(BigDecimal confidenceScore) {
        this.confidenceScore = confidenceScore;
    }

    public String getSourceCorrelationId() {
        return sourceCorrelationId;
    }

    public void setSourceCorrelationId(String sourceCorrelationId) {
        this.sourceCorrelationId = sourceCorrelationId;
    }

    public String getStatus() {
        return status;
    }

    public void setStatus(String status) {
        this.status = status;
    }

    public String getRejectionReason() {
        return rejectionReason;
    }

    public void setRejectionReason(String rejectionReason) {
        this.rejectionReason = rejectionReason;
    }

    public String getReviewedBy() {
        return reviewedBy;
    }

    public void setReviewedBy(String reviewedBy) {
        this.reviewedBy = reviewedBy;
    }

    public OffsetDateTime getReviewedAt() {
        return reviewedAt;
    }

    public void setReviewedAt(OffsetDateTime reviewedAt) {
        this.reviewedAt = reviewedAt;
    }
}
