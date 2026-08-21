package com.aurora.aigovernance.governance.domain.entity;

import com.aurora.shared.entity.AuditableEntity;
import com.aurora.aigovernance.governance.domain.enums.TenantStatus;
import jakarta.persistence.*;

import java.util.UUID;

@Entity
@Table(name = "tenants")
public class Tenant extends AuditableEntity {

    @Column(name = "external_tenant_id", nullable = false, unique = true)
    private UUID externalTenantId;

    @ManyToOne(fetch = FetchType.LAZY)
    @JoinColumn(name = "plan_id", nullable = false)
    private Plan plan;

    @Enumerated(EnumType.STRING)
    @Column(name = "status", nullable = false, length = 20)
    private TenantStatus status = TenantStatus.ACTIVE;

    @Column(name = "cloud_ai_enabled", nullable = false)
    private boolean cloudAiEnabled = true;

    public UUID getExternalTenantId() { return externalTenantId; }
    public void setExternalTenantId(UUID externalTenantId) { this.externalTenantId = externalTenantId; }

    public Plan getPlan() { return plan; }
    public void setPlan(Plan plan) { this.plan = plan; }

    public TenantStatus getStatus() { return status; }
    public void setStatus(TenantStatus status) { this.status = status; }

    public boolean isCloudAiEnabled() { return cloudAiEnabled; }
    public void setCloudAiEnabled(boolean cloudAiEnabled) { this.cloudAiEnabled = cloudAiEnabled; }
}
