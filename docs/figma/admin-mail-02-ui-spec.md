# Aurora Admin Mail UI — UI Specification

> **Design Target:** Figma AI / Figma Make Component & Screen Specification  
> **Complementary Document:** `docs/figma/admin-mail-01-product-context.md`  
> **Source of Truth:** Audited against `.NET 10` `MailService`, `Admin.Bff`, `protos/mail_platform.proto`, and `PermissionConstants.cs`.

---

## 1. Module Layout in Aurora Admin Shell

Admin Mail resides inside the **Aurora Admin Console** shell under the `MAIL ADMINISTRATION` sidebar group:

```text
Aurora Admin Console
├── Overview
├── People & Access
├── Operations Configuration
├── MAIL ADMINISTRATION
│   ├── Mail Overview   (`/admin/mail/overview`)
│   ├── Domains         (`/admin/mail/domains`)
│   ├── Shared Mailboxes(`/admin/mail/mailboxes`)
│   ├── Aliases         (`/admin/mail/aliases`)
│   ├── Quarantine      (`/admin/mail/quarantine`)
│   └── Mail Audit      (`/admin/mail/audit`)
└── Audit & Security
```

---

## 2. Screen 1: Domains (`/admin/mail/domains`)

**Purpose:** View assigned enterprise mail domains and verify DKIM DNS records.

### Table Columns
| Column | Type | Content Example | Description |
|---|---|---|---|
| **Domain Name** | Text + Badge | `acmelogistics.com` `[ASSIGNED]` | FQDN assigned by System Admin |
| **Status** | Status Badge | `Active` (Green) | Stalwart routing status |
| **DKIM Selector** | Text (Mono) | `aurora-2025` | DNS selector tag |
| **DKIM Record** | Action | `[View DNS Instructions]` | Opens drawer with TXT record |
| **Mailbox Usage**| Progress Bar | `4 / 100` | Configured mailboxes vs quota |
| **Retention** | Text | `365 days` | Email archival retention |

> [!NOTE]
> **Domain Provisioning Policy**: `+ Add Domain` button is removed in Target UX. Domain allocation is performed by `SYSTEM_ADMIN`. The screen provides DNS instructions and status verification for assigned domains.

---

## 3. Screen 2: Shared Mailboxes (`/admin/mail/mailboxes`)

**Purpose:** Manage shared department mailboxes and designate the primary tenant intake.

### Table Columns
| Column | Type | Content Example | Description |
|---|---|---|---|
| **Mailbox Address** | Text + Badge | `operations@acmelogistics.com` `[DEFAULT]` | Primary operational intake |
| **Domain** | Text | `acmelogistics.com` | Parent domain |
| **Status** | Badge | `Active` | Operational state |
| **Created At** | Timestamp | `2026-08-20 14:00` | Creation timestamp |
| **Actions** | Dropdown Menu | `View Details`, `View Audit Trail` | Administrative actions |

### Create Mailbox Drawer (`[+ Create Mailbox]`)
- **Domain:** Single-select dropdown of assigned domains.
- **Local Part:** Text input (e.g. `operations`, `customs`, `pricing`).
- **Preview:** `operations@acmelogistics.com`.

---

## 4. Screen 3: Aliases (`/admin/mail/aliases`)

**Purpose:** Configure inbound forwarding aliases mapping alternate public addresses to canonical shared mailboxes.

### Table Columns
| Column | Type | Content Example | Description |
|---|---|---|---|
| **Alias Address** | Text | `contact@acmelogistics.com` | Alternate public inbound address |
| **Target Shared Mailbox** | Text (Link) | `operations@acmelogistics.com` | Canonical destination mailbox |
| **Created At** | Timestamp | `2026-08-25 09:30` | Creation timestamp |
| **Actions** | Button | `[Delete]` | Remove forwarding alias |

### Create Alias Drawer (`[+ Create Alias]`)
- **Domain:** Single-select dropdown of assigned domains.
- **Alias Local Part:** Text input (e.g. `contact`, `sales`, `info`).
- **Target Shared Mailbox:** **Single-select dropdown** of existing tenant shared mailboxes (e.g. `operations@acmelogistics.com`).  
  *(Target invariant: 1 Alias routes strictly to 1 Shared Mailbox to eliminate duplicate thread processing).*

---

## 5. Screen 4: Quarantine Oversight (`/admin/mail/quarantine`)

**Purpose:** Review flagged security threats (malware, phishing, severe spam) and permanently purge confirmed threats.

### Table Columns
| Column | Content | Action |
|---|---|---|
| **Sender** | `evil-phisher@spamsite.xyz` | View threat details |
| **Recipient** | `operations@acmelogistics.com` | Target shared mailbox |
| **Reason / Threat** | `Phishing (Score: 0.95) • SPF Fail` | Threat classification |
| **Quarantined At** | `2026-09-04 11:20` | Timestamp |
| **Actions** | `[Release to Queue]` `[Purge Permanently]` | Admin security remediation |
