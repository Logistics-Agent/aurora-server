# Aurora Admin Mail UI — Product Context

> **Design Target:** Figma AI / UI Designer Reference Specification  
> **Source of Truth:** Audited against `.NET 10` MailService, `Admin.Bff`, `Staff.Bff`, `System.Bff`, `protos/mail_platform.proto`, and `PermissionConstants.cs`.

---

## 1. Product Summary

**Aurora** is an enterprise multi-tenant B2B SaaS logistics and freight execution platform. Each customer organization operates as an isolated **Tenant** with dedicated staff, custom roles, and isolated data boundaries.

The Aurora Mail Platform replaces uncoordinated personal inboxes with **Shared Company Mailboxes** (e.g. `ops@acmelogistics.com`, `customs@acmelogistics.com`). Inbound and outbound communications pass through a multi-stage security pipeline (SPF, DKIM, DMARC, ClamAV antivirus, SpamAssassin, AI phishing detection) before surfacing in operational queues.

The **Admin Mail UI** is the Tenant Administrator's control plane for configuring tenant mail resources—custom domains, shared department mailboxes, email forwarding aliases, security quarantine oversight, and compliance audit trails. It is strictly separated from day-to-day email triage (Staff Mail UI) and mail server infrastructure operations (Stalwart Admin UI).

---

## 2. Primary User

**Persona:** `TENANT_ADMIN` / Organization IT Administrator

### Responsibilities:
- Provision and configure custom email domains and retrieve generated DKIM DNS records.
- Create shared department mailboxes and manage forwarding aliases.
- Review emails flagged by security filters (malware, phishing, severe spam).
- Release false-positive emails to operational queues or permanently purge confirmed threats.
- Inspect the immutable audit log for tenant mail configuration and security events.

### Non-Responsibilities:
- Does **not** handle day-to-day email reading, thread claiming, drafting, or replying to customers.
- Does **not** configure low-level mail server infrastructure (SMTP listeners, IMAP/JMAP ports, TLS certs, cluster topology).

---

## 3. Admin vs Staff vs System Boundary

```text
┌─────────────────────────────────────────────────────────────────────────────┐
│                             SYSTEM_ADMIN                                    │
│  - Stalwart Server UI: SMTP/IMAP/JMAP listeners, TLS, clustering, storage   │
│  - Aurora System.Bff: Platform-wide audit, Dead-letter message recovery     │
└──────────────────────────────────────┬──────────────────────────────────────┘
                                       │
                                       ▼ Tenant Boundary
┌─────────────────────────────────────────────────────────────────────────────┐
│                       TENANT_ADMIN (Admin Mail UI)                          │
│  - Domain Provisioning & DKIM DNS records                                   │
│  - Shared Department Mailbox & Forwarding Alias provisioning                │
│  - Security Quarantine Review, Release & Permanent Purge                    │
│  - Tenant Mail Security & Configuration Audit Trail                         │
└──────────────────────────────────────┬──────────────────────────────────────┘
                                       │
                                       ▼ Operational Queue
┌─────────────────────────────────────────────────────────────────────────────┐
│                      STAFF / MANAGER (Staff Mail UI)                        │
│  - Shared Triage Queues (UNASSIGNED, MY_WORK, ALL)                          │
│  - Atomic Thread Claiming, Reassignment & Unassignment                      │
│  - Email Drafting, AI Negotiation Counter-offers & Outbound Sending         │
└─────────────────────────────────────────────────────────────────────────────┘
```

| Area | Admin Mail UI (`Admin.Bff`) | Staff Mail UI (`Staff.Bff`) | System UI / Stalwart Admin |
| :--- | :--- | :--- | :--- |
| **Domain Setup** | Provision domain, view DKIM TXT record | None | Global server domain routing |
| **Mailboxes** | Create shared mailbox, reset password | Select sender address on send | Server mailbox database storage |
| **Aliases** | Create forwarding rules | None | SMTP routing lookup |
| **Thread Triage** | None | Claim, Reassign, Unassign, Reply | None |
| **Quarantine** | List, inspect, release, **permanently delete** | List, inspect, **release only** | Raw server quarantine store |
| **Audit Logs** | Tenant-scoped configuration audit | None | Cross-tenant platform audit |
| **Dead-Letter Recovery** | None | None | Requeue failed pipeline messages |

---

## 4. Current Mail Domain Model

```text
Tenant (Organization)
  │
  ├── Domain (e.g. acmelogistics.com)
  │     ├── DKIM Selector & Generated DNS TXT Record
  │     └── Retention & Security Policies
  │
  ├── Mailbox (Shared Identity, e.g. ops@acmelogistics.com)
  │     │
  │     └── EmailThread (Business Work Item / Conversation)
  │           ├── PrimaryAssigneeUserId (Operational Responsibility)
  │           ├── Status: UNASSIGNED | IN_PROGRESS | WAITING_CUSTOMER | RESOLVED
  │           │
  │           ├── ProcessedMessage (Immutable Email Record)
  │           │     ├── SentByUserId (Human Accountability on Outbound)
  │           │     ├── SecurityCheckResults (ClamAV, SpamAssassin, AI BEC)
  │           │     └── Raw MIME (Stored on Cloudflare R2 / S3)
  │           │
  │           └── EmailDraft (Versioned Draft / AI Negotiation Proposal)
  │
  ├── Alias (e.g. freight@acmelogistics.com ──► [ops@..., sales@...])
  │
  └── QuarantineRecord (Flagged Security Threat)
        └── Linked to ProcessedMessage
```

---

## 5. Shared Mailbox Concept

- **Shared Identities Only**: In Aurora MVP, mailboxes represent organizational departments (`ops@`, `pricing@`, `customs@`, `sales@`).
- **No Individual Staff Mailboxes**: Individual employees do not hold personal IMAP mailboxes.
- **Traceable Attribution**:
  - Outbound emails display the shared mailbox address (e.g. `ops@acmelogistics.com`) as sender, while the backend records `SentByUserId` for auditability.
  - Inbound emails enter shared thread triage where staff claim ownership via `PrimaryAssigneeUserId`.

---

## 6. Admin Mail Permissions

Aurora uses strict **Capability-Based Access Control (CBAC)**. UI actions are governed by specific permission strings:

| Permission String | Admin Capability Granted | Gated UI Element / Action |
| :--- | :--- | :--- |
| `mail:domain:manage` | Provision custom email domains and retrieve DKIM records | **"+ Add Domain"** button, Domain setup drawer |
| `mail:mailbox:manage` | Create shared department mailboxes and email aliases | **"+ Create Mailbox"**, **"+ Add Alias"**, Reset Password |
| `mail:quarantine:read`| View quarantined email list, threat scores, and headers | Quarantine table, threat detail drawer |
| `mail:quarantine:release` | Release safe email from quarantine to shared thread queue | **"Release to Inbound"** button |
| `mail:quarantine:delete`  | Permanently purge quarantined threat record | **"Delete Permanently"** button & modal |
| `mail:audit:read` | View immutable tenant mail audit logs | Audit Log tab and filter controls |

> ⚠️ **Security Policy Rule**: `mail:system:manage` is reserved exclusively for `SYSTEM_ADMIN` and must **never** be exposed or required in the Tenant Admin Mail UI.

---

## 7. Admin Mail Information Architecture

```text
Admin Portal
  └── Mail Management (/admin/mail)
        ├── Overview (/admin/mail/overview)
        │     ├── Summary metrics (Domains, Shared Mailboxes, Aliases, Pending Quarantine)
        │     ├── Pending Quarantine threat widget
        │     └── Recent Administrative Audit activity widget
        │
        ├── Domains (/admin/mail/domains)
        │     ├── Domain list table (Status, DKIM Key Generated, Mailbox count)
        │     ├── "+ Add Domain" dialog / slide-out
        │     └── Domain DNS Instructions Drawer (DKIM TXT, SPF, DMARC guidance)
        │
        ├── Shared Mailboxes (/admin/mail/mailboxes)
        │     ├── Mailbox list table (Address, Domain, Status, Creation Date)
        │     ├── "+ Create Mailbox" dialog
        │     └── Mailbox Credentials / Password Reset dialog
        │
        ├── Aliases (/admin/mail/aliases)
        │     ├── Alias list table (Alias Address, Target Recipients, Domain)
        │     └── "+ Add Alias" dialog
        │
        ├── Security Quarantine (/admin/mail/quarantine)
        │     ├── Threat list table (Received, Sender, Recipient, Reason, Threat Score, Status)
        │     ├── Quarantined Message Inspection Drawer (Headers, Security Stage breakdown, Safe Body Preview)
        │     ├── "Release to Queue" confirmation dialog
        │     └── "Permanent Delete" destructive confirmation modal
        │
        └── Audit Trail (/admin/mail/audit)
              ├── Audit log table (Timestamp, Actor, Action, Resource Type, Resource ID, Result)
              └── Audit Detail JSON inspection drawer
```

---

## 8. Admin Business Flows

### Flow 1: Custom Domain Provisioning & DKIM Key Generation
1. Admin opens **Domains** (`/admin/mail/domains`) and clicks **"+ Add Domain"**.
2. Admin enters `DomainName` (e.g. `acmelogistics.com`), `MaxMailboxCount`, and `RetentionDays`.
3. Admin clicks **"Provision Domain"** (`POST /api/v1/admin/mail/domains`).
4. System returns generated `DkimSelector` (e.g. `aurora-2025`) and `DkimTxtRecord`.
5. UI presents a **DNS Setup Modal** with copyable DNS TXT records for DKIM, SPF, and DMARC.
6. Domain status displays as **Active / DKIM Generated (DNS Publication Required)**.

### Flow 2: Shared Mailbox Creation
1. Admin opens **Shared Mailboxes** (`/admin/mail/mailboxes`) and clicks **"+ Create Mailbox"**.
2. Admin selects an active domain from dropdown and inputs `LocalPart` (e.g. `operations`).
3. Admin submits form (`POST /api/v1/admin/mail/mailboxes`).
4. Backend provisions account on Stalwart and creates `Mailbox` record (`operations@acmelogistics.com`).
5. Mailbox immediately appears in the shared mailbox inventory.

### Flow 3: Quarantine Threat Inspection, Release & Purge
1. Security pipeline flags an inbound email with severe spam (`SpamScore >= 10.0`), malware, or AI phishing score (`PhishingScore >= 0.70`).
2. Email is stored in R2, marked `IsQuarantined = true`, and a `QuarantineRecord` is created in state `Pending`.
3. Admin views **Security Quarantine** (`/admin/mail/quarantine`), clicks a flagged row, and opens the **Threat Inspection Drawer**.
4. Drawer displays sanitized metadata, detection stages (ClamAV / SpamAssassin / AI Phishing), and safe sandboxed text preview (scripts/HTML disabled).
5. **If False Positive**: Admin clicks **"Release to Inbound"** (`POST /api/v1/mail/quarantine/{id}/release`). Pipeline re-injects message into the shared thread queue (`UNASSIGNED`).
6. **If Confirmed Threat**: Admin clicks **"Delete Permanently"** (`DELETE /api/v1/admin/mail/quarantine/{id}`). Record is purged and action is recorded in `AuditRecord`.

---

## 9. Security Rules for Admin UI

1. **Sandboxed Message Previews**: Quarantined email body previews must disable script execution, active HTML objects, and external image rendering by default to prevent admin compromise.
2. **Raw Score Presentation**: Display SpamAssassin scores as raw numerical values (e.g. `12.4`) against domain thresholds (`5.0` Tag / `10.0` Reject), not artificial `/10` percentages.
3. **DKIM Verification Distinction**: Display status as `"DKIM Key Generated"`, not `"DKIM Verified"`, until external DNS resolver validation is implemented.
4. **Actor Attribution Fallback**: Audit logs contain `ActorId` (UUID) and `ActorType` (e.g. `TenantAdmin`, `System`). UI must display `ActorType (Short-ID)` when user display names cannot be resolved from IAM.

---

## 10. MVP Scope (Current Supported vs Target)

| Feature Area | MVP Status | UI Handling / Figma Note |
| :--- | :--- | :--- |
| **Domain Provisioning** | `SUPPORTED_CURRENTLY` | Full form with DKIM DNS TXT display. |
| **Shared Mailbox Creation** | `SUPPORTED_CURRENTLY` | Form with domain selection and local part. |
| **Forwarding Alias Creation** | `SUPPORTED_CURRENTLY` | Form with target email chip inputs. |
| **Quarantine List & Details** | `SUPPORTED_CURRENTLY` | Full table, filters, threat breakdown drawer. |
| **Quarantine Release** | `SUPPORTED_CURRENTLY` | Single-click release to operational queue. |
| **Quarantine Permanent Delete**| `SUPPORTED_CURRENTLY` | Destructive modal with confirmation. |
| **Audit Trail** | `SUPPORTED_CURRENTLY` | Filterable table with JSON detail drawer. |
| **Domain List / Detail API** | `TARGET_SUPPORTED_BUT_API_MISSING` | Design standard list/detail; mock frontend state until backend query RPC is added. |
| **Mailbox List / Detail API**| `TARGET_SUPPORTED_BUT_API_MISSING` | Design standard list/detail table; mock frontend state until backend query RPC is added. |
| **Alias List / Delete API** | `TARGET_SUPPORTED_BUT_API_MISSING` | Design standard list table; mock frontend state until backend query RPC is added. |
| **Mailbox Suspension Toggle** | `TARGET_SUPPORTED_BUT_API_MISSING` | Status badge supported; toggle action pending backend endpoint. |

---

## 11. Backend / API Gaps

The current backend implementation provides robust create/action endpoints but has query gaps in `Admin.Bff` / `mail_platform.proto`:

1. **Missing Query RPCs**:
   - `ListDomains` / `GetDomain` (Proto currently only defines `ProvisionDomain`).
   - `ListMailboxes` / `GetMailbox` (Proto currently only defines `CreateMailbox`).
   - `ListAliases` / `DeleteAlias` (Proto currently only defines `CreateAlias`).
2. **Client State Strategy**: UI designs should represent these standard list tables. Frontend implementations may populate these via initial cached create results or await standard query endpoints in v1.1.

---

## 12. Policy Review Items

1. **Domain Creation Authorization**: In v1, Tenant Admins can provision any FQDN. Production hardening should add DNS ownership challenge (e.g. `_aurora-challenge` TXT record verification) before activating outbound relay.
2. **Mailbox Quota Limits**: Domain entity defines `MaxMailboxCount` (default: 100). UI should display quota progress bar (e.g. `12 / 50 Mailboxes Used`).
