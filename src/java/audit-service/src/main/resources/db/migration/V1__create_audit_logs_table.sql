CREATE TABLE IF NOT EXISTS audit_logs (
    id VARCHAR(36) PRIMARY KEY,
    service_name VARCHAR(64) NOT NULL,
    event_type VARCHAR(64) NOT NULL,
    tenant_id VARCHAR(64),
    user_id VARCHAR(64),
    user_role VARCHAR(32) DEFAULT 'SYSTEM',
    resource_id VARCHAR(128),
    payload_json TEXT,
    ip_address VARCHAR(45),
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX IF NOT EXISTS idx_audit_logs_tenant_user ON audit_logs(tenant_id, user_id);
CREATE INDEX IF NOT EXISTS idx_audit_logs_service_event ON audit_logs(service_name, event_type);
CREATE INDEX IF NOT EXISTS idx_audit_logs_created_at ON audit_logs(created_at DESC);
