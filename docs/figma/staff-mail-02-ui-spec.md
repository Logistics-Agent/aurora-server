# Aurora Staff & Manager Mail UI — UI Specification

> **Design Target:** Figma AI / Figma Make Component & Screen Specification  
> **Complementary Document:** `docs/figma/staff-mail-01-product-context.md`  
> **Source of Truth:** Audited against `.NET 10` `MailService`, `Staff.Bff`, `mail_platform.proto`, and `PermissionConstants.cs`.

---

## 1. 3-Pane Mail Workspace Layout

Mail is rendered as a 3-pane operational workspace inside the **Aurora Operations Workspace**:

```text
┌─────────────────────────────────────────────────────────────────────────────┐
│ Header: Aurora Operations | Tenant: Acme Logistics | User: Alex Nguyen      │
├──────────────┬──────────────────┬───────────────────────────────────────────┤
│ Pane 1       │ Pane 2           │ Pane 3                                    │
│ Queue Nav    │ Thread List      │ Thread Conversation & Composer            │
│ (Width:200px)│ (Width: 380px)   │ (Flexible Width: 860px+)                  │
│              │                  │                                           │
│ • Unassigned │ Search & Filters │ Subject: Urgent Booking Request - 2x40HC  │
│ • My Work    │                  │ Assignee: Alex Nguyen [Reassign] [Release]│
│ • All [Gated]│ [Thread Card 1]  │ Status: IN_PROGRESS | Priority: HIGH      │
│ • Drafts     │ • Shipper RFQ    │                                           │
│              │ • Status: NEW    │ [Message 1: Shipper Inbound + Attachment] │
│              │                  │ [Message 2: Outbound Quote Sent by Alex]  │
│              │ [Thread Card 2]  │                                           │
│              │ • Customs Notice │ ───────────────────────────────────────── │
│              │                  │ Email Composer (Reply / AI Draft)         │
│              │                  │ From: operations@acmelogistics.com        │
│              │                  │ [Insert AI Suggestion] [Send Outbound]    │
└──────────────┴──────────────────┴───────────────────────────────────────────┘
```

---

## 2. Pane 1: Queue Navigation

| Queue Tab | Scope Query | Required Capability | Description |
|---|---|---|---|
| **`Unassigned`** | `scope=UNASSIGNED` | `mail:read` | Unclaimed customer inquiries awaiting staff pickup. |
| **`My Work`** | `scope=MY_WORK` | `mail:read` | Threads currently owned by the authenticated user. |
| **`All Threads`** | `scope=ALL` | `mail:thread:read_all` | Supervisory team queue across all tenant mailboxes. |
| **`Drafts`** | Local / Mailbox | `mail:read` | In-progress drafts and pending AI suggestions. |

---

## 3. Pane 2: Thread List Card Specifications

Each thread card renders:
- **Sender / Shipper:** Customer name and company.
- **Subject & Snippet:** Thread subject and last message preview.
- **Timestamp:** Relative time of last message.
- **Status Badge:** `UNASSIGNED` (Grey), `IN_PROGRESS` (Blue), `WAITING_CUSTOMER` (Amber), `RESOLVED` (Green).
- **Assignee Avatar:** Avatar/Name of current owner, or `Unassigned` badge with **[Take Thread]** quick action.
- **Priority Indicator:** High/Urgent priority flag.

---

## 4. Pane 3: Conversation Timeline & Actions

### Header Action Bar
- **If Unassigned:** Primary button **[Take Thread]** (`POST /api/v1/mail/threads/{id}/claim`).
- **If Assigned to Current User:** Actions `[Set Priority]`, `[Mark Resolved]`.
- **If Supervisory User (`mail:thread:reassign`):** Actions **[Reassign Thread]** (opens staff selector modal), **[Release to Unassigned]**.

### Composer & AI Integration
- **Sender Selection:** Dropdown of assigned tenant shared mailboxes (e.g. `operations@acmelogistics.com`).
- **Human Attribution:** Authenticated user is logged on outbound delivery (`SentByUserId`).
- **AI Suggested Reply:** If linked to a Negotiation session, staff can click **[Insert AI Counter-Offer]** to populate the draft from the validated negotiation agent.
