# Centralized AI Governance & Gateway Service — Interview Q&A Guide

> **Target Audience**: Junior, Mid-level, Senior & AI System Design Interviewers  
> **Source-of-Truth**: Grounded 100% in Java `ai-governance` implementation.

---

### Q1 (Junior): Why did the architecture extract AI handling into a dedicated Java service?
**Answer**:  
Rather than scattering OpenAI/Anthropic SDKs, API keys, and model parameters across .NET, NestJS, and Python microservices, `ai-governance` acts as a single centralized gateway. This isolates API keys in one secure vault, unifies rate limiting and tenant token billing, and allows the platform to switch or upgrade underlying LLM models without modifying domain microservices.

---

### Q2 (Mid): What is "Capability Routing" and how does it decouple domain logic from AI models?
**Answer**:  
Domain services never ask for a specific model like `gpt-4o`. Instead, they request a **Capability** (e.g. `capability: "mail.bec_check"` or `capability: "route.plan"`). The AI Governance service maps this capability to the appropriate model, system prompt, temperature, JSON schema, and token budget based on the calling tenant's subscription tier.

---

### Q3 (Mid): How does the service enforce multi-tenant cost controls and token quotas?
**Answer**:  
The service uses atomic Redis counters (`INCRBY`) keyed by `{tenantId}:{period}` to track real-time token spend. When an invocation request arrives, it checks whether the current consumption plus estimated prompt tokens exceeds the tenant's quota. If exceeded, it throws gRPC `RESOURCE_EXHAUSTED`. Every invocation also persists an immutable `AiInvocationAudit` record to PostgreSQL for financial accounting.

---

### Q4 (Senior): How does the gateway handle external LLM outages and rate limits?
**Answer**:  
The gateway implements multi-provider failover with Resilience4j circuit breakers:
1. If the primary provider (e.g. OpenAI) returns `429` or `5xx`, the adapter transparently retries against a secondary provider (e.g. Anthropic Claude or Google Gemini).
2. If all external providers fail or time out, the gateway returns a typed `FALLBACK_REQUIRED` response so the calling domain service can execute deterministic rules (e.g. VROOM algorithm for routes or SpamAssassin for mail) without throwing an unhandled exception.

---

### Q5 (Senior / System Design): How does the service defend against prompt injection and data leaks?
**Answer**:  
All requests pass through an inbound `SecurityFilter`:
1. Regex and heuristic scanning for known jailbreak strings and prompt injection vectors.
2. Automated PII masking to scrub sensitive customer data before sending prompts to external APIs.
3. Strict JSON Schema parsing on LLM outputs using schema validators; malformed outputs are discarded rather than passed to domain handlers.

---

### Q6 (System Design): What are the tradeoffs of building a centralized AI gateway vs. calling LLM APIs directly in each microservice?
**Answer**:  
- **Pros**: Centralized security, zero key sprawl, tenant-level token budgeting, auditability, dynamic provider switching, and automated fallback.
- **Cons**: Adds a small internal network hop (~5ms via gRPC) and introduces a centralized point of failure (which Aurora mitigates via container replication and multi-provider failover).
