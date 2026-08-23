-- Flyway V3 Schema Migration for DevOps-Agent: Approval Stages & AiGovernance Metadata Persistence

-- 1. Two-Stage PR Approvals (Decouple Stage from Status)
ALTER TABLE pr_approval_records ADD COLUMN IF NOT EXISTS stage VARCHAR(50) NOT NULL DEFAULT 'MERGE';

-- 2. RCA Analyses: RAG Fallback & AiGovernance Metadata
ALTER TABLE rca_analyses ADD COLUMN IF NOT EXISTS rag_augmented BOOLEAN NOT NULL DEFAULT TRUE;
ALTER TABLE rca_analyses ADD COLUMN IF NOT EXISTS rag_failure_reason VARCHAR(255);
ALTER TABLE rca_analyses ADD COLUMN IF NOT EXISTS governance_decision_id VARCHAR(100);
ALTER TABLE rca_analyses ADD COLUMN IF NOT EXISTS automation_level VARCHAR(50);
ALTER TABLE rca_analyses ADD COLUMN IF NOT EXISTS requires_approval BOOLEAN NOT NULL DEFAULT FALSE;
ALTER TABLE rca_analyses ADD COLUMN IF NOT EXISTS input_tokens BIGINT NOT NULL DEFAULT 0;
ALTER TABLE rca_analyses ADD COLUMN IF NOT EXISTS output_tokens BIGINT NOT NULL DEFAULT 0;

-- 3. Pending Rules: AiGovernance Policy Traceability
ALTER TABLE pending_rules ADD COLUMN IF NOT EXISTS governance_decision_id VARCHAR(100);
ALTER TABLE pending_rules ADD COLUMN IF NOT EXISTS automation_level VARCHAR(50);
ALTER TABLE pending_rules ADD COLUMN IF NOT EXISTS requires_approval BOOLEAN NOT NULL DEFAULT FALSE;
