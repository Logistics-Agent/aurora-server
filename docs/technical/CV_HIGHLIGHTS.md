# Aurora Logistics Platform — Technical CV Highlights & Interview Talking Points

> **Purpose**: Authoritative, production-grounded technical resume bullets, architectural evidence, and interview defense talking points.  
> **Source-of-Truth**: Grounded 100% in verified codebase implementation across .NET 10, Java 21 Spring Boot, NestJS TypeScript, Rust Stalwart, PostgreSQL, and RabbitMQ.

---

## 1. Architecture

### Bullet 1: Multi-Tenant Microservice & Micro-BFF Architecture
* **Recruiter-Friendly**: Architected a scalable multi-tenant SaaS logistics platform with specialized Backend-for-Frontend (BFF) layers separating Staff, Admin, and System Admin interfaces.
* **Technical Version**: Designed a polyglot microservice ecosystem (.NET 10, Java 21, NestJS) partitioned by domain boundaries, interconnected via internal gRPC contracts and asynchronous RabbitMQ events, fronted by role-scoped Micro-BFF gateways (`Staff.Bff`, `Admin.Bff`, `System.Bff`) with YARP reverse proxy routing.
* **Evidence / Services**: `src/dotnet/BFF/`, `src/dotnet/IamTenant`, `protos/`, `BuildingBlocks.BFF`.
* **Interview Talking Points**:
  - Why Micro-BFFs? Prevents monolithic API leakage, tailors payload schemas to user personas, and allows independent scaling and permission caching.
  - How gRPC is leveraged: High-throughput synchronous inter-service communication with strong Protobuf contract freezing and zero JSON serialization overhead.

---

## 2. Distributed Systems

### Bullet 2: Guaranteed At-Least-Once Delivery via Transactional Outbox
* **Recruiter-Friendly**: Engineered reliable distributed messaging between shipment, billing, and notification services to eliminate data loss.
* **Technical Version**: Implemented the Transactional Outbox Pattern with MassTransit and RabbitMQ across PostgreSQL databases; domain state mutations and integration events (`EmailReceivedEvent`, `ShipmentDeliveredEvent`, `DocumentOcrCompletedEvent`) commit atomically in local ACID transactions and are asynchronously dispatched by polling workers.
* **Evidence / Services**: `MailService`, `ShipmentWorkflow`, `DocumentOcr`, `RegulatoryCompliance`, `Notification`.
* **Interview Talking Points**:
  - Eliminates the Dual-Write Problem without needing heavy, fragile 2-Phase Commit (2PC) distributed transactions.
  - Consumer idempotency is enforced using `ConsumedIntegrationEvents` tables to safely tolerate message redelivery.

---

## 3. AI Engineering

### Bullet 3: Centralized AI Governance & Dynamic Capability Routing
* **Recruiter-Friendly**: Built a centralized enterprise AI gateway that eliminates vendor lock-in, enforces safety rails, and manages token budgets across all services.
* **Technical Version**: Designed and implemented a Java 21 / Spring Boot `ai-governance` service that abstracts LLM providers (OpenAI, Anthropic, Gemini) behind abstract capability tokens (`route.plan`, `mail.bec_check`, `compliance.rag`), with real-time Redis token quota enforcement, multi-provider circuit breakers, and prompt-injection sanitization.
* **Evidence / Services**: `src/java/ai-governance`, `CapabilityRouter.java`, `TokenQuotaManager.java`.
* **Interview Talking Points**:
  - Why decouple domain services from LLMs? Domain services never store API keys or select models; capability tokens allow zero-downtime model upgrades.
  - Resilience: Automatic failover across providers if a primary vendor encounters rate limits (`429`) or server errors (`5xx`).

### Bullet 4: Deterministic Financial Guardrails & Human-in-the-Loop AI
* **Recruiter-Friendly**: Designed safe AI agents for rate negotiations and email drafting where AI cannot make financial commitments or send unauthorized emails.
* **Technical Version**: Architected a hybrid deterministic-AI negotiation engine in NestJS where pricing, concession curves, and bottom floor margins are computed mathematically, while LLMs generate natural language draft suggestions (`SuggestedReplyDto`) that require explicit human review (`[Create Mail Draft]` $\rightarrow$ `[Send]`) before SMTP transmission.
* **Evidence / Services**: `src/nestjs/negotiation-agent-service`, `NegotiationsController.cs`, `MailService`.
* **Interview Talking Points**:
  - Financial safety: LLMs are strictly forbidden from altering numbers; prices are derived from TypeScript domain services.
  - Strict human review: AI produces suggestions; staff explicitly triggers draft creation, reviews in rich text editor, and signs with authenticated `SentByUserId`.

---

## 4. Security & Access Governance

### Bullet 5: Simplified RBAC + Direct Capability-Based Authorization
* **Recruiter-Friendly**: Modernized enterprise access control by separating user UI personas from operational execution rights to eliminate privilege escalation.
* **Technical Version**: Re-architected IAM into a Single Base Role (`STAFF`, `MANAGER`, `TENANT_ADMIN`, `SYSTEM_ADMIN`) governing navigation layouts, combined with direct capability permissions (`UserPermissions`) governing operational execution, backed by Redis caching and delta grant/revoke semantics.
* **Evidence / Services**: `src/dotnet/IamTenant`, `PermissionConstants.cs`, `IamAuthorizationTests.cs`.
* **Interview Talking Points**:
  - Base Role $\ne$ Operational Authority: Eliminates role explosion and prevents privilege escalation.
  - Bulk updates utilize delta semantics (`Grant`/`Revoke`) to avoid wiping dissimilar baseline permissions across multi-user selections.

### Bullet 6: Multi-Stage Inbound/Outbound Email Security Pipeline
* **Recruiter-Friendly**: Built a two-way email defense pipeline protecting enterprise logistics mailboxes from phishing, malware, and spam.
* **Technical Version**: Engineered a 12-stage inbound and 6-stage outbound email security pipeline in .NET 10 integrating SPF/DKIM/DMARC cryptographic validation, ClamAV antivirus daemon (TCP 3310), Apache SpamAssassin (TCP 783), AI BEC/phishing scoring, sliding-window Redis rate limiting, and quarantine workflows.
* **Evidence / Services**: `src/dotnet/MailService/Application/Pipeline/`, `InboundStages.cs`, `OutboundStages.cs`.
* **Interview Talking Points**:
  - Short-circuit quarantine: Malicious emails or phishing threats exceeding threshold scores are isolated in Cloudflare R2 and flagged for manager review.
  - Reply-to-Claim guard: Enforces that outbound emails on unassigned shared mailbox threads atomically claim thread ownership before dispatch.

---

## 5. Backend Engineering

### Bullet 7: Risk-Based Operational Governance & Vehicle Route Optimization
* **Recruiter-Friendly**: Implemented automated vehicle route optimization integrated with an intelligent risk-based approval workflow.
* **Technical Version**: Integrated VROOM and OSRM C++ optimization engines for Capacitated Vehicle Routing with Time Windows (CVRPTW), coupled with a 4-tier composite risk governance engine (`LOW` $\rightarrow$ Auto, `MEDIUM` $\rightarrow$ Staff Acknowledge, `HIGH` $\rightarrow$ Manager Approval, `CRITICAL` $\rightarrow$ Hard Block) and tenant policy versioning.
* **Evidence / Services**: `src/dotnet/RoutePlanningAgent`, `VroomClient.cs`, `TenantRiskPolicyConfig.cs`.
* **Interview Talking Points**:
  - Replaced bottlenecking mandatory manager approvals with automated risk tiers.
  - Fail-closed security: Unconfigured tenant risk policies throw `RiskPolicyNotConfiguredException` rather than silently using defaults.

### Bullet 8: Shared Mailbox Triage & Thread Concurrency Engine
* **Recruiter-Friendly**: Built a collaborative shared mailbox triage system that prevents duplicate customer replies in high-volume operations.
* **Technical Version**: Designed an `EmailThread` aggregation and responsibility engine featuring atomic claiming (`POST /api/v1/mail/threads/{id}/claim`), optimistic concurrency tokens (`thread.Version`), supervisory reassignment, and real-time WebSocket live-locking.
* **Evidence / Services**: `src/dotnet/MailService`, `ClaimThreadCommandHandler.cs`, `THREAD_ASSIGNMENT.md`.
* **Interview Talking Points**:
  - Optimistic concurrency: Prevents race conditions when two operators attempt to claim the same customer inquiry simultaneously (`409 Conflict`).
  - Workspaces: Strict segregation into `UNASSIGNED`, `MY_WORK`, and supervisory `ALL` queues.

---

## 6. DevOps, Infrastructure & Production

### Bullet 9: Self-Hosted Stalwart Mail Server & Bare-Metal Topology
* **Recruiter-Friendly**: Deployed and automated a self-hosted enterprise mail server stack with automated backups and disaster recovery runbooks.
* **Technical Version**: Automated deployment of Stalwart All-in-One Mail Server in Rust on dedicated Ubuntu 24.04 nodes, including UFW firewall configuration, Cloudflare DNS-01 Let's Encrypt wildcard TLS automation, daily rolling backup scripts (`backup.sh`), and deliverability verification (`verify-dns-deliverability.sh`).
* **Evidence / Services**: `src/dotnet/MailService/deploy/`, `docker-compose.prod.yml`, `setup-host.sh`, `backup.sh`.
* **Interview Talking Points**:
  - Port isolation: Administrative REST API (8080) bound strictly to loopback (`127.0.0.1`), with public ports 25, 587, 993 open to the internet.
  - Zero-downtime deployment: `deploy.sh` executes additive database migrations (`efbundle`), performs health check verification on port 9090, and automatically triggers `rollback.sh` on failure.

---

## 7. Database & Data Architecture

### Bullet 10: PostgreSQL Legal RAG Knowledge Base via `pgvector`
* **Recruiter-Friendly**: Built a trade regulatory knowledge retrieval system utilizing native vector search for customs compliance.
* **Technical Version**: Implemented a cross-border regulatory compliance service in .NET 10 using PostgreSQL 16 `pgvector` with HNSW cosine similarity indexing, combining keyword matching with semantic vector retrieval and mandatory legal statute citation verification (`ComplianceCitation`).
* **Evidence / Services**: `src/dotnet/RegulatoryCompliance`, `RegulatoryChunk.cs`, `ComplianceCitation.cs`.
* **Interview Talking Points**:
  - Why `pgvector` over standalone vector DBs? Single ACID transaction for vector chunks, relational documents, and transactional outbox events.
  - Zero citation-free claims: Compliance findings must cite verified legal statute articles or be flagged for human officer review.

---

## 8. Testing & Reliability

### Bullet 11: Comprehensive Automated Testing & Expand-Contract Migrations
* **Recruiter-Friendly**: Maintained high code quality and zero-downtime schema evolution through rigorous unit/integration tests and backward-compatible migrations.
* **Technical Version**: Established a robust test suite covering multi-tenant query filters, concurrency conflicts, gRPC exception mapping, and permission gates (e.g. 90 passing tests in `MailService.Tests`), enforced with an Expand-and-Contract database migration policy using standalone `efbundle` executables.
* **Evidence / Services**: `tests/dotnet/MailService.Tests/`, `IamAuthorizationTests.cs`, `deploy/RUNBOOK.md`.
* **Interview Talking Points**:
  - Expand and Contract rule: Additive-only schema changes during active releases; destructive drops/renames must be staged across multi-phase deployments to ensure rollback safety.
  - Deterministic test execution: Comprehensive mock/in-memory test isolation for Redis and gRPC clients.
