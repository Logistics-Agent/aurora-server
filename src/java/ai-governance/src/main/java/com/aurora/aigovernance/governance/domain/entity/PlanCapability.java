package com.aurora.aigovernance.governance.domain.entity;

import com.aurora.shared.entity.AuditableEntity;
import com.aurora.aigovernance.governance.domain.enums.AiProvider;
import com.aurora.aigovernance.governance.domain.enums.AutomationLevel;
import com.aurora.aigovernance.governance.domain.enums.ModelTier;
import jakarta.persistence.*;

@Entity
@Table(name = "plan_capabilities")
public class PlanCapability extends AuditableEntity {

    @ManyToOne(fetch = FetchType.LAZY)
    @JoinColumn(name = "plan_id", nullable = false)
    private Plan plan;

    @Column(name = "capability_code", nullable = false, length = 100)
    private String capabilityCode;

    @Column(name = "allowed_providers", length = 200)
    private String allowedProviders;

    @Enumerated(EnumType.STRING)
    @Column(name = "model_tier", length = 20)
    private ModelTier modelTier;

    @Column(name = "max_tokens")
    private int maxTokens;

    @Enumerated(EnumType.STRING)
    @Column(name = "automation_level", length = 30)
    private AutomationLevel automationLevel;

    @Column(name = "require_approval")
    private boolean requireApproval;

    public Plan getPlan() { return plan; }
    public void setPlan(Plan plan) { this.plan = plan; }

    public String getCapabilityCode() { return capabilityCode; }
    public void setCapabilityCode(String capabilityCode) { this.capabilityCode = capabilityCode; }

    /**
     * Comma-separated list of allowed provider names (e.g. "GEMINI,AZURE_OPENAI").
     * Parsed to {@code Set<AiProvider>} by service layer.
     */
    public String getAllowedProviders() { return allowedProviders; }
    public void setAllowedProviders(String allowedProviders) { this.allowedProviders = allowedProviders; }

    public ModelTier getModelTier() { return modelTier; }
    public void setModelTier(ModelTier modelTier) { this.modelTier = modelTier; }

    public int getMaxTokens() { return maxTokens; }
    public void setMaxTokens(int maxTokens) { this.maxTokens = maxTokens; }

    public AutomationLevel getAutomationLevel() { return automationLevel; }
    public void setAutomationLevel(AutomationLevel automationLevel) { this.automationLevel = automationLevel; }

    public boolean isRequireApproval() { return requireApproval; }
    public void setRequireApproval(boolean requireApproval) { this.requireApproval = requireApproval; }
}
