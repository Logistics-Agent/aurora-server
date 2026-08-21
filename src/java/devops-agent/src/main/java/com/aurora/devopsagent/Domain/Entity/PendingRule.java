package com.aurora.devopsagent.Domain.Entity;

import com.aurora.shared.entity.AuditableEntity;
import jakarta.persistence.Column;
import jakarta.persistence.Entity;
import jakarta.persistence.Table;

import java.util.UUID;

@Entity
@Table(name = "pending_rules")
public class PendingRule extends AuditableEntity {

    @Column(name = "proposed_rule_name", nullable = false, length = 100)
    private String proposedRuleName;

    @Column(name = "error_pattern", nullable = false, columnDefinition = "TEXT")
    private String errorPattern;

    @Column(name = "action_type", nullable = false, length = 50)
    private String actionType;

    @Column(name = "action_params_json", columnDefinition = "TEXT")
    private String actionParamsJson = "{}";

    @Column(name = "source_incident_id")
    private UUID sourceIncidentId;

    @Column(name = "status", nullable = false, length = 50)
    private String status = "PENDING"; // PENDING, APPROVED, REJECTED

    @Column(name = "governance_decision_id", length = 100)
    private String governanceDecisionId;

    @Column(name = "automation_level", length = 50)
    private String automationLevel;

    @Column(name = "requires_approval", nullable = false)
    private boolean requiresApproval = false;

    public String getProposedRuleName() {
        return proposedRuleName;
    }

    public void setProposedRuleName(String proposedRuleName) {
        this.proposedRuleName = proposedRuleName;
    }

    public String getErrorPattern() {
        return errorPattern;
    }

    public void setErrorPattern(String errorPattern) {
        this.errorPattern = errorPattern;
    }

    public String getErrorSignature() {
        return errorPattern;
    }

    public void setErrorSignature(String errorSignature) {
        this.errorPattern = errorSignature;
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

    public UUID getSourceIncidentId() {
        return sourceIncidentId;
    }

    public void setSourceIncidentId(UUID sourceIncidentId) {
        this.sourceIncidentId = sourceIncidentId;
    }

    public String getStatus() {
        return status;
    }

    public void setStatus(String status) {
        this.status = status;
    }

    public String getGovernanceDecisionId() {
        return governanceDecisionId;
    }

    public void setGovernanceDecisionId(String governanceDecisionId) {
        this.governanceDecisionId = governanceDecisionId;
    }

    public String getAutomationLevel() {
        return automationLevel;
    }

    public void setAutomationLevel(String automationLevel) {
        this.automationLevel = automationLevel;
    }

    public boolean isRequiresApproval() {
        return requiresApproval;
    }

    public void setRequiresApproval(boolean requiresApproval) {
        this.requiresApproval = requiresApproval;
    }
}
