# Aurora Platform — Engineering Roadmap & Priority Backlog

> **Roadmap Classification**:
> - **P0**: Security, Data Integrity, High-Severity Integration Blockers
> - **P1**: Core Operational Workflows & End-to-End User Experience Completeness
> - **P2**: Performance Optimizations, Advanced Automation & SRE Observability

---

## 1. P0: Security, Data Integrity & Critical Integration

| Task ID | Component | Title & Objective | Description / Fix |
|---|---|---|---|
| **P0-1** | `ShipmentWorkflow` $\longleftrightarrow$ `RegulatoryCompliance` | **Asynchronous Customs Hold Consumer Wiring** | Implement `ShipmentCustomsHoldConsumer` in `RegulatoryCompliance` to automatically trigger legal RAG evaluation upon shipment customs flag. |
| **P0-2** | `IamTenant` / `Shared.Security` | **JWT Token Revocation Blacklist via Redis** | Add instant JWT jti blacklisting in Redis upon user role modification or account suspension to invalidate active browser bearer tokens immediately. |
| **P0-3** | `MailService` / `OutboundPipeline` | **DKIM Alignment Pre-Submission Hard Check** | Add strict outbound validation verifying that the RFC 5322 `From` header domain strictly matches an active DKIM private key before SMTP relay. |
| **P0-4** | `RoutePlanningAgent` | **VROOM Vehicle Capacity Hard Constraint Guard** | Enforce multi-compartment refrigerated/dry cargo constraints in `VroomClient` to prevent cross-contamination during automated stop optimization. |

---

## 2. P1: Complete Operational Workflows & End-to-End Features

| Task ID | Component | Title & Objective | Description / Fix |
|---|---|---|---|
| **P1-1** | `DocumentOcr` | **Mobile Image Skew & Perspective Pre-Processor** | Implement automatic 4-point quadrilateral contour detection and perspective un-skewing to boost OCR accuracy on smartphone photo uploads. |
| **P1-2** | `MailService` $\longleftrightarrow$ `Stalwart` | **Real-Time Sieve Inbound Push Webhook** | Configure Sieve push webhooks in Stalwart to deliver inbound emails to `MailService` in $<100\text{ms}$, replacing periodic JMAP polling. |
| **P1-3** | `BillingService` $\longleftrightarrow$ `FinancialService` | **Forward Exchange Rate Snapshot Locking** | Lock exchange rates at booking time with 30-day validity to eliminate foreign exchange variance on multi-currency carrier settlements. |
| **P1-4** | `CustomerAssistant` | **Real-Time Webhook Notification Tool** | Add a customer assistant tool allowing shippers to register SMS/email delivery event triggers directly from natural language chat. |
| **P1-5** | `GpsTracking` | **Multi-Polygon Dynamic Route Deviation Detection** | Implement dynamic corridor buffering along planned VROOM route polylines to trigger real-time route deviation alerts if vehicles detour $>2\text{km}$. |

---

## 3. P2: Observability, UX & Advanced SRE Optimization

| Task ID | Component | Title & Objective | Description / Fix |
|---|---|---|---|
| **P2-1** | `ai-governance` | **Proactive 80%/90% Token Quota Warning Events** | Emit `TenantAiQuotaThresholdReachedEvent` when Redis consumption counters cross 80% and 90% of allocated monthly budgets. |
| **P2-2** | `MailService` / `Stalwart` | **Automated Annual DKIM Key Rotation Job** | Create a Quartz.NET background job to generate new annual DKIM selector keypairs and manage 30-day dual-signing transitions. |
| **P2-3** | `All Microservices` | **Distributed OpenTelemetry Tracing Dashboard** | Unify Jaeger/Tempo distributed trace visualization across .NET, Java, NestJS, and Rust Stalwart spans. |
| **P2-4** | `Staff.Bff` / `Admin.Bff` | **Optimistic UI State Reconciliation for WebSocket Pushes** | Implement optimistic local state updates with rollback on WebSocket `THREAD_CLAIMED` and `SHIPMENT_UPDATED` events in Staff SPA. |
| **P2-5** | `RegulatoryCompliance` | **Incremental Vector Index Embedding Re-indexing** | Implement scheduled differential vector index optimization for updated customs tariff schedules in `pgvector`. |
