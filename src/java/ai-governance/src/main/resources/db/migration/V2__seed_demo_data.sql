-- V2__seed_demo_data.sql
-- Seed illustrative development/demo data for AiGovernanceService

-- ========================================================
-- 1. Plans
-- ========================================================

INSERT INTO plans (id, code, name, default_provider, cloud_ai_enabled, created_at, created_by, version)
VALUES
    ('11111111-1111-1111-1111-111111111111', 'FREE', 'Free Tier', 'GEMINI', true, NOW(), 'system', 1),
    ('22222222-2222-2222-2222-222222222222', 'STANDARD', 'Standard Tier', 'GEMINI', true, NOW(), 'system', 1),
    ('33333333-3333-3333-3333-333333333333', 'ENTERPRISE', 'Enterprise Tier', 'GEMINI', true, NOW(), 'system', 1);

-- ========================================================
-- 2. Plan Quotas (Composite: plan_id + quota_metric + quota_period)
-- ========================================================

-- FREE Plan
INSERT INTO plan_quotas (plan_id, quota_metric, quota_period, limit_value)
VALUES
    ('11111111-1111-1111-1111-111111111111', 'REQUESTS', 'MINUTE', 5),
    ('11111111-1111-1111-1111-111111111111', 'TOKENS', 'MINUTE', 75000),
    ('11111111-1111-1111-1111-111111111111', 'REQUESTS', 'DAY', 150);

-- STANDARD Plan
INSERT INTO plan_quotas (plan_id, quota_metric, quota_period, limit_value)
VALUES
    ('22222222-2222-2222-2222-222222222222', 'REQUESTS', 'MINUTE', 10),
    ('22222222-2222-2222-2222-222222222222', 'TOKENS', 'MINUTE', 150000),
    ('22222222-2222-2222-2222-222222222222', 'REQUESTS', 'DAY', 300);

-- ENTERPRISE Plan
INSERT INTO plan_quotas (plan_id, quota_metric, quota_period, limit_value)
VALUES
    ('33333333-3333-3333-3333-333333333333', 'REQUESTS', 'MINUTE', 20),
    ('33333333-3333-3333-3333-333333333333', 'TOKENS', 'MINUTE', 300000),
    ('33333333-3333-3333-3333-333333333333', 'REQUESTS', 'DAY', 750);

-- ========================================================
-- 3. Plan Capabilities
-- ========================================================

-- FREE Plan capabilities
INSERT INTO plan_capabilities (id, plan_id, capability_code, allowed_providers, model_tier, max_tokens, automation_level, require_approval, created_at, created_by, version)
VALUES
    (gen_random_uuid(), '11111111-1111-1111-1111-111111111111', 'compliance.answer', 'GEMINI', 'STANDARD', 2048, 'ASSISTED', false, NOW(), 'system', 1),
    (gen_random_uuid(), '11111111-1111-1111-1111-111111111111', 'ocr.extract', 'GEMINI', 'STANDARD', 2048, 'ASSISTED', false, NOW(), 'system', 1);

-- STANDARD Plan capabilities
INSERT INTO plan_capabilities (id, plan_id, capability_code, allowed_providers, model_tier, max_tokens, automation_level, require_approval, created_at, created_by, version)
VALUES
    (gen_random_uuid(), '22222222-2222-2222-2222-222222222222', 'compliance.answer', 'GEMINI,AZURE_OPENAI', 'STANDARD', 4096, 'SEMI_AUTONOMOUS', false, NOW(), 'system', 1),
    (gen_random_uuid(), '22222222-2222-2222-2222-222222222222', 'ocr.extract', 'GEMINI', 'STANDARD', 4096, 'SEMI_AUTONOMOUS', false, NOW(), 'system', 1),
    (gen_random_uuid(), '22222222-2222-2222-2222-222222222222', 'route.plan', 'GEMINI', 'STANDARD', 4096, 'SEMI_AUTONOMOUS', true, NOW(), 'system', 1);

-- ENTERPRISE Plan capabilities
INSERT INTO plan_capabilities (id, plan_id, capability_code, allowed_providers, model_tier, max_tokens, automation_level, require_approval, created_at, created_by, version)
VALUES
    (gen_random_uuid(), '33333333-3333-3333-3333-333333333333', 'compliance.answer', 'GEMINI,AZURE_OPENAI', 'PREMIUM', 8192, 'SUPERVISED_AUTONOMOUS', false, NOW(), 'system', 1),
    (gen_random_uuid(), '33333333-3333-3333-3333-333333333333', 'ocr.extract', 'GEMINI,AZURE_OPENAI', 'PREMIUM', 8192, 'SUPERVISED_AUTONOMOUS', false, NOW(), 'system', 1),
    (gen_random_uuid(), '33333333-3333-3333-3333-333333333333', 'route.plan', 'GEMINI,AZURE_OPENAI', 'PREMIUM', 8192, 'SUPERVISED_AUTONOMOUS', true, NOW(), 'system', 1),
    (gen_random_uuid(), '33333333-3333-3333-3333-333333333333', 'devops.diagnose', 'AZURE_OPENAI,GEMINI', 'PREMIUM', 8192, 'SUPERVISED_AUTONOMOUS', false, NOW(), 'system', 1);

-- ========================================================
-- 4. Demo Tenant
-- ========================================================

INSERT INTO tenants (id, external_tenant_id, plan_id, status, cloud_ai_enabled, created_at, created_by, version)
VALUES
    ('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', '22222222-2222-2222-2222-222222222222', 'ACTIVE', true, NOW(), 'system', 1);

-- ========================================================
-- 5. Provider Pools
-- ========================================================

INSERT INTO provider_pools (id, code, name, created_at, created_by, version)
VALUES
    ('aaaaaaaa-0000-0000-0000-000000000001', 'shared-ai', 'Shared Multi-Tenant AI Pool', NOW(), 'system', 1),
    ('aaaaaaaa-0000-0000-0000-000000000002', 'devops-internal', 'DevOps Dedicated Internal Pool', NOW(), 'system', 1);

-- ========================================================
-- 6. Provider Slots (11 active demo slots)
-- ========================================================

-- Shared Pool GENERATE Slots (5 slots)
INSERT INTO provider_slots (id, pool_id, provider, operation, slot_alias, project_id, secret_ref, model_name, rpm_limit, tpm_limit, rpd_limit, priority, weight, enabled, created_at, created_by, version)
VALUES
    (gen_random_uuid(), 'aaaaaaaa-0000-0000-0000-000000000001', 'GEMINI', 'GENERATE', 'gemini-shared-generate-01', 'synchro-ai-prod-01', 'gemini-api-key-shared-01', 'gemini-1.5-flash', 15, 250000, 500, 1, 1, true, NOW(), 'system', 1),
    (gen_random_uuid(), 'aaaaaaaa-0000-0000-0000-000000000001', 'GEMINI', 'GENERATE', 'gemini-shared-generate-02', 'synchro-ai-prod-02', 'gemini-api-key-shared-02', 'gemini-1.5-flash', 15, 250000, 500, 1, 1, true, NOW(), 'system', 1),
    (gen_random_uuid(), 'aaaaaaaa-0000-0000-0000-000000000001', 'GEMINI', 'GENERATE', 'gemini-shared-generate-03', 'synchro-ai-prod-03', 'gemini-api-key-shared-03', 'gemini-1.5-flash', 15, 250000, 500, 1, 1, true, NOW(), 'system', 1),
    (gen_random_uuid(), 'aaaaaaaa-0000-0000-0000-000000000001', 'GEMINI', 'GENERATE', 'gemini-shared-generate-04', 'synchro-ai-prod-04', 'gemini-api-key-shared-04', 'gemini-1.5-flash', 15, 250000, 500, 1, 1, true, NOW(), 'system', 1),
    (gen_random_uuid(), 'aaaaaaaa-0000-0000-0000-000000000001', 'GEMINI', 'GENERATE', 'gemini-shared-generate-05', 'synchro-ai-prod-05', 'gemini-api-key-shared-05', 'gemini-1.5-flash', 15, 250000, 500, 1, 1, true, NOW(), 'system', 1);

-- Shared Pool EMBED Slot (1 slot)
INSERT INTO provider_slots (id, pool_id, provider, operation, slot_alias, project_id, secret_ref, model_name, rpm_limit, tpm_limit, rpd_limit, priority, weight, enabled, created_at, created_by, version)
VALUES
    (gen_random_uuid(), 'aaaaaaaa-0000-0000-0000-000000000001', 'GEMINI', 'EMBED', 'gemini-shared-embed-01', 'synchro-ai-prod-01', 'gemini-api-key-shared-01', 'text-embedding-004', 15, 250000, 500, 1, 1, true, NOW(), 'system', 1);

-- DevOps Pool Slots (1 Azure Primary + 4 Gemini Fallback)
INSERT INTO provider_slots (id, pool_id, provider, operation, slot_alias, project_id, secret_ref, model_name, rpm_limit, tpm_limit, rpd_limit, priority, weight, enabled, created_at, created_by, version)
VALUES
    (gen_random_uuid(), 'aaaaaaaa-0000-0000-0000-000000000002', 'AZURE_OPENAI', 'GENERATE', 'azure-devops-generate-01', 'synchro-azure-openai', 'azure-openai-key-devops', 'gpt-4o', 30, 500000, 1000, 1, 1, true, NOW(), 'system', 1),
    (gen_random_uuid(), 'aaaaaaaa-0000-0000-0000-000000000002', 'GEMINI', 'GENERATE', 'gemini-devops-generate-01', 'synchro-ai-devops-01', 'gemini-api-key-devops-01', 'gemini-1.5-flash', 15, 250000, 500, 10, 1, true, NOW(), 'system', 1),
    (gen_random_uuid(), 'aaaaaaaa-0000-0000-0000-000000000002', 'GEMINI', 'GENERATE', 'gemini-devops-generate-02', 'synchro-ai-devops-02', 'gemini-api-key-devops-02', 'gemini-1.5-flash', 15, 250000, 500, 10, 1, true, NOW(), 'system', 1),
    (gen_random_uuid(), 'aaaaaaaa-0000-0000-0000-000000000002', 'GEMINI', 'GENERATE', 'gemini-devops-generate-03', 'synchro-ai-devops-03', 'gemini-api-key-devops-03', 'gemini-1.5-flash', 15, 250000, 500, 10, 1, true, NOW(), 'system', 1),
    (gen_random_uuid(), 'aaaaaaaa-0000-0000-0000-000000000002', 'GEMINI', 'GENERATE', 'gemini-devops-generate-04', 'synchro-ai-devops-04', 'gemini-api-key-devops-04', 'gemini-1.5-flash', 15, 250000, 500, 10, 1, true, NOW(), 'system', 1);

-- ========================================================
-- 7. Service Provider Pool Policies
-- ========================================================

INSERT INTO service_provider_pool_policies (id, service_id, pool_id, priority, created_at, created_by, version)
VALUES
    (gen_random_uuid(), 'devops-agent', 'aaaaaaaa-0000-0000-0000-000000000002', 1, NOW(), 'system', 1),
    (gen_random_uuid(), 'regulatory-compliance-rag', 'aaaaaaaa-0000-0000-0000-000000000001', 1, NOW(), 'system', 1),
    (gen_random_uuid(), 'document-ocr-agent', 'aaaaaaaa-0000-0000-0000-000000000001', 1, NOW(), 'system', 1),
    (gen_random_uuid(), 'route-planning-agent', 'aaaaaaaa-0000-0000-0000-000000000001', 1, NOW(), 'system', 1);
