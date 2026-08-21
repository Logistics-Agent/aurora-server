package com.aurora.aigovernance.governance.domain.valueobject;

/**
 * Time-bucketed key for quota period windows.
 * <p>
 * Examples:
 * <ul>
 *   <li>MINUTE → {@code "2026-08-17T22:38"}</li>
 *   <li>DAY → {@code "2026-08-17"}</li>
 *   <li>MONTH → {@code "2026-08"}</li>
 * </ul>
 *
 * @param value String representation of the time bucket
 */
public record PeriodKey(String value) {}
