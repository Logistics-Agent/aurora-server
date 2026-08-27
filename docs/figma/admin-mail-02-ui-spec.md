# Aurora Admin Mail UI — UI Specification

> **Purpose:** Design input for Figma AI / Figma Make. Contains screen-by-screen specs, components, states, interactions, and responsive rules. Use alongside `admin-mail-01-product-context.md` for business context.

---

## 1. Design Direction

**Do:**
- Enterprise B2B, professional, operational, clean
- Dense enough for admin work without feeling cluttered
- Desktop-first with data tables as primary content
- Restrained typography and consistent spacing
- Clear visual hierarchy: title → description → actions → content

**Don't:**
- Gmail clone or consumer mail app
- Large marketing cards, hero sections, heavy gradients
- Excessive animations or playful illustrations
- Rounded bubbly mobile-first layouts

---

## 2. Global Layout

**Desktop targets:** 1440px primary, 1280px supported, 1024px minimum.

```
┌──────────────────────────────────────────────────┐
│  Top Header (logo, tenant name, user menu)       │
├──────────┬───────────────────────────────────────┤
│          │                                       │
│  Left    │  Main Content Area                    │
│  Sidebar │                                       │
│          │                                       │
│          │                                       │
└──────────┴───────────────────────────────────────┘
```

**Sidebar — Mail section (active):**
```
Mail
  Overview
  Domains
  Mailboxes
  Aliases
  Quarantine
  Audit
```

The sidebar may show other Aurora platform sections (Shipments, Routes, IAM, etc.) but Mail is the active section for these designs.

---

## 3. Screen — Mail Overview

**Route:** `/admin/mail`

**Layout:**
```
Page Header: "Mail Overview"

Summary Cards Row:
┌──────────┐ ┌──────────────┐ ┌────────┐ ┌───────────────────┐
│ Domains  │ │ Active       │ │ Aliases│ │ Pending           │
│ [count]  │ │ Mailboxes    │ │ [count]│ │ Quarantine [count]│
│          │ │ [count]      │ │        │ │                   │
└──────────┘ └──────────────┘ └────────┘ └───────────────────┘

Recent Quarantine Table (5 rows max)
  Columns: Received, Sender, Subject, Reason, Status
  "View all →" link

Recent Audit Activity Table (5 rows max)
  Columns: Timestamp, Actor, Action, Resource, Result
  "View all →" link
```

**Data sources:** Summary counts come from list endpoints. If list endpoints are unavailable, cards show "—" with a note.

**Do not show:** Delivery rate, server uptime, spam prevented %, or any metric the backend does not provide.

---

## 4. Screen — Domains

**Route:** `/admin/mail/domains`

**Page Header:**
```
Title: "Domains"
Description: "Manage email domains for your organization"
Primary Action: "+ Add Domain" (requires mail:domain:manage)
```

**Table columns:**

| Column | Source Field | Notes |
| --- | --- | --- |
| Domain | `DomainName` | Primary identifier |
| Status | `Status` | Badge: Active (green), Suspended (amber) |
| DKIM | `DkimSelector` presence | Badge: Configured / Not Set |
| Max Mailboxes | `MaxMailboxCount` | Numeric |
| Retention | `RetentionDays` | e.g. "365 days" |
| Created | `CreatedAt` | Relative or absolute date |
| Actions | — | Overflow menu |

**Domain Detail — Drawer:**
```
Domain: example.com
Status: Active
DKIM Selector: aurora-2025
DKIM TXT Record: [copyable text block]
Max Mailboxes: 100
Retention: 365 days
Created: 2026-01-15
```

> The DKIM TXT record display is important — admin needs to copy this value to configure DNS.

**Add Domain — Modal/Drawer:**
```
Fields:
  Domain Name* (text input, FQDN validation)
  Max Mailbox Count (number input, default 100)
  Retention Days (number input, default 365)

Actions:
  Cancel | Add Domain
```

> **⚠ BACKEND/POLICY REVIEW:** Domain provisioning calls Stalwart management API. This is existing source behavior. UI should present it as-is but the feature may be subject to policy gating in production.

---

## 5. Screen — Mailboxes

**Route:** `/admin/mail/mailboxes`

**Page Header:**
```
Title: "Shared Mailboxes"
Description: "Company email accounts shared across your team"
Primary Action: "+ Create Mailbox" (requires mail:mailbox:manage)
```

Do **not** use "Personal Mailboxes" anywhere.

**Table columns:**

| Column | Source Field | Notes |
| --- | --- | --- |
| Email Address | `FullAddress` | e.g. operations@company.com |
| Domain | Domain `DomainName` (via `DomainId`) | |
| Status | `Status` | Badge: Active (green), Suspended (amber), Deleted (red) |
| Created | `CreatedAt` | |
| Actions | — | Overflow menu |

**Create Mailbox — Modal/Drawer:**
```
Fields:
  Domain* (select from tenant domains)
  Local Part* (text input, e.g. "operations")

Preview: operations@selected-domain.com

Actions:
  Cancel | Create Mailbox
```

**Row Actions (overflow menu):**
- View Details
- Suspend (if Active) — requires confirm
- Activate (if Suspended) — requires confirm
- Reset Password — note: currently stub, delegated to Cognito OIDC

Do **not** show Delete mailbox action (not supported by backend).

**Mailbox Detail — Drawer:**
```
Email: operations@company.com
Local Part: operations
Domain: company.com
Status: Active
Created: 2026-03-01
```

---

## 6. Screen — Aliases

**Route:** `/admin/mail/aliases`

**Page Header:**
```
Title: "Aliases"
Description: "Email forwarding rules for your domains"
Primary Action: "+ Create Alias" (requires mail:mailbox:manage)
```

**Table columns:**

| Column | Source Field | Notes |
| --- | --- | --- |
| Alias Address | `AliasAddress` | e.g. info@company.com |
| Targets | `Targets[]` | Comma-separated or chip list |
| Domain | Domain name (via `DomainId`) | |
| Created | `CreatedAt` | |
| Actions | — | Overflow menu: Delete |

**Create Alias — Modal/Drawer:**
```
Fields:
  Domain* (select)
  Alias Address* (text input)
  Target Addresses* (multi-input, at least 1)

Actions:
  Cancel | Create Alias
```

**Delete Alias action:** Show confirmation dialog before proceeding.

> **Note:** Delete alias API (`DELETE /admin/mail/aliases/{id}`) does not exist yet. Design the interaction; backend will follow.

---

## 7. Screen — Quarantine

**Route:** `/admin/mail/quarantine`

This is a **security-sensitive** page.

**Page Header:**
```
Title: "Quarantine"
Description: "Emails flagged by security pipeline for review"
```

No primary create action.

**Filter Bar:**
```
Status: All | Pending | Released | Deleted
```

**Table columns:**

| Column | Source Field | Notes |
| --- | --- | --- |
| Received | `QuarantinedAt` | Date/time |
| Sender | ProcessedMessage → `SenderAddress` | |
| Recipient | ProcessedMessage → `RecipientAddresses` | First or truncated |
| Subject | ProcessedMessage → `Subject` | Truncated |
| Reason | `QuarantineReason` | Security severity badge |
| Spam Score | ProcessedMessage → `SpamScore` | Numeric with severity color |
| Phishing Score | ProcessedMessage → `PhishingScore` | Numeric with severity color |
| Status | `Status` | Badge: Pending (amber), Released (green), Deleted (red) |
| Actions | — | Contextual per status |

**Quarantine Detail — Drawer:**
```
Section: Message Info
  Sender: attacker@spam.example
  Recipients: sales@company.com
  Subject: Urgent wire transfer
  Received: 2026-08-15 14:32 UTC

Section: Security Analysis
  Quarantine Reason: High phishing score
  Spam Score: 8.5 / 10    [severity badge]
  Phishing Score: 0.92    [severity badge]

  Security Checks:
  ┌────────────────────────┬────────┬──────┐
  │ Stage                  │ Result │ Time │
  │ SPF Validation         │ Pass   │ 12ms │
  │ DKIM Validation        │ Fail   │ 8ms  │
  │ AI Phishing Detection  │ Fail   │ 340ms│
  │ ...                    │        │      │
  └────────────────────────┴────────┴──────┘

Section: Message Preview
  [sanitized text preview — no remote images]

Section: Review
  Reviewed By: [name or "—"]
  Reviewed At: [date or "—"]

Actions:
  Release (requires mail:quarantine:release)
  Delete Permanently (requires mail:quarantine:delete)
```

**Do not** render external remote images in message preview.

**Security severity badge rules:**
- Spam Score ≥ 5.0 → Warning (amber)
- Spam Score ≥ 10.0 → Critical (red)
- Phishing Score ≥ 0.7 → Critical (red)

---

## 8. Screen — Audit

**Route:** `/admin/mail/audit`

**Page Header:**
```
Title: "Audit Log"
Description: "Security and administrative activity trail"
```

**Filter Bar:**
```
Resource Type (text/select)
Resource ID (text)
```

**Table columns:**

| Column | Source Field | Notes |
| --- | --- | --- |
| Timestamp | `Timestamp` | Date/time, sorted desc |
| Actor | `ActorId` + `ActorType` | Resolve to name if possible |
| Action | `Action` | e.g. "DOMAIN_PROVISIONED", "QUARANTINE_DELETED" |
| Resource | `ResourceType` + `ResourceId` | e.g. "Domain / abc-123" |
| Result | `Result` | Badge: Success (green), Failure (red) |

**Audit Detail — Drawer:**
```
Timestamp: 2026-08-15 09:12:34 UTC
Actor: admin@company.com (TenantAdmin)
Action: QUARANTINE_DELETED
Resource Type: QuarantineRecord
Resource ID: 550e8400-e29b-41d4-a716-446655440000
Result: Success
Details: [formatted JSON block]
```

Pagination: cursor-based, max 100 per page.

---

## 9. Shared Components

Figma should create or reuse these components:

| Component | Notes |
| --- | --- |
| Admin Sidebar | Collapsible, section grouping, active state |
| Page Header | Title + description + optional primary action button |
| Stat Card | Icon + label + count, clickable to navigate |
| Data Table | Sortable headers, row hover, row click → detail |
| Search Input | Debounced, clear button |
| Filter Bar | Horizontal, chips or dropdowns |
| Pagination | Page size selector + prev/next, cursor-based |
| Status Badge | Variants: Active, Suspended, Deleted, Pending, Released |
| Security Severity Badge | Variants: Low, Medium, High, Critical |
| Dropdown Menu | For overflow row actions |
| Button | Primary, Secondary, Destructive, Ghost, Icon-only |
| Form Field | Label, input, helper text, error state |
| Select | Single and multi-select variants |
| Modal | For create forms, centered overlay |
| Drawer | Right-side panel for detail views |
| Alert / Banner | Info, Warning, Error, Success |
| Confirmation Dialog | Standard and destructive variants |
| Toast | Success, error, info — auto-dismiss |
| Skeleton | Table rows, cards, drawer content |
| Empty State | Illustration + message + optional action |
| Error State | Error message + retry action |
| Permission Denied State | Lock icon + "Insufficient permissions" message |

---

## 10. Table Behavior

Desktop-first. Every table must support these states:

| State | Behavior |
| --- | --- |
| Loading | Skeleton rows (5-10 shimmer rows) |
| Empty | Empty state illustration + message |
| Error | Error message + "Retry" button |
| Loaded | Data rows with hover highlight |
| Pagination | Previous / Next buttons, page size selector |
| Row Actions | Overflow `⋯` menu on each row |
| Row Click | Opens detail drawer |

Actions used infrequently belong in overflow `⋯` menu. Destructive actions must be visually distinct (red text or icon).

---

## 11. Required States

**Every page must have:**
```
Loading       → skeleton content
Loaded        → data displayed
Empty         → no records message
Error         → fetch failed, retry option
Permission Denied → user lacks required permission
```

**Every form must have:**
```
Idle              → fields enabled, ready for input
Validation Error  → inline field errors shown
Submitting        → button disabled, spinner
Server Error      → error banner with message
Success           → toast + return to list / close drawer
```

Do not design only the happy path.

---

## 12. Permission Variants

Components must have variants reflecting permission state:

| Scenario | Variant |
| --- | --- |
| User has permission | Action visible and enabled |
| User lacks permission | Action hidden (preferred) or disabled with tooltip |
| Read-only page | All mutation actions hidden |

**Examples:**

```
mail:mailbox:manage present → "Create Mailbox" button visible
mail:mailbox:manage absent  → "Create Mailbox" button hidden

mail:quarantine:read present  → Quarantine page accessible
mail:quarantine:release present → "Release" button visible in detail
mail:quarantine:delete present  → "Delete" button visible in detail
mail:quarantine:release absent  → "Release" button hidden

mail:audit:read present → Audit page accessible
mail:audit:read absent  → Audit nav item hidden or disabled
```

---

## 13. Destructive Interactions

### Delete Quarantine Record
```
┌────────────────────────────────────┐
│  Delete permanently?               │
│                                    │
│  This message will be permanently  │
│  removed. This action cannot be    │
│  undone.                           │
│                                    │
│          [Cancel]  [Delete]        │
│                    (red button)    │
└────────────────────────────────────┘
```

### Release Quarantine Record
```
┌────────────────────────────────────┐
│  Release message?                  │
│                                    │
│  The message will be released from │
│  quarantine for further processing │
│  and delivery.                     │
│                                    │
│          [Cancel]  [Release]       │
└────────────────────────────────────┘
```

### Suspend Mailbox
```
┌────────────────────────────────────┐
│  Suspend mailbox?                  │
│                                    │
│  operations@company.com will no    │
│  longer send or receive email.     │
│                                    │
│          [Cancel]  [Suspend]       │
│                    (amber button)  │
└────────────────────────────────────┘
```

---

## 14. Visual Hierarchy

Standard page structure:
```
[Breadcrumb (optional)]
[Title]
[Description]                              [Primary Action Button]

[Filter Bar / Search]

[Data Table]

[Pagination]
```

- Typography should be clear, restrained, and professional
- Tables are the primary content component — not cards
- Use cards only for overview summary stats
- Maintain consistent spacing between sections
- Use horizontal dividers sparingly

---

## 15. Responsive Rules

**At 1440px:**
- Full sidebar visible
- Full table with all columns
- Drawers open alongside table

**At 1280px:**
- Same structure
- Slightly tighter spacing
- All columns remain

**At 1024px:**
- Sidebar may collapse to icons or hamburger toggle
- Secondary table columns may hide (e.g. Retention, Max Mailboxes)
- Detail view moves to full-width drawer or overlay
- Filter bar may wrap to multiple lines

Mobile-first design is **not required** for MVP.

---

## 16. Accessibility Notes

Figma annotations should include:
- Visible focus indicators on all interactive elements
- Keyboard-navigable tables, menus, and dialogs
- Labels on all form controls (not placeholder-only)
- Tooltips on icon-only buttons
- Destructive buttons use explicit text labels (not just icons)
- Status badges use icon + text, not color alone
- Sufficient contrast ratios (WCAG AA minimum)

---

## 17. Prototype Flows

Figma prototype should connect at minimum:

**Flow A — Create Mailbox**
```
Mail Overview → Mailboxes → Create Mailbox → Success Toast → Mailboxes List
```

**Flow B — Mailbox Detail**
```
Mailboxes → Click Row → Mailbox Detail Drawer
```

**Flow C — Create Alias**
```
Aliases → Create Alias → Success Toast → Aliases List
```

**Flow D — Release Quarantine**
```
Quarantine → Click Row → Detail Drawer → Release → Confirmation → Success Toast
```

**Flow E — Delete Quarantine**
```
Quarantine → Click Row → Detail Drawer → Delete → Destructive Confirmation → Success Toast
```

**Flow F — Browse Audit**
```
Audit → Apply Filter → Click Row → Audit Detail Drawer
```

---

## 18. Figma Frame Checklist

Minimum frames to create:

| # | Frame | Notes |
| --- | --- | --- |
| 01 | Mail Overview | Summary cards + recent tables |
| 02 | Domains | Table view |
| 03 | Domain Detail | Drawer with DKIM display |
| 04 | Add Domain | Modal/drawer form |
| 05 | Shared Mailboxes | Table view |
| 06 | Create Mailbox | Modal/drawer form with preview |
| 07 | Mailbox Detail | Drawer |
| 08 | Aliases | Table view |
| 09 | Create Alias | Modal/drawer form |
| 10 | Quarantine | Table with filter bar |
| 11 | Quarantine Detail | Drawer with security analysis |
| 12 | Release Confirmation | Dialog |
| 13 | Delete Confirmation | Destructive dialog |
| 14 | Audit | Table with filter bar |
| 15 | Audit Detail | Drawer |
| 16 | Loading State | Skeleton table |
| 17 | Empty State | No records illustration |
| 18 | Error State | Fetch failed + retry |
| 19 | Permission Denied | Lock + message |

No need to create separate frames for each toast notification.

---

## 19. Do Not Design

The following belong to other UIs or system layers:

**Staff Mail UI (separate project):**
- Email inbox / reading pane
- Email compose / reply / forward
- Thread claim / reassign / unassign
- My Work / Unassigned / All views
- Draft management
- Manager monitoring inbox

**Stalwart Admin UI (separate system):**
- SMTP / IMAP / JMAP listener configuration
- Server queue management
- Cluster configuration
- Global TLS certificates
- Storage backend settings

**Not in MVP:**
- SLA tracking / Breached indicators
- Team / Queue / Collaborator management
- Auto assignment rules
- Personal mailbox ownership
- Mailbox membership management
- Negotiation UI
- Shipment UI
