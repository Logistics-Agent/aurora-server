# Aurora Mail Platform — Overview & Architectural Principles

> **Status**: AUTHORITATIVE / PRODUCTION ARCHITECTURE (CODE-FIRST)  
> **Source-of-Truth**: Audited directly against `MailService`, `BuildingBlocks.BFF.Mail`, `Staff.Bff.Controllers.MailController`, `Admin.Bff.Controllers.MailAdminController`, `System.Bff.Controllers.MailSystemController`, `NegotiationsController`, `mail_platform.proto`, and deployment assets.

---

## 1. Executive Summary

The **Aurora Mail Platform** is a multi-tenant enterprise email communication and security platform tailored for modern freight forwarding and logistics workflows. It bridges standard internet email protocols (SMTP, IMAP, JMAP via Stalwart) with Aurora's event-driven microservices architecture, automated threat detection pipelines, and AI-assisted negotiation workflows.

Unlike legacy monolithic email systems where every user connects to a personal mailbox client, Aurora operates on a **Shared Mailbox & Thread Triage Model**:
- External emails arrive at tenant shared mailboxes (e.g., `ops@acmelogistics.com`, `quotes@acmelogistics.com`).
- Emails are automatically parsed, sanitized, grouped into conversation **`EmailThread`** aggregates, and queued in triage folders.
- Operational Staff claim threads into **`MY_WORK`**, draft responses, or convert negotiation proposals into drafts.
- Supervisors and Managers maintain complete visibility across **`ALL`** threads, with capability to reassign or rebalance workloads.
- All outbound messages pass strict security inspection (ClamAV, Prompt-Injection/BEC checks, Rate Limits) before SMTP relay.

```
[ External Internet ] (SMTP: Port 25 / 587)
         │
         ▼
┌─────────────────────────────────────────────────────────────┐
│                Stalwart All-in-One Mail Server              │
└──────────────────────────────┬──────────────────────────────┘
                               │ Inbound Webhook / JMAP
                               ▼
┌─────────────────────────────────────────────────────────────┐
│                 Aurora MailService (.NET 10)                │
│  ├── Inbound Security Pipeline (12 Stages)                  │
│  ├── Shared Mailbox & Thread Aggregator                     │
│  ├── Responsibility & Assignment Engine (Claim / Reassign)  │
│  ├── Human-in-the-Loop Negotiation Linkage                  │
│  ├── Outbound Security Pipeline & Rate Limiting             │
│  └── Transactional Outbox & Event Publisher                 │
└──────────────┬───────────────────────────────┬──────────────┘
               │ gRPC / Events                 │ SMTP Relay
               ▼                               ▼
┌──────────────────────────────┐ ┌────────────────────────────┐
│      Aurora Micro-BFFs       │ │    Internet Recipients     │
│ (Staff, Admin, System, Realtime)│ └────────────────────────────┘
└──────────────────────────────┘
```

---

## 2. Core Architectural Invariants

### Invariant 1: MVP Uses Shared Mailboxes (No Personal Mailbox for Normal Staff)
- Logistics customer service and operations function in team queues. Individual staff members **do not** have dedicated personal SMTP/IMAP inboxes.
- Emails belong to tenant-level Shared Mailboxes (e.g., `sea-freight@acme.com`, `air-ops@acme.com`).
- `Mailbox.UserId` is optional and reserved for automated routing or admin ownership; normal operational triage operates over shared mailboxes.

### Invariant 2: `EmailThread` is the Operational Conversation Unit
- All inbound and outbound messages (`ProcessedMessage`) and drafts (`EmailDraft`) are grouped under an `EmailThread`.
- Thread aggregation uses standard RFC 5322 headers (`In-Reply-To`, `References`) and normalized Subject threading.
- Staff interact with threads, not disconnected isolated messages.

### Invariant 3: `PrimaryAssigneeUserId` Owns Operational Responsibility
- A thread has at most **one** `PrimaryAssigneeUserId` at any time.
- Ownership is acquired via **Atomic Claim** (`POST /api/v1/mail/threads/{id}/claim`) or **Implicit Reply-to-Claim**.
- Staff can only send replies on threads they own (or unassigned threads which are claimed on reply).
- Supervisory managers with `mail:thread:reassign` can reassign threads between team members or unassign them back to the pool.

### Invariant 4: Queue Scopes & Granular Triage
- **`UNASSIGNED` Scope**: Threads with `PrimaryAssigneeUserId == null`. Visible to all operational staff for discovery and claim.
- **`MY_WORK` Scope**: Threads where `PrimaryAssigneeUserId == CurrentUser.UserId`. Dedicated workspace for assigned staff.
- **`ALL` Scope**: Tenant-wide view of all employee assignments. Strictly requires supervisory permission `mail:thread:read_all`.

### Invariant 5: Human-in-the-Loop AI (Zero Autonomous Outbound Mail)
- AI Agents (e.g., Negotiation Agent, Copilot Assistant) **CANNOT DIRECTLY SEND OUTBOUND MAIL**.
- AI produces structured suggestions (`SuggestedReplyDto`).
- An authorized staff member must explicitly review the suggestion and click **[Create Mail Draft]** (`POST /api/v1/negotiations/{id}/mail-draft`), review/edit the draft, and trigger outbound sending.

### Invariant 6: Authenticated `SentByUserId` Tracking
- Every outbound email captures the authenticated human user (`SentByUserId`) from the current JWT session context, establishing immutable auditability.

### Invariant 7: Pure Capability-Based Authorization
- Access control is governed strictly by granular permissions in `PermissionConstants.Mail` (e.g., `mail:read`, `mail:send`, `mail:thread:claim`, `mail:thread:reassign`, `mail:quarantine:release`).
- `StaffType` (Operations, Finance, CS) does **not** exist in authorization. Role (`STAFF`, `MANAGER`, `TENANT_ADMIN`) defines layout shell and persona; direct permissions define actual authority.

---

## 3. High-Level Lifecycle Flows

### 3.1 Inbound Email Lifecycle
```
External Sender
  │ (Internet SMTP Port 25)
  ▼
Stalwart Mail Server
  │ (Inbound Webhook / JMAP Fetch)
  ▼
MailService Inbound Pipeline Runner
  ├── Stage 0: TLS Verification
  ├── Stage 1: Header Parsing (RFC 5322)
  ├── Stage 2: Recipient Validation
  ├── Stage 3-5: SPF, DKIM, DMARC Authentication
  ├── Stage 6: Tenant & Domain Resolution
  ├── Stage 7: Attachment Validation & ClamAV Antivirus Scan
  ├── Stage 8: SpamAssassin Scoring
  ├── Stage 9: AI BEC & Phishing Detection (AiGovernance)
  ├── Stage 10: Header Forgery Analysis
  └── Stage 11: Email Category Classification
  │
  ├── [If Threat Detected] ──> Quarantined (QuarantineRecord created)
  │                             └── Awaits manager review / release
  │
  └── [If Clean] ──> ProcessedMessage persisted + Raw EML to Cloudflare R2
                      │
                      ├── Thread Resolution (Match existing or create new EmailThread)
                      ├── Transactional Outbox: EmailReceivedEvent published to RabbitMQ
                      └── RealtimeHub pushes WebSocket event to Staff UI
```

### 3.2 Outbound Email Lifecycle
```
Staff / Manager in Staff SPA
  │
  ├── 1. Create / Edit Draft (POST /api/v1/mail/drafts or via Negotiation flow)
  │      └── EmailDraft persisted with immutable content hash & revision number
  │
  └── 2. Submit Outbound Email (POST /api/v1/mail/messages/outbound)
         │
         ├── Thread Assignment Check (Claim unassigned or verify PrimaryAssignee)
         ├── Captured SentByUserId = CurrentUser.UserId
         │
         ▼
Outbound Pipeline Runner
  ├── Stage 12: Outbound Attachment Validation & ClamAV Scan
  ├── Stage 13: Policy & Sensitive Data Validation
  ├── Stage 14: AI Risk & Prompt-Injection BEC Check
  ├── Stage 15: Tenant Rate Limit Verification (Redis Sliding Window)
  ├── Stage 16: Immutable Audit Record Creation
  └── Stage 17: Stalwart Authenticated SMTP Submission (Port 587 / 25)
         │
         ├── [SMTP 2xx OK] ──> Draft marked as SENT
         │                     └── ProcessedMessage recorded (Direction = Outbound)
         │                     └── Outbox: EmailSentEvent published
         │
         └── [SMTP Error / Rejected] ──> Draft remains in DRAFT status
                                         └── Error returned to Frontend
```

---

## 4. Platform Component Breakdown

| Component | Technology | Role & Responsibility | Hosting / Topology |
|---|---|---|---|
| **Stalwart Server** | Rust (`stalwartlabs/mail-server`) | Inbound SMTP (25), Submission (587), IMAPS (993), JMAP/REST API (8080). | Mini PC Container |
| **MailService** | .NET 10 (C#) | Inbound/Outbound pipelines, Threading & Assignment, Drafts, Quarantine, Audit, gRPC & Outbox. | Mini PC Container (Port 5003/9090) |
| **PostgreSQL** | PostgreSQL 16 (Neon Serverless) | Relational persistence (`Domains`, `Mailboxes`, `EmailThreads`, `EmailDrafts`, `ProcessedMessages`, `AuditRecords`). | Managed Cloud (SSL Required) |
| **Redis** | Redis 7 Alpine | Thread claim distributed locks, Outbound sliding-window rate limiting, Permission caching. | Mini PC Container (Port 6379) |
| **RabbitMQ** | RabbitMQ 3.13 Management | Asynchronous event distribution (`EMAIL_RECEIVED`, `EMAIL_QUARANTINED`, `EMAIL_SENT`, `MAILBOX_PROVISIONED`). | Mini PC Container (Port 5672/15672) |
| **ClamAV** | ClamAV Daemon (`clamav/clamav`) | Antivirus and malicious payload scanning for all inbound and outbound attachments. | Mini PC Container (Port 3310) |
| **SpamAssassin** | Apache SpamAssassin | Heuristic spam scoring and Bayesian filtering for inbound emails. | Mini PC Container (Port 783) |
| **Cloudflare R2** | S3-Compatible Object Storage | Durable, tamper-evident storage for raw `.eml` files and email attachments. | Managed Cloudflare Storage |
| **AiGovernance** | Java / gRPC | LLM capability routing, BEC detection, phishing classification, and prompt injection defense. | Central Service (gRPC) |
| **BFF Layer** | .NET 10 Micro-BFFs | `Staff.Bff`, `Admin.Bff`, `System.Bff` expose HTTP REST endpoints to frontend clients. | API Gateway Cluster |
