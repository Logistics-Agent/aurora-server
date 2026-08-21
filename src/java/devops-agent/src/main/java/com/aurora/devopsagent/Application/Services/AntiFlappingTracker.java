package com.aurora.devopsagent.Application.Services;

import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.data.redis.core.StringRedisTemplate;
import org.springframework.data.redis.core.script.DefaultRedisScript;
import org.springframework.stereotype.Service;

import java.nio.charset.StandardCharsets;
import java.security.MessageDigest;
import java.security.NoSuchAlgorithmException;
import java.time.Instant;
import java.util.Collections;
import java.util.UUID;

/**
 * AntiFlappingTracker: Redis ZSET sliding window 10 phút để phát hiện flapping incidents.
 * Threshold: >= 5 occurrences / 10 minutes window.
 * Uses atomic Redis Lua script (ZADD + ZREMRANGEBYSCORE + ZCARD + EXPIRE).
 */
@Service
public class AntiFlappingTracker {

    private static final Logger log = LoggerFactory.getLogger(AntiFlappingTracker.class);
    private static final String FLAP_KEY_PREFIX = "devops:flap:";
    private static final long TEN_MINUTES_MS = 10 * 60 * 1000L;
    private static final int FLAPPING_THRESHOLD = 5;

    private static final String ANTI_FLAP_LUA_SCRIPT =
            "local key = KEYS[1]\n" +
            "local member = ARGV[1]\n" +
            "local score = tonumber(ARGV[2])\n" +
            "local windowStart = tonumber(ARGV[3])\n" +
            "local ttl = tonumber(ARGV[4])\n" +
            "\n" +
            "redis.call('ZADD', key, score, member)\n" +
            "redis.call('ZREMRANGEBYSCORE', key, 0, windowStart)\n" +
            "local count = redis.call('ZCARD', key)\n" +
            "redis.call('EXPIRE', key, ttl)\n" +
            "return count";

    private final StringRedisTemplate redisTemplate;
    private final DefaultRedisScript<Long> redisScript;

    public AntiFlappingTracker(StringRedisTemplate redisTemplate) {
        this.redisTemplate = redisTemplate;
        this.redisScript = new DefaultRedisScript<>(ANTI_FLAP_LUA_SCRIPT, Long.class);
    }

    /**
     * Record alert occurrence and check if flapping threshold (>= 5 in 10 mins) is reached.
     */
    public boolean recordEventAndCheckFlapping(String errorSignature) {
        String normalizedKey = FLAP_KEY_PREFIX + hashSignature(errorSignature);
        long nowMs = Instant.now().toEpochMilli();
        long windowStartMs = nowMs - TEN_MINUTES_MS;
        String eventId = UUID.randomUUID().toString();

        Long count = redisTemplate.execute(
                redisScript,
                Collections.singletonList(normalizedKey),
                eventId,
                String.valueOf(nowMs),
                String.valueOf(windowStartMs),
                "600" // 10 minutes TTL
        );

        boolean isFlapping = count != null && count >= FLAPPING_THRESHOLD;
        if (isFlapping) {
            log.warn("Flapping detected for signature [{}]: {} occurrences in 10 minutes window. Escalating severity to Medium.",
                    errorSignature, count);
        }
        return isFlapping;
    }

    private String hashSignature(String input) {
        if (input == null) return "null";
        try {
            MessageDigest digest = MessageDigest.getInstance("SHA-256");
            byte[] hash = digest.digest(input.trim().toLowerCase().getBytes(StandardCharsets.UTF_8));
            StringBuilder hexString = new StringBuilder(2 * hash.length);
            for (byte b : hash) {
                String hex = Integer.toHexString(0xff & b);
                if (hex.length() == 1) hexString.append('0');
                hexString.append(hex);
            }
            return hexString.toString();
        } catch (NoSuchAlgorithmException e) {
            return String.valueOf(input.hashCode());
        }
    }
}
