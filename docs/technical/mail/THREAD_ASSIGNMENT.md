# Aurora Mail Platform — Threading & Assignment Model

> **Status**: AUTHORITATIVE / PRODUCTION ARCHITECTURE (CODE-FIRST)  
> **Source-of-Truth**: Audited against `EmailThread`, `ThreadAssignmentHistory`, `ClaimThreadCommandHandler`, `ReassignThreadCommandHandler`, `UnassignThreadCommandHandler`, `SubmitOutboundMessageCommandHandler`, and `MailController.cs`.

---

## 1. Executive Summary & Why Shared Mailbox Triage

Logistics operations are fundamentally collaborative: freight quotes, booking confirmations, customs queries, and delay alerts arrive at shared department mailboxes (`ops@`, `pricing@`, `customs@`).

In Aurora:
- **No Individual Mailboxes**: Staff members do not manage separate IMAP inboxes.
- **`EmailThread` is the Core Entity**: All incoming and outgoing messages are grouped into conversation threads.
- **Single Assignee Ownership**: To prevent duplicate customer replies and dropped requests, each thread has at most **one** `PrimaryAssigneeUserId` at any time.

```
Incoming Email
      │
      ▼
Shared Mailbox
      │
      ▼
[UNASSIGNED Queue] ──(Staff Claims / Replies)──> [MY_WORK Queue]
      ▲                                                │
      │                                                ▼
      └─────────(Manager Reassigns / Unassigns)────────┘
```

---

## 2. Thread Responsibility Model & Entities

### 2.1 `EmailThread` Domain Entity Fields
```csharp
public class EmailThread : TenantAuditableEntity
{
    public Guid MailboxId { get; set; }
    public string Subject { get; set; } = string.Empty;
    public List<string> Participants { get; set; } = new();
    public DateTimeOffset LastMessageAt { get; set; } = DateTimeOffset.UtcNow;
    public int MessageCount { get; set; } = 1;
    public int DraftCount { get; set; } = 0;
    public bool HasUnread { get; set; } = false;

    // Responsibility & Assignment
    public Guid? PrimaryAssigneeUserId { get; set; }
    public DateTimeOffset? AssignedAt { get; set; }
    public ThreadStatus Status { get; set; } = ThreadStatus.Unassigned;
    public ThreadPriority Priority { get; set; } = ThreadPriority.Normal;
    public uint Version { get; set; } = 1; // Optimistic Concurrency Token
}
```

### 2.2 Thread Lifecycle Statuses

| Status | Code | Meaning | Who Moves Thread Here? |
|---|---|---|---|
| **`UNASSIGNED`** | `0` | Available in general pool. No staff member currently owns it. | Inbound Pipeline (new thread) or Manager Unassign action. |
| **`IN_PROGRESS`** | `1` | A staff member has claimed the thread and is actively drafting / handling. | Staff Explicit Claim, Staff Reply-to-Claim, or Manager Reassignment. |
| **`WAITING_CUSTOMER`**| `2` | Outbound reply sent; waiting for response from shipper/carrier. | Automated upon outbound email delivery. |
| **`RESOLVED`** | `3` | Operational inquiry completed (e.g. quote accepted, issue closed). | Staff / Manager status transition. |

---

## 3. Staff Triage Workflows

### 3.1 Unassigned Queue & Atomic Claim
1. **Queue Listing**:
   ```http
   GET /api/v1/mail/threads?scope=UNASSIGNED&page=1&pageSize=20
   ```
2. **Atomic Claim (`POST /api/v1/mail/threads/{id}/claim`)**:
   - Requires capability: `mail:thread:claim` (or legacy `mail:assign`).
   - Uses optimistic concurrency via `thread.Version`.
   - If two staff members click **"Claim"** simultaneously, the first commit succeeds (`200 OK`) and the second is rejected with `409 Conflict` (`THREAD_ALREADY_ASSIGNED`).
   - The thread immediately moves to `Status: IN_PROGRESS` and assigns `PrimaryAssigneeUserId = CurrentUser.UserId`.
   - RealtimeHub broadcasts WebSocket event `THREAD_CLAIMED` to gray out or remove the thread from other staff screens in real time.

### 3.2 "Reply-to-Claim" (Implicit Claim on Reply)
If a staff member opens an `UNASSIGNED` thread and immediately sends a reply (`POST /api/v1/mail/messages/outbound`):
- `SubmitOutboundMessageCommandHandler` detects `thread.PrimaryAssigneeUserId == null`.
- The system automatically executes an **atomic claim** in the database transaction, assigning `PrimaryAssigneeUserId = CurrentUser.UserId`.
- Even if SMTP submission encounters a downstream transient failure, the thread assignment remains intact.

### 3.3 My Work Workspace
Staff monitor assigned work using:
```http
GET /api/v1/mail/threads?scope=MY_WORK&page=1&pageSize=20
```
- Returns strictly threads where `PrimaryAssigneeUserId == CurrentUser.UserId`.
- Gated by `mail:read`.

### 3.4 Cross-Staff Lock Protection
If Staff A attempts to send an outbound message on a thread owned by Staff B:
- `SubmitOutboundMessageCommandHandler` checks if caller has `mail:thread:reassign`.
- If not a supervisor, the command throws `InvalidOperationException("THREAD_ASSIGNED_TO_ANOTHER_STAFF")` (`403 Forbidden`).

---

## 4. Supervisor & Manager Workflows

### 4.1 Supervisory Queue (`ALL`)
Supervisors and team leads monitor the entire department workload:
```http
GET /api/v1/mail/threads?scope=ALL&page=1&pageSize=20
```
- **Permission Required**: `mail:thread:read_all`.
- Regular staff lacking this permission receive `403 Forbidden`.

### 4.2 Reassigning Staff (`POST /api/v1/mail/threads/{id}/reassign`)
- **Permission Required**: `mail:thread:reassign`.
- Updates `PrimaryAssigneeUserId = request.NewAssigneeUserId`.
- Appends immutable audit entry to `ThreadAssignmentHistories`.
- Broadcasts WebSocket event `THREAD_REASSIGNED`.

### 4.3 Unassigning Thread (`POST /api/v1/mail/threads/{id}/unassign`)
- **Permission Required**: `mail:thread:unassign`.
- Resets `PrimaryAssigneeUserId = null` and sets `Status = ThreadStatus.Unassigned`.
- Appends audit entry with manager's explanation.

---

## 5. Immutable Assignment Audit History

Every assignment change is recorded in `ThreadAssignmentHistory` and queried via:
```http
GET /api/v1/mail/threads/{id}/assignment-history
```

```json
{
  "threadId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "histories": [
    {
      "id": "hist-101",
      "action": "CLAIMED",
      "fromUserId": null,
      "toUserId": "9a3c7e81-...",
      "actorUserId": "9a3c7e81-...",
      "reason": "Staff explicitly claimed unassigned thread",
      "createdAt": "2026-08-28T07:20:00Z"
    },
    {
      "id": "hist-102",
      "action": "REASSIGNED",
      "fromUserId": "9a3c7e81-...",
      "toUserId": "b41c2299-...",
      "actorUserId": "manager-4421-...",
      "reason": "Rebalancing European desk workload",
      "createdAt": "2026-08-28T08:00:00Z"
    }
  ]
}
```
