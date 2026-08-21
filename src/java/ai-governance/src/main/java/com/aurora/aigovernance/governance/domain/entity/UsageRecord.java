package com.aurora.aigovernance.governance.domain.entity;

import com.aurora.shared.entity.AuditableEntity;
import com.aurora.aigovernance.governance.domain.enums.QuotaMetric;
import com.aurora.aigovernance.governance.domain.enums.QuotaPeriod;
import jakarta.persistence.*;

import java.util.UUID;

/**
 * Durable usage record per (tenant, quota_metric, quota_period, period_key).
 * <p>
 * PostgreSQL is the source of truth. Redis mirrors for fast quota checks.
 */
@Entity
@Table(name = "usage_records",
        uniqueConstraints = @UniqueConstraint(
                columnNames = {"tenant_id", "quota_metric", "quota_period", "period_key"}))
public class UsageRecord extends AuditableEntity {

    @Column(name = "tenant_id", nullable = false)
    private UUID tenantId;

    @Enumerated(EnumType.STRING)
    @Column(name = "quota_metric", nullable = false, length = 20)
    private QuotaMetric quotaMetric;

    @Enumerated(EnumType.STRING)
    @Column(name = "quota_period", nullable = false, length = 20)
    private QuotaPeriod quotaPeriod;

    @Column(name = "period_key", nullable = false, length = 30)
    private String periodKey;

    @Column(name = "usage_value", nullable = false)
    private long usageValue;

    public UUID getTenantId() { return tenantId; }
    public void setTenantId(UUID tenantId) { this.tenantId = tenantId; }

    public QuotaMetric getQuotaMetric() { return quotaMetric; }
    public void setQuotaMetric(QuotaMetric quotaMetric) { this.quotaMetric = quotaMetric; }

    public QuotaPeriod getQuotaPeriod() { return quotaPeriod; }
    public void setQuotaPeriod(QuotaPeriod quotaPeriod) { this.quotaPeriod = quotaPeriod; }

    public String getPeriodKey() { return periodKey; }
    public void setPeriodKey(String periodKey) { this.periodKey = periodKey; }

    public long getUsageValue() { return usageValue; }
    public void setUsageValue(long usageValue) { this.usageValue = usageValue; }
}
