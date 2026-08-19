package com.aurora.devopsagent.Domain.Entity;

import com.aurora.shared.entity.AuditableEntity;
import jakarta.persistence.Column;
import jakarta.persistence.Entity;
import jakarta.persistence.Table;

import java.util.UUID;

@Entity
@Table(name = "existing_rules")
public class ExistingRule extends AuditableEntity {

    @Column(name = "name", nullable = false, length = 200)
    private String name;

    @Column(name = "error_signature_pattern", nullable = false, columnDefinition = "TEXT")
    private String errorSignaturePattern;

    @Column(name = "target_service", nullable = false, length = 100)
    private String targetService;

    @Column(name = "target_deployment", length = 200)
    private String targetDeployment;

    @Column(name = "action_type", nullable = false, length = 50)
    private String actionType; // restart_pod, adjust_config, rollback_deployment

    @Column(name = "action_params_json", nullable = false, columnDefinition = "TEXT")
    private String actionParamsJson;

    @Column(name = "scope_constraint_json", nullable = false, columnDefinition = "TEXT")
    private String scopeConstraintJson;

    @Column(name = "promoted_from_pending_id")
    private UUID promotedFromPendingId;

    public String getName() {
        return name;
    }

    public void setName(String name) {
        this.name = name;
    }

    public String getErrorSignaturePattern() {
        return errorSignaturePattern;
    }

    public void setErrorSignaturePattern(String errorSignaturePattern) {
        this.errorSignaturePattern = errorSignaturePattern;
    }

    public String getTargetService() {
        return targetService;
    }

    public void setTargetService(String targetService) {
        this.targetService = targetService;
    }

    public String getTargetDeployment() {
        return targetDeployment;
    }

    public void setTargetDeployment(String targetDeployment) {
        this.targetDeployment = targetDeployment;
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

    public String getScopeConstraintJson() {
        return scopeConstraintJson;
    }

    public void setScopeConstraintJson(String scopeConstraintJson) {
        this.scopeConstraintJson = scopeConstraintJson;
    }

    public UUID getPromotedFromPendingId() {
        return promotedFromPendingId;
    }

    public void setPromotedFromPendingId(UUID promotedFromPendingId) {
        this.promotedFromPendingId = promotedFromPendingId;
    }
}
