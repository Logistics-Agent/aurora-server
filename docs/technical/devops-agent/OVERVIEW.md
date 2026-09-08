# Autonomous DevOps & Incident Response Agent (`devops-agent`) — Service Overview

> **Service Layer**: Autonomous SRE, Intelligent Incident Ingestion, AI Root Cause Analysis (RCA) & Governed Auto-Remediation  
> **Target Audience**: SRE/DevOps Engineers, Backend Architects, AI Engineers, Technical Interviewers  
> **Source-of-Truth**: `src/java/devops-agent`, `RcaOrchestratorService`, `DedupService`, `AntiFlappingTracker`, `ActionExecutorRegistry`, `IncidentGrpcHandler`.

---

## 1. Service Purpose & Problem Solved

In high-throughput microservice ecosystems, infrastructure and application anomalies (OOM errors, cascading connection pool exhaustions, deadlocks, and network partitions) trigger massive alert storms. Traditional SRE workflows suffer from alert fatigue, slow manual triage (MTTR > 45 mins), and risky unverified automated scripts.

The **DevOps-Agent Microservice** provides **Autonomous, Governed AIOps & Incident Remediation**:
- **Smart Ingestion & Anti-Flapping**: Ingests alerts from Prometheus, AlertManager, Logstash, and cloud monitors. Employs sliding-window deduplication and anti-flapping trackers to suppress transient noise.
- **Governed AI Root Cause Analysis**: Feeds sanitized incident context into internal `DevOpsRagClient` and queries `AiGovernance` (capability: `devops.rca`) to generate deterministic root cause diagnostics and mitigation plans.
- **Fail-Safe Auto-Remediation**: Executes guarded infrastructure actions (`RestartPod`, `ClearCache`, `RollbackDeployment`) with strict verification post-checks, rollback mechanisms, and mandatory Human-in-the-Loop (HitL) gates for high-severity incidents.
- **Self-Configuring Alert Rules**: Proposes and optimizes alert rule thresholds based on historical incident recurrence and false-positive rates.

---

## 2. Architecture & Tech Stack

```
[ Prometheus / CloudWatch / App Logs ]
                  │ (Alert Ingestion / gRPC)
                  ▼
[ DevOps-Agent Service (Spring Boot 3.3 / Java 21) ]
  ├── 1. Ingestion & IngestionGrpcHandler
  │      ├── DedupService (Redis Sliding Window)
  │      └── AntiFlappingTracker (State hysteresis)
  ├── 2. Security & Redaction Pipeline (RedactionService)
  ├── 3. DevOps RAG & Context Builder (DevOpsRagClient)
  ├── 4. AI Governance Integration (capability: "devops.rca")
  ├── 5. Safety-Guarded Action Framework
  │      ├── ActionExecutorRegistry (RestartPod, ClearCache, Rollback)
  │      ├── VerificationService (Post-action health probe)
  │      └── Human-in-the-Loop Approval Gate (High/Critical)
  └── 6. Transactional Audit Outbox (AuditEventOutboxService -> RabbitMQ)
```

| Layer | Technology |
|---|---|
| **Runtime & Framework** | Java 21, Spring Boot 3.3.2 |
| **RPC & Communication** | gRPC (Protobuf), Netty, Spring gRPC Starter |
| **AI Integration** | Spring AI, Central `AiGovernance` Client (`devops.rca`) |
| **Deduplication & Cache** | Redis (Sliding window alert key hashes & TTLs) |
| **Relational Storage** | PostgreSQL 16 + Spring Data JPA / Hibernate |
| **Event Streaming** | RabbitMQ (Transactional Outbox pattern for audit events) |
| **Testing** | JUnit 5, Mockito, Jqwik (Property-based testing) |

---

## 3. Owned Data & Schema Boundaries

```
┌─────────────────────────┐         ┌─────────────────────────┐
│        Incident         │ 1     * │       RcaAnalysis       │
│─────────────────────────│─────────│─────────────────────────│
│ id (UUID)               │         │ id (UUID)               │
│ correlation_id (String) │         │ incident_id (FK)        │
│ error_signature (String)│         │ analysis_type (Enum)    │
│ severity (Enum)         │         │ recommendation_json     │
│ status (Enum)           │         │ confidence (BigDecimal) │
│ affected_service (Str)  │         │ llm_tokens_used (Int)   │
│ rca_root_cause (Text)   │         │ rag_augmented (Boolean) │
│ rca_recommendation (Txt)│         │ governance_decision_id  │
└─────────────────────────┘         └─────────────────────────┘
             │ 1
             │ *
┌─────────────────────────┐         ┌─────────────────────────┐
│    RemediationAction    │         │    AuditEventOutbox     │
│─────────────────────────│         │─────────────────────────│
│ id (UUID)               │         │ id (UUID)               │
│ incident_id (FK)        │         │ correlation_id (String) │
│ action_type (Enum)      │         │ action_type (Enum)      │
│ target_resource (String)│         │ payload_json (Text)     │
│ execution_status (Enum) │         │ published (Boolean)     │
│ requires_approval (Bool)│         │ created_at (Timestamp)  │
└─────────────────────────┘         └─────────────────────────┘
```

---

## 4. gRPC Service Surface

The service exposes 4 authoritative gRPC contracts:

1. **`IngestionGrpcService` (`IngestionGrpcHandler`)**:
   - `IngestAlert(AlertPayload)`: Ingests external alarms, performs deduplication, and routes to incident triage.
2. **`IncidentGrpcService` (`IncidentGrpcHandler`)**:
   - `GetIncident(IncidentId)`: Retrieves incident timeline, RCA analysis, and action history.
   - `ListActiveIncidents(Filter)`: Queries unresolved incidents by service or severity.
   - `ApproveRemediation(ApprovalRequest)`: Staff/Admin approval for HitL actions.
3. **`RuleGrpcService` (`RuleGrpcHandler`)**:
   - `ProposeRule(RuleRequest)`: Evaluates noise patterns and suggests alert rule adaptations.
   - `ApplyRule(RulePayload)`: Commits optimized thresholds to monitoring backends.
4. **`SelfConfigGrpcService` (`SelfConfigGrpcHandler`)**:
   - Manages autonomous threshold tuning, silence policies, and agent operating modes.

---

## 5. Security, Guardrails & Production Invariants

1. **Mandatory PII & Secret Redaction**: All raw log payloads, stack traces, and database connection strings pass through `RedactionService` before storage or RAG/LLM invocation.
2. **Zero-LLM Guard on Low Severity**: Incidents with `Severity.Low` are strictly barred from triggering LLM inference (`AiGovernanceClient`) to prevent token waste and cost surges.
3. **Fail-Closed Remediation**: Destructive actions (e.g., service restart, traffic rerouting) on production pods require human sign-off unless explicitly whitelisted with a confidence score $\ge 0.95$.
4. **Anti-Flapping Thresholds**: If an alert signature fires $> 5$ times in a 60-second window, it is locked into flapping state, suppressing repetitive notifications while escalating severity.
