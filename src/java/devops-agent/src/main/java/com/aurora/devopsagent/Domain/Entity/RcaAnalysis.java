package com.aurora.devopsagent.Domain.Entity;

import com.aurora.devopsagent.Domain.Enums.RcaAnalysisStatus;
import com.aurora.devopsagent.Domain.Enums.RcaAnalysisType;
import com.aurora.shared.entity.AuditableEntity;
import jakarta.persistence.*;

import java.math.BigDecimal;

@Entity
@Table(name = "rca_analyses")
public class RcaAnalysis extends AuditableEntity {

    @ManyToOne(fetch = FetchType.LAZY)
    @JoinColumn(name = "incident_id", nullable = false)
    private Incident incident;

    @Column(name = "correlation_id", nullable = false, length = 64)
    private String correlationId;

    @Column(name = "analysis_type", nullable = false, length = 50)
    @Enumerated(EnumType.STRING)
    private RcaAnalysisType analysisType; // INFRASTRUCTURE, APPLICATION, DATABASE, NETWORK

    @Column(name = "status", nullable = false, length = 50)
    @Enumerated(EnumType.STRING)
    private RcaAnalysisStatus status; // PENDING, RUNNING, COMPLETED, FAILED, CANCELLED

    @Column(name = "recommendation_json", columnDefinition = "jsonb")
    private String recommendationJson; // Structured Recommendation (versioned)

    @Column(name = "recommendation_version", nullable = false)
    private int recommendationVersion = 1;

    @Column(name = "confidence", precision = 5, scale = 4)
    private BigDecimal confidence;

    @Column(name = "llm_tokens_used")
    private int llmTokensUsed;

    @Column(name = "context_quality_score", precision = 3, scale = 2)
    private BigDecimal contextQualityScore;

    @Column(name = "warning_flags_json", columnDefinition = "jsonb")
    private String warningFlagsJson;

    @Column(name = "duration_ms")
    private Long durationMs;

    public Incident getIncident() {
        return incident;
    }

    public void setIncident(Incident incident) {
        this.incident = incident;
    }

    public String getCorrelationId() {
        return correlationId;
    }

    public void setCorrelationId(String correlationId) {
        this.correlationId = correlationId;
    }

    public RcaAnalysisType getAnalysisType() {
        return analysisType;
    }

    public void setAnalysisType(RcaAnalysisType analysisType) {
        this.analysisType = analysisType;
    }

    public RcaAnalysisStatus getStatus() {
        return status;
    }

    public void setStatus(RcaAnalysisStatus status) {
        this.status = status;
    }

    public String getRecommendationJson() {
        return recommendationJson;
    }

    public void setRecommendationJson(String recommendationJson) {
        this.recommendationJson = recommendationJson;
    }

    public int getRecommendationVersion() {
        return recommendationVersion;
    }

    public void setRecommendationVersion(int recommendationVersion) {
        this.recommendationVersion = recommendationVersion;
    }

    public BigDecimal getConfidence() {
        return confidence;
    }

    public void setConfidence(BigDecimal confidence) {
        this.confidence = confidence;
    }

    public int getLlmTokensUsed() {
        return llmTokensUsed;
    }

    public void setLlmTokensUsed(int llmTokensUsed) {
        this.llmTokensUsed = llmTokensUsed;
    }

    public BigDecimal getContextQualityScore() {
        return contextQualityScore;
    }

    public void setContextQualityScore(BigDecimal contextQualityScore) {
        this.contextQualityScore = contextQualityScore;
    }

    public String getWarningFlagsJson() {
        return warningFlagsJson;
    }

    public void setWarningFlagsJson(String warningFlagsJson) {
        this.warningFlagsJson = warningFlagsJson;
    }

    public Long getDurationMs() {
        return durationMs;
    }

    public void setDurationMs(Long durationMs) {
        this.durationMs = durationMs;
    }
}
