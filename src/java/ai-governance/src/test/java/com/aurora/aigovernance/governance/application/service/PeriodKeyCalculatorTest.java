package com.aurora.aigovernance.governance.application.service;

import com.aurora.aigovernance.governance.domain.enums.QuotaPeriod;
import com.aurora.aigovernance.governance.domain.valueobject.PeriodKey;
import org.junit.jupiter.api.Test;

import java.time.Instant;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertNotNull;

public class PeriodKeyCalculatorTest {

    private final PeriodKeyCalculator calculator = new PeriodKeyCalculator();

    @Test
    public void testMinuteBucketCalculation() {
        Instant instant = Instant.parse("2026-08-18T10:15:30Z");
        PeriodKey key = calculator.calculate(QuotaPeriod.MINUTE, instant);
        assertEquals("2026-08-18T10:15", key.value());
    }

    @Test
    public void testDayBucketCalculation() {
        Instant instant = Instant.parse("2026-08-18T10:15:30Z");
        PeriodKey key = calculator.calculate(QuotaPeriod.DAY, instant);
        assertEquals("2026-08-18", key.value());
    }

    @Test
    public void testMonthBucketCalculation() {
        Instant instant = Instant.parse("2026-08-18T10:15:30Z");
        PeriodKey key = calculator.calculate(QuotaPeriod.MONTH, instant);
        assertEquals("2026-08", key.value());
    }

    @Test
    public void testCalculateNowNotNull() {
        PeriodKey key = calculator.calculateNow(QuotaPeriod.MINUTE);
        assertNotNull(key);
        assertNotNull(key.value());
    }
}
