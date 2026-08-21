package com.aurora.aigovernance.governance.domain.valueobject;

import com.aurora.aigovernance.governance.domain.enums.QuotaMetric;
import com.aurora.aigovernance.governance.domain.enums.QuotaPeriod;

/**
 * Convenience value object combining quota metric, period, and limit.
 * <p>
 * Examples:
 * <ul>
 *   <li>{@code REQUESTS + MINUTE + 10} → RPM limit of 10</li>
 *   <li>{@code TOKENS + MINUTE + 150000} → TPM limit of 150K</li>
 *   <li>{@code REQUESTS + DAY + 300} → RPD limit of 300</li>
 * </ul>
 */
public record QuotaDefinition(
        QuotaMetric metric,
        QuotaPeriod period,
        long limit
) {}
