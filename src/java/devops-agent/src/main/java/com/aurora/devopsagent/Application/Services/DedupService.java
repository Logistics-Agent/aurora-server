package com.aurora.devopsagent.Application.Services;

import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.data.redis.core.StringRedisTemplate;
import org.springframework.data.redis.core.script.DefaultRedisScript;
import org.springframework.stereotype.Service;

import java.nio.charset.StandardCharsets;
import java.security.MessageDigest;
import java.security.NoSuchAlgorithmException;
import java.util.Collections;

/**
 * DedupService: Atomic deduplication layer using Redis Lua script.
 * Dedup is based on normalized errorSignature (source-independent) to match across Loki & Azure Monitor.
 */
@Service
public class DedupService {

    private static final Logger log = LoggerFactory.getLogger(DedupService.class);
    private static final String DEDUP_KEY_PREFIX = "devops:dedup:";
    private static final long DEDUP_TTL_SECONDS = 300; // 5-minute sliding window

    private static final String DEDUP_LUA_SCRIPT =
            "local key = KEYS[1]\n" +
            "local correlationId = ARGV[1]\n" +
            "local ttl = tonumber(ARGV[2])\n" +
            "local existing = redis.call('GET', key)\n" +
            "if existing then\n" +
            "    return existing\n" +
            "else\n" +
            "    redis.call('SET', key, correlationId, 'EX', ttl)\n" +
            "    return nil\n" +
            "end";

    private final StringRedisTemplate redisTemplate;
    private final DefaultRedisScript<String> redisScript;

    public DedupService(StringRedisTemplate redisTemplate) {
        this.redisTemplate = redisTemplate;
        this.redisScript = new DefaultRedisScript<>(DEDUP_LUA_SCRIPT, String.class);
    }

    /**
     * Computes dedup key based on normalized error signature (source removed for cross-source dedup).
     */
    public String computeDedupKey(String source, String errorSignature, long timestampSeconds) {
        String normalizedSignature = errorSignature != null ? errorSignature.trim().toLowerCase() : "";
        return sha256Hex(normalizedSignature);
    }

    /**
     * Atomically check and store in Redis.
     * Returns null if newly stored (not duplicate), or existing correlationId if already present.
     */
    public String checkAndStore(String dedupKey, String correlationId) {
        String redisKey = DEDUP_KEY_PREFIX + dedupKey;
        String existingCorrelationId = redisTemplate.execute(
                redisScript,
                Collections.singletonList(redisKey),
                correlationId,
                String.valueOf(DEDUP_TTL_SECONDS)
        );

        if (existingCorrelationId == null) {
            log.debug("New Dedup Key stored atomically in Redis: {} -> correlationId: {}", dedupKey, correlationId);
            return null;
        } else {
            log.info("Duplicate alert detected atomically for Dedup Key {}. Existing correlationId: {}", dedupKey, existingCorrelationId);
            return existingCorrelationId;
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
