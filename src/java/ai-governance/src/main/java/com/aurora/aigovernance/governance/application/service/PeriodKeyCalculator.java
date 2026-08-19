package com.aurora.aigovernance.governance.application.service;

import com.aurora.aigovernance.governance.domain.enums.QuotaPeriod;
import com.aurora.aigovernance.governance.domain.valueobject.PeriodKey;
import org.springframework.stereotype.Component;

import java.time.Instant;
import java.time.ZoneOffset;
import java.time.format.DateTimeFormatter;

/**
 * Calculates time-bucketed period keys for quota tracking.
 * <p>
 * All calculations use UTC.
 */
@Component
public class PeriodKeyCalculator {

    private static final DateTimeFormatter MINUTE_FORMAT =
            DateTimeFormatter.ofPattern("yyyy-MM-dd'T'HH:mm").withZone(ZoneOffset.UTC);

    private static final DateTimeFormatter DAY_FORMAT =
            DateTimeFormatter.ofPattern("yyyy-MM-dd").withZone(ZoneOffset.UTC);

    private static final DateTimeFormatter MONTH_FORMAT =
            DateTimeFormatter.ofPattern("yyyy-MM").withZone(ZoneOffset.UTC);

    /**
     * Generate a period key for the given period at the current instant.
     *
     * @return PeriodKey with format:
     *   MINUTE → "2026-08-17T22:38"
     *   DAY → "2026-08-17"
     *   MONTH → "2026-08"
     */
    public PeriodKey calculate(QuotaPeriod period, Instant now) {
        return switch (period) {
            case MINUTE -> new PeriodKey(MINUTE_FORMAT.format(now));
            case DAY -> new PeriodKey(DAY_FORMAT.format(now));
            case MONTH -> new PeriodKey(MONTH_FORMAT.format(now));
        };
    }

    public PeriodKey calculateNow(QuotaPeriod period) {
        return calculate(period, Instant.now());
    }
}
