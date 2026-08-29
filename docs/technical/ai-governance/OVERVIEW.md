# Centralized AI Governance & Gateway Service — Service Overview

> **Service Layer**: AI Infrastructure, Capability Routing & Safety Governance  
> **Target Audience**: Technical Recruiters, AI/ML Engineers, System Architects  
> **Source-of-Truth**: `src/java/ai-governance`, `AiGovernanceService`, `CapabilityRouter`, `TokenQuotaManager`, `PromptInjectionFilter`, `protos/ai_governance.proto`.

---

## 1. Service Purpose & Problem Solved

In modern microservice ecosystems, embedding direct LLM provider SDKs (OpenAI, Anthropic, Gemini) across multiple backend services leads to severe architectural anti-patterns:
- **API Key Sprawl & Leakage**: Credentials distributed across numerous repositories and containers.
- **Uncontrolled Token Costs**: Zero unified rate limiting, budget enforcement, or tenant cost allocation.
- **Provider Lock-In**: Domain logic tightly coupled to specific model parameters and SDK versions.
- **Compliance & Security Blindspots**: Inability to enforce uniform PII redaction, prompt injection defense, or audit trails.

The **AI Governance Service** resolves this by establishing a **Unified AI Gateway and Governance Layer** in Java 21 / Spring Boot:
- **Zero Direct LLM Access in Domain Services**: Services like `RoutePlanningAgent`, `MailService`, and `RegulatoryCompliance` **never** connect to OpenAI or manage models.
- **Capability-Based Routing**: Services request abstract tasks (e.g. `capability: "route.plan"`, `capability: "mail.bec_check"`), and AI Governance dynamically selects the optimal model, parameters, fallback strategy, and temperature.
- **Enforced Safety & Budget Rails**: Every AI invocation is subjected to real-time token quota checks, prompt injection filtering, and audit logging.

---

## 2. Architecture & Tech Stack

```
[ Domain Microservices (.NET / NestJS / Java) ]
  (RoutePlanning, MailService, RegulatoryCompliance, Negotiation)
                     │
                     ▼ (gRPC Port 50051 / capability: "...")
┌─────────────────────────────────────────────────────────────┐
│                 Central AI Governance Service               │
│  ├── Dynamic Capability Router (Capability -> Model Config) │
│  ├── Token Quota & Cost Control Engine (Tenant Budgeting)   │
│  ├── Safety & Security Filter (Prompt Injection, PII Mask)  │
│  ├── Provider Abstraction Layer (OpenAI, Anthropic, Gemini) │
│  └── Telemetry & Audit Logger (OpenTelemetry, Token Stats)  │
└────────────────────────────┬────────────────────────────────┘
                             │
            ┌────────────────┼────────────────┐
            ▼                ▼                ▼
     [ OpenAI API ]   [ Anthropic API ]  [ Google Gemini ]
```

| Layer | Technology |
|---|---|
| **Runtime & Framework** | Java 21 (LTS), Spring Boot 3.3, gRPC Spring Boot Starter |
| **Persistence** | PostgreSQL 16 (Neon Serverless SSL), Spring Data JPA / Flyway |
| **Caching & Rate Limiting** | Redis 7 (Token bucket rate limiting, daily/monthly quota tracking) |
| **Observability** | OpenTelemetry Java Agent, Prometheus metrics, Micrometer tracing |
| **Protobuf Contracts** | `protos/ai_governance.proto` |

---

## 3. Owned Data & Schema Boundaries

The `ai-governance` service strictly owns:
- **`AiCapabilities`**: Registry of defined capabilities (`route.plan`, `mail.bec_check`, `mail.phishing_check`, `ocr.extract`, `negotiation.strategy`).
- **`TenantAiConfigs`**: Tenant-level enable/disable flags, custom provider preferences, and allocated model tiers.
- **`TenantTokenBudgets`**: Monthly and daily token allowances, current consumption counters, and hard spend caps.
- **`AiInvocationAudits`**: Immutable audit logs capturing `TenantId`, `Capability`, `Provider`, `Model`, `PromptTokens`, `CompletionTokens`, `LatencyMs`, `CostUsd`, and content hashes.

---

## 4. API & Contract Surface

Exposed via `protos/ai_governance.proto` (`AiGovernanceService`):

- `ExecuteCapability`: Executes an abstract AI capability with structured prompt context and returns validated JSON output.
- `EvaluateSecurity`: High-speed binary classifier for prompt-injection, executive impersonation, and phishing checks.
- `GetTenantUsage`: Queries real-time token consumption, quota status, and monthly cost metrics.
- `ConfigureTenantPolicy`: Admin API to set token limits and model routing rules.

---

## 5. Security & Invariants

1. **Deterministic Business Fallbacks**: If the AI provider times out or produces unparseable JSON, the gateway falls back to configured deterministic rules without crashing the caller.
2. **PII & Credential Shield**: Inbound prompts are sanitized against regexes for credit cards, secret keys, and JWTs before transmission to external providers.
3. **Current Maturity**: Production-ready core gateway with high-throughput gRPC routing and multi-provider failover.
