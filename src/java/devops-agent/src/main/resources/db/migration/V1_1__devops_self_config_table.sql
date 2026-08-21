-- V1_1__devops_self_config_table.sql
CREATE TABLE IF NOT EXISTS devops_agent_self_config (
    id UUID PRIMARY KEY,
    created_at TIMESTAMP WITH TIME ZONE NOT NULL,
    updated_at TIMESTAMP WITH TIME ZONE NOT NULL,
    model_provider VARCHAR(50),
    model_name VARCHAR(100),
    api_endpoint TEXT,
    max_tokens_per_request INT NOT NULL DEFAULT 4096,
    alert_threshold_usd_per_day NUMERIC(10, 4) NOT NULL DEFAULT 50.0000
);
