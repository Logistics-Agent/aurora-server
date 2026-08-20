package com.aurora.devopsagent.Infrastructure.Cache;

import com.aurora.devopsagent.Domain.Entity.ExistingRule;
import com.aurora.devopsagent.Infrastructure.Persistence.ExistingRuleJpaRepository;
import com.github.benmanes.caffeine.cache.Cache;
import com.github.benmanes.caffeine.cache.Caffeine;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.data.redis.core.StringRedisTemplate;
import org.springframework.stereotype.Service;

import java.time.Duration;
import java.util.List;
import java.util.Optional;

@Service
public class RuleCacheService {

    private static final Logger log = LoggerFactory.getLogger(RuleCacheService.class);
    private static final String RULE_INVALIDATION_TOPIC = "devops:rules:invalidation";

    private final ExistingRuleJpaRepository ruleRepository;
    private final StringRedisTemplate redisTemplate;
    private final Cache<String, List<ExistingRule>> localCache;

    public RuleCacheService(ExistingRuleJpaRepository ruleRepository, StringRedisTemplate redisTemplate) {
        this.ruleRepository = ruleRepository;
        this.redisTemplate = redisTemplate;
        this.localCache = Caffeine.newBuilder()
                .maximumSize(500)
                .expireAfterWrite(Duration.ofMinutes(10))
                .build();
    }

    public List<ExistingRule> getRulesForService(String serviceName) {
        String key = serviceName != null ? serviceName : "ALL_SERVICES";
        return localCache.get(key, k -> {
            log.debug("Rule cache miss for service '{}', loading from DB", k);
            return ruleRepository.findAll().stream()
                    .filter(ExistingRule::isActive)
                    .toList();
        });
    }

    public void invalidateLocalCache() {
        log.info("Invalidating local rule cache.");
        localCache.invalidateAll();
    }

    public void broadcastInvalidation() {
        invalidateLocalCache();
        try {
            redisTemplate.convertAndSend(RULE_INVALIDATION_TOPIC, "INVALIDATE_ALL");
            log.debug("Broadcasted rule cache invalidation event via Redis Pub/Sub.");
        } catch (Exception e) {
            log.warn("Failed to broadcast Redis invalidation message: {}", e.getMessage());
        }
    }
}
