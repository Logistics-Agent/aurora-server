# Intelligent Route Planning & Risk Governance — Interview Q&A Guide

> **Target Audience**: Junior, Mid-level, Senior & System Design Interviewers  
> **Source-of-Truth**: Grounded 100% in .NET 10 `RoutePlanningAgent` implementation.

---

### Q1 (Junior): How does the risk-based operational governance model improve operations over a mandatory manager approval workflow?
**Answer**:  
Under mandatory manager approval, every minor dispatch modification (e.g. adding a small detour stop or adjusting a sequence) created a queue bottleneck that caused missed delivery windows. The risk governance model assesses a composite risk score $[0, 100]$. Low-risk routes (score $\le 25$) are auto-approved instantly, reserving manager attention exclusively for high-risk operations (score $51-75$), while critical-risk operations are automatically blocked.

---

### Q2 (Mid): What happens if a tenant has not configured a risk policy (`PolicyMode = Unconfigured`)?
**Answer**:  
The system enforces a **Fail-Closed Policy Guard**. Rather than silently defaulting to platform baselines (which could violate tenant-specific compliance), the policy provider throws `RiskPolicyNotConfiguredException`. The tenant administrator must explicitly select either `UsePlatformDefault` or configure a `CustomPolicy` before route planning operations are permitted.

---

### Q3 (Mid): Why was `TenantAiConfig` removed from `RoutePlanningAgent` during the refactoring?
**Answer**:  
Managing AI model parameters, LLM API keys, and prompt providers inside domain services violated separation of concerns. All AI infrastructure was centralized in the Java `AiGovernance` service. RoutePlanning now communicates with `AiGovernance` exclusively via gRPC using capability tokens (`capability: "route.plan"`), allowing RoutePlanning to focus strictly on its core domain: VROOM optimization, risk scoring, and policy versioning.

---

### Q4 (Senior): How are custom tenant risk policies versioned to prevent breaking active routes?
**Answer**:  
`TenantRiskPolicy` entities are immutable. When an administrator updates risk weights or thresholds, a new version record is created (`Version = N + 1`). `TenantRiskPolicyConfig` points to `ActivePolicyId` and `ActivePolicyVersion`. Active routes store the specific policy version they were evaluated against in `RiskAssessment.PolicyVersion`, ensuring historical auditability and preventing mid-flight re-evaluations from invalidating approved routes.

---

### Q5 (System Design): How does the service handle high-concurrency route optimization requests?
**Answer**:  
1. **Asynchronous Processing**: Heavy multi-stop VROOM calculations run asynchronously via task queues.
2. **OSRM Caching**: Road distance matrices are cached in Redis to avoid redundant graph traversals.
3. **Optimistic Locking**: Route updates use `route.Version` concurrency tokens to reject conflicting simultaneous stop mutations (`409 Conflict`).
