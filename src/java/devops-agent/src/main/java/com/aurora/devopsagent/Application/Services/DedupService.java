package com.aurora.devopsagent.Application.Services;

import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.data.redis.core.StringRedisTemplate;
import org.springframework.stereotype.Service;

import java.nio.charset.StandardCharsets;
import java.security.MessageDigest;
import java.security.NoSuchAlgorithmException;
import java.time.Duration;
import java.time.Instant;

/**
 * DedupService: Deduplication layer chống xử lý trùng lặp alerts trong 5 phút time bucket & Redis TTL 30 phút.
 */
@Service
public class DedupService {

    private static final Logger log = LoggerFactory.getLogger(DedupService.class);
    private static final String DEDUP_KEY_PREFIX = "devops:dedup:";
    private static final Duration DEDUP_TTL = Duration.ofMinutes(30);

    private final StringRedisTemplate redisTemplate;

    public DedupService(StringRedisTemplate redisTemplate) {
        this.redisTemplate = redisTemplate;
    }

    public String computeDedupKey(String source, String errorSignature, long timestampSeconds) {
        long timeWindowBucket = (timestampSeconds / 300) * 300; // 5-minute bucket
        String rawInput = source + ":" + errorSignature + ":" + timeWindowBucket;
        return sha256Hex(rawInput);
    }

    /**
     * Check if dedup key exists in Redis. If exists returns stored correlation_id, otherwise stores correlation_id and returns null.
     */
    public String checkAndStore(String dedupKey, String correlationId) {
        String redisKey = DEDUP_KEY_PREFIX + dedupKey;
        Boolean setIfAbsent = redisTemplate.opsForValue().setIfAbsent(redisKey, correlationId, DEDUP_TTL);
        if (Boolean.TRUE.equals(setIfAbsent)) {
            log.debug("New Dedup Key stored in Redis: {} -> correlationId: {}", dedupKey, correlationId);
            return null; // Is new, not duplicate
        } else {
            String existingCorrelationId = redisTemplate.opsForValue().get(redisKey);
            log.info("Duplicate alert detected for Dedup Key {}. Existing correlationId: {}", dedupKey, existingCorrelationId);
            return existingCorrelationId != null ? existingCorrelationId : correlationId;
        }
    }

    private String sha256Hex(String input) {
        try {
            MessageDigest digest = MessageDigest.getInstance("SHA-256");
            byte[] hash = digest.digest(input.getBytes(StandardCharsets.UTF_8));
            StringBuilder hexString = new StringBuilder(2 * hash.length);
            for (byte b : hash) {
                String hex = Integer.toHexString(0xff & b);
                if (hex.length() == 1) hexString.append('0');
                hexString.append(hex);
            }
            return hexString.toString();
        } catch (NoSuchAlgorithmException e) {
            throw new RuntimeException("SHA-256 algorithm not available", e);
        }
    }
}
