# Aurora Admin Mail UI — UI Specification

> **Design Target:** Figma AI / Figma Make Component & Screen Specification  
> **Complementary Document:** `docs/figma/admin-mail-01-product-context.md`  
> **Source of Truth:** Audited against `.NET 10` MailService, `Admin.Bff`, `Staff.Bff`, and `PermissionConstants.cs`.

---

## 1. Design Direction

- **Aesthetic:** Enterprise B2B, security-focused, operational, clean, high-density data management.
- **Layout Model:** Desktop-first (1440px primary viewport, 1280px supported, 1024px minimum).
- **Core Paradigm:** Data tables with filter toolbars, action side drawers, and confirmation modals.
- **Tone:** Professional, restrained typography, clear status signaling (Success, Warning, Critical, Neutral).
- **Anti-Patterns:**
  - Do **not** design a Gmail-style reading pane or composer (that is the Staff Mail UI).
  - Do **not** design server infrastructure controls (SMTP listeners, IMAP ports, TLS certs belong to Stalwart).
  - Do **not** use heavy marketing cards, playful illustrations, or consumer-style rounded bubbles.

---

## 2. Global Admin Layout & Navigation

### Shell Layout Structure (1440px)
```text
┌─────────────────────────────────────────────────────────────────────────────┐
│ Header (Height: 56px) | Logo: Aurora Admin | Tenant: Acme Logistics [v] | User Menu │
├──────────────┬──────────────────────────────────────────────────────────────┤
│ Sidebar      │ Main Content Area (Padding: 24px 32px)                        │
│ (Width:240px)│                                                              │
│              │ Breadcrumbs: Admin / Mail Management / [Active Tab]          │
│ Dashboard    │ Page Header (Title, Subtitle, Primary Action Button)         │
│ IAM & Staff  │                                                              │
│ ──────────── │ Filter Toolbar / Search Controls                             │
│ > Mail Admin │                                                              │
│   • Overview │ Data Table / Content Grid                                    │
│   • Domains  │                                                              │
│   • Mailboxes│                                                              │
│   • Aliases  │ Pagination & Record Summary Footer                           │
│   • Quarantine│                                                             │
│   • Audit    │                                                              │
│ ──────────── │                                                              │
│ System Config│                                                              │
└──────────────┴──────────────────────────────────────────────────────────────┘
```

---

## 3. Screen 1: Mail Overview (`/admin/mail/overview`)

**Purpose:** Executive posture of tenant mail resources and pending security issues.

### 3.1 Metric Summary Cards (4 Columns)
| Card Title | Value | Subtext / Indicator | Action Link |
| :--- | :--- | :--- | :--- |
| **Configured Domains** | `2` | `1 Pending DNS Setup` (Warning badge) | Manage Domains → |
| **Shared Mailboxes** | `6` | `Limit: 100 max` | View Mailboxes → |
| **Active Aliases** | `4` | Forwarding to 9 recipients | View Aliases → |
| **Pending Quarantine** | `3` | `2 High Phishing, 1 Malware` (Critical badge) | Review Threats → |

### 3.2 Recent Security Quarantine Widget (Top 5 rows)
- **Columns:** Received Time, Sender, Recipient Mailbox, Primary Threat Reason, Threat Score, Action (`Inspect`).
- **Footer:** `"View all quarantined messages (3) →"`

### 3.3 Recent Administrative Audit Activity Widget (Top 5 rows)
- **Columns:** Timestamp, Actor (`TenantAdmin (3a7f)`), Action (`DomainProvisioned`, `MailboxCreated`), Resource, Result.
- **Footer:** `"View full audit log →"`

---

## 4. Screen 2: Domains (`/admin/mail/domains`)

**Purpose:** Manage organizational email domains and access DKIM DNS records.

### 4.1 Header Actions
- **Title:** `Domains`
- **Subtitle:** `Manage verified email domains, DKIM keys, and retention policies for your tenant.`
- **Primary Action:** `[+ Add Domain]` *(Requires `mail:domain:manage`)*

### 4.2 Domains Table
| Column Name | Data Field | Component Type | Value Example |
| :--- | :--- | :--- | :--- |
| **Domain Name** | `DomainName` | Text (Bold) | `acmelogistics.com` |
| **Status** | `Status` | Status Badge | `Active` (Green) \| `Suspended` (Gray) |
| **DKIM Status** | `DkimSelector` | Status Badge + Popover | `DKIM Key Generated` (Blue/Neutral) |
| **Mailboxes** | `Mailboxes.Count` | Text + Progress | `4 / 100` |
| **Retention** | `RetentionDays` | Text | `365 days` |
| **Created At** | `CreatedAt` | Timestamp | `2026-08-15 10:30` |
| **Actions** | — | Button Group | `[DNS Settings]` `[...]` |

### 4.3 DNS Settings Drawer / Modal (Slide-out from Right, 520px)
- **Header:** `DNS Configuration — acmelogistics.com`
- **Notice Banner:** `"Publish the following DNS TXT records with your domain registrar to enable authenticated mail delivery."`
- **Record Blocks (with One-Click Copy buttons):**
  1. **DKIM Record**:
     - Host / Name: `aurora-2025._domainkey.acmelogistics.com`
     - Type: `TXT`
     - Value: `v=DKIM1; k=rsa; p=MIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8...`
  2. **SPF Guidance Record**:
     - Type: `TXT`
     - Value: `v=spf1 include:relay.aurora-logistics.com ~all`
  3. **DMARC Guidance Record**:
     - Host: `_dmarc.acmelogistics.com`
     - Type: `TXT`
     - Value: `v=DMARC1; p=quarantine; rua=mailto:dmarc-reports@acmelogistics.com`

---

## 5. Screen 3: Shared Mailboxes (`/admin/mail/mailboxes`)

**Purpose:** Manage departmental shared identities (`ops@`, `sales@`, `customs@`).

### 5.1 Header Actions
- **Title:** `Shared Mailboxes`
- **Subtitle:** `Company communication identities used by staff teams for operational thread triage.`
- **Primary Action:** `[+ Create Mailbox]` *(Requires `mail:mailbox:manage`)*

### 5.2 Mailboxes Table
| Column Name | Data Field | Component Type | Value Example |
| :--- | :--- | :--- | :--- |
| **Full Address** | `FullAddress` | Text (Bold) | `operations@acmelogistics.com` |
| **Domain** | `DomainName` | Text (Secondary) | `acmelogistics.com` |
| **Department / Local**| `LocalPart` | Text | `operations` |
| **Status** | `Status` | Status Badge | `Active` (Green) \| `Suspended` (Gray) |
| **Created At** | `CreatedAt` | Timestamp | `2026-08-18 14:20` |
| **Actions** | — | Dropdown Menu | `Reset Credentials`, `View Audit` |

---

## 6. Screen 4: Aliases (`/admin/mail/aliases`)

**Purpose:** Configure inbound email forwarding rules to one or multiple targets.

### 6.1 Header Actions
- **Title:** `Email Aliases`
- **Subtitle:** `Forward incoming emails from an alias address to one or more internal mailboxes.`
- **Primary Action:** `[+ Add Alias]` *(Requires `mail:mailbox:manage`)*

### 6.2 Aliases Table
| Column Name | Data Field | Component Type | Value Example |
| :--- | :--- | :--- | :--- |
| **Alias Address** | `AliasAddress` | Text (Bold) | `freight-inquiry@acmelogistics.com` |
| **Domain** | `DomainName` | Text | `acmelogistics.com` |
| **Target Recipients** | `Targets[]` | Chip List (Tag Group) | `[ops@...]` `[sales@...]` |
| **Created At** | `CreatedAt` | Timestamp | `2026-08-20 09:15` |

---

## 7. Screen 5: Security Quarantine (`/admin/mail/quarantine`)

**Purpose:** Review, release, or permanently purge emails flagged by security checks.

### 7.1 Filter Toolbar
- **Status Filter:** Tabs or segmented button: `Pending (3)` | `Released` | `Deleted` | `All`
- **Search:** Input field searching Sender Address, Subject, or Message-ID.

### 7.2 Quarantine Table
| Column Name | Data Field | Component Type | Value Example |
| :--- | :--- | :--- | :--- |
| **Quarantined At** | `QuarantinedAt` | Timestamp | `2026-08-28 09:14:22` |
| **Sender** | `SenderAddress` | Text (Mono) | `invoice-update@suspicious-bank.com` |
| **Recipient Mailbox**| `RecipientAddresses` | Text | `ops@acmelogistics.com` |
| **Subject** | `Subject` | Text (Truncated) | `URGENT: Updated wire transfer instructions` |
| **Reason** | `QuarantineReason` | Badge (Warning/Crit)| `AI Phishing (Score: 0.94)` |
| **Spam Score** | `SpamScore` | Metric Pill | `14.2` *(Threshold: 10.0)* |
| **Status** | `Status` | Status Badge | `Pending Review` (Yellow) |
| **Actions** | — | Button Group | `[Inspect]` `[Release]` `[Delete]` |

### 7.3 Threat Inspection Drawer (Width: 640px)
- **Header:** `Threat Analysis — Message Inspection`
- **Status Bar:** Badge (`Pending Review`), Quarantined At timestamp, Message-ID.
- **Section 1: Threat Scores & Security Breakdown:**
  - **ClamAV Antivirus:** `Passed (Clean)`
  - **SpamAssassin Score:** `14.2` (Exceeds reject threshold `10.0`)
  - **AI Phishing / BEC Model:** `0.94 / 1.00` (High Probability Phishing)
  - **Authentication Checks:** `SPF: FAIL`, `DKIM: NONE`, `DMARC: FAIL`
- **Section 2: Message Headers:**
  - Collapsible key-value inspector (From, To, Reply-To, Return-Path, Client IP).
- **Section 3: Sandboxed Body Preview:**
  - Isolated read-only text viewer with external images and active scripts stripped.
- **Footer Actions:**
  - `[Cancel / Close]`
  - `[Release to Inbound Queue]` *(Requires `mail:quarantine:release` — Primary Outline)*
  - `[Delete Permanently]` *(Requires `mail:quarantine:delete` — Destructive Red)*

---

## 8. Screen 6: Audit Trail (`/admin/mail/audit`)

**Purpose:** Immutable log of all administrative and security actions on tenant mail resources.

### 8.1 Filter Toolbar
- **Resource Type Filter:** `All` | `Domain` | `Mailbox` | `Alias` | `Quarantine`
- **Date Range Picker:** Preset ranges (`Today`, `Last 7 Days`, `Last 30 Days`).

### 8.2 Audit Table
| Column Name | Data Field | Component Type | Value Example |
| :--- | :--- | :--- | :--- |
| **Timestamp** | `Timestamp` | Timestamp | `2026-08-28 09:20:11 UTC` |
| **Actor** | `ActorType` + `ActorId` | Text + Subtext | `TenantAdmin`<br/>`id: 3a7f...8812` |
| **Action** | `Action` | Action Pill | `QuarantineReleased`, `MailboxCreated` |
| **Resource Type** | `ResourceType` | Badge | `QuarantineRecord` |
| **Resource ID** | `ResourceId` | Text (Mono Short) | `9a3c...44aa` |
| **Result** | `Result` | Status Badge | `Success` (Green) \| `Failure` (Red) |
| **Details** | `DetailJson` | Action Link | `[View JSON]` |

### 8.3 Audit Detail Modal (JSON Viewer)
- Formatted code block showing structured payload: Actor IP, mutated fields, previous/new status.

---

## 9. Shared Components & Data Table Behavior

1. **Table States:**
   - **Loading State:** Skeleton table rows (5 rows).
   - **Empty State:** Centered icon, descriptive title (e.g. `"No Quarantined Messages"`), subtext (`"All inbound emails have passed security checks."`).
   - **Error State:** Alert banner with retry button (`"Failed to load audit records. [Retry]"`).
2. **Pagination:**
   - Server-driven cursor pagination (`PageSize`: 20 default, `Previous` / `Next` controls).
3. **Status Badges Color Scheme:**
   - `Active` / `Success` / `Released`: Emerald (`#10B981`, light bg `#ECFDF5`)
   - `Pending` / `Warning`: Amber (`#F59E0B`, light bg `#FFFBEB`)
   - `Suspended` / `Neutral`: Slate (`#64748B`, light bg `#F1F5F9`)
   - `Deleted` / `Critical` / `Malware`: Rose (`#F43F5E`, light bg `#FFF1F2`)

---

## 10. Forms, Drawers & Dialogs

### 10.1 Add Domain Modal / Drawer
- **Input 1:** `DomainName` (Text, Placeholder: `company.com`, validation: valid FQDN).
- **Input 2:** `MaxMailboxCount` (Number, default: `100`).
- **Input 3:** `RetentionDays` (Number, default: `365`).
- **Actions:** `[Cancel]` | `[Provision Domain]`

### 10.2 Create Shared Mailbox Modal
- **Input 1:** `Domain` (Dropdown selecting active tenant domain).
- **Input 2:** `LocalPart` (Text with `@domain.com` prefix, e.g. `customs`).
- **Hint:** `"Created mailboxes will be available for staff thread assignment."`
- **Actions:** `[Cancel]` | `[Create Mailbox]`

### 10.3 Add Alias Modal
- **Input 1:** `Domain` (Dropdown).
- **Input 2:** `AliasAddress` (Text, e.g. `inquiries@company.com`).
- **Input 3:** `TargetRecipients` (Multi-select / Tag input of existing mailboxes).
- **Actions:** `[Cancel]` | `[Add Alias]`

### 10.4 Delete Quarantine Record Confirmation Modal (Destructive)
- **Title:** `"Permanently Delete Quarantined Message?"`
- **Body:** `"This action will permanently purge message Message-ID: <...> from the system and raw storage. This action cannot be undone."`
- **Actions:** `[Cancel]` | `[Delete Permanently (Red)]`

---

## 11. Permission Variants & Access Control Rules

| User Permission Set | UI Element State |
| :--- | :--- |
| **Has `mail:domain:manage`** | `+ Add Domain` active. Full access to DNS drawers. |
| **Missing `mail:domain:manage`**| `+ Add Domain` hidden or disabled with tooltip `"Requires mail:domain:manage capability"`. |
| **Has `mail:mailbox:manage`**| `+ Create Mailbox`, `+ Add Alias`, Reset Password active. |
| **Missing `mail:mailbox:manage`**| Mailbox and Alias creation actions disabled. |
| **Has `mail:quarantine:delete`**| `Delete Permanently` button enabled on quarantine drawer. |
| **Missing `mail:quarantine:delete`**| `Delete Permanently` button hidden (Tenant Admin without purge rights). |
| **Has `mail:quarantine:release`**| `Release to Inbound` button enabled. |

---

## 12. Security-Sensitive Message Preview

Quarantine email body inspection must adhere to strict security rendering rules:
1. **Isolated Iframe / Sandboxed Container**: Render body in a sandboxed frame with `sandbox="allow-same-origin"`, `allow-scripts` disabled.
2. **External Resource Blocking**: Do not load external `<img>`, `<link>`, or font assets by default. Provide a button: `[Load External Assets (Unsafe)]`.
3. **Link Defanging**: Render hyperlinks as plain text or rewrite them to prevent accidental click-throughs.

---

## 13. Figma Frame Checklist

When generating or constructing Figma artboards, create the following 12 distinct frames:

- [ ] `01_Mail_Overview` — Full dashboard with metric cards and recent widgets.
- [ ] `02_Domains_List` — Domains table with status badges and actions.
- [ ] `03_Domains_AddModal` — Provision new domain form dialog.
- [ ] `04_Domains_DnsDrawer` — Slide-out drawer with copyable DKIM/SPF/DMARC TXT records.
- [ ] `05_Mailboxes_List` — Shared mailboxes inventory table.
- [ ] `06_Mailboxes_CreateModal` — New shared department mailbox modal.
- [ ] `07_Aliases_List` — Email aliases table with target recipient tag chips.
- [ ] `08_Aliases_AddModal` — Create forwarding alias modal with chip input.
- [ ] `09_Quarantine_List` — Security quarantine table with status filters.
- [ ] `10_Quarantine_InspectDrawer` — Full threat analysis drawer with score breakdown & preview.
- [ ] `11_Quarantine_DeleteModal` — Destructive permanent purge confirmation dialog.
- [ ] `12_Audit_List_And_Detail` — Filterable audit table with JSON viewer open.

---

## 14. Do Not Design (Out of Scope Guardrails)

1. **Staff Thread Triage / Inboxes**: Do not design `UNASSIGNED`, `MY_WORK`, `Claim`, `Reply`, or email composing in this Admin UI.
2. **Stalwart Infrastructure Controls**: Do not design SMTP listener port configs, IMAP server clustering, or certificate file uploaders.
3. **Personal User Inboxes**: Do not design personal mailboxes or user-specific inbox folders.
4. **Artificial Percentages**: Do not invent metrics like `"99.4% Spam Catch Rate"` or `"Server Uptime"` not backed by backend APIs.
