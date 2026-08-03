package com.aurora.devopsagent.Application.Services;

import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.data.redis.core.StringRedisTemplate;
import org.springframework.stereotype.Service;

import java.time.Duration;
import java.time.Instant;
import java.util.UUID;

/**
 * AntiFlappingTracker: Redis ZSET sliding window 10 phút để phát hiện flapping Low incidents.
 */
@Service
public class AntiFlappingTracker {

    private static final Logger log = LoggerFactory.getLogger(AntiFlappingTracker.class);
    private static final String FLAP_KEY_PREFIX = "devops:flap:";
    private static final long TEN_MINUTES_MS = 10 * 60 * 1000L;

    private final StringRedisTemplate redisTemplate;

    public AntiFlappingTracker(StringRedisTemplate redisTemplate) {
        this.redisTemplate = redisTemplate;
    }

    public boolean recordEventAndCheckFlapping(String dedupKey) {
        String flapKey = FLAP_KEY_PREFIX + dedupKey;
        long nowMs = Instant.now().toEpochMilli();
        String eventId = UUID.randomUUID().toString();

        // 1. ZADD
        redisTemplate.opsForZSet().add(flapKey, eventId, nowMs);

        // 2. Remove events older than 10 minutes
        redisTemplate.opsForZSet().removeRangeByScore(flapKey, 0, nowMs - TEN_MINUTES_MS);

        // 3. Count events in window
        Long count = redisTemplate.opsForZSet().zCard(flapKey);

        // 4. Set expire 10 mins
        redisTemplate.expire(flapKey, Duration.ofMinutes(10));

        boolean isFlapping = count != null && count > 3;
        if (isFlapping) {
            log.warn("Flapping detected for Dedup Key {}: {} occurrences in 10 minutes window. Escalating severity to Medium.", dedupKey, count);
        }
        return isFlapping;
    }
}
