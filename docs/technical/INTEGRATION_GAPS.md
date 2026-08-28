# Aurora Platform — Verified Integration Gaps & Architectural Technical Debt

> **Audit Scope**: Real, code-proven gaps across microservice boundaries, event publishers/consumers, gRPC handlers, and deployment assets.

---

## 1. Summary of Identified Integration Gaps

| # | Gap Title | Affected Services | Severity | Category | Status |
|---|---|---|:---:|---|---|
| **GAP-01** | Missing Direct Outbox Consumer for `CustomsHoldEvent` | `ShipmentWorkflow` $\rightarrow$ `RegulatoryCompliance` | **HIGH** | Event Ingestion | Unwired consumer |
| **GAP-02** | Automatic Email Ingestion Webhook vs JMAP Polling | `MailService` $\longleftrightarrow$ `Stalwart` | **MEDIUM** | Inbound Transport | Polling fallback |
| **GAP-03** | Automated OCR Pre-processing for Mobile Camera Warping | `DocumentOcr` | **MEDIUM** | Pipeline Enhancement | Perspective correction needed |
| **GAP-04** | Redis Single-Node Topology for Rate Limiting & Claims | `MailService`, `GpsTracking`, `IamTenant` | **MEDIUM** | High Availability | Cluster Sentinel needed |
| **GAP-05** | Realtime Token Usage Webhook Alerting | `ai-governance` $\rightarrow$ `Notification` | **LOW** | Observability | 90% quota alert event |
| **GAP-06** | Carrier Settlement Multi-Currency Auto-Hedging | `billing-settlement`, `financial-tax` | **LOW** | Fintech Feature | Spot exchange fallback |
| **GAP-07** | Automated DKIM Key Rotation Cron Job | `MailService` $\longleftrightarrow$ `Stalwart` | **LOW** | Security Maintenance | Manual CLI rotation |

---

## 2. Detailed Gap Analysis & Actionable Fixes

### GAP-01: Asynchronous `CustomsHoldEvent` Consumer Linkage
- **Severity**: **HIGH**
- **Affected Services**: `ShipmentWorkflow` (.NET 10), `RegulatoryCompliance` (.NET 10)
- **Evidence**: `ShipmentWorkflow` emits `CustomsHoldEvent` when a customs inspection flag is raised, but `RegulatoryCompliance` currently relies on synchronous gRPC evaluation calls rather than an automated MassTransit background consumer.
- **Recommended Fix**: Add `ShipmentCustomsHoldConsumer` in `RegulatoryCompliance.Application.Consumers` to automatically execute a RAG compliance re-evaluation and attach updated statutory legal citations directly to the shipment ticket.

---

### GAP-02: Real-time Inbound Email Push Webhook vs JMAP Poller
- **Severity**: **MEDIUM**
- **Affected Services**: `MailService`, `Stalwart`
- **Evidence**: Stalwart delivers inbound mail to local mailboxes. `MailService` polls JMAP accounts on a short interval. While functional for MVP, high-volume operations benefit from immediate HTTP push webhooks.
- **Recommended Fix**: Configure Stalwart Sieve webhook script (`redirect :copy "http://mail-service:5003/api/v1/internal/mail/inbound-webhook"`) in `/opt/stalwart/config.toml` to eliminate polling latency.

---

### GAP-03: Document OCR Perspective & Skew Correction for Mobile Uploads
- **Severity**: **MEDIUM**
- **Affected Services**: `DocumentOcr` (.NET 10)
- **Evidence**: `DocumentOcr` handles clean digital PDFs and scanned tiffs with high accuracy ($>95\%$). Mobile camera photos with trapezoidal perspective skew can drop confidence below the 0.85 threshold, forcing human review.
- **Recommended Fix**: Integrate OpenCV / SkiaSharp 4-point perspective warp and un-skew pre-processing filter prior to running multi-provider OCR extraction.

---

### GAP-04: Redis High Availability Sentinel / Cluster for Production
- **Severity**: **MEDIUM**
- **Affected Services**: `MailService`, `IamTenant`, `GpsTracking`, `ai-governance`
- **Evidence**: `docker-compose.prod.yml` runs a standalone Redis 7 container. While adequate for Mini PC deployments, a node crash would temporarily disable rate limiters and claim locks until Docker restarts the container.
- **Recommended Fix**: Configure Redis Sentinel with primary-replica replication or managed Redis cluster for multi-node production enterprise tiers.

---

### GAP-05: Real-time 90% AI Token Quota Warning Event
- **Severity**: **LOW**
- **Affected Services**: `ai-governance` (Java 21), `Notification` (.NET 10)
- **Evidence**: `TokenQuotaManager.java` rejects requests at 100% monthly quota with `RESOURCE_EXHAUSTED`. An early warning notification at 80% and 90% is currently logged but not published as an outbox event.
- **Recommended Fix**: Emit `TenantAiQuotaThresholdReachedEvent` when Redis token counter exceeds 85% and 95% of monthly allocation to notify tenant administrators via email.

---

### GAP-06: Carrier Invoice Auto-Hedging & Forward Contract Allocation
- **Severity**: **LOW**
- **Affected Services**: `billing-settlement` (NestJS), `financial-service` (NestJS)
- **Evidence**: `BillingService` reconciles carrier freight invoices using spot exchange rates on the date of billing. For long ocean voyages (45+ days), currency drift between booking date and billing date requires manual accounting adjustment.
- **Recommended Fix**: Implement forward contract exchange rate locking in `FinancialService` to fix the USD/VND rate at time of booking confirmation.

---

### GAP-07: Automated Annual DKIM Selector Rotation
- **Severity**: **LOW**
- **Affected Services**: `MailService`, `Stalwart`
- **Evidence**: DKIM selector `aurora-2025` is generated during domain provisioning. Rotation to a new year selector (e.g. `aurora-2026`) is executed manually via CLI.
- **Recommended Fix**: Implement a background Quartz.NET scheduled job to automatically generate new DKIM keypairs in Stalwart, publish DNS alert notices, and rotate selectors after 30-day dual-signing periods.
