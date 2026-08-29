# Aurora Mail Platform — System Architecture & Data Flow

> **Status**: AUTHORITATIVE / PRODUCTION ARCHITECTURE (CODE-FIRST)  
> **Source-of-Truth**: Audited against `MailService.Domain`, `MailServiceDbContext`, `PipelineRunners.cs`, `InboundStages.cs`, `OutboundStages.cs`, `MailSecurityService.cs`, and `OutboxProcessorBackgroundService.cs`.

---

## 1. System Architecture Diagram

```mermaid
flowchart TD
    subgraph Internet ["External Internet & SMTP Clients"]
        ExtSender[External Sender / Customer]
        ExtRecipient[External Recipient]
    end

    subgraph MiniPC ["Dedicated Production Node (Ubuntu 24.04 LTS / Docker Compose)"]
        Stalwart[Stalwart Mail Server\nPorts: 25, 587, 993, 8080]
        ClamAV[ClamAV Daemon\nPort: 3310]
        SpamAss[SpamAssassin\nPort: 783]
        Redis[Redis 7\nPort: 6379\nRate Limits & Claims]
        RabbitMQ[RabbitMQ 3.13\nPort: 5672\nDomain Events]
        
        subgraph MailServiceApp ["MailService (.NET 10 Microservice)"]
            InboundPipeline[Inbound Pipeline Runner\n12 Security Stages]
            ThreadEngine[Thread Resolution &\nAssignment Engine]
            OutboundPipeline[Outbound Pipeline Runner\n6 Security & Policy Stages]
            GrpcServer[gRPC Server\nMailManagement & MailSecurity]
            OutboxProc[Outbox Processor\nBackground Service]
        end
    end

    subgraph ManagedCloud ["Managed Cloud Infrastructure"]
        NeonDB[(Neon PostgreSQL 16\nMailServiceDbContext)]
        CloudflareR2[(Cloudflare R2 Object Storage\nRaw EML & Attachments)]
        AiGov[Java AiGovernance Service\ngRPC BEC & Phishing Scoring]
    end

    subgraph Clients ["Aurora Micro-BFFs & Realtime Gateway"]
        StaffBFF[Staff.Bff\n/api/v1/mail\n/api/v1/negotiations]
        AdminBFF[Admin.Bff\n/api/v1/admin/mail]
        SystemBFF[System.Bff\n/api/v1/system/mail]
        RealtimeHub[RealtimeHub / Socket.IO\nReal-time Notifications]
    end

    ExtSender -->|SMTP Port 25| Stalwart
    Stalwart -->|Inbound Webhook / JMAP| InboundPipeline
    InboundPipeline --> ClamAV & SpamAss & AiGov
    InboundPipeline -->|Persist Message & Raw EML| NeonDB & CloudflareR2
    InboundPipeline --> ThreadEngine
    ThreadEngine --> NeonDB
    InboundPipeline -->|Outbox Pattern| NeonDB
    OutboxProc -->|Poll Outbox| NeonDB
    OutboxProc -->|Publish EmailReceived| RabbitMQ
    RabbitMQ --> RealtimeHub
    RealtimeHub -->|Push THREAD_CLAIMED / EMAIL_RECEIVED| StaffBFF

    StaffBFF & AdminBFF & SystemBFF -->|gRPC Port 5003| GrpcServer
    GrpcServer --> ThreadEngine & OutboundPipeline
    OutboundPipeline --> ClamAV & Redis & NeonDB
    OutboundPipeline -->|SMTP Submission Port 587| Stalwart
    Stalwart -->|Internet SMTP| ExtRecipient
```

---

## 2. Inbound Pipeline & Thread Resolution Deep-Dive

When an inbound email arrives via SMTP, `Stalwart` triggers the inbound ingestion mechanism in `MailService`. The `InboundPipelineRunner` executes 12 ordered, deterministic stages:

### 2.1 The 12 Inbound Pipeline Stages

```
 0. TLS Verification          ──> Enforces TLS 1.2+ encryption on inbound connection.
 1. Header Parsing             ──> Extracts RFC 5322 headers (From, To, Cc, Subject, Message-ID, In-Reply-To, References).
 2. Recipient Validation       ──> Validates format and checks local parts against tenant alias/mailbox rules.
 3. SPF Validation             ──> Evaluates sender IP against domain SPF TXT records (Pass / SoftFail / Fail).
 4. DKIM Validation            ──> Cryptographically validates cryptographic signature headers against DNS public keys.
 5. DMARC Evaluation           ──> Validates alignment between SPF/DKIM and From header policy.
 6. Tenant & Domain Validation ──> Resolves TenantId by matching recipient domain against active registered domains.
 7. Attachment & Virus Scan    ──> Extracts MIME attachments and streams to ClamAV Daemon (Port 3310).
 8. SpamAssassin Scoring       ──> Calculates heuristic spam score (Port 783). Compares against SpamTag / SpamReject thresholds.
 9. AI BEC & Phishing Scoring  ──> Calls AiGovernance gRPC for zero-day phishing, executive impersonation, and BEC detection.
10. Header Forgery Analysis    ──> Analyzes Received header paths for anomalous intermediate relay hops.
11. Email Classification       ──> Classifies intent into BookingRequest, Quotation, ShipmentUpdate, Complaint, or Unknown.
```

### 2.2 Thread Resolution Algorithm
After passing security validation, `MailService` resolves the conversation thread:

```mermaid
flowchart TD
    Start[Parsed Inbound Message] --> CheckHeaders{Has In-Reply-To or References Header?}
    
    CheckHeaders -->|Yes| FindParent[Search ProcessedMessages by Message-ID]
    FindParent --> FoundParent{Parent Message Found with ThreadId?}
    FoundParent -->|Yes| AttachExisting[Attach to Existing EmailThread]
    
    FoundParent -->|No| CheckSubject{Search by Normalized Subject\nand Shared MailboxId}
    CheckHeaders -->|No| CheckSubject
    
    CheckSubject --> SubjectMatch{Found Active Thread\nwithin 30 Days?}
    SubjectMatch -->|Yes| AttachExisting
    SubjectMatch -->|No| CreateNew[Create New EmailThread\nStatus = Unassigned\nPrimaryAssigneeUserId = null]
    
    AttachExisting --> UpdateThread[Increment MessageCount\nUpdate LastMessageAt\nUpdate Participants List\nSet HasUnread = true]
    CreateNew --> UpdateThread
    UpdateThread --> OutboxWrite[Write EmailReceivedEvent to Outbox]
```

---

## 3. Outbound Pipeline & Delivery Deep-Dive

When an authorized user clicks **Send** in the frontend (`POST /api/v1/mail/messages/outbound`), the `SubmitOutboundMessageCommandHandler` coordinates draft revision locking, thread assignment, and the outbound pipeline:

### 3.1 Assignment & Human Actor Capturing
1. **Authenticated Context**: Resolves `SentByUserId = CurrentUser.UserId` and `TenantId = CurrentUser.TenantId`.
2. **Reply-to-Claim Guard**:
   - If the thread is `Unassigned`, the system **atomically claims** the thread for `SentByUserId`, updates `Status = InProgress`, and appends an entry to `ThreadAssignmentHistory`.
   - If the thread is assigned to another staff member and the caller lacks supervisory authority (`mail:thread:reassign`), the command **aborts fail-closed** with `THREAD_ASSIGNED_TO_ANOTHER_STAFF`.

### 3.2 Outbound Pipeline Stages
```
12. Outbound Attachment Check  ──> Scans outgoing files with ClamAV to prevent accidental malware distribution.
13. Policy & Sensitive Data    ──> Verifies data loss prevention (DLP) rules and tenant outbound policies.
14. AI Risk & Prompt-Injection ──> Detects prompt-injection remnants or anomalous financial account alterations.
15. Tenant Rate Limit Check    ──> Evaluates Redis sliding-window counter against Domain.OutboundRateLimitPerHour.
16. Immutable Audit Record     ──> Generates AuditRecord logging ActorId, recipient list, and message hash.
17. Stalwart SMTP Submission   ──> Submits authenticated payload via SMTP to Stalwart (Port 587/25).
```

### 3.3 Post-Delivery Revision State
- Upon **SMTP 2xx Acceptance**: The associated `EmailDraft` is permanently marked as `Sent` (`DraftStatus.Sent`), and `ProcessedMessage` (Direction = `Outbound`) is committed.
- Upon **SMTP Rejection**: The draft remains in `Draft` status, allowing the user to correct errors and retry without data loss.

---

## 4. Transactional Outbox Pattern & Event Schema

To maintain consistency between the database and message broker without distributed 2PC transactions, `MailService` writes domain events to table `OutboxMessages` in the same local ACID transaction. The `OutboxProcessorBackgroundService` polls and publishes events to `RabbitMQ`:

```
MailService DbContext Transaction
  ├── INSERT ProcessedMessage
  ├── UPDATE EmailThread
  └── INSERT OutboxMessage (EventType: "EmailReceivedEvent", Payload: JSON)
         │
         ▼ (Commit OK)
OutboxProcessorBackgroundService (Every 5s)
  ├── SELECT OutboxMessages WHERE ProcessedAt IS NULL ORDER BY CreatedAt ASC
  ├── Publish to RabbitMQ Exchange: "aurora.mail"
  └── UPDATE OutboxMessages SET ProcessedAt = UtcNow()
```

### Supported Domain Events

| Event Name | Routing Key | Payload Summary | Consumer / Effect |
|---|---|---|---|
| `EmailReceivedEvent` | `mail.received` | `{ messageId, threadId, tenantId, mailboxId, sender, subject, category }` | RealtimeHub pushes inbox notification; AI Negotiation triage triggered. |
| `EmailQuarantinedEvent` | `mail.quarantined`| `{ messageId, tenantId, sender, reason, spamScore, phishingScore }` | Notifies security managers via RealtimeHub / WebSocket. |
| `EmailSentEvent` | `mail.sent` | `{ messageId, threadId, tenantId, sender, recipients, sentByUserId }` | RealtimeHub updates thread timeline for connected staff. |
| `ThreadClaimedEvent` | `mail.thread.claimed` | `{ threadId, tenantId, assignedUserId, assignedStaffName }` | Live-locks thread in other staff browsers to avoid duplicate work. |
| `ThreadReassignedEvent` | `mail.thread.reassigned`| `{ threadId, tenantId, previousAssigneeId, newAssigneeId }` | Re-routes thread in staff work queues. |

---

## 5. Persistence & Data Schema

The relational data model in `MailServiceDbContext` is structured as follows:

```mermaid
erDiagram
    Domain ||--o{ Mailbox : "owns"
    Domain ||--o{ Alias : "defines"
    Mailbox ||--o{ EmailThread : "receives"
    EmailThread ||--o{ ProcessedMessage : "contains"
    EmailThread ||--o{ EmailDraft : "holds"
    EmailThread ||--o{ ThreadAssignmentHistory : "tracks"
    ProcessedMessage ||--o{ SecurityCheckResult : "details"
    ProcessedMessage ||--o| QuarantineRecord : "triggers"
    EmailDraft ||--o| EmailDraft : "revises (parent)"

    Domain {
        Guid Id PK
        Guid TenantId
        string DomainName
        string DkimSelector
        string DkimTxtRecord
        decimal SpamRejectThreshold
        int OutboundRateLimitPerHour
    }

    Mailbox {
        Guid Id PK
        Guid TenantId
        Guid DomainId FK
        string LocalPart
        string FullAddress
        MailboxStatus Status
        Guid UserId
    }

    EmailThread {
        Guid Id PK
        Guid TenantId
        Guid MailboxId FK
        string Subject
        Guid PrimaryAssigneeUserId
        DateTimeOffset AssignedAt
        ThreadStatus Status
        ThreadPriority Priority
        uint Version
        int MessageCount
        int DraftCount
    }

    ProcessedMessage {
        Guid Id PK
        Guid TenantId
        Guid ThreadId FK
        string MessageId
        EmailDirection Direction
        string SenderAddress
        string Subject
        string R2RawEmlPath
        Guid SentByUserId
        PipelineStatus PipelineStatus
        decimal SpamScore
        decimal PhishingScore
    }

    EmailDraft {
        Guid Id PK
        Guid TenantId
        Guid ThreadId FK
        Guid DraftRootId
        int RevisionNumber
        bool IsLatestRevision
        DraftSource Source
        DraftStatus Status
        string Subject
        string Body
        string ContentHash
        string IdempotencyKey
    }

    ThreadAssignmentHistory {
        Guid Id PK
        Guid TenantId
        Guid ThreadId FK
        Guid FromUserId
        Guid ToUserId
        ThreadAssignmentAction Action
        Guid ActorUserId
        string Reason
    }
```
