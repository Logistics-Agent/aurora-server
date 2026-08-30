# Aurora Staff & Manager Mail UI — UI Specification

> **Design Target:** Figma AI / Figma Make Component & Screen Specification  
> **Complementary Document:** `docs/figma/staff-mail-01-product-context.md`  
> **Source of Truth:** Audited against `.NET 10` `MailService`, `Staff.Bff`, `mail_platform.proto`, and `PermissionConstants.cs`.

---

## 1. Design Direction & Workspace Aesthetic

- **Design Philosophy:** Operational freight triage workspace with unified communication history.
- **Aesthetic:** High-density, professional, clear contrast between customer and internal staff messages, restrained operational color coding.
- **Grid & Tokens:** 8-point spatial system (`8px`, `16px`, `24px`, `32px`).
- **Primary Desktop Viewport:** `1440px` (3-Pane Split), with support for `1280px` and `1024px`.

### Color & Elevation Tokens
| Token Name | Hex / CSS Value | Usage |
| :--- | :--- | :--- |
| **Workspace Canvas** | `#F8FAFC` (Slate 50) | App background, list surfaces |
| **Pane Surface** | `#FFFFFF` (White) | Conversation pane, composer, modals |
| **Sidebar Canvas** | `#0F172A` (Slate 900) or `#1E293B` | Left queue navigation |
| **Inbound Message Bubble**| `#F1F5F9` (Slate 100) | Customer messages (Left-aligned) |
| **Outbound Message Bubble**| `#EFF6FF` (Blue 50), border `#DBEAFE` | Staff replies (Right/Full-width styled) |
| **Primary Action** | `#2563EB` (Blue 600) | `Take Thread`, `Send Email` |
| **Status: In Progress** | `#3B82F6` (Blue 500), bg `#EFF6FF` | Staff actively working |
| **Status: Waiting Customer**| `#F59E0B` (Amber 500), bg `#FFFBEB` | Reply sent, awaiting client |
| **Status: Resolved** | `#10B981` (Emerald 500), bg `#ECFDF5` | Inquiry completed |
| **Status: Unassigned** | `#64748B` (Slate 500), bg `#F1F5F9` | Open triage queue |
| **Priority: Urgent** | `#EF4444` (Red 500), bg `#FEF2F2` | Time-sensitive freight inquiry |
| **Priority: High** | `#F97316` (Orange 500), bg `#FFF7ED` | Rate quotation / customs block |

---

## 2. Global 3-Pane Workspace Layout (Desktop 1440px)

```text
┌────────────────────────────────────────────────────────────────────────────────────────────────────────┐
│ Header (56px) | Aurora Freight Operations | Tenant: Acme Logistics [v] | User: Alex Nguyen (Staff)     │
├──────────────┬───────────────────────────────┬─────────────────────────────────────────────────────────┤
│ Pane 1:      │ Pane 2: Thread List           │ Pane 3: Conversation & Execution Workspace              │
│ Queue Nav    │ (Width: 380px)                │ (Flex: 1fr, Min: 700px)                                 │
│ (Width:200px)│                               │                                                         │
│              │ [ Search threads...         ] │ Thread Header: Subject, Status, Priority, Assignee      │
│ [UNASSIGNED] │ Filter: [Status v] [Priority v]│ Actions: [ Take Thread ] [ Reassign ] [ Return ] [Hist] │
│   Count: 4   │ ───────────────────────────── │ ─────────────────────────────────────────────────────── │
│              │ • Card 1 (Active)             │ Message Stream (Chronological Inbound / Outbound)       │
│ [MY WORK]    │   Shipper Corp • 10m ago      │ ┌─────────────────────────────────────────────────────┐ │
│   Count: 7   │   Urgent: 2x40HC HCM-Rotterdam│ │ Inbound: Shipper RFQ with attached B/L              │ │
│              │   Status: IN_PROGRESS         │ └─────────────────────────────────────────────────────┘ │
│ [ALL]*       │   Assignee: Alex Nguyen       │ ┌─────────────────────────────────────────────────────┐ │
│   Count: 28  │ ───────────────────────────── │ │ Outbound: Rate Quote (Sent by Alex as ops@)         │ │
│   *(Manager) │ • Card 2                      │ └─────────────────────────────────────────────────────┘ │
│              │   Global Trade • 25m ago      │ ─────────────────────────────────────────────────────── │
│ [DRAFTS]     │   Customs clearance query     │ [ ⚡ AI Negotiation Suggestion Panel (Collapsible)    ] │
│   Count: 2   │   Status: UNASSIGNED          │ ─────────────────────────────────────────────────────── │
│              │   Assignee: [ None ]          │ Reply Composer (Rich Text, Attachments, Send Button)   │
└──────────────┴───────────────────────────────┴─────────────────────────────────────────────────────────┘
```

---

## 3. Pane 1: Queue & Mailbox Navigation

- **Queue Navigation Items:**
  1. **`Unassigned`** (`scope=UNASSIGNED`): Open customer threads requiring staff claiming. Shows active count badge (e.g. `4`).
  2. **`My Work`** (`scope=MY_WORK`): Threads assigned strictly to current user (`PrimaryAssigneeUserId == me`).
  3. **`All Threads`** (`scope=ALL`): Supervisory queue showing all team work. **Gated by `mail:thread:read_all`** (hidden if permission absent).
  4. **`Drafts`**: In-progress unsent drafts.
- **Shared Mailbox Filter Dropdown:**
  - `All Shared Mailboxes` | `ops@acmelogistics.com` | `customs@acmelogistics.com` | `pricing@acmelogistics.com`.

---

## 4. Pane 2: Thread List & Card Specifications

### 4.1 Header Controls
- **Search Input:** Searches subjects, senders, and thread IDs.
- **Filters:** Status dropdown (`All`, `In Progress`, `Waiting Customer`, `Resolved`), Priority dropdown (`All`, `Urgent`, `High`, `Normal`, `Low`).

### 4.2 Thread Card Component (`ThreadCard`)
- **Card States:** Default (`#FFFFFF`), Hover (`#F8FAFC`), Selected/Active (`#EFF6FF` with `3px` solid Blue left border), Unread (`Font-Weight: SemiBold` + Blue dot).
- **Card Content Layout:**
  - Row 1: External Contact / Sender Name (Bold) + Time elapsed (`12m ago`).
  - Row 2: Subject Line (Truncated, 1 line).
  - Row 3: Message Snippet preview (Secondary text, 1 line).
  - Row 4 (Footer Badges):
    - Priority Badge (e.g. `HIGH` Orange).
    - Status Badge (e.g. `IN_PROGRESS` Blue).
    - Assignee Pill: In `ALL` queue, displays `Alex N.` or `[ Unassigned ]`. In `MY_WORK`, suppressed to reduce clutter.

---

## 5. Pane 3: Conversation Workspace & Thread Header

### 5.1 Thread Header
- **Top Row:**
  - Subject Title: `Urgent Booking Request - 2x40HC Ho Chi Minh to Rotterdam` (H2, Bold).
  - Thread ID: `#3fa85f64` (Small mono, copyable).
- **Metadata Sub-Bar:**
  - External Contact: `shipper@clientcorp.com` (with company badge).
  - Shared Mailbox: `Received at: ops@acmelogistics.com`.
  - Status Badge: `IN_PROGRESS` (Dropdown to manually transition to `RESOLVED`).
  - Priority Badge: `HIGH`.
  - Current Assignee: `Alex Nguyen (You)` or `Unassigned`.
- **Header Action Button Group:**
  - If Unassigned: **`[ ⚡ Take Thread ]`** (Primary Blue Button).
  - If Assigned & Has `mail:thread:reassign`: **`[ ⇄ Reassign ]`** (Ghost Outline).
  - If Assigned & Has `mail:thread:unassign`: **`[ ↶ Return to Unassigned ]`** (Ghost Outline).
  - Always Visible: **`[ 🕒 History ]`** (Audit timeline drawer toggle).

---

## 6. Message Timeline Components

### 6.1 Inbound Message Card (`InboundMessage`)
- **Header:** Shipper Name (`shipper@clientcorp.com`) • Timestamp (`Aug 28, 2026, 07:15 UTC`) • Security Tag (`✓ SPF/DKIM Verified • Clean`).
- **Body:** Formatted sanitized email body.
- **Attachments Row:** File chips with download icons (e.g. `[ 📄 Bill_of_Lading_MSK9901.pdf • 1.4 MB ]`).

### 6.2 Outbound Message Card (`OutboundMessage`)
- **Header:** `From: ops@acmelogistics.com` • `Sent by: Alex Nguyen` • Timestamp (`Aug 28, 2026, 08:30 UTC`).
- **Status Indicator:** `✓ Delivered via SMTP` (Green).
- **Body:** Staff reply text.

---

## 7. AI Negotiation Suggestion Panel (Collapsible Co-Pilot Card)

Appears above the composer when the Negotiation Agent provides a rate recommendation:

```text
┌─────────────────────────────────────────────────────────────────────────────┐
│ ⚡ AI Negotiation Assistant                        [ Rate Suggestion Ready ] │
├─────────────────────────────────────────────────────────────────────────────┤
│ Based on Route SG-VN tariff rules, suggested counter-offer:                │
│ • Proposed Rate: $1,380 USD per 40HC (Target Margin: 18.5%)                 │
│ • Free Detention: 7 Days at Port of Rotterdam                               │
│                                                                             │
│ [ Insert into Draft ]                             [ Dismiss Suggestion ]    │
└─────────────────────────────────────────────────────────────────────────────┘
```

- **Action:** Clicking `[ Insert into Draft ]` populates the rich text editor with professional email wording containing the exact parameters.

---

## 8. Reply Composer & Draft Experience

- **Header Notice:**
  - If thread is unassigned: `ℹ️ Replying will automatically assign this conversation to you.`
  - Outbound identity indicator: `Sending as: ops@acmelogistics.com (Authenticated as Alex Nguyen)`.
- **Editor Controls:**
  - Rich Text toolbar (Bold, Italic, Lists, Hyperlink, Code, Attachments).
  - Recipient field: Pre-populated with customer email (`To: shipper@clientcorp.com`).
- **Footer Actions:**
  - Left: Auto-save status (`✓ Draft saved 10s ago`).
  - Right: `[ Discard Draft ]` (Ghost) + `[ Send Email ]` (Primary Blue).

---

## 9. Supervisory Modals & Drawers

### 9.1 Reassign Thread Modal (Dialog, Width: 480px)
- **Title:** `Reassign Thread Ownership`
- **Current Assignee:** `Alex Nguyen`
- **Target Staff Member:** Dropdown searchable selector (e.g. `Linh Tran (Operations)`).
- **Handover Reason:** Text area (e.g. `"Covering European freight desk during sick leave"`).
- **Actions:** `[ Cancel ]` | `[ Confirm Reassignment ]` *(Requires `mail:thread:reassign`)*.

### 9.2 Return to Unassigned Modal (Dialog, Width: 460px)
- **Title:** `Return Conversation to Shared Queue?`
- **Body:** `"This conversation will be moved back to the UNASSIGNED queue. Any staff member will be able to claim it."`
- **Reason:** Text area (e.g. `"Requires specialist customs evaluation"`).
- **Actions:** `[ Cancel ]` | `[ Return to Queue (Amber) ]` *(Requires `mail:thread:unassign`)*.

### 9.3 Assignment History Drawer (Slide-out Right, Width: 440px)
- **Title:** `Thread Assignment & Ownership History`
- **Timeline Items:**
  - `08:30 UTC` • **Thread Created** (Inbound email from `shipper@clientcorp.com`).
  - `08:45 UTC` • **Claimed** by `Alex Nguyen` (Explicit staff claim).
  - `11:00 UTC` • **Reassigned** from `Alex Nguyen` to `Linh Tran` by `Manager (David M.)` — Reason: *"Shift handover"*.
  - `11:15 UTC` • **Reply Sent** by `Linh Tran`.

---

## 10. Concurrency & Error States

### 10.1 Concurrency Claim Conflict (409 State)
- **Trigger:** Another colleague clicks "Take Thread" milliseconds earlier.
- **Inline Alert Banner:**  
  `⚠️ This conversation was just claimed by Linh Tran. [ Refresh Queue ] [ Return to Unassigned ]`
- **Action:** Primary claim button disables; workspace shifts to read-only mode.

### 10.2 Cross-Staff Reply Block (403 State)
- **Trigger:** Staff A attempts to reply to a thread assigned to Staff B without supervisor rights.
- **Alert Banner:**  
  `🔒 This conversation is currently assigned to Linh Tran. You cannot send replies on another staff member's thread.`
- **Action:** Composer disabled with lock icon.

### 10.3 Outbound Delivery Failure State
- **Trigger:** Outbound SMTP relay fails due to external server timeout or security bounce.
- **Alert Banner:**  
  `❌ Message delivery failed: Downstream SMTP timeout. Your assignment remains intact in My Work. [ Retry Send ]`

---

## 11. Responsive Redesign Rules

| Viewport | Layout Strategy |
| :--- | :--- |
| **Desktop 1440px** | Full 3-Pane View (Queue Sidebar + Thread List + Conversation Workspace). |
| **Desktop 1024px** | Collapsible Queue Sidebar (Icon-only mode, `64px`), full Thread List and Workspace. |
| **Tablet 768px** | 2-Pane Split (Thread List + Conversation). Queue nav accessible via top dropdown. |
| **Mobile 375px** | 1-Pane Drill-down Navigation: Screen 1 = Thread List; Screen 2 = Conversation Detail. |

---

## 12. Accessibility & Keyboard Shortcuts

- **Shortcuts:**
  - `C` → Claim active unassigned thread (`Take Thread`).
  - `R` → Focus Reply composer.
  - `J` / `K` → Navigate to Next / Previous thread in list.
  - `Esc` → Close modals, drawers, and cancel draft.
- **ARIA Standards:** `aria-live="polite"` on queue count badges; `role="region"` on conversation timeline.

---

## 13. Figma Frame Checklist (18 Artboards)

Construct the following 18 distinct frames for the Staff & Manager Mail Suite:

- [ ] `01_Mail_Unassigned_Queue` — 3-pane layout showing Unassigned threads.
- [ ] `02_Mail_MyWork_Queue` — 3-pane layout showing active My Work threads.
- [ ] `03_Mail_Manager_All_Queue` — Supervisory queue with assignee column and filters.
- [ ] `04_Thread_Unassigned_Detail` — Thread view with prominent `[ Take Thread ]` CTA.
- [ ] `05_Thread_MyAssigned_Work` — Active conversation with composer open.
- [ ] `06_Thread_WaitingCustomer` — Resolved/waiting conversation with delivered badge.
- [ ] `07_Reply_Composer_Active` — Rich text composer with populated attachments.
- [ ] `08_ReplyToClaim_Warning` — Composer displaying auto-assignment notification.
- [ ] `09_Reassign_Modal` — Supervisor modal with staff selector and reason text.
- [ ] `10_Unassign_Modal` — Return to unassigned confirmation dialog.
- [ ] `11_AssignmentHistory_Drawer` — Chronological audit timeline slide-out.
- [ ] `12_AiNegotiation_Suggestion` — AI Co-Pilot card showing rate proposal.
- [ ] `13_Claim_ConcurrencyConflict_409` — Conflict alert when thread is taken by peer.
- [ ] `14_CrossStaff_StealBlock_403` — Block banner when attempting to reply on peer thread.
- [ ] `15_Delivery_Failure_State` — Banner showing SMTP failure with assignment preserved.
- [ ] `16_Mobile_Thread_List` — 375px mobile thread queue view.
- [ ] `17_Mobile_Conversation_Detail` — 375px mobile message timeline & composer.
- [ ] `18_Component_Library_Mail` — UI kit with badges, thread cards, and timeline bubbles.

---

## 14. React / Next.js Component Architecture Mapping

```text
src/components/mail/
├── MailWorkspaceLayout.tsx          (3-pane responsive grid shell)
├── QueueSidebar.tsx                 (Unassigned, My Work, All, Drafts)
├── ThreadList.tsx                   (Filter toolbar & thread scroll list)
├── ThreadCard.tsx                   (Individual queue card with status/priority)
├── ConversationWorkspace.tsx        (Header, timeline, AI co-pilot, composer)
├── ThreadHeader.tsx                 (Subject, status/priority, claim/reassign actions)
├── InboundMessageBubble.tsx         (Customer message, attachments, security tags)
├── OutboundMessageBubble.tsx        (Staff reply with 'Sent by' human attribution)
├── AiNegotiationPanel.tsx           (Rate proposal card with 'Insert into Draft')
├── ReplyComposer.tsx                (Rich text editor with auto-save and send)
├── ReassignModal.tsx                (Staff selector & reason dialog)
├── UnassignModal.tsx                (Return to queue confirmation dialog)
└── AssignmentHistoryDrawer.tsx      (Chronological audit stream)
```
