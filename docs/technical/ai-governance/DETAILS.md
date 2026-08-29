# Centralized AI Governance & Gateway Service — Deep Technical Details

> **Service Layer**: Architecture, Capability Routing, Resilience & Cost Controls  
> **Source-of-Truth**: `src/java/ai-governance`, `CapabilityRouter.java`, `TokenQuotaManager.java`, `ProviderAdapterFactory.java`, `SecurityFilter.java`.

---

## 1. Architectural Patterns & Domain Model

The service is structured following **Hexagonal / Ports-and-Adapters Architecture** in Java 21:

```
[ Inbound Port: gRPC ] ──> [ Application Service / Orchestrator ]
                                   │
                 ┌─────────────────┴─────────────────┐
                 ▼                                   ▼
        [ Domain Entities ]               [ Outbound Ports ]
        - AiCapability                     - ProviderAdapter (OpenAI, Anthropic)
        - TenantQuota                      - QuotaRepository (Redis + SQL)
        - InvocationAudit                  - AuditLogger
```

---

## 2. Deep-Dive: Capability Routing Engine

### 2.1 Decoupling Domain Logic from Model Specs
When `RoutePlanningAgent` calls:
```protobuf
message ExecuteCapabilityRequest {
  string tenant_id = 1;
  string capability = 2; // e.g. "route.plan"
  string prompt_context_json = 3;
}
```
The `CapabilityRouter`:
1. Looks up the registered `AiCapability` for `"route.plan"`.
2. Resolves tenant-specific overrides from `TenantAiConfig` (e.g. standard tier uses `gpt-4o-mini`, enterprise tier uses `claude-3-5-sonnet`).
3. Formats system prompt templates with strict JSON schema constraints.
4. Dispatches to the appropriate `ProviderAdapter`.

### 2.2 Dynamic Failover & Retry
If the primary provider returns `503 Service Unavailable` or `429 Rate Limited`:
- The adapter catches the error and executes an **Automatic Secondary Failover** (e.g., OpenAI $\rightarrow$ Anthropic or Gemini).
- If all external LLMs fail, it returns an explicit `FALLBACK_REQUIRED` status code, enabling domain callers to trigger deterministic heuristics.

---

## 3. Real-Time Token Quota & Cost Control

Cost and token limits are managed using a **Two-Tier Redis + PostgreSQL Architecture**:

1. **Redis Atomic Counters**:
   - `ratelimit:ai:tokens:{tenantId}:{yyyyMM}` (Monthly usage counter).
   - Invocations check `INCRBY` against the tenant's allocated monthly cap.
   - If the counter exceeds the hard limit, the request is rejected with gRPC `RESOURCE_EXHAUSTED` (`TENANT_AI_QUOTA_EXCEEDED`).
2. **PostgreSQL Asynchronous Reconciliation**:
   - Every completed invocation writes an immutable `AiInvocationAudit` record.
   - A background job reconciles Redis counters against PostgreSQL sums daily.

---

## 4. Prompt Injection & Security Filter Pipeline

Before any prompt is sent to external LLMs, the `SecurityFilter` executes:

1. **Prompt Sanitization**: Strips known jailbreak patterns (`"Ignore previous instructions"`, `"System prompt override"`).
2. **PII Masking**: Redacts credit card numbers, national IDs, and credentials.
3. **Structured Output Validation**: Enforces JSON Schema validation on LLM completions before returning to domain microservices.

---

## 5. Observability, Resilience & Tradeoffs

- **Micrometer & OpenTelemetry**: Tracks latency percentiles ($p50, p95, p99$), token usage per capability, and provider error rates.
- **Resilience**: Integrated Resilience4j Circuit Breakers around external provider HTTP clients.
- **Tradeoff**: Introducing a centralized AI gateway adds a small network hop (~5-10ms gRPC overhead), which is negligible compared to typical LLM inference times (500ms-3000ms), while providing centralized security, auditability, and provider independence.
