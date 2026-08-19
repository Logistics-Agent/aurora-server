package com.aurora.aigovernance.governance.domain.entity;

import com.aurora.shared.entity.AuditableEntity;
import com.aurora.aigovernance.governance.domain.enums.AiProvider;
import jakarta.persistence.*;

import java.util.ArrayList;
import java.util.List;

@Entity
@Table(name = "plans")
public class Plan extends AuditableEntity {

    @Column(name = "code", nullable = false, unique = true, length = 50)
    private String code;

    @Column(name = "name", nullable = false, length = 100)
    private String name;

    @Enumerated(EnumType.STRING)
    @Column(name = "default_provider", length = 30)
    private AiProvider defaultProvider;

    @Column(name = "cloud_ai_enabled", nullable = false)
    private boolean cloudAiEnabled = true;

    @OneToMany(mappedBy = "plan", cascade = CascadeType.ALL, orphanRemoval = true)
    private List<PlanCapability> capabilities = new ArrayList<>();

    @OneToMany(mappedBy = "plan", cascade = CascadeType.ALL, orphanRemoval = true)
    private List<PlanQuota> quotas = new ArrayList<>();

    public String getCode() { return code; }
    public void setCode(String code) { this.code = code; }

    public String getName() { return name; }
    public void setName(String name) { this.name = name; }

    public AiProvider getDefaultProvider() { return defaultProvider; }
    public void setDefaultProvider(AiProvider defaultProvider) { this.defaultProvider = defaultProvider; }

    public boolean isCloudAiEnabled() { return cloudAiEnabled; }
    public void setCloudAiEnabled(boolean cloudAiEnabled) { this.cloudAiEnabled = cloudAiEnabled; }

    public List<PlanCapability> getCapabilities() { return capabilities; }
    public List<PlanQuota> getQuotas() { return quotas; }
}
