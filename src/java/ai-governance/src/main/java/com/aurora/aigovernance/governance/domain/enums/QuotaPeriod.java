package com.aurora.aigovernance.governance.domain.enums;

/**
 * Quota time period — WHEN is usage measured.
 * <p>
 * Combined with {@link QuotaMetric} to express quota types:
 * <ul>
 *   <li>{@code REQUESTS + MINUTE} = RPM</li>
 *   <li>{@code TOKENS + MINUTE} = TPM</li>
 *   <li>{@code REQUESTS + DAY} = RPD</li>
 *   <li>{@code TOKENS + MONTH} = Monthly Token Budget</li>
 * </ul>
 */
public enum QuotaPeriod {
    MINUTE,
    DAY,
    MONTH
}
