# Aurora Mail Platform — Deep Technical Details

> **Service Layer**: Architecture, Pipeline Stages, Stalwart Integration & Concurrency  
> **Source-of-Truth**: `src/dotnet/MailService`, `InboundStages.cs`, `OutboundStages.cs`, `ClaimThreadCommandHandler.cs`, `SubmitOutboundMessageCommandHandler.cs`, `OutboxProcessorBackgroundService.cs`.

---

## 1. Architectural Patterns & Domain Model

`MailService` is implemented as an event-driven .NET 10 microservice using Clean Architecture, CQRS, and the Pipeline Pattern.

```
                  ┌──────────────────────────────┐
                  │    MailGrpcService (gRPC)    │
                  └──────────────┬───────────────┘
                                 │ MediatR
            ┌────────────────────┴────────────────────┐
            ▼                                         ▼
┌─────────────────────────┐               ┌─────────────────────────┐
│   Inbound Execution     │               │   Outbound Execution    │
│ - InboundPipelineRunner │               │ - SubmitOutboundCommand │
│ - 12 Security Stages    │               │ - 6 Outbound Stages     │
│ - Thread Aggregator     │               │ - Reply-to-Claim Guard  │
└───────────┬─────────────┘               └───────────┬─────────────┘
            │                                         │
            ▼                                         ▼
┌───────────────────────────────────────────────────────────────────┐
│                     PostgreSQL Relational Schema                  │
│ Domain ──> Mailbox ──> EmailThread ──> ProcessedMessage / Draft   │
│ EmailThread ──> ThreadAssignmentHistory                           │
│ ProcessedMessage ──> SecurityCheckResult / QuarantineRecord       │
└───────────────────────────────────────────────────────────────────┘
```

---

## 2. Shared Mailbox & Thread Responsibility Deep-Dive

### 2.1 The Shared Mailbox Paradigm
- Customer service and freight forwarding operate on shared department queues (`ops@acmelogistics.com`, `pricing@acmelogistics.com`).
- Normal staff members do not have individual personal mailboxes; they interact with threads aggregated under shared mailboxes.

### 2.2 Atomic Thread Claiming & Concurrency Token
```csharp
// ClaimThreadCommandHandler.cs
var thread = await _dbContext.EmailThreads
    .FirstOrDefaultAsync(t => t.Id == request.ThreadId && t.TenantId == tenantId, cancellationToken);

if (thread.PrimaryAssigneeUserId.HasValue && thread.PrimaryAssigneeUserId.Value != currentUserId)
{
    throw new ConcurrencyException("THREAD_ALREADY_ASSIGNED");
}

thread.PrimaryAssigneeUserId = currentUserId;
thread.AssignedAt = DateTimeOffset.UtcNow;
thread.Status = ThreadStatus.InProgress;
thread.Version++; // Increments optimistic concurrency token

_dbContext.ThreadAssignmentHistories.Add(new ThreadAssignmentHistory {
    ThreadId = thread.Id,
    TenantId = tenantId,
    Action = ThreadAssignmentAction.Claimed,
    ActorUserId = currentUserId,
    ToUserId = currentUserId,
    Reason = "Explicit user claim"
});

await _dbContext.SaveChangesAsync(cancellationToken);
```

### 2.3 Implicit Reply-to-Claim
When a staff member replies to an unassigned thread (`POST /api/v1/mail/messages/outbound`):
- `SubmitOutboundMessageCommandHandler` detects `thread.PrimaryAssigneeUserId == null`.
- Automatically executes an atomic claim in the database, moves status to `InProgress`, and records history.
- If the thread is assigned to another staff member and the caller lacks `mail:thread:reassign`, it aborts fail-closed with `THREAD_ASSIGNED_TO_ANOTHER_STAFF`.

---

## 3. Two-Way Defense-in-Depth Security Pipeline

### 3.1 Inbound Pipeline (12 Stages)
1. `TlsVerification`: Enforces TLS 1.2+ on inbound connection.
2. `HeaderParsing`: RFC 5322 extraction and normalized subject threading.
3. `RecipientValidation`: Validates recipient against tenant domains/aliases.
4. `SpfValidation`: Validates sender IP against domain SPF TXT records.
5. `DkimValidation`: Cryptographic signature verification using DNS public keys.
6. `DmarcEvaluation`: DMARC alignment and policy evaluation (`p=none/quarantine/reject`).
7. `TenantValidation`: Resolves tenant ID and security thresholds.
8. `AttachmentValidation`: Streams attachments to ClamAV Daemon (`tcp://clamav:3310`).
9. `SpamScoring`: Streams to Apache SpamAssassin (`tcp://spamassassin:783`).
10. `AiPhishingDetection`: Calls `AiGovernance` gRPC for BEC and executive impersonation scoring.
11. `HeaderForgeryAnalysis`: Detects forged hops in Received path headers.
12. `Classification`: Classifies intent into Booking, Quotation, Issue, or Unknown.

### 3.2 Outbound Pipeline (6 Stages)
1. `OutboundAttachmentValidation`: ClamAV scanning on outbound files.
2. `PolicyValidation`: DLP regexes for sensitive numbers and unallowed forwarding.
3. `AiRiskCheck`: Prompt injection defense and financial modification check.
4. `RateLimitValidation`: Sliding window rate limiting in Redis (`ratelimit:outbound:{tenantId}`).
5. `AuditRecordCreation`: Immutable audit entry of outgoing message and hash.
6. `SmtpSubmission`: Authenticated submission to Stalwart (Port 587/25) with DKIM signing.

---

## 4. Stalwart Integration & Deployment Architecture

- **Server**: Self-hosted Stalwart All-in-One Mail Server (`stalwartlabs/mail-server:v0.10.8`) running on Ubuntu 24.04 LTS Mini PC.
- **Port Security**: Public ports 25, 587, 993 open to internet; Admin REST port 8080 bound strictly to `127.0.0.1`.
- **DKIM Generation**: Generated dynamically on Stalwart during domain provisioning (`aurora-2025` RSA-2048).
- **Outbox Pattern**: Outbox messages committed in the same database transaction and published to RabbitMQ (`EmailReceivedEvent`, `EmailSentEvent`, `ThreadClaimedEvent`).
