package com.aurora.aigovernance.governance.domain;

import com.aurora.aigovernance.governance.domain.enums.QuotaMetric;
import com.aurora.aigovernance.governance.domain.enums.QuotaPeriod;
import com.aurora.aigovernance.governance.domain.valueobject.QuotaDefinition;
import org.junit.jupiter.api.Test;

import static org.junit.jupiter.api.Assertions.assertEquals;

public class QuotaDimensionMappingTest {

    @Test
    public void testRpmMapping_RequestsPerMinute() {
        QuotaDefinition rpm = new QuotaDefinition(QuotaMetric.REQUESTS, QuotaPeriod.MINUTE, 15);
        assertEquals(QuotaMetric.REQUESTS, rpm.metric());
        assertEquals(QuotaPeriod.MINUTE, rpm.period());
        assertEquals(15, rpm.limit());
    }

    @Test
    public void testTpmMapping_TokensPerMinute() {
        QuotaDefinition tpm = new QuotaDefinition(QuotaMetric.TOKENS, QuotaPeriod.MINUTE, 250000);
        assertEquals(QuotaMetric.TOKENS, tpm.metric());
        assertEquals(QuotaPeriod.MINUTE, tpm.period());
        assertEquals(250000, tpm.limit());
    }

    @Test
    public void testRpdMapping_RequestsPerDay() {
        QuotaDefinition rpd = new QuotaDefinition(QuotaMetric.REQUESTS, QuotaPeriod.DAY, 500);
        assertEquals(QuotaMetric.REQUESTS, rpd.metric());
        assertEquals(QuotaPeriod.DAY, rpd.period());
        assertEquals(500, rpd.limit());
    }

    @Test
    public void testMonthlyTokenBudget_TokensPerMonth() {
        QuotaDefinition monthly = new QuotaDefinition(QuotaMetric.TOKENS, QuotaPeriod.MONTH, 10000000);
        assertEquals(QuotaMetric.TOKENS, monthly.metric());
        assertEquals(QuotaPeriod.MONTH, monthly.period());
        assertEquals(10000000, monthly.limit());
    }
}
