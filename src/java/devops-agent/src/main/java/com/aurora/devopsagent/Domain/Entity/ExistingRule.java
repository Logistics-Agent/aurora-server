package com.aurora.devopsagent.Domain.Entity;

import com.aurora.shared.entity.AuditableEntity;
import jakarta.persistence.Column;
import jakarta.persistence.Entity;
import jakarta.persistence.Table;

import java.math.BigDecimal;
import java.time.OffsetDateTime;

import org.hibernate.annotations.JdbcTypeCode;
import org.hibernate.type.SqlTypes;

@Entity
@Table(name = "existing_rules")
public class ExistingRule extends AuditableEntity {

    @Column(name = "rule_name", nullable = false, unique = true, length = 100)
    private String ruleName;

    @Column(name = "error_pattern", nullable = false, columnDefinition = "TEXT")
    private String errorPattern;

    @Column(name = "action_type", nullable = false, length = 50)
    private String actionType; // restart_pod, adjust_config, rollback_deployment

    @JdbcTypeCode(SqlTypes.JSON)
    @Column(name = "action_params_json", columnDefinition = "jsonb")
    private String actionParamsJson = "{}";

    @Column(name = "confidence", nullable = false, precision = 5, scale = 4)
    private BigDecimal confidence = new BigDecimal("1.0000");

    @Column(name = "active", nullable = false)
    private boolean active = true;

    @Column(name = "match_count", nullable = false)
    private int matchCount = 0;

    @Column(name = "last_matched_at")
    private OffsetDateTime lastMatchedAt;

    public String getName() {
        return ruleName;
    }

    public void setName(String name) {
        this.ruleName = name;
    }

    public String getRuleName() {
        return ruleName;
    }

    public void setRuleName(String ruleName) {
        this.ruleName = ruleName;
    }

    public String getErrorSignaturePattern() {
        return errorPattern;
    }

    public void setErrorSignaturePattern(String errorSignaturePattern) {
        this.errorPattern = errorSignaturePattern;
    }

    public String getErrorPattern() {
        return errorPattern;
    }

    public void setErrorPattern(String errorPattern) {
        this.errorPattern = errorPattern;
    }

    public String getTargetService() {
        return "";
    }

    public void setTargetService(String targetService) {
    }

    public String getTargetDeployment() {
        return "";
    }

    public void setTargetDeployment(String targetDeployment) {
    }

    public String getScopeConstraintJson() {
        return "{}";
    }

    public void setScopeConstraintJson(String scopeConstraintJson) {
    }

    public String getActionType() {
        return actionType;
    }

    public void setActionType(String actionType) {
        this.actionType = actionType;
    }

    public String getActionParamsJson() {
        return actionParamsJson;
    }

    public void setActionParamsJson(String actionParamsJson) {
        this.actionParamsJson = actionParamsJson;
    }

    public BigDecimal getConfidence() {
        return confidence;
    }

    public void setConfidence(BigDecimal confidence) {
        this.confidence = confidence;
    }

    public boolean isActive() {
        return active;
    }

    public void setActive(boolean active) {
        this.active = active;
    }

    public int getMatchCount() {
        return matchCount;
    }

    public void setMatchCount(int matchCount) {
        this.matchCount = matchCount;
    }

    public OffsetDateTime getLastMatchedAt() {
        return lastMatchedAt;
    }

    public void setLastMatchedAt(OffsetDateTime lastMatchedAt) {
        this.lastMatchedAt = lastMatchedAt;
    }
}
