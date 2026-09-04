-- =============================================================================
-- V3__configure_gemini_production_slots.sql
-- Configure 6 Gemini Production Slots matching Azure Key Vault (logistic-agent)
-- =============================================================================

-- 1. Đảm bảo Pool "shared-ai" & "devops-internal" đã tồn tại
INSERT INTO provider_pools (id, code, name, created_at, created_by, version)
VALUES 
    ('aaaaaaaa-0000-0000-0000-000000000001', 'shared-ai', 'Shared Multi-Tenant AI Pool', NOW(), 'system', 1),
    ('aaaaaaaa-0000-0000-0000-000000000002', 'devops-internal', 'DevOps Dedicated Internal Pool', NOW(), 'system', 1)
ON CONFLICT (id) DO NOTHING;

-- 2. Dọn dẹp các slot mẫu cũ trong shared-ai pool
DELETE FROM provider_slots
WHERE pool_id = 'aaaaaaaa-0000-0000-0000-000000000001';

-- =============================================================================
-- 3. GEMINI 3.5 FLASH-LITE
--    4 API keys: 01 -> 04
--    Free Tier:
--      15 RPM
--      250K TPM
--      500 RPD
-- =============================================================================

INSERT INTO provider_slots (
    id,
    pool_id,
    provider,
    operation,
    slot_alias,
    secret_ref,
    model_name,
    rpm_limit,
    tpm_limit,
    rpd_limit,
    priority,
    weight,
    enabled,
    created_at,
    created_by,
    version
)
VALUES
    (
        gen_random_uuid(),
        'aaaaaaaa-0000-0000-0000-000000000001',
        'GEMINI',
        'GENERATE',
        'gemini-gen-slot-01',
        'gemini-api-key-01',
        'gemini-3.5-flash-lite',
        15,
        250000,
        500,
        1,
        1,
        true,
        NOW(),
        'system',
        1
    ),
    (
        gen_random_uuid(),
        'aaaaaaaa-0000-0000-0000-000000000001',
        'GEMINI',
        'GENERATE',
        'gemini-gen-slot-02',
        'gemini-api-key-02',
        'gemini-3.5-flash-lite',
        15,
        250000,
        500,
        1,
        1,
        true,
        NOW(),
        'system',
        1
    ),
    (
        gen_random_uuid(),
        'aaaaaaaa-0000-0000-0000-000000000001',
        'GEMINI',
        'GENERATE',
        'gemini-gen-slot-03',
        'gemini-api-key-03',
        'gemini-3.5-flash-lite',
        15,
        250000,
        500,
        1,
        1,
        true,
        NOW(),
        'system',
        1
    ),
    (
        gen_random_uuid(),
        'aaaaaaaa-0000-0000-0000-000000000001',
        'GEMINI',
        'GENERATE',
        'gemini-gen-slot-04',
        'gemini-api-key-04',
        'gemini-3.5-flash-lite',
        15,
        250000,
        500,
        1,
        1,
        true,
        NOW(),
        'system',
        1
    );

-- =============================================================================
-- 4. GEMINI EMBEDDING 2
--    2 API keys: 05 -> 06
--    Free Tier:
--      100 RPM
--      30K TPM
--      1000 RPD
-- =============================================================================

INSERT INTO provider_slots (
    id,
    pool_id,
    provider,
    operation,
    slot_alias,
    secret_ref,
    model_name,
    rpm_limit,
    tpm_limit,
    rpd_limit,
    priority,
    weight,
    enabled,
    created_at,
    created_by,
    version
)
VALUES
    (
        gen_random_uuid(),
        'aaaaaaaa-0000-0000-0000-000000000001',
        'GEMINI',
        'EMBED',
        'gemini-embed-slot-01',
        'gemini-api-key-05',
        'gemini-embedding-2',
        100,
        30000,
        1000,
        1,
        1,
        true,
        NOW(),
        'system',
        1
    ),
    (
        gen_random_uuid(),
        'aaaaaaaa-0000-0000-0000-000000000001',
        'GEMINI',
        'EMBED',
        'gemini-embed-slot-02',
        'gemini-api-key-06',
        'gemini-embedding-2',
        100,
        30000,
        1000,
        1,
        1,
        true,
        NOW(),
        'system',
        1
    );

-- 5. Service -> Provider Pool mappings
INSERT INTO service_provider_pool_policies (
    id,
    service_id,
    pool_id,
    priority,
    created_at,
    created_by,
    version
)
VALUES
    (
        gen_random_uuid(),
        'devops-agent',
        'aaaaaaaa-0000-0000-0000-000000000002',
        1,
        NOW(),
        'system',
        1
    ),
    (
        gen_random_uuid(),
        'regulatory-compliance-rag',
        'aaaaaaaa-0000-0000-0000-000000000001',
        1,
        NOW(),
        'system',
        1
    ),
    (
        gen_random_uuid(),
        'document-ocr-agent',
        'aaaaaaaa-0000-0000-0000-000000000001',
        1,
        NOW(),
        'system',
        1
    ),
    (
        gen_random_uuid(),
        'route-planning-agent',
        'aaaaaaaa-0000-0000-0000-000000000001',
        1,
        NOW(),
        'system',
        1
    ),
    (
        gen_random_uuid(),
        'customer-assistant-service',
        'aaaaaaaa-0000-0000-0000-000000000001',
        1,
        NOW(),
        'system',
        1
    ),
    (
        gen_random_uuid(),
        'negotiation-agent-service',
        'aaaaaaaa-0000-0000-0000-000000000001',
        1,
        NOW(),
        'system',
        1
    ),
    (
        gen_random_uuid(),
        'mail-service',
        'aaaaaaaa-0000-0000-0000-000000000001',
        1,
        NOW(),
        'system',
        1
    )
ON CONFLICT (service_id, pool_id) DO NOTHING;