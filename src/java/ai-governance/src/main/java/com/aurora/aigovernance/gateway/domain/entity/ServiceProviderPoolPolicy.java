package com.aurora.aigovernance.gateway.domain.entity;

import com.aurora.shared.entity.AuditableEntity;
import jakarta.persistence.*;

/**
 * Maps a caller service to its allowed provider pool(s) with priority.
 * <p>
 * This represents <b>pool eligibility</b>, not provider-specific fallback.
 * Provider failover within a pool is handled by slot priority in routing.
 * <p>
 * Cross-pool fallback (e.g., DevOps→shared-ai) requires explicit additional policy entry.
 */
@Entity
@Table(name = "service_provider_pool_policies")
public class ServiceProviderPoolPolicy extends AuditableEntity {

    @Column(name = "service_id", nullable = false, length = 100)
    private String serviceId;

    @ManyToOne(fetch = FetchType.LAZY)
    @JoinColumn(name = "pool_id", nullable = false)
    private ProviderPool pool;

    @Column(name = "priority", nullable = false)
    private int priority;

    public String getServiceId() { return serviceId; }
    public void setServiceId(String serviceId) { this.serviceId = serviceId; }

    public ProviderPool getPool() { return pool; }
    public void setPool(ProviderPool pool) { this.pool = pool; }

    public int getPriority() { return priority; }
    public void setPriority(int priority) { this.priority = priority; }
}
