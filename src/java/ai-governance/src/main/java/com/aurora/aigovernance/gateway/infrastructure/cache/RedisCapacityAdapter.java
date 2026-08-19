package com.aurora.aigovernance.gateway.infrastructure.cache;

import com.aurora.aigovernance.gateway.application.port.ProviderCapacityPort;
import com.aurora.aigovernance.gateway.application.port.ProviderRateWindowPolicy;
import com.aurora.aigovernance.gateway.domain.entity.ProviderSlot;
import com.aurora.aigovernance.gateway.domain.valueobject.ProviderCapacityLimits;
import com.aurora.aigovernance.gateway.domain.valueobject.ProviderReservation;
import com.aurora.aigovernance.gateway.domain.valueobject.RateWindow;
import com.aurora.aigovernance.gateway.domain.valueobject.SlotCapacity;
import com.aurora.aigovernance.governance.domain.enums.AiProvider;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.core.io.ClassPathResource;
import org.springframework.data.redis.core.StringRedisTemplate;
import org.springframework.data.redis.core.script.DefaultRedisScript;
import org.springframework.stereotype.Component;

import java.time.Instant;
import java.util.List;
import java.util.Map;
import java.util.Optional;
import java.util.UUID;

@Component
public class RedisCapacityAdapter implements ProviderCapacityPort {

    private static final Logger log = LoggerFactory.getLogger(RedisCapacityAdapter.class);

    private final StringRedisTemplate redisTemplate;
    private final Map<AiProvider, ProviderRateWindowPolicy> rateWindowPolicies;
    private final DefaultRedisScript<Long> reserveScript;

    public RedisCapacityAdapter(
            StringRedisTemplate redisTemplate,
            Map<AiProvider, ProviderRateWindowPolicy> rateWindowPolicies) {
        this.redisTemplate = redisTemplate;
        this.rateWindowPolicies = rateWindowPolicies;

        this.reserveScript = new DefaultRedisScript<>();
        this.reserveScript.setLocation(new ClassPathResource("lua/reserve_capacity.lua"));
        this.reserveScript.setResultType(Long.class);
    }

    @Override
    public Optional<ProviderReservation> tryReserve(
            ProviderSlot slot,
            ProviderCapacityLimits effectiveLimits,
            long requestedTokens) {

        Instant now = Instant.now();
        ProviderRateWindowPolicy windowPolicy = resolveWindowPolicy(slot.getProvider());

        RateWindow rpmWindow = windowPolicy.rpmWindow(slot, now);
        RateWindow tpmWindow = windowPolicy.tpmWindow(slot, now);
        RateWindow rpdWindow = windowPolicy.rpdWindow(slot, now);

        String rpmKey = buildCapacityKey(slot, "rpm", rpmWindow.bucketKey());
        String tpmKey = buildCapacityKey(slot, "tpm", tpmWindow.bucketKey());
        String rpdKey = buildCapacityKey(slot, "rpd", rpdWindow.bucketKey());

        List<String> keys = List.of(rpmKey, tpmKey, rpdKey);
        String[] args = new String[]{
                String.valueOf(effectiveLimits.rpmLimit()),
                String.valueOf(effectiveLimits.tpmLimit()),
                String.valueOf(effectiveLimits.rpdLimit()),
                String.valueOf(requestedTokens),
                String.valueOf(rpmWindow.ttlSeconds()),
                String.valueOf(tpmWindow.ttlSeconds()),
                String.valueOf(rpdWindow.ttlSeconds())
        };

        try {
            Long result = redisTemplate.execute(reserveScript, keys, (Object[]) args);
            if (result != null && result == 1L) {
                String reservationId = UUID.randomUUID().toString();
                return Optional.of(new ProviderReservation(
                        reservationId,
                        slot,
                        requestedTokens,
                        rpmKey,
                        tpmKey,
                        rpdKey
                ));
            }
            log.debug("Capacity reservation rejected by Lua script for slot: {}", slot.getSlotAlias());
            return Optional.empty();
        } catch (Exception e) {
            log.error("Redis error executing capacity reservation for slot {}: {}", slot.getSlotAlias(), e.getMessage());
            return Optional.empty();
        }
    }

    @Override
    public void reconcile(ProviderReservation reservation, long actualTokens) {
        long delta = actualTokens - reservation.reservedTokens();
        if (delta != 0) {
            try {
                redisTemplate.opsForValue().increment(reservation.tpmKey(), delta);
                log.debug("Reconciled TPM capacity for slot {}: delta={}", reservation.slot().getSlotAlias(), delta);
            } catch (Exception e) {
                log.warn("Failed to reconcile TPM capacity for slot {}: {}", reservation.slot().getSlotAlias(), e.getMessage());
            }
        }
    }

    @Override
    public void release(ProviderReservation reservation) {
        try {
            // Decrement RPM by 1, TPM by reservedTokens, RPD by 1
            redisTemplate.opsForValue().decrement(reservation.rpmKey(), 1);
            redisTemplate.opsForValue().decrement(reservation.tpmKey(), reservation.reservedTokens());
            redisTemplate.opsForValue().decrement(reservation.rpdKey(), 1);
            log.debug("Released reservation {} for slot {}", reservation.reservationId(), reservation.slot().getSlotAlias());
        } catch (Exception e) {
            log.warn("Failed to release reservation {} for slot {}: {}", reservation.reservationId(), reservation.slot().getSlotAlias(), e.getMessage());
        }
    }

    @Override
    public SlotCapacity getSlotCapacity(ProviderSlot slot) {
        Instant now = Instant.now();
        ProviderRateWindowPolicy windowPolicy = resolveWindowPolicy(slot.getProvider());

        String rpmKey = buildCapacityKey(slot, "rpm", windowPolicy.rpmWindow(slot, now).bucketKey());
        String tpmKey = buildCapacityKey(slot, "tpm", windowPolicy.tpmWindow(slot, now).bucketKey());
        String rpdKey = buildCapacityKey(slot, "rpd", windowPolicy.rpdWindow(slot, now).bucketKey());

        try {
            List<String> values = redisTemplate.opsForValue().multiGet(List.of(rpmKey, tpmKey, rpdKey));
            long rpm = (values != null && values.get(0) != null) ? Long.parseLong(values.get(0)) : 0L;
            long tpm = (values != null && values.get(1) != null) ? Long.parseLong(values.get(1)) : 0L;
            long rpd = (values != null && values.get(2) != null) ? Long.parseLong(values.get(2)) : 0L;
            return new SlotCapacity(rpm, tpm, rpd);
        } catch (Exception e) {
            log.warn("Failed to get slot capacity for {}: {}", slot.getSlotAlias(), e.getMessage());
            return new SlotCapacity(0, 0, 0);
        }
    }

    private String buildCapacityKey(ProviderSlot slot, String metric, String bucket) {
        return String.format("ai:capacity:%s:%s:%s:%s",
                slot.getProvider().name().toLowerCase(),
                slot.getSlotAlias(),
                metric,
                bucket);
    }

    private ProviderRateWindowPolicy resolveWindowPolicy(AiProvider provider) {
        ProviderRateWindowPolicy policy = rateWindowPolicies.get(provider);
        if (policy == null) {
            // Fallback default
            return new ProviderRateWindowPolicy() {
                @Override
                public RateWindow rpmWindow(ProviderSlot s, Instant n) { return new RateWindow("rpm:default", 70); }
                @Override
                public RateWindow tpmWindow(ProviderSlot s, Instant n) { return new RateWindow("tpm:default", 70); }
                @Override
                public RateWindow rpdWindow(ProviderSlot s, Instant n) { return new RateWindow("rpd:default", 90000); }
            };
        }
        return policy;
    }
}
