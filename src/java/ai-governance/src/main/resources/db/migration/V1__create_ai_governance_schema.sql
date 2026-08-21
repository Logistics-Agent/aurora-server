-- V1__create_ai_governance_schema.sql
-- PostgreSQL DDL for AiGovernanceService

-- ========================================================
-- 1. Governance Module Tables
-- ========================================================

CREATE TABLE plans (
    id UUID PRIMARY KEY,
    code VARCHAR(50) NOT NULL UNIQUE,
    name VARCHAR(100) NOT NULL,
    default_provider VARCHAR(30),
    cloud_ai_enabled BOOLEAN NOT NULL DEFAULT TRUE,
    created_at TIMESTAMP WITH TIME ZONE NOT NULL,
    created_by VARCHAR(100),
    updated_at TIMESTAMP WITH TIME ZONE,
    updated_by VARCHAR(100),
    version BIGINT
);

CREATE TABLE plan_capabilities (
    id UUID PRIMARY KEY,
    plan_id UUID NOT NULL REFERENCES plans(id) ON DELETE CASCADE,
    capability_code VARCHAR(100) NOT NULL,
    allowed_providers VARCHAR(200),
    model_tier VARCHAR(20),
    max_tokens INT,
    automation_level VARCHAR(30),
    require_approval BOOLEAN DEFAULT FALSE,
    created_at TIMESTAMP WITH TIME ZONE NOT NULL,
    created_by VARCHAR(100),
    updated_at TIMESTAMP WITH TIME ZONE,
    updated_by VARCHAR(100),
    version BIGINT,
    CONSTRAINT uq_plan_capability UNIQUE (plan_id, capability_code)
);

CREATE TABLE plan_quotas (
    plan_id UUID NOT NULL REFERENCES plans(id) ON DELETE CASCADE,
    quota_metric VARCHAR(20) NOT NULL CHECK (quota_metric IN ('REQUESTS', 'TOKENS')),
    quota_period VARCHAR(20) NOT NULL CHECK (quota_period IN ('MINUTE', 'DAY', 'MONTH')),
    limit_value BIGINT NOT NULL,
    PRIMARY KEY (plan_id, quota_metric, quota_period)
);

CREATE TABLE tenants (
    id UUID PRIMARY KEY,
    external_tenant_id UUID NOT NULL UNIQUE,
    plan_id UUID NOT NULL REFERENCES plans(id),
    status VARCHAR(20) NOT NULL DEFAULT 'ACTIVE' CHECK (status IN ('ACTIVE', 'SUSPENDED', 'CANCELLED')),
    cloud_ai_enabled BOOLEAN NOT NULL DEFAULT TRUE,
    created_at TIMESTAMP WITH TIME ZONE NOT NULL,
    created_by VARCHAR(100),
    updated_at TIMESTAMP WITH TIME ZONE,
    updated_by VARCHAR(100),
    version BIGINT
);

CREATE TABLE usage_records (
    id UUID PRIMARY KEY,
    tenant_id UUID NOT NULL,
    quota_metric VARCHAR(20) NOT NULL CHECK (quota_metric IN ('REQUESTS', 'TOKENS')),
    quota_period VARCHAR(20) NOT NULL CHECK (quota_period IN ('MINUTE', 'DAY', 'MONTH')),
    period_key VARCHAR(30) NOT NULL,
    usage_value BIGINT NOT NULL DEFAULT 0,
    created_at TIMESTAMP WITH TIME ZONE NOT NULL,
    created_by VARCHAR(100),
    updated_at TIMESTAMP WITH TIME ZONE,
    updated_by VARCHAR(100),
    version BIGINT,
    CONSTRAINT uq_usage_record UNIQUE (tenant_id, quota_metric, quota_period, period_key)
);

CREATE TABLE processed_events (
    id UUID PRIMARY KEY,
    event_id VARCHAR(100) NOT NULL UNIQUE,
    processed_at TIMESTAMP WITH TIME ZONE NOT NULL,
    version BIGINT
);

-- ========================================================
-- 2. AI Gateway Module Tables
-- ========================================================

CREATE TABLE provider_pools (
    id UUID PRIMARY KEY,
    code VARCHAR(50) NOT NULL UNIQUE,
    name VARCHAR(100) NOT NULL,
    created_at TIMESTAMP WITH TIME ZONE NOT NULL,
    created_by VARCHAR(100),
    updated_at TIMESTAMP WITH TIME ZONE,
    updated_by VARCHAR(100),
    version BIGINT
);

CREATE TABLE provider_slots (
    id UUID PRIMARY KEY,
    pool_id UUID NOT NULL REFERENCES provider_pools(id),
    provider VARCHAR(30) NOT NULL CHECK (provider IN ('GEMINI', 'AZURE_OPENAI')),
    operation VARCHAR(20) NOT NULL CHECK (operation IN ('GENERATE', 'EMBED')),
    slot_alias VARCHAR(80) NOT NULL UNIQUE,
    project_id VARCHAR(100),
    secret_ref VARCHAR(200) NOT NULL,
    model_name VARCHAR(100) NOT NULL,
    rpm_limit INT NOT NULL,
    tpm_limit INT NOT NULL,
    rpd_limit INT NOT NULL,
    priority INT NOT NULL DEFAULT 1,
    weight INT NOT NULL DEFAULT 1,
    enabled BOOLEAN NOT NULL DEFAULT TRUE,
    cooldown_until TIMESTAMP WITH TIME ZONE,
    created_at TIMESTAMP WITH TIME ZONE NOT NULL,
    created_by VARCHAR(100),
    updated_at TIMESTAMP WITH TIME ZONE,
    updated_by VARCHAR(100),
    version BIGINT
);

CREATE INDEX idx_slot_routing ON provider_slots (pool_id, provider, operation, enabled, priority);

CREATE TABLE service_provider_pool_policies (
    id UUID PRIMARY KEY,
    service_id VARCHAR(100) NOT NULL,
    pool_id UUID NOT NULL REFERENCES provider_pools(id),
    priority INT NOT NULL DEFAULT 1,
    created_at TIMESTAMP WITH TIME ZONE NOT NULL,
    created_by VARCHAR(100),
    updated_at TIMESTAMP WITH TIME ZONE,
    updated_by VARCHAR(100),
    version BIGINT,
    CONSTRAINT uq_service_pool UNIQUE (service_id, pool_id)
);
