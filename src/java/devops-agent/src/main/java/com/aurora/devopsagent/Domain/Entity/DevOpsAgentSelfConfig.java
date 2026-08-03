package com.aurora.devopsagent.Domain.Entity;

import com.aurora.shared.entity.AuditableEntity;
import jakarta.persistence.Column;
import jakarta.persistence.Entity;
import jakarta.persistence.Table;

import java.math.BigDecimal;

@Entity
@Table(name = "devops_agent_self_config")
public class DevOpsAgentSelfConfig extends AuditableEntity {

    @Column(name = "model_provider", nullable = false, length = 50)
    private String modelProvider; // azure_openai, gemini

    @Column(name = "model_name", nullable = false, length = 100)
    private String modelName; // gpt-4o, gemini-1.5-pro

    @Column(name = "api_endpoint", nullable = false, columnDefinition = "TEXT")
    private String apiEndpoint;

    @Column(name = "max_tokens_per_request", nullable = false)
    private int maxTokensPerRequest = 4096;

    @Column(name = "alert_threshold_usd_per_day", nullable = false, precision = 10, scale = 4)
    private BigDecimal alertThresholdUsdPerDay = new BigDecimal("50.0000");

    public String getModelProvider() {
        return modelProvider;
    }

    public void setModelProvider(String modelProvider) {
        this.modelProvider = modelProvider;
    }

    public String getModelName() {
        return modelName;
    }

    public void setModelName(String modelName) {
        this.modelName = modelName;
    }

    public String getApiEndpoint() {
        return apiEndpoint;
    }

    public void setApiEndpoint(String apiEndpoint) {
        this.apiEndpoint = apiEndpoint;
    }

    public int getMaxTokensPerRequest() {
        return maxTokensPerRequest;
    }

    public void setMaxTokensPerRequest(int maxTokensPerRequest) {
        this.maxTokensPerRequest = maxTokensPerRequest;
    }

    public BigDecimal getAlertThresholdUsdPerDay() {
        return alertThresholdUsdPerDay;
    }

    public void setAlertThresholdUsdPerDay(BigDecimal alertThresholdUsdPerDay) {
        this.alertThresholdUsdPerDay = alertThresholdUsdPerDay;
    }
}
