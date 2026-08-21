package com.aurora.aigovernance.governance.domain.valueobject;

import com.aurora.aigovernance.governance.domain.enums.QuotaMetric;
import com.aurora.aigovernance.governance.domain.enums.QuotaPeriod;

/**
 * Redis key components for tenant quota tracking.
 * <p>
 * Pattern: {@code ai:tenant-quota:{tenantId}:{metric}:{period}:{periodKey}}
 */
public record QuotaKey(
        String tenantId,
        QuotaMetric metric,
        QuotaPeriod period,
        PeriodKey periodKey
) {
    public String toRedisKey() {
        return String.format("ai:tenant-quota:%s:%s:%s:%s",
                tenantId, metric.name(), period.name(), periodKey.value());
    }
}
