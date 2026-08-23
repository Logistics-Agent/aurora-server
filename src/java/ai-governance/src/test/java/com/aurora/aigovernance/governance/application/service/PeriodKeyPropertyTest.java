package com.aurora.aigovernance.governance.application.service;

import com.aurora.aigovernance.governance.domain.enums.QuotaPeriod;
import com.aurora.aigovernance.governance.domain.valueobject.PeriodKey;
import net.jqwik.api.*;

import java.time.Instant;

public class PeriodKeyPropertyTest {

    private final PeriodKeyCalculator calculator = new PeriodKeyCalculator();

    @Property
    public boolean minutePeriodKeyAlwaysFollowsUtcFormat(@ForAll("validInstants") Instant instant) {
        PeriodKey key = calculator.calculate(QuotaPeriod.MINUTE, instant);
        // Format: yyyy-MM-ddTHH:mm (length 16)
        return key.value() != null && key.value().length() == 16 && key.value().charAt(10) == 'T';
    }

    @Property
    public boolean dayPeriodKeyAlwaysFollowsUtcFormat(@ForAll("validInstants") Instant instant) {
        PeriodKey key = calculator.calculate(QuotaPeriod.DAY, instant);
        // Format: yyyy-MM-dd (length 10)
        return key.value() != null && key.value().length() == 10;
    }

    @Property
    public boolean monthPeriodKeyAlwaysFollowsUtcFormat(@ForAll("validInstants") Instant instant) {
        PeriodKey key = calculator.calculate(QuotaPeriod.MONTH, instant);
        // Format: yyyy-MM (length 7)
        return key.value() != null && key.value().length() == 7;
    }

    @Provide
    Arbitrary<Instant> validInstants() {
        return Arbitraries.longs()
                .between(1577836800L, 1893456000L) // 2020-01-01 to 2030-01-01
                .map(Instant::ofEpochSecond);
    }
}
