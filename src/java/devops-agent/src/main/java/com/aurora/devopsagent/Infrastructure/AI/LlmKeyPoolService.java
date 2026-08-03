package com.aurora.devopsagent.Infrastructure.AI;

import com.aurora.devopsagent.Domain.Entity.LlmApiKeyPool;
import com.aurora.devopsagent.Infrastructure.Persistence.LlmApiKeyPoolJpaRepository;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

import java.time.OffsetDateTime;
import java.time.ZoneOffset;
import java.util.List;

/**
 * Service quản lý LLM API Key Pool cho Azure OpenAI và Gemini.
 * Tự động chọn key khả dụng có độ ưu tiên cao nhất, và xoay (rotate) sang key tiếp theo khi gặp HTTP 429 rate limit.
 */
@Service
public class LlmKeyPoolService {

    private static final Logger log = LoggerFactory.getLogger(LlmKeyPoolService.class);

    private final LlmApiKeyPoolJpaRepository keyPoolRepository;

    public LlmKeyPoolService(LlmApiKeyPoolJpaRepository keyPoolRepository) {
        this.keyPoolRepository = keyPoolRepository;
    }

    @Transactional(readOnly = true)
    public LlmApiKeyPool getActiveKey(String provider) {
        List<LlmApiKeyPool> keys = keyPoolRepository.findAvailableKeysForProvider(provider);
        if (keys.isEmpty()) {
            log.warn("No available API key in pool for provider {}. Will rely on default configuration.", provider);
            return null;
        }
        LlmApiKeyPool selectedKey = keys.get(0);
        log.debug("Selected LLM API Key alias '{}' for provider '{}'", selectedKey.getKeyAlias(), provider);
        return selectedKey;
    }

    @Transactional
    public void markRateLimited(String keyAlias, int cooldownMinutes) {
        log.warn("API Key alias '{}' encountered HTTP 429 (Rate Limited). Marking cooldown for {} minutes.", keyAlias, cooldownMinutes);
        keyPoolRepository.findAll().stream()
                .filter(k -> k.getKeyAlias().equalsIgnoreCase(keyAlias))
                .findFirst()
                .ifPresent(key -> {
                    OffsetDateTime now = OffsetDateTime.now(ZoneOffset.UTC);
                    key.setLastRateLimitedAt(now);
                    key.setCooldownUntil(now.plusMinutes(cooldownMinutes));
                    keyPoolRepository.save(key);
                });
    }

    @Transactional
    public void recordTokenUsage(String keyAlias, int tokensUsed) {
        keyPoolRepository.findAll().stream()
                .filter(k -> k.getKeyAlias().equalsIgnoreCase(keyAlias))
                .findFirst()
                .ifPresent(key -> {
                    int updatedTokens = key.getTokensUsedToday() + tokensUsed;
                    key.setTokensUsedToday(updatedTokens);
                    keyPoolRepository.save(key);
                    int threshold = (key.getDailyTokenLimit() * key.getTokensUsedTodayAlertThresholdPct()) / 100;
                    if (updatedTokens >= threshold) {
                        log.info("API Key alias '{}' reached {}% daily quota threshold ({} / {} tokens).",
                                keyAlias, key.getTokensUsedTodayAlertThresholdPct(), updatedTokens, key.getDailyTokenLimit());
                    }
                });
    }
}
