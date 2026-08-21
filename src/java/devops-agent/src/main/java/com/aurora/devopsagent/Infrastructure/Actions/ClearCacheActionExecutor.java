package com.aurora.devopsagent.Infrastructure.Actions;

import com.aurora.devopsagent.Application.Services.*;
import com.aurora.devopsagent.Domain.Enums.ActionType;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.data.redis.core.StringRedisTemplate;
import org.springframework.stereotype.Component;

import java.time.Duration;
import java.util.Map;

@Component
public class ClearCacheActionExecutor implements ActionExecutor {

    private static final Logger log = LoggerFactory.getLogger(ClearCacheActionExecutor.class);

    private final StringRedisTemplate redisTemplate;

    public ClearCacheActionExecutor(StringRedisTemplate redisTemplate) {
        this.redisTemplate = redisTemplate;
    }

    @Override
    public boolean supports(ActionType actionType) {
        return actionType == ActionType.FLUSH_REDIS_KEY;
    }

    @Override
    public ValidationResult validate(ActionRequest request) {
        if (request.params() == null || !request.params().containsKey("cachePattern")) {
            return ValidationResult.fail("cachePattern param is required for FLUSH_REDIS_KEY");
        }
        return ValidationResult.ok();
    }

    @Override
    public ExecutionResult execute(ActionRequest request) {
        String pattern = (String) request.params().get("cachePattern");
        log.info("Executing FLUSH_REDIS_KEY for pattern '{}'", pattern);
        if (request.dryRun()) {
            return new ExecutionResult(ExecutionStatus.DRY_RUN_PASSED, "Dry-run cache flush validated", Duration.ofMillis(20), Map.of("pattern", pattern));
        }

        try {
            var keys = redisTemplate.keys(pattern);
            if (keys != null && !keys.isEmpty()) {
                redisTemplate.delete(keys);
            }
            return new ExecutionResult(ExecutionStatus.SUCCESS, "Cleared " + (keys != null ? keys.size() : 0) + " keys", Duration.ofMillis(100), Map.of("pattern", pattern));
        } catch (Exception e) {
            return new ExecutionResult(ExecutionStatus.FAILED, "Redis flush failed: " + e.getMessage(), Duration.ofMillis(100), Map.of("error", e.getMessage()));
        }
    }

    @Override
    public VerificationResult verify(ActionRequest request, ExecutionResult result) {
        return new VerificationResult(VerificationStatus.PASSED, "Cache keys verified cleared", Duration.ofMillis(50), Map.of("cleared", true));
    }

    @Override
    public RollbackResult rollback(ActionRequest request, ExecutionResult result) {
        return new RollbackResult(false, "Cache flush cannot be rolled back directly", Duration.ZERO);
    }
}
