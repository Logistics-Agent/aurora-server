# Autonomous DevOps & Incident Response Agent — Architectural Interview Q&A

> **Target Role**: Staff SRE, Principal Backend Architect, AIOps / Platform Engineer  
> **Topic**: Autonomous Reliability, AI Incident Response, Distributed Guardrails, and Self-Healing Systems

---

### Q1: How do you prevent an AI-driven DevOps agent from hallucinating destructive actions (e.g., deleting a database or purging all caches)?
**Answer**:
We apply **Defense-in-Depth and Multi-Tier Safety Guardrails**:
1. **Bounded Capability Whitelist**: The LLM never generates arbitrary bash commands or raw API calls. It can only emit strongly-typed `ActionType` enums defined in `ActionExecutorRegistry` (`RESTART_POD`, `CLEAR_CACHE_PATTERN`, `TRIGGER_ROLLBACK`).
2. **Deterministic Pre-Execution Validation**: `ActionExecutor` validates target namespaces, resource limits, and tenant boundaries. Unrecognized or unbounded actions throw `UnsupportedActionException`.
3. **Mandatory Human-in-the-Loop (HitL)**: For `Severity.High` and `Severity.Critical` incidents, autonomous execution is blocked. The state transitions to `WAITING_APPROVAL`, sending actionable Slack/gRPC notification cards to on-call SREs with 1-click approvals.
4. **Automated Verification & Immediate Rollback**: Every action execution must pass a post-execution health check (`VerificationService`). If 5xx error rates do not drop within 60s, `rollback()` is triggered automatically.

---

### Q2: How does the system handle "Alert Storms" and Flapping Incidents?
**Answer**:
1. **Sliding-Window Deduplication**: Incoming alerts calculate a composite hash `SHA256(service + errorSignature + errorCategory)`. Redis stores this key with atomic increment and a 60-second sliding TTL. Repeated alerts within this window increment the hit count without creating duplicate incident rows or triggering redundant LLM calls.
2. **Anti-Flapping Tracker**: Uses exponential decay counters across a 5-minute sliding window. If an alert toggles on/off $> 5$ times in 60 seconds, the status flips to `FLAPPING_SUPPRESSED`. The system ceases individual alerts, raises the incident priority, and creates a consolidated timeline to prevent alert fatigue.

---

### Q3: Why is there a "Zero-LLM" policy for Low Severity alerts?
**Answer**:
Cost optimization and latency management. In large distributed systems, $> 80\%$ of alerts are minor warnings (e.g., transient network latency spike, minor disk usage warning). Sending all alerts to LLMs would cause enormous token costs and saturate inference rate limits. `RcaOrchestratorService` intercepts `Severity.Low` at step 1 and routes it strictly through rule-based heuristics (`RULE_ANALYSIS`), reserving AI tokens for complex, multi-service incidents.

---

### Q4: How do you ensure Zero Data Leakage when sending stack traces and logs to LLMs?
**Answer**:
All raw incident evidence and logs pass through `RedactionService` before reaching the RAG embedding database or LLM prompts. The redactor executes high-throughput regex and AST tokenizers to redact:
- Passwords, DB connection strings, and API keys.
- Authorization Bearer tokens and JWT signatures.
- Customer PII (Email addresses, phone numbers, IP addresses).
The LLM only ever receives a sanitized `RedactedIncidentContext`.

---

### Q5: How is audit compliance maintained for autonomous SRE actions?
**Answer**:
Through the **Transactional Outbox Pattern** (`AuditEventOutboxService`). Every incident transition, AI prompt/response metadata, human approval, and executor action is committed to `devops_audit_outbox` in the exact same database transaction as the business entity update. An asynchronous worker publishes these events to RabbitMQ (`aurora.devops.events`), guaranteeing an immutable, at-least-once audit trail for SOC2/ISO27001 compliance.
