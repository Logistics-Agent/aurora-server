package com.aurora.devopsagent.Domain.Entity;

import com.aurora.shared.entity.AuditableEntity;
import jakarta.persistence.Column;
import jakarta.persistence.Entity;
import jakarta.persistence.Table;

import java.math.BigDecimal;

@Entity
@Table(name = "devops_agent_self_config")
public class DevOpsAgentSelfConfig extends AuditableEntity {

    @Column(name = "model_provider", length = 50)
    private String modelProvider; // Deprecated: provider routing managed by AiGovernance

    @Column(name = "model_name", length = 100)
    private String modelName; // Deprecated: model selection managed by AiGovernance

    @Column(name = "api_endpoint", columnDefinition = "TEXT")
    private String apiEndpoint; // Deprecated: endpoints managed by AiGovernance

    @Column(name = "max_tokens_per_request", nullable = false)
    private int maxTokensPerRequest = 4096; // Requested ceiling preference

    @Column(name = "alert_threshold_usd_per_day", nullable = false, precision = 10, scale = 4)
    private BigDecimal alertThresholdUsdPerDay = new BigDecimal("50.0000"); // Domain cost threshold

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
