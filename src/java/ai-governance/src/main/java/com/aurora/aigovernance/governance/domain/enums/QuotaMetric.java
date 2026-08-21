package com.aurora.aigovernance.governance.domain.enums;

/**
 * Quota measurement dimension — WHAT is being measured.
 * <p>
 * Combined with {@link QuotaPeriod} to represent quota types:
 * <ul>
 *   <li>{@code REQUESTS + MINUTE} = RPM (requests per minute)</li>
 *   <li>{@code TOKENS + MINUTE} = TPM (tokens per minute)</li>
 *   <li>{@code REQUESTS + DAY} = RPD (requests per day)</li>
 *   <li>{@code TOKENS + MONTH} = Monthly Token Budget</li>
 * </ul>
 * <p>
 * No hard-coded RPM/TPM/RPD constants — these are just shorthand for metric × period.
 */
public enum QuotaMetric {
    REQUESTS,
    TOKENS
}
