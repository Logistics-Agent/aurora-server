# Aurora Mail Platform — Interview Q&A Guide

> **Target Audience**: Junior, Mid-level, Senior & System Design Interviewers  
> **Source-of-Truth**: Grounded 100% in Aurora `MailService` implementation.

---

### Q1 (Junior): Why did Aurora adopt a Shared Mailbox & Thread model instead of personal user mailboxes?
**Answer**:  
Logistics operations are team-based: customer quotation requests and booking updates arrive at shared department addresses (`ops@acmelogistics.com`). If individual employees had separate mailboxes, customer inquiries would be duplicated or lost when an employee is off-shift. In Aurora, emails arrive at shared mailboxes, are grouped into `EmailThread` conversation units, and are claimed into staff queues.

---

### Q2 (Mid): How is concurrent claiming of the same email thread prevented?
**Answer**:  
Concurrent claims are prevented using **Optimistic Concurrency Control**:
1. `EmailThread` contains an integer `Version` column.
2. `ClaimThreadCommandHandler` checks `if (thread.PrimaryAssigneeUserId != null && thread.PrimaryAssigneeUserId != currentUserId)` and throws `ConcurrencyException("THREAD_ALREADY_ASSIGNED")` (`409 Conflict`).
3. Upon updating `PrimaryAssigneeUserId = currentUserId`, `thread.Version++` is committed atomically with an entry in `ThreadAssignmentHistory`.
4. RealtimeHub broadcasts WebSocket event `THREAD_CLAIMED` to instantly lock or remove the thread from all other active staff browser screens.

---

### Q3 (Mid): What is "Reply-to-Claim" and why is it implemented?
**Answer**:  
In fast-paced operations, staff often open an `UNASSIGNED` thread and immediately type and send a reply without clicking a separate "Claim" button first. `SubmitOutboundMessageCommandHandler` detects that `thread.PrimaryAssigneeUserId` is null, automatically assigns ownership to `CurrentUser.UserId`, sets status to `InProgress`, and commits the assignment before proceeding through the outbound pipeline.

---

### Q4 (Senior): How does Aurora prevent AI agents from sending unauthorized or rogue emails?
**Answer**:  
Aurora enforces a strict **Human-in-the-Loop Architectural Guardrail**:
1. AI agents (such as the Negotiation Agent) **cannot** invoke outbound SMTP or draft sending APIs.
2. AI agents produce structured suggestions (`SuggestedReplyDto`).
3. An authorized human staff member must explicitly click `[Create Mail Draft]` (`POST /api/v1/negotiations/{id}/mail-draft`), which fetches the validated suggestion and creates an `EmailDraft`.
4. The human staff member reviews and edits the draft in the rich text editor and must explicitly click `[Send]`. Every outbound email logs the human `SentByUserId`.

---

### Q5 (Senior / System Design): How does the 12-stage Inbound Security Pipeline protect against zero-day phishing and malware?
**Answer**:  
1. **SPF/DKIM/DMARC**: Cryptographically validates sender domain alignment against DNS records.
2. **ClamAV Antivirus Daemon**: Streams attachments over TCP port 3310 to scan for malicious payloads; infected emails immediately short-circuit to quarantine.
3. **Apache SpamAssassin**: Heuristically scores spam indicators on port 783.
4. **Central AI Governance gRPC**: Evaluates executive display name impersonation, wire transfer instruction modifications, and credential harvesting patterns.
5. **Quarantine Isolation**: Any message exceeding safety thresholds is stored in Cloudflare R2 and flagged in `QuarantineRecord`, preventing it from reaching user inboxes until explicitly released by a security manager.

---

### Q6 (System Design): What are the tradeoffs of self-hosting Stalwart on a Mini PC vs. using SendGrid or Amazon SES?
**Answer**:  
- **Pros**: Complete data sovereignty for enterprise logistics clients, zero per-email SaaS fees, native JMAP/IMAP support for shared mailboxes, and full control over DKIM key rotation and TLS policies.
- **Cons**: Requires operational ownership of IP reputation, reverse DNS (PTR) management, automated backup/restore scripts (`backup.sh`), and deliverability monitoring (`verify-dns-deliverability.sh`).
