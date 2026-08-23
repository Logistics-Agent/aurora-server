package com.aurora.aigovernance.gateway.infrastructure.provider.gemini;

import com.aurora.aigovernance.gateway.application.port.ProviderRateWindowPolicy;
import com.aurora.aigovernance.gateway.domain.entity.ProviderSlot;
import com.aurora.aigovernance.gateway.domain.valueobject.RateWindow;
import org.springframework.stereotype.Component;

import java.time.Instant;
import java.time.ZoneOffset;
import java.time.format.DateTimeFormatter;

@Component
public class GeminiRateWindowPolicy implements ProviderRateWindowPolicy {

    private static final DateTimeFormatter MINUTE_FMT =
            DateTimeFormatter.ofPattern("yyyyMMddHHmm").withZone(ZoneOffset.UTC);

    private static final DateTimeFormatter DAY_FMT =
            DateTimeFormatter.ofPattern("yyyyMMdd").withZone(ZoneOffset.UTC);

    @Override
    public RateWindow rpmWindow(ProviderSlot slot, Instant now) {
        return new RateWindow("rpm:" + MINUTE_FMT.format(now), 70); // 70s TTL
    }

    @Override
    public RateWindow tpmWindow(ProviderSlot slot, Instant now) {
        return new RateWindow("tpm:" + MINUTE_FMT.format(now), 70);
    }

    @Override
    public RateWindow rpdWindow(ProviderSlot slot, Instant now) {
        return new RateWindow("rpd:" + DAY_FMT.format(now), 90000); // 25h TTL
    }
}
