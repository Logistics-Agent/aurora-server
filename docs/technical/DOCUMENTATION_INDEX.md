# Aurora Platform — Authoritative Technical Documentation Index

> **Directory Root**: `docs/technical/`  
> **Audience**: Backend Engineers, Frontend Engineers, DevOps/SRE, System Architects, Technical Interviewers.

---

## 1. Architecture & Platform Standards

- [`docs/technical/ARCHITECTURE.md`](file:///D:/IT/CD/aurora-server/docs/technical/ARCHITECTURE.md): Global polyglot architecture, microservice boundaries, gRPC contracts, MassTransit RabbitMQ outbox patterns, and data flow.
- [`docs/technical/OVERVIEW.md`](file:///D:/IT/CD/aurora-server/docs/technical/OVERVIEW.md): Comprehensive system overview and business domain introduction.
- [`docs/technical/SERVICE_INTEGRATION_MATRIX.md`](file:///D:/IT/CD/aurora-server/docs/technical/SERVICE_INTEGRATION_MATRIX.md): Complete cross-service integration dependency and communication matrix.
- [`docs/technical/IMPLEMENTATION_STATUS.md`](file:///D:/IT/CD/aurora-server/docs/technical/IMPLEMENTATION_STATUS.md): Current completion status and test coverage metrics across services.
- [`docs/technical/FINAL_AUDIT.md`](file:///D:/IT/CD/aurora-server/docs/technical/FINAL_AUDIT.md): Production readiness classification, consistency verification, and connection scores.
- [`docs/technical/INTEGRATION_GAPS.md`](file:///D:/IT/CD/aurora-server/docs/technical/INTEGRATION_GAPS.md): Code-verified integration gaps, severities, and actionable fixes.
- [`docs/technical/ROADMAP.md`](file:///D:/IT/CD/aurora-server/docs/technical/ROADMAP.md): Prioritized P0, P1, P2 engineering roadmap and backlog.

---

## 2. Frontend & BFF Integration Guides

- [`docs/technical/frontend/API_CATALOG.md`](file:///D:/IT/CD/aurora-server/docs/technical/frontend/API_CATALOG.md): Unified REST API catalog across all Micro-BFFs (`Staff.Bff`, `Admin.Bff`, `System.Bff`).
- [`docs/technical/frontend/FE_INTEGRATION_GUIDE.md`](file:///D:/IT/CD/aurora-server/docs/technical/frontend/FE_INTEGRATION_GUIDE.md): Frontend architectural patterns, cookie authentication, gRPC error translation, and API clients.
- [`docs/technical/frontend/FE_FLOW_COOKBOOK.md`](file:///D:/IT/CD/aurora-server/docs/technical/frontend/FE_FLOW_COOKBOOK.md): Step-by-step UI recipes for shipment booking, thread claiming, and OCR reviews.
- [`docs/technical/frontend/ROLE_PERMISSION_API_MATRIX.md`](file:///D:/IT/CD/aurora-server/docs/technical/frontend/ROLE_PERMISSION_API_MATRIX.md): Capability permissions and role requirement matrix for all frontend routes.
- [`docs/technical/frontend/NOTIFICATION-FE-INTEGRATION.md`](frontend/NOTIFICATION-FE-INTEGRATION.md): Detailed FE Notification/FCM integration, contracts, sequence flows, token lifecycle, auth, and local testing.

---

## 3. Microservice Technical Suites (13 Services)

### 3.1 Identity & Access Management (`iam`)
- [`docs/technical/iam/OVERVIEW.md`](file:///D:/IT/CD/aurora-server/docs/technical/iam/OVERVIEW.md): Identity & tenant access overview.
- [`docs/technical/iam/DETAILS.md`](file:///D:/IT/CD/aurora-server/docs/technical/iam/DETAILS.md): Single Base Role + direct capabilities, delta updates, Redis caching.
- [`docs/technical/iam/INTERVIEW_QA.md`](file:///D:/IT/CD/aurora-server/docs/technical/iam/INTERVIEW_QA.md): IAM architectural defense Q&A.
- [`docs/technical/iam/AUTHORIZATION_MODEL.md`](file:///D:/IT/CD/aurora-server/docs/technical/iam/AUTHORIZATION_MODEL.md): Deep-dive authorization specification.
- [`docs/technical/iam/USER_PERMISSION_MANAGEMENT.md`](file:///D:/IT/CD/aurora-server/docs/technical/iam/USER_PERMISSION_MANAGEMENT.md): Single and bulk permission delta operations.
- [`docs/technical/iam/FE_AUTHORIZATION_GUIDE.md`](file:///D:/IT/CD/aurora-server/docs/technical/iam/FE_AUTHORIZATION_GUIDE.md): Frontend permission gates and React hooks.
- [`docs/technical/iam/MIGRATION_STATUS.md`](file:///D:/IT/CD/aurora-server/docs/technical/iam/MIGRATION_STATUS.md): IAM migration status and test evidence.

### 3.2 Central AI Governance & Gateway (`ai-governance`)
- [`docs/technical/ai-governance/OVERVIEW.md`](file:///D:/IT/CD/aurora-server/docs/technical/ai-governance/OVERVIEW.md): Central AI gateway purpose, capability routing, and cost control.
- [`docs/technical/ai-governance/DETAILS.md`](file:///D:/IT/CD/aurora-server/docs/technical/ai-governance/DETAILS.md): Ports-and-adapters architecture, token quota counters, and security filters.
- [`docs/technical/ai-governance/INTERVIEW_QA.md`](file:///D:/IT/CD/aurora-server/docs/technical/ai-governance/INTERVIEW_QA.md): AI infrastructure & governance interview Q&A.

### 3.3 Enterprise Mail Platform (`mail`)
- [`docs/technical/mail/OVERVIEW.md`](file:///D:/IT/CD/aurora-server/docs/technical/mail/OVERVIEW.md): Shared mailbox model, thread lifecycle, and component overview.
- [`docs/technical/mail/DETAILS.md`](file:///D:/IT/CD/aurora-server/docs/technical/mail/DETAILS.md): Inbound/outbound pipeline deep-dive, atomic claim, and reply-to-claim.
- [`docs/technical/mail/INTERVIEW_QA.md`](file:///D:/IT/CD/aurora-server/docs/technical/mail/INTERVIEW_QA.md): Mail architecture & security interview Q&A.
- [`docs/technical/mail/ARCHITECTURE.md`](file:///D:/IT/CD/aurora-server/docs/technical/mail/ARCHITECTURE.md): Complete component & sequence diagrams, outbox schema, and ERD.
- [`docs/technical/mail/API.md`](file:///D:/IT/CD/aurora-server/docs/technical/mail/API.md): 24-endpoint REST and gRPC API catalog.
- [`docs/technical/mail/THREAD_ASSIGNMENT.md`](file:///D:/IT/CD/aurora-server/docs/technical/mail/THREAD_ASSIGNMENT.md): Queue triage (`UNASSIGNED`, `MY_WORK`, `ALL`), claim locks, and assignment history.
- [`docs/technical/mail/SECURITY_PIPELINE.md`](file:///D:/IT/CD/aurora-server/docs/technical/mail/SECURITY_PIPELINE.md): 12 inbound / 6 outbound stages, ClamAV, SpamAssassin, AI BEC, and quarantine.
- [`docs/technical/mail/NEGOTIATION_FLOW.md`](file:///D:/IT/CD/aurora-server/docs/technical/mail/NEGOTIATION_FLOW.md): Human-in-the-loop AI negotiation draft creation.
- [`docs/technical/mail/STALWART_INTEGRATION.md`](file:///D:/IT/CD/aurora-server/docs/technical/mail/STALWART_INTEGRATION.md): Stalwart Rust mail server configuration, ports, and JMAP API.
- [`docs/technical/mail/DEPLOYMENT.md`](file:///D:/IT/CD/aurora-server/docs/technical/mail/DEPLOYMENT.md): Production host setup, Docker Compose, and automated deployment runners.
- [`docs/technical/mail/OPERATIONS_RUNBOOK.md`](file:///D:/IT/CD/aurora-server/docs/technical/mail/OPERATIONS_RUNBOOK.md): SRE operational commands, automated backups, and disaster recovery.
- [`docs/technical/mail/DELIVERABILITY.md`](file:///D:/IT/CD/aurora-server/docs/technical/mail/DELIVERABILITY.md): Deliverability triangle (SPF, DKIM, DMARC, PTR), verify scripts, and IP warm-up.

### 3.4 Rate Negotiation Agent (`negotiation`)
- [`docs/technical/negotiation/OVERVIEW.md`](file:///D:/IT/CD/aurora-server/docs/technical/negotiation/OVERVIEW.md): Deterministic financial engine + natural language AI hybrid overview.
- [`docs/technical/negotiation/DETAILS.md`](file:///D:/IT/CD/aurora-server/docs/technical/negotiation/DETAILS.md): Concession curve mathematics, floor price guards, and HitL flow.
- [`docs/technical/negotiation/INTERVIEW_QA.md`](file:///D:/IT/CD/aurora-server/docs/technical/negotiation/INTERVIEW_QA.md): Bidding and negotiation agent interview Q&A.

### 3.5 Customer & Operational Assistant (`customer-assistant`)
- [`docs/technical/customer-assistant/OVERVIEW.md`](file:///D:/IT/CD/aurora-server/docs/technical/customer-assistant/OVERVIEW.md): Intent routing, conversational orchestrator, and tool calling overview.
- [`docs/technical/customer-assistant/DETAILS.md`](file:///D:/IT/CD/aurora-server/docs/technical/customer-assistant/DETAILS.md): Sandboxed tool calling, tenant context injection, and Redis session memory.
- [`docs/technical/customer-assistant/INTERVIEW_QA.md`](file:///D:/IT/CD/aurora-server/docs/technical/customer-assistant/INTERVIEW_QA.md): Conversational assistant interview Q&A.

### 3.6 Document OCR & Extraction (`document-ocr`)
- [`docs/technical/document-ocr/OVERVIEW.md`](file:///D:/IT/CD/aurora-server/docs/technical/document-ocr/OVERVIEW.md): Multi-type document OCR and human review queue overview.
- [`docs/technical/document-ocr/DETAILS.md`](file:///D:/IT/CD/aurora-server/docs/technical/document-ocr/DETAILS.md): ISO 6346 container checksum algorithm, state machine, and correction API.
- [`docs/technical/document-ocr/INTERVIEW_QA.md`](file:///D:/IT/CD/aurora-server/docs/technical/document-ocr/INTERVIEW_QA.md): Document OCR interview Q&A.

### 3.7 Regulatory Compliance RAG (`regulatory-compliance`)
- [`docs/technical/regulatory-compliance/OVERVIEW.md`](file:///D:/IT/CD/aurora-server/docs/technical/regulatory-compliance/OVERVIEW.md): Cross-border customs, sanctions screening, and legal RAG overview.
- [`docs/technical/regulatory-compliance/DETAILS.md`](file:///D:/IT/CD/aurora-server/docs/technical/regulatory-compliance/DETAILS.md): PostgreSQL `pgvector` HNSW index, citation verification, and override workflow.
- [`docs/technical/regulatory-compliance/INTERVIEW_QA.md`](file:///D:/IT/CD/aurora-server/docs/technical/regulatory-compliance/INTERVIEW_QA.md): Legal RAG & compliance interview Q&A.

### 3.8 Route Planning & Risk Governance (`route-planning`)
- [`docs/technical/route-planning/OVERVIEW.md`](file:///D:/IT/CD/aurora-server/docs/technical/route-planning/OVERVIEW.md): VROOM/OSRM multi-vehicle routing and risk governance overview.
- [`docs/technical/route-planning/DETAILS.md`](file:///D:/IT/CD/aurora-server/docs/technical/route-planning/DETAILS.md): 4-tier risk governance, tenant risk policy versioning, and fail-closed security.
- [`docs/technical/route-planning/INTERVIEW_QA.md`](file:///D:/IT/CD/aurora-server/docs/technical/route-planning/INTERVIEW_QA.md): Route planning & optimization interview Q&A.

### 3.9 Shipment Workflow State Machine (`shipment`)
- [`docs/technical/shipment/OVERVIEW.md`](file:///D:/IT/CD/aurora-server/docs/technical/shipment/OVERVIEW.md): Core freight lifecycle, finite state machine, and outbox overview.
- [`docs/technical/shipment/DETAILS.md`](file:///D:/IT/CD/aurora-server/docs/technical/shipment/DETAILS.md): FSM state transitions, milestone tracking, and MassTransit outbox.
- [`docs/technical/shipment/INTERVIEW_QA.md`](file:///D:/IT/CD/aurora-server/docs/technical/shipment/INTERVIEW_QA.md): Shipment workflow interview Q&A.

### 3.10 Real-Time GPS Tracking & Geofencing (`gps-tracking`)
- [`docs/technical/gps-tracking/OVERVIEW.md`](file:///D:/IT/CD/aurora-server/docs/technical/gps-tracking/OVERVIEW.md): Fleet telematics, geofencing, and speed alerts overview.
- [`docs/technical/gps-tracking/DETAILS.md`](file:///D:/IT/CD/aurora-server/docs/technical/gps-tracking/DETAILS.md): Haversine distance, ray-casting point-in-polygon algorithm, and Redis hot cache.
- [`docs/technical/gps-tracking/INTERVIEW_QA.md`](file:///D:/IT/CD/aurora-server/docs/technical/gps-tracking/INTERVIEW_QA.md): GPS tracking & spatial algorithms interview Q&A.

### 3.11 Financial Rating & Customs Tax (`financial-tax`)
- [`docs/technical/financial-tax/OVERVIEW.md`](file:///D:/IT/CD/aurora-server/docs/technical/financial-tax/OVERVIEW.md): Freight rating, surcharge matrix, and customs duties overview.
- [`docs/technical/financial-tax/DETAILS.md`](file:///D:/IT/CD/aurora-server/docs/technical/financial-tax/DETAILS.md): Chargeable volumetric weight formulas, CIF tax summation, and Decimal precision.
- [`docs/technical/financial-tax/INTERVIEW_QA.md`](file:///D:/IT/CD/aurora-server/docs/technical/financial-tax/INTERVIEW_QA.md): Financial & tax rating interview Q&A.

### 3.12 Invoicing, Credit & Carrier Settlement (`billing-settlement`)
- [`docs/technical/billing-settlement/OVERVIEW.md`](file:///D:/IT/CD/aurora-server/docs/technical/billing-settlement/OVERVIEW.md): Invoicing (AR), credit limits, and carrier settlements (AP) overview.
- [`docs/technical/billing-settlement/DETAILS.md`](file:///D:/IT/CD/aurora-server/docs/technical/billing-settlement/DETAILS.md): Invoice state machine, customer credit aging, and 3-way invoice matching.
- [`docs/technical/billing-settlement/INTERVIEW_QA.md`](file:///D:/IT/CD/aurora-server/docs/technical/billing-settlement/INTERVIEW_QA.md): Billing & settlement interview Q&A.

### 3.13 Multi-Channel Notification & Alerting (`notification`)
- [`docs/technical/notification/OVERVIEW.md`](file:///D:/IT/CD/aurora-server/docs/technical/notification/OVERVIEW.md): Event-driven multi-channel alerting and preference routing overview.
- [`docs/technical/notification/DETAILS.md`](file:///D:/IT/CD/aurora-server/docs/technical/notification/DETAILS.md): Idempotent event consumer, channel router, and retry pipeline.
- [`docs/technical/notification/INTERVIEW_QA.md`](file:///D:/IT/CD/aurora-server/docs/technical/notification/INTERVIEW_QA.md): Notification service interview Q&A.

---

## 4. Career & Technical Interview Portfolio

- [`docs/technical/CV_HIGHLIGHTS.md`](file:///D:/IT/CD/aurora-server/docs/technical/CV_HIGHLIGHTS.md): Consolidated, recruiter-friendly bullets, deep technical descriptions, code evidence, and interview talking points across Architecture, Distributed Systems, AI Engineering, Security, Backend Engineering, DevOps, Database, and Reliability.
