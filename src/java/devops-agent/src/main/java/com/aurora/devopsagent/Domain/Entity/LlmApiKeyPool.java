package com.aurora.devopsagent.Domain.Entity;

import com.aurora.shared.entity.AuditableEntity;
import jakarta.persistence.Column;
import jakarta.persistence.Entity;
import jakarta.persistence.Table;

import java.time.OffsetDateTime;

/**
 * Entity `llm_api_key_pool` quản lý pool API key cho LLM providers.
 * DevOps-Agent tự động xoay (rotate) key khi key bị HTTP 429 hoặc đạt alert quota threshold.
 */
@Entity
@Table(name = "llm_api_key_pool")
public class LlmApiKeyPool extends AuditableEntity {

    @Column(name = "provider", nullable = false, length = 50)
    private String provider; // azure_openai, gemini

    @Column(name = "key_alias", nullable = false, length = 100)
    private String keyAlias; // alias ví dụ: azure-key-1

    @Column(name = "key_secret_ref", nullable = false, columnDefinition = "TEXT")
    private String keySecretRef; // Azure Key Vault secret name reference

    @Column(name = "is_active", nullable = false)
    private boolean isActive = true;

    @Column(name = "daily_token_limit", nullable = false)
    private int dailyTokenLimit = 100000;

    @Column(name = "tokens_used_today", nullable = false)
    private int tokensUsedToday = 0;

    @Column(name = "tokens_used_today_alert_threshold_pct", nullable = false)
    private int tokensUsedTodayAlertThresholdPct = 80;

    @Column(name = "last_rate_limited_at")
    private OffsetDateTime lastRateLimitedAt;

    @Column(name = "cooldown_until")
    private OffsetDateTime cooldownUntil;

    @Column(name = "priority", nullable = false)
    private int priority = 1; // 1 = highest

    public String getProvider() {
        return provider;
    }

    public void setProvider(String provider) {
        this.provider = provider;
    }

    public String getKeyAlias() {
        return keyAlias;
    }

    public void setKeyAlias(String keyAlias) {
        this.keyAlias = keyAlias;
    }

    public String getKeySecretRef() {
        return keySecretRef;
    }

    public void setKeySecretRef(String keySecretRef) {
        this.keySecretRef = keySecretRef;
    }

    public boolean isActive() {
        return isActive;
    }

    public void setActive(boolean active) {
        isActive = active;
    }

    public int getDailyTokenLimit() {
        return dailyTokenLimit;
    }

    public void setDailyTokenLimit(int dailyTokenLimit) {
        this.dailyTokenLimit = dailyTokenLimit;
    }

    public int getTokensUsedToday() {
        return tokensUsedToday;
    }

    public void setTokensUsedToday(int tokensUsedToday) {
        this.tokensUsedToday = tokensUsedToday;
    }

    public int getTokensUsedTodayAlertThresholdPct() {
        return tokensUsedTodayAlertThresholdPct;
    }

    public void setTokensUsedTodayAlertThresholdPct(int tokensUsedTodayAlertThresholdPct) {
        this.tokensUsedTodayAlertThresholdPct = tokensUsedTodayAlertThresholdPct;
    }

    public OffsetDateTime getLastRateLimitedAt() {
        return lastRateLimitedAt;
    }

    public void setLastRateLimitedAt(OffsetDateTime lastRateLimitedAt) {
        this.lastRateLimitedAt = lastRateLimitedAt;
    }

    public OffsetDateTime getCooldownUntil() {
        return cooldownUntil;
    }

    public void setCooldownUntil(OffsetDateTime cooldownUntil) {
        this.cooldownUntil = cooldownUntil;
    }

    public int getPriority() {
        return priority;
    }

    public void setPriority(int priority) {
        this.priority = priority;
    }
}
