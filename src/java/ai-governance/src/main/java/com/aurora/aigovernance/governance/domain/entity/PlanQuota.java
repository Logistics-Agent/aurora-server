package com.aurora.aigovernance.governance.domain.entity;

import com.aurora.aigovernance.governance.domain.enums.QuotaMetric;
import com.aurora.aigovernance.governance.domain.enums.QuotaPeriod;
import jakarta.persistence.*;

import java.io.Serializable;
import java.util.Objects;
import java.util.UUID;

/**
 * Plan quota configuration using composite key (plan_id, quota_metric, quota_period).
 * <p>
 * Quota types are NOT hard-coded RPM/TPM/RPD constants. They are expressed as
 * {@code metric × period} combinations:
 * <ul>
 *   <li>{@code REQUESTS + MINUTE} = RPM</li>
 *   <li>{@code TOKENS + MINUTE} = TPM</li>
 *   <li>{@code REQUESTS + DAY} = RPD</li>
 * </ul>
 */
@Entity
@Table(name = "plan_quotas")
@IdClass(PlanQuota.PlanQuotaId.class)
public class PlanQuota {

    @Id
    @ManyToOne(fetch = FetchType.LAZY)
    @JoinColumn(name = "plan_id", nullable = false)
    private Plan plan;

    @Id
    @Enumerated(EnumType.STRING)
    @Column(name = "quota_metric", nullable = false, length = 20)
    private QuotaMetric quotaMetric;

    @Id
    @Enumerated(EnumType.STRING)
    @Column(name = "quota_period", nullable = false, length = 20)
    private QuotaPeriod quotaPeriod;

    @Column(name = "limit_value", nullable = false)
    private long limitValue;

    public Plan getPlan() { return plan; }
    public void setPlan(Plan plan) { this.plan = plan; }

    public QuotaMetric getQuotaMetric() { return quotaMetric; }
    public void setQuotaMetric(QuotaMetric quotaMetric) { this.quotaMetric = quotaMetric; }

    public QuotaPeriod getQuotaPeriod() { return quotaPeriod; }
    public void setQuotaPeriod(QuotaPeriod quotaPeriod) { this.quotaPeriod = quotaPeriod; }

    public long getLimitValue() { return limitValue; }
    public void setLimitValue(long limitValue) { this.limitValue = limitValue; }

    /**
     * Composite primary key for PlanQuota.
     */
    public static class PlanQuotaId implements Serializable {
        private UUID plan;
        private QuotaMetric quotaMetric;
        private QuotaPeriod quotaPeriod;

        public PlanQuotaId() {}

        public PlanQuotaId(UUID plan, QuotaMetric quotaMetric, QuotaPeriod quotaPeriod) {
            this.plan = plan;
            this.quotaMetric = quotaMetric;
            this.quotaPeriod = quotaPeriod;
        }

        @Override
        public boolean equals(Object o) {
            if (this == o) return true;
            if (!(o instanceof PlanQuotaId that)) return false;
            return Objects.equals(plan, that.plan) &&
                    quotaMetric == that.quotaMetric &&
                    quotaPeriod == that.quotaPeriod;
        }

        @Override
        public int hashCode() {
            return Objects.hash(plan, quotaMetric, quotaPeriod);
        }
    }
}
