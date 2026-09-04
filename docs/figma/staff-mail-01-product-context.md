# Aurora Staff & Manager Mail UI — Product Context

> **Design Target:** Figma AI / Figma Make Operational Mail Workspace Specification  
> **Source of Truth:** Audited against `.NET 10` `MailService`, `Staff.Bff`, `mail_platform.proto`, `PermissionConstants.cs`, and `docs/technical/mail/**`.

---

## 1. Product Summary

The **Aurora Operational Mail Workspace** is the unified collaborative communication control plane for logistics operators, customer service agents, customs documentation specialists, and freight operations supervisors.

Unlike generic consumer webmail (Gmail/Outlook), Aurora's mail platform is **an operational work queue with customer communication attached**. Incoming inquiries to shared company identities (`ops@acmelogistics.com`, `customs@acmelogistics.com`) are automatically organized into **`EmailThread`** work items, triaged through structured queues (`UNASSIGNED`, `MY_WORK`, `ALL`), and assigned to a single responsible staff member (`PrimaryAssigneeUserId`).

Staff and Managers share the **exact same Mail Workspace application**. Differences in functionality (e.g. supervisory queue access, reassignment, unassignment, team history) are governed dynamically by **Capability-Based Access Control (CBAC)** tokens and thread scope queries, rather than separate interfaces.

---

## 2. Users: Staff vs. Manager Persona

| Attribute | Operational Staff (`STAFF`) | Operations Manager / Supervisor (`MANAGER`) |
| :--- | :--- | :--- |
| **Primary Goal** | Triage unassigned customer inquiries, claim work items, draft compliant replies, and resolve freight tickets. | Monitor team workload, balance queue backlogs, reassign abandoned threads, and review assignment history. |
| **Typical Permissions** | `mail:read`, `mail:draft:create`, `mail:send`, `mail:thread:claim` | Base staff permissions + `mail:thread:read_all`, `mail:thread:reassign`, `mail:thread:unassign` |
| **Visible Scopes** | `UNASSIGNED`, `MY_WORK` | `UNASSIGNED`, `MY_WORK`, **`ALL`** (Full team queue) |
| **Thread Ownership** | Claims unassigned threads to become `PrimaryAssigneeUserId`. | Can reassign threads between team members or release them back to unassigned. |
| **Outbound Identity** | Sends on behalf of shared mailbox (`ops@`), authenticated as self (`SentByUserId`). | Same, with supervisor audit tracking. |

---

## 3. Shared Company Mailbox Concept

- **No Individual Employee Inboxes**: Aurora does not provision personal employee inboxes (`john@company.com`). All customer communication arrives at shared departmental identities (`ops@`, `pricing@`, `customs@`, `sales@`).
- **Elimination of Siloed Chaos**: Prevents lost emails when staff members are on leave, eliminates duplicated replies from multiple colleagues, and provides unbroken organizational memory.
- **Traceable Human Attribution**: Outbound emails render the shared mailbox as the public sender (`From: ops@acmelogistics.com`), while the system immutably logs the authenticated author (`SentByUserId = alex.nguyen`).

---

## 4. EmailThread as the Core Operational Work Unit

```text
Tenant (Acme Logistics)
  │
  └── Shared Company Mailbox (ops@acmelogistics.com)
        │
        └── EmailThread (Work Unit: "Urgent Booking Request - 2x40HC HCM to Rotterdam")
              ├── PrimaryAssigneeUserId (Alex Nguyen — Single Owner)
              ├── Status (IN_PROGRESS | WAITING_CUSTOMER | RESOLVED | UNASSIGNED)
              ├── Priority (LOW | NORMAL | HIGH | URGENT)
              ├── Version (Optimistic Concurrency Lock Token)
              │
              ├── ProcessedMessages (Immutable Timeline)
              │     ├── Inbound: Shipper RFQ with B/L Attachment
              │     └── Outbound: Freight Quote (Sent by Alex Nguyen)
              │
              ├── EmailDraft (Working Draft / AI Negotiation Proposal)
              │
              └── ThreadAssignmentHistory (Audit Log of Ownership Changes)
```

---

## 5. Assignment & Responsibility Invariants

1. **Single Assignee Invariant**: At any point in time, an `EmailThread` has **zero or exactly one** `PrimaryAssigneeUserId`. There are no multi-assignee groups, co-owners, or round-robin auto-assignments in MVP.
2. **Explicit Atomic Claiming**: Clicking **"Take Thread"** (`POST /api/v1/mail/threads/{id}/claim`) uses database-level concurrency locking (`thread.Version`). If two staff members attempt to claim simultaneously, the first commit succeeds and the second receives `409 Conflict` (`THREAD_ALREADY_ASSIGNED`).
3. **Cross-Staff Steal Protection**: If a thread is assigned to Staff A, Staff B cannot send outbound messages on it. Attempting to reply throws `403 Forbidden` (`THREAD_ASSIGNED_TO_ANOTHER_STAFF`) unless the user possesses supervisory authority (`mail:thread:reassign`).
4. **Explicit Supervisory Reassignment**: Managers cannot implicitly "steal and reply." They must explicitly execute **Reassign Thread** (`POST /api/v1/mail/threads/{id}/reassign`) with a stated reason before ownership shifts.

---

## 6. Permission & Scope Access Model

Aurora governs mail operations through 7 granular capability permissions:

| Permission Code | Operational Capability | Gated UI Feature / Action |
| :--- | :--- | :--- |
| `mail:read` | Read mail threads, drafts, messages, and history | Access Mail workspace, view `UNASSIGNED` and `MY_WORK` queues |
| `mail:draft:create` | Create and edit draft messages | Rich text composer, AI suggestion insertion |
| `mail:send` | Submit outbound messages for SMTP relay | **"Send Email"** button |
| `mail:thread:claim` | Atomically claim unassigned threads | **"Take Thread"** button, Reply-to-Claim |
| `mail:thread:read_all` | Supervisory visibility across entire tenant team | **`ALL`** tab in queue navigation |
| `mail:thread:reassign` | Reassign thread ownership to another staff member | **"Reassign Thread"** action & modal |
| `mail:thread:unassign` | Release thread back to shared triage pool | **"Return to Unassigned"** action & modal |

---

## 7. Thread Lifecycle: Status vs. Assignment

Assignment ownership and conversation progress are strictly separated:

```text
[ Incoming Email ] ──► [ UNASSIGNED Queue ] (Assignee: None, Status: UNASSIGNED)
                             │
                  (Staff Claims / Replies)
                             │
                             ▼
                     [ MY_WORK Queue ]
                             │
     ┌───────────────────────┴───────────────────────┐
     ▼                                               ▼
[ IN_PROGRESS ]                             [ WAITING_CUSTOMER ]
(Staff actively drafting/investigating)    (Outbound reply sent; waiting on client)
     │                                               │
     └───────────────────────┬───────────────────────┘
                             ▼
                        [ RESOLVED ]
             (Inquiry completed / quote booked)
```

| Lifecycle Status | Meaning | Automatic System Trigger |
| :--- | :--- | :--- |
| **`UNASSIGNED`** | No staff member currently owns this work item. | New inbound email arriving on fresh thread or Manager Unassign action. |
| **`IN_PROGRESS`** | A staff member has claimed the thread and is actively working. | Explicit Claim, Reply-to-Claim, or Manager Reassignment. |
| **`WAITING_CUSTOMER`**| Outbound response sent; waiting for shipper/carrier feedback. | Triggered automatically upon successful outbound message delivery. |
| **`RESOLVED`** | Operational inquiry completed (e.g. rate accepted, issue closed). | Manual staff/manager status transition. |

---

## 8. The "Reply-to-Claim" Workflow

To minimize operational friction, staff members can open an unassigned thread and immediately start typing a reply:

1. Staff opens an inquiry in `UNASSIGNED` queue.
2. Composer displays a subtle helper banner: `ℹ️ Replying will automatically assign this conversation to you.`
3. When staff clicks **"Send Email"** (`POST /api/v1/mail/messages/outbound`):
   - The backend detects `thread.PrimaryAssigneeUserId == null`.
   - The system executes an **atomic claim** in the database transaction (`PrimaryAssigneeUserId = current user`, `Status = IN_PROGRESS`).
   - The assignment history records: `"Implicit claim on reply to unassigned thread"`.
   - **Failure Resilience**: Even if outbound SMTP relay or security pipeline encounters a transient failure, **the assignment remains intact**. The thread moves to `MY_WORK` with a `Delivery Failed` banner so the staff member can retry.

---

## 9. AI Negotiation Co-Pilot Workflow

When an inbound email contains a freight rate counter-offer or rate inquiry, Aurora's Negotiation Agent assists staff with bounded rate recommendations:

```text
Inbound Freight RFQ
        ↓
Negotiation Agent (Deterministic Tariff Matrix + Governed LLM)
        ↓
Suggested Rate Counter-Offer (e.g. "$1,380 USD per 40HC, Valid 7 Days")
        ↓
Staff Reviews Suggestion in Mail UI
        ├── [ Insert into Draft ] ──► Staff customizes wording ──► Staff Clicks [ Send Email ]
        └── [ Dismiss Suggestion ]
```

### Critical Governance Rule:
AI **never** sends emails automatically. AI proposals only generate structured drafts (`POST /api/v1/negotiations/{id}/mail-draft`), requiring human review and explicit send execution.

---

## 10. Information Architecture (Mail Workspace)

```text
Operational Mail Workspace
  │
  ├── Left Queue Sidebar (Width: 200px)
  │     ├── [UNASSIGNED] (Count Badge: 4)
  │     ├── [MY_WORK]    (Count Badge: 7)
  │     ├── [ALL]        (Supervisory view — Gated by mail:thread:read_all)
  │     ├── [DRAFTS]     (Unsent working drafts)
  │     └── Mailbox Filter Dropdown (All Mailboxes | ops@ | customs@)
  │
  ├── Center Thread List (Width: 360px)
  │     ├── Search Bar & Filter Controls (Status, Priority, Date)
  │     └── Thread Cards (Customer, Subject, Status, Priority, Snippet, Last Time)
  │
  └── Right Conversation & Execution Pane (Flex Width: 1fr)
        ├── Thread Header (Subject, Customer, Status, Priority, Assignee, Actions)
        ├── Thread Actions: [ Take Thread ] | [ Reassign ] | [ Return to Unassigned ] | [ History ]
        ├── Message Timeline (Chronological stream of Inbound & Outbound messages)
        ├── AI Negotiation Co-Pilot Panel (When rate suggestion is available)
        └── Rich Text Reply Composer (Body, Attachments, Send as Shared Identity)
```

---

## 11. Main User Flows

### Flow 1: Staff Triage & Explicit Claim
1. Staff navigates to `UNASSIGNED` queue.
2. Staff clicks an urgent booking inquiry thread from `client@shipper.com`.
3. Conversation pane displays thread history with a prominent blue **`[ Take Thread ]`** button.
4. Staff clicks **`[ Take Thread ]`** (`POST /api/v1/mail/threads/{id}/claim`).
5. Thread atomically transitions to `PrimaryAssigneeUserId = current user`, moves to `MY_WORK` queue, and displays active composer.

### Flow 2: Manager Reassignment
1. Manager navigates to `ALL` queue and filters by `Assignee: Sick/Away Staff`.
2. Manager selects thread and clicks **`[ Reassign ]`** (`mail:thread:reassign`).
3. Reassign modal opens: Manager selects `Target Staff: Linh Tran` and enters `Reason: Shift coverage`.
4. Manager clicks **`[ Confirm Reassignment ]`** (`POST /api/v1/mail/threads/{id}/reassign`).
5. Thread transfers ownership; `ThreadAssignmentHistory` appends the audit event.

### Flow 3: Inspecting Thread Assignment History
1. Staff or Manager clicks **`[ History ]`** icon in the thread header.
2. Drawer slides out displaying chronological audit timeline:
   - `08:30 UTC` — Inbound Email Arrived (Unassigned)
   - `08:45 UTC` — Claimed by Alex Nguyen
   - `11:00 UTC` — Reassigned from Alex Nguyen to Linh Tran by Operations Manager (Reason: "Shift handover")
   - `11:15 UTC` — Outbound Reply Sent by Linh Tran

---

## 12. Security Invariants & Guardrails

1. **Strict Tenant Isolation**: All queries enforce `TenantId == CurrentUser.TenantId`. Staff cannot view or claim threads belonging to other organizations.
2. **Actor Immutability**: `SentByUserId` is derived strictly from the backend session cookie / JWT context and can never be spoofed or overridden by client payloads.
3. **Optimistic Concurrency Control**: All thread mutations evaluate `thread.Version` to eliminate race conditions during high-volume triage.
4. **Sandboxed Rendering**: Inbound HTML bodies strip malicious scripts, defang URLs, and block external web beacons.

---

## 13. MVP Scope (Current Supported vs. Target)

| Feature | MVP Status | UI Handling / Figma Note |
| :--- | :--- | :--- |
| **`UNASSIGNED` & `MY_WORK` Queues** | `SUPPORTED_CURRENTLY` | Standard operational queues. |
| **Atomic Claim (`ClaimThread`)** | `SUPPORTED_CURRENTLY` | Single-click claim with conflict handling. |
| **Reply-to-Claim** | `SUPPORTED_CURRENTLY` | Automatic claim on outbound reply. |
| **Supervisory `ALL` Queue** | `SUPPORTED_CURRENTLY` | Visible only if possessing `mail:thread:read_all`. |
| **Reassign Thread** | `SUPPORTED_CURRENTLY` | Modal with target user picker and reason text. |
| **Return to Unassigned** | `SUPPORTED_CURRENTLY` | Modal with reason text. |
| **Assignment History** | `SUPPORTED_CURRENTLY` | Chronological audit timeline drawer. |
| **AI Negotiation Proposal to Draft**| `SUPPORTED_CURRENTLY` | Co-pilot panel with "Insert into Draft" action. |
| **Outbound Email Sending** | `SUPPORTED_CURRENTLY` | Composer with shared sender identity. |
| **Thread Search API** | `TARGET_SUPPORTED_BUT_API_MISSING` | UI specifies search bar; client mocks local search until backend query endpoint is added. |

---

## 14. Backend / API Gaps

1. **Thread Full-Text Search**: `GET /api/v1/mail/threads` currently filters by `scope`, `mailboxId`, and `status`. Backend query endpoint for full-text subject/snippet search is planned for v1.1.
2. **Staff Name Resolution in Assignment History**: `ThreadAssignmentHistory` stores `FromUserId`, `ToUserId`, and `ActorUserId` as GUIDs. BFF currently returns GUIDs; UI must handle fallback formatting (`User (3a7f)`) until IAM name enrichment is wired.
