package com.aurora.aigovernance.governance.infrastructure.cache;

import com.aurora.aigovernance.governance.application.port.TenantQuotaPort;
import com.aurora.aigovernance.governance.domain.enums.QuotaMetric;
import com.aurora.aigovernance.governance.domain.enums.QuotaPeriod;
import com.aurora.aigovernance.governance.domain.valueobject.PeriodKey;
import com.aurora.aigovernance.governance.domain.valueobject.QuotaDefinition;
import com.aurora.aigovernance.governance.domain.valueobject.QuotaKey;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.data.redis.core.StringRedisTemplate;
import org.springframework.stereotype.Component;

import java.time.Duration;
import java.util.UUID;

/**
 * Redis adapter for tenant quota tracking.
 * <p>
 * Implements V1 soft quota check via GET.
 * Domain interface ready for production hard atomic reservation.
 */
@Component
public class QuotaRedisAdapter implements TenantQuotaPort {

    private static final Logger log = LoggerFactory.getLogger(QuotaRedisAdapter.class);

    private final StringRedisTemplate redisTemplate;

    public QuotaRedisAdapter(StringRedisTemplate redisTemplate) {
        this.redisTemplate = redisTemplate;
    }

    @Override
    public long getCurrentUsage(UUID tenantId, QuotaMetric metric, QuotaPeriod period, PeriodKey periodKey) {
        QuotaKey key = new QuotaKey(tenantId.toString(), metric, period, periodKey);
        String redisKey = key.toRedisKey();
        try {
            String val = redisTemplate.opsForValue().get(redisKey);
            if (val == null) {
                return 0L;
            }
            return Long.parseLong(val);
        } catch (Exception e) {
            log.warn("Failed to read tenant quota from Redis: key={}, error={}", redisKey, e.getMessage());
            return 0L; // Non-blocking for soft quota read failure
        }
    }

    /**
     * Increment usage in Redis (called by QuotaSyncService after successful AI execution).
     */
    public void incrementUsage(UUID tenantId, QuotaMetric metric, QuotaPeriod period, PeriodKey periodKey, long amount) {
        QuotaKey key = new QuotaKey(tenantId.toString(), metric, period, periodKey);
        String redisKey = key.toRedisKey();
        try {
            Long newVal = redisTemplate.opsForValue().increment(redisKey, amount);
            // Set TTL if newly created
            if (newVal != null && newVal == amount) {
                Duration ttl = resolveTtl(period);
                redisTemplate.expire(redisKey, ttl);
            }
        } catch (Exception e) {
            log.error("Failed to increment tenant quota in Redis: key={}, amount={}, error={}", redisKey, amount, e.getMessage());
        }
    }

    private Duration resolveTtl(QuotaPeriod period) {
        return switch (period) {
            case MINUTE -> Duration.ofMinutes(5);
            case DAY -> Duration.ofDays(2);
            case MONTH -> Duration.ofDays(35);
        };
    }

    @Override
    public TenantQuotaReservation tryReserve(UUID tenantId, QuotaDefinition quota, long requestedAmount) {
        throw new UnsupportedOperationException("Production hard atomic quota reservation is deferred for V1 demo.");
    }

    @Override
    public void reconcile(TenantQuotaReservation reservation, long actualAmount) {
        throw new UnsupportedOperationException("Production hard atomic quota reservation is deferred for V1 demo.");
    }

    @Override
    public void release(TenantQuotaReservation reservation) {
        throw new UnsupportedOperationException("Production hard atomic quota reservation is deferred for V1 demo.");
    }
}
