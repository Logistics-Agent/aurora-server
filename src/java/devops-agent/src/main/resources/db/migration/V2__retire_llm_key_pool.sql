-- Flyway V2 Schema Migration for DevOps-Agent: Retire Legacy LLM Key Pool
-- All LLM provider/key management is now centralized in AiGovernanceService.

DROP TABLE IF EXISTS llm_api_key_pool;
