package com.aurora.devopsagent.Application.Services;

import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.springframework.data.redis.core.StringRedisTemplate;
import org.springframework.data.redis.core.script.RedisScript;

import java.util.List;

import static org.junit.jupiter.api.Assertions.*;
import static org.mockito.ArgumentMatchers.*;
import static org.mockito.Mockito.*;

class DedupAndAntiFlappingTest {

    private StringRedisTemplate redisTemplate;
    private DedupService dedupService;
    private AntiFlappingTracker antiFlappingTracker;

    @BeforeEach
    void setUp() {
        redisTemplate = mock(StringRedisTemplate.class);
        dedupService = new DedupService(redisTemplate);
        antiFlappingTracker = new AntiFlappingTracker(redisTemplate);
    }

    @Test
    @DisplayName("Dedup key is source-independent: Loki and Azure Monitor alerts with same signature produce same hash")
    void testCrossSourceDedup() {
        String signature = "NullPointerException in OrderWorkflowService";
        long timestamp = 1700000000L;

        String lokiKey = dedupService.computeDedupKey("loki", signature, timestamp);
        String azureKey = dedupService.computeDedupKey("azure_monitor", signature, timestamp);

        assertNotNull(lokiKey);
        assertEquals(lokiKey, azureKey, "Dedup keys from different sources for same error signature must be identical");
    }

    @Test
    @DisplayName("checkAndStore returns null for new alerts and existing correlationId for duplicates")
    void testCheckAndStoreAtomic() {
        when(redisTemplate.execute(any(RedisScript.class), anyList(), eq("corr-1"), anyString()))
                .thenReturn(null); // First call: newly stored

        String firstResult = dedupService.checkAndStore("dedup-hash-1", "corr-1");
        assertNull(firstResult, "First occurrence must return null (new incident)");

        when(redisTemplate.execute(any(RedisScript.class), anyList(), eq("corr-2"), anyString()))
                .thenReturn("corr-1"); // Second call: duplicate found

        String secondResult = dedupService.checkAndStore("dedup-hash-1", "corr-2");
        assertEquals("corr-1", secondResult, "Duplicate occurrence must return original correlationId");
    }

    @Test
    @DisplayName("AntiFlappingTracker detects flapping when occurrences >= 5 in 10 mins window")
    void testAntiFlappingThreshold() {
        when(redisTemplate.execute(any(RedisScript.class), anyList(), anyString(), anyString(), anyString(), anyString()))
                .thenReturn(4L); // Count is 4 (< 5)

        boolean notFlapping = antiFlappingTracker.recordEventAndCheckFlapping("Timeout in Gateway");
        assertFalse(notFlapping, "4 occurrences should not trigger flapping");

        when(redisTemplate.execute(any(RedisScript.class), anyList(), anyString(), anyString(), anyString(), anyString()))
                .thenReturn(5L); // Count is 5 (>= 5)

        boolean isFlapping = antiFlappingTracker.recordEventAndCheckFlapping("Timeout in Gateway");
        assertTrue(isFlapping, "5 occurrences must trigger flapping escalation");
    }
}
