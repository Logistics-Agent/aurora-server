package com.aurora.aigovernance.gateway.domain.entity;

import com.aurora.shared.entity.AuditableEntity;
import com.aurora.aigovernance.governance.domain.enums.AiProvider;
import com.aurora.aigovernance.shared.domain.AiOperation;
import jakarta.persistence.*;

import java.time.OffsetDateTime;

/**
 * A single provider project/deployment capacity boundary.
 * <p>
 * One slot = one operation = one model/deployment.
 * Embedding requests cannot route to generation-only slots and vice versa.
 * <p>
 * Physical rate limits ({@code rpmLimit}, {@code tpmLimit}, {@code rpdLimit})
 * are environment-specific per-slot configuration, not global constants.
 * Provider headroom is applied by {@code ProviderCapacityLimitPolicy} to produce effective limits.
 */
@Entity
@Table(name = "provider_slots",
        indexes = @Index(name = "idx_slot_routing",
                columnList = "pool_id, provider, operation, enabled, priority"))
public class ProviderSlot extends AuditableEntity {

    @ManyToOne(fetch = FetchType.LAZY)
    @JoinColumn(name = "pool_id", nullable = false)
    private ProviderPool pool;

    @Enumerated(EnumType.STRING)
    @Column(name = "provider", nullable = false, length = 30)
    private AiProvider provider;

    @Enumerated(EnumType.STRING)
    @Column(name = "operation", nullable = false, length = 20)
    private AiOperation operation;

    @Column(name = "slot_alias", nullable = false, unique = true, length = 80)
    private String slotAlias;

    @Column(name = "project_id", length = 100)
    private String projectId;

    @Column(name = "secret_ref", nullable = false, length = 200)
    private String secretRef;

    @Column(name = "model_name", nullable = false, length = 100)
    private String modelName;

    @Column(name = "rpm_limit", nullable = false)
    private int rpmLimit;

    @Column(name = "tpm_limit", nullable = false)
    private int tpmLimit;

    @Column(name = "rpd_limit", nullable = false)
    private int rpdLimit;

    @Column(name = "priority", nullable = false)
    private int priority;

    @Column(name = "weight", nullable = false)
    private int weight = 1;

    @Column(name = "enabled", nullable = false)
    private boolean enabled = true;

    @Column(name = "cooldown_until")
    private OffsetDateTime cooldownUntil;

    // --- Getters & Setters ---

    public ProviderPool getPool() { return pool; }
    public void setPool(ProviderPool pool) { this.pool = pool; }

    public AiProvider getProvider() { return provider; }
    public void setProvider(AiProvider provider) { this.provider = provider; }

    public AiOperation getOperation() { return operation; }
    public void setOperation(AiOperation operation) { this.operation = operation; }

    public String getSlotAlias() { return slotAlias; }
    public void setSlotAlias(String slotAlias) { this.slotAlias = slotAlias; }

    public String getProjectId() { return projectId; }
    public void setProjectId(String projectId) { this.projectId = projectId; }

    public String getSecretRef() { return secretRef; }
    public void setSecretRef(String secretRef) { this.secretRef = secretRef; }

    public String getModelName() { return modelName; }
    public void setModelName(String modelName) { this.modelName = modelName; }

    public int getRpmLimit() { return rpmLimit; }
    public void setRpmLimit(int rpmLimit) { this.rpmLimit = rpmLimit; }

    public int getTpmLimit() { return tpmLimit; }
    public void setTpmLimit(int tpmLimit) { this.tpmLimit = tpmLimit; }

    public int getRpdLimit() { return rpdLimit; }
    public void setRpdLimit(int rpdLimit) { this.rpdLimit = rpdLimit; }

    public int getPriority() { return priority; }
    public void setPriority(int priority) { this.priority = priority; }

    public int getWeight() { return weight; }
    public void setWeight(int weight) { this.weight = weight; }

    public boolean isEnabled() { return enabled; }
    public void setEnabled(boolean enabled) { this.enabled = enabled; }

    public OffsetDateTime getCooldownUntil() { return cooldownUntil; }
    public void setCooldownUntil(OffsetDateTime cooldownUntil) { this.cooldownUntil = cooldownUntil; }

    public boolean isInCooldown() {
        return cooldownUntil != null && OffsetDateTime.now().isBefore(cooldownUntil);
    }
}
