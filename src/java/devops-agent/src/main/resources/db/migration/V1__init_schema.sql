-- Flyway V1 Schema Migration for DevOps-Agent

CREATE TABLE IF NOT EXISTS incidents (
    id UUID PRIMARY KEY,
    created_at TIMESTAMP WITH TIME ZONE NOT NULL,
    updated_at TIMESTAMP WITH TIME ZONE NOT NULL,
    correlation_id VARCHAR(64) NOT NULL UNIQUE,
    dedup_key VARCHAR(64) NOT NULL,
    source VARCHAR(50) NOT NULL,
    error_signature TEXT NOT NULL,
    severity VARCHAR(20) NOT NULL,
    original_severity VARCHAR(20) NOT NULL,
    status VARCHAR(50) NOT NULL,
    flap_count INT NOT NULL DEFAULT 0,
    affected_service VARCHAR(100),
    affected_tenant_id UUID,
    impact_score NUMERIC(5, 2) NOT NULL DEFAULT 0.00,
    rca_root_cause TEXT,
    rca_recommendation TEXT,
    selected_recommendation_id UUID
);

CREATE TABLE IF NOT EXISTS rca_analyses (
    id UUID PRIMARY KEY,
    created_at TIMESTAMP WITH TIME ZONE NOT NULL,
    updated_at TIMESTAMP WITH TIME ZONE NOT NULL,
    incident_id UUID NOT NULL REFERENCES incidents(id) ON DELETE CASCADE,
    correlation_id VARCHAR(64) NOT NULL,
    analysis_type VARCHAR(50) NOT NULL,
    status VARCHAR(50) NOT NULL,
    recommendation_json JSONB,
    recommendation_version INT NOT NULL DEFAULT 1,
    confidence NUMERIC(5, 4),
    llm_tokens_used INT,
    context_quality_score NUMERIC(3, 2),
    warning_flags_json JSONB,
    duration_ms BIGINT
);

CREATE TABLE IF NOT EXISTS existing_rules (
    id UUID PRIMARY KEY,
    created_at TIMESTAMP WITH TIME ZONE NOT NULL,
    updated_at TIMESTAMP WITH TIME ZONE NOT NULL,
    rule_name VARCHAR(100) NOT NULL UNIQUE,
    error_pattern TEXT NOT NULL,
    action_type VARCHAR(50) NOT NULL,
    action_params_json JSONB,
    confidence NUMERIC(5, 4) NOT NULL,
    active BOOLEAN NOT NULL DEFAULT TRUE,
    match_count INT NOT NULL DEFAULT 0,
    last_matched_at TIMESTAMP WITH TIME ZONE
);

CREATE TABLE IF NOT EXISTS pending_rules (
    id UUID PRIMARY KEY,
    created_at TIMESTAMP WITH TIME ZONE NOT NULL,
    updated_at TIMESTAMP WITH TIME ZONE NOT NULL,
    proposed_rule_name VARCHAR(100) NOT NULL,
    error_pattern TEXT NOT NULL,
    action_type VARCHAR(50) NOT NULL,
    action_params_json JSONB,
    source_incident_id UUID REFERENCES incidents(id) ON DELETE SET NULL,
    status VARCHAR(50) NOT NULL DEFAULT 'PENDING'
);

CREATE TABLE IF NOT EXISTS self_configs (
    id UUID PRIMARY KEY,
    created_at TIMESTAMP WITH TIME ZONE NOT NULL,
    updated_at TIMESTAMP WITH TIME ZONE NOT NULL,
    config_key VARCHAR(100) NOT NULL UNIQUE,
    config_value TEXT NOT NULL,
    description VARCHAR(255)
);

CREATE TABLE IF NOT EXISTS llm_api_key_pool (
    id UUID PRIMARY KEY,
    created_at TIMESTAMP WITH TIME ZONE NOT NULL,
    updated_at TIMESTAMP WITH TIME ZONE NOT NULL,
    provider_name VARCHAR(50) NOT NULL,
    api_key_encrypted TEXT NOT NULL,
    active BOOLEAN NOT NULL DEFAULT TRUE,
    usage_count INT NOT NULL DEFAULT 0,
    last_used_at TIMESTAMP WITH TIME ZONE,
    rate_limit_reset_at TIMESTAMP WITH TIME ZONE
);

CREATE TABLE IF NOT EXISTS pr_approval_records (
    id UUID PRIMARY KEY,
    created_at TIMESTAMP WITH TIME ZONE NOT NULL,
    updated_at TIMESTAMP WITH TIME ZONE NOT NULL,
    incident_id UUID NOT NULL REFERENCES incidents(id) ON DELETE CASCADE,
    recommendation_id UUID NOT NULL,
    github_pr_url VARCHAR(500),
    pr_number INT,
    status VARCHAR(50) NOT NULL DEFAULT 'PENDING',
    severity VARCHAR(20) NOT NULL,
    decided_by VARCHAR(100),
    decision_reason TEXT,
    decided_at TIMESTAMP WITH TIME ZONE,
    timeout_minutes INT NOT NULL,
    expires_at TIMESTAMP WITH TIME ZONE NOT NULL
);

CREATE TABLE IF NOT EXISTS rule_approval_records (
    id UUID PRIMARY KEY,
    created_at TIMESTAMP WITH TIME ZONE NOT NULL,
    updated_at TIMESTAMP WITH TIME ZONE NOT NULL,
    pending_rule_id UUID NOT NULL REFERENCES pending_rules(id) ON DELETE CASCADE,
    status VARCHAR(50) NOT NULL DEFAULT 'PENDING',
    reviewed_by VARCHAR(100),
    review_comment TEXT,
    reviewed_at TIMESTAMP WITH TIME ZONE
);

CREATE TABLE IF NOT EXISTS audit_event_outbox (
    id UUID PRIMARY KEY,
    created_at TIMESTAMP WITH TIME ZONE NOT NULL,
    updated_at TIMESTAMP WITH TIME ZONE NOT NULL,
    correlation_id VARCHAR(64) NOT NULL,
    incident_id UUID,
    action_type VARCHAR(100) NOT NULL,
    actor VARCHAR(100) NOT NULL,
    payload_json JSONB NOT NULL,
    processed BOOLEAN NOT NULL DEFAULT FALSE,
    processed_at TIMESTAMP WITH TIME ZONE,
    retry_count INT NOT NULL DEFAULT 0,
    last_error TEXT
);

CREATE TABLE IF NOT EXISTS service_criticality_registry (
    id UUID PRIMARY KEY,
    created_at TIMESTAMP WITH TIME ZONE NOT NULL,
    updated_at TIMESTAMP WITH TIME ZONE NOT NULL,
    service_name VARCHAR(100) NOT NULL UNIQUE,
    criticality_tier VARCHAR(20) NOT NULL,
    weight NUMERIC(3, 2) NOT NULL DEFAULT 1.00,
    owner_team VARCHAR(100),
    slo_availability_percent NUMERIC(5, 2),
    slo_latency_ms INT
);

CREATE TABLE IF NOT EXISTS shedlock (
    name VARCHAR(64) NOT NULL PRIMARY KEY,
    lock_until TIMESTAMP WITH TIME ZONE NOT NULL,
    locked_at TIMESTAMP WITH TIME ZONE NOT NULL,
    locked_by VARCHAR(255) NOT NULL
);

-- Indexes
CREATE INDEX IF NOT EXISTS idx_incidents_correlation_id ON incidents(correlation_id);
CREATE INDEX IF NOT EXISTS idx_incidents_status ON incidents(status);
CREATE INDEX IF NOT EXISTS idx_rca_analyses_incident_id ON rca_analyses(incident_id);
CREATE INDEX IF NOT EXISTS idx_audit_outbox_processed ON audit_event_outbox(processed) WHERE processed = FALSE;
