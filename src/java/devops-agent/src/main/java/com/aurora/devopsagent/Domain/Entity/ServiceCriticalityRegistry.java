package com.aurora.devopsagent.Domain.Entity;

import com.aurora.shared.entity.AuditableEntity;
import jakarta.persistence.*;

import java.math.BigDecimal;

@Entity
@Table(name = "service_criticality_registry")
public class ServiceCriticalityRegistry extends AuditableEntity {

    @Column(name = "service_name", nullable = false, unique = true, length = 100)
    private String serviceName;

    @Column(name = "criticality_tier", nullable = false, length = 20)
    private String criticalityTier; // CRITICAL, HIGH, MEDIUM, LOW

    @Column(name = "weight", nullable = false, precision = 3, scale = 2)
    private BigDecimal weight = BigDecimal.ONE;

    @Column(name = "owner_team", length = 100)
    private String ownerTeam;

    @Column(name = "slo_availability_percent", precision = 5, scale = 2)
    private BigDecimal sloAvailabilityPercent;

    @Column(name = "slo_latency_ms")
    private Integer sloLatencyMs;

    public String getServiceName() {
        return serviceName;
    }

    public void setServiceName(String serviceName) {
        this.serviceName = serviceName;
    }

    public String getCriticalityTier() {
        return criticalityTier;
    }

    public void setCriticalityTier(String criticalityTier) {
        this.criticalityTier = criticalityTier;
    }

    public BigDecimal getWeight() {
        return weight;
    }

    public void setWeight(BigDecimal weight) {
        this.weight = weight;
    }

    public String getOwnerTeam() {
        return ownerTeam;
    }

    public void setOwnerTeam(String ownerTeam) {
        this.ownerTeam = ownerTeam;
    }

    public BigDecimal getSloAvailabilityPercent() {
        return sloAvailabilityPercent;
    }

    public void setSloAvailabilityPercent(BigDecimal sloAvailabilityPercent) {
        this.sloAvailabilityPercent = sloAvailabilityPercent;
    }

    public Integer getSloLatencyMs() {
        return sloLatencyMs;
    }

    public void setSloLatencyMs(Integer sloLatencyMs) {
        this.sloLatencyMs = sloLatencyMs;
    }
}
