package com.aurora.aigovernance.governance.application.port;

import com.aurora.aigovernance.governance.domain.enums.QuotaMetric;
import com.aurora.aigovernance.governance.domain.enums.QuotaPeriod;
import com.aurora.aigovernance.governance.domain.valueobject.PeriodKey;
import com.aurora.aigovernance.governance.domain.valueobject.QuotaDefinition;

import java.util.UUID;

/**
 * Port for tenant quota operations.
 * <p>
 * V1: implements soft quota via {@link #getCurrentUsage(UUID, QuotaMetric, QuotaPeriod, PeriodKey)}.
 * Production: atomic reservation via {@link #tryReserve(UUID, QuotaDefinition, long)}.
 * <p>
 * Domain model supports hard reservation from day one, even though V1 only uses soft check.
 */
public interface TenantQuotaPort {

    /**
     * V1 soft quota: get current usage for a quota dimension.
     */
    long getCurrentUsage(UUID tenantId, QuotaMetric metric, QuotaPeriod period, PeriodKey periodKey);

    /**
     * Production hard quota: atomic reserve.
     * V1 stub throws {@link UnsupportedOperationException}.
     */
    TenantQuotaReservation tryReserve(UUID tenantId, QuotaDefinition quota, long requestedAmount);

    /**
     * Reconcile actual usage against reservation.
     * V1 stub throws {@link UnsupportedOperationException}.
     */
    void reconcile(TenantQuotaReservation reservation, long actualAmount);

    /**
     * Release a reservation (e.g., on failure before execution).
     * V1 stub throws {@link UnsupportedOperationException}.
     */
    void release(TenantQuotaReservation reservation);

    /**
     * Reservation handle for production atomic quota management.
     */
    record TenantQuotaReservation(
            UUID tenantId,
            QuotaMetric metric,
            QuotaPeriod period,
            PeriodKey periodKey,
            long reservedAmount
    ) {}
}
