# Aurora Staff & Manager Mail UI — Product Context

> **Design Target:** Figma AI / Figma Make Operational Mail Workspace Specification  
> **Source of Truth:** Audited against `.NET 10` `MailService`, `Staff.Bff`, `mail_platform.proto`, `PermissionConstants.cs`, and `docs/technical/mail/**`.

---

## 1. Product Summary

The **Aurora Operational Mail Workspace** is the collaborative communication control plane for freight forwarders, dispatchers, customs documentation specialists, and operations managers.

Mail is an integrated module inside the **Aurora Operations Workspace** alongside Shipments, Route Planning, OCR Documents, Compliance, and GPS Tracking:

```text
Aurora Operations Workspace
├── Operational Dashboard
├── Shipments
├── Route Planning
├── Documents / OCR
├── Compliance
├── Mail
│   ├── Unassigned
│   ├── My Work
│   ├── All             [Requires mail:thread:read_all]
│   └── Drafts
├── Tracking
└── Financial / Billing
```

Unlike generic consumer webmail, Aurora Mail is **an operational work queue with customer communication attached**. Customer inquiries arriving at shared company identities (`operations@acmelogistics.com`) are organized into **`EmailThread`** work items, triaged through structured queues (`UNASSIGNED`, `MY_WORK`, `ALL`), and assigned to a single responsible staff member (`PrimaryAssigneeUserId`).

Staff and Managers share the **exact same UI workspace**. Differences in functionality (supervisory queue visibility, thread reassignment, unassignment) are governed dynamically by direct capability permissions.

---

## 2. Shared Company Mailbox Concept

- **No Individual Employee Inboxes**: Aurora does not provision personal employee inboxes (`john@company.com`). Customer communication arrives at shared departmental identities (`operations@`, `customs@`, `pricing@`).
- **Elimination of Siloed Communication**: Prevents lost customer inquiries when staff take leave and eliminates duplicate replies.
- **Traceable Human Attribution**: Outbound emails display the shared company mailbox as the sender (`From: operations@acmelogistics.com`), while the system immutably logs the authenticated author (`SentByUserId = alex.nguyen`).

---

## 3. EmailThread Work Item Model

```text
Tenant (Acme Logistics)
  │
  └── Shared Company Mailbox (operations@acmelogistics.com)
        │
        └── EmailThread (Work Unit: "Urgent Booking Request - 2x40HC HCM to Rotterdam")
              ├── PrimaryAssigneeUserId (Alex Nguyen — Single Responsible Owner)
              ├── Status (UNASSIGNED | IN_PROGRESS | WAITING_CUSTOMER | RESOLVED)
              ├── Priority (LOW | NORMAL | HIGH | URGENT)
              ├── Version (Optimistic Concurrency Lock Token)
              │
              ├── ProcessedMessages (Immutable Timeline)
              │     ├── Inbound: Shipper RFQ with Packing List
              │     └── Outbound: Freight Quote (Sent by Alex Nguyen)
              │
              ├── EmailDraft (Working Draft / AI Negotiation Proposal)
              │
              └── ThreadAssignmentHistory (Audit Log of Ownership Changes)
```

---

## 4. Operational Invariants & Permissions

1. **Single Assignee Invariant**: At any point in time, an `EmailThread` has **zero or exactly one** `PrimaryAssigneeUserId`.
2. **Explicit Atomic Claiming**: Clicking **"Take Thread"** (`POST /api/v1/mail/threads/{id}/claim`) locks the thread using `thread.Version`. If two staff members claim simultaneously, the second receives `409 Conflict` (`THREAD_ALREADY_ASSIGNED`).
3. **Cross-Staff Steal Protection**: Staff members cannot reply to threads owned by colleagues unless they possess supervisory capability (`mail:thread:reassign`).
4. **Supervisory Governance**:
   - `mail:thread:read_all` unlocks the **`ALL`** team queue.
   - `mail:thread:reassign` allows reassigning thread ownership with mandatory business justification.
   - `mail:thread:unassign` allows releasing threads back to the shared pool.
