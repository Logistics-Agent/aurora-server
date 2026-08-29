# Aurora Admin Mail UI — Product Context

> **Purpose:** Design input for Figma AI / Figma Make. Describes what the product is, who it serves, what Admin can and cannot do, and the key objects and flows.

---

## 1. Product Summary

Aurora is an enterprise B2B logistics platform serving freight forwarders, customs brokers, and carrier organizations. Each customer organization operates as an isolated **Tenant** with its own staff, data, and configuration.

Aurora's mail system uses **shared company mailboxes** (e.g. `sales@company.com`, `operations@company.com`) rather than personal Gmail-style inboxes. Inbound and outbound email passes through a multi-stage security pipeline (SPF, DKIM, DMARC, ClamAV, SpamAssassin, AI phishing detection) before reaching staff.

The **Aurora Admin Mail UI** is the tenant administrator's interface for managing mail resources — domains, shared mailboxes, aliases, quarantine review, and audit logs. It does **not** handle day-to-day email reading/composing (that is the Staff Mail UI) or mail server infrastructure (that is Stalwart Admin UI).

All data is tenant-scoped. Admin never sees data from other tenants.

---

## 2. User

**Primary User:** `TENANT_ADMIN`

Responsibilities:
- Provision and manage tenant mail domains
- Create and manage shared company mailboxes
- Create and manage email aliases (forwarding rules)
- Review quarantined email flagged by security pipeline
- Release safe quarantined messages or permanently delete threats
- Review audit trail of mail-related administrative actions

Admin does **not** directly read, compose, or reply to customer emails. Admin does **not** configure SMTP listeners, IMAP/JMAP, server clustering, or TLS certificates.

---

## 3. Product Boundary

| Belongs to Aurora Admin Mail UI | Does NOT belong |
| --- | --- |
| Domain provisioning & DKIM setup | SMTP / IMAP / JMAP listener config |
| Shared mailbox creation & status management | Server clustering & storage config |
| Email alias management | Global TLS certificates |
| Quarantine list, detail, release, delete | Server queue management |
| Mail audit log viewing | Staff inbox (read / compose / reply) |
| Domain security thresholds (view) | Thread claim / reassign / unassign |
| | Draft management |
| | Outbound email sending |
| | AI suggested reply / negotiation |
| | Stalwart Admin UI features |

---

## 4. Core Domain Model

| Entity | Key UX Fields | Purpose |
| --- | --- | --- |
| **Domain** | `DomainName`, `Status` (Active/Suspended), `DkimSelector`, `DkimTxtRecord`, `MaxMailboxCount`, `RetentionDays` | Tenant's email domain identity |
| **Mailbox** | `FullAddress`, `LocalPart`, `DomainId`, `Status` (Active/Suspended/Deleted) | Shared company mailbox account |
| **Alias** | `AliasAddress`, `Targets[]`, `DomainId` | Email forwarding rule |
| **QuarantineRecord** | `MessageId`, `QuarantineReason`, `QuarantinedAt`, `Status` (Pending/Released/Deleted), `ReviewedBy`, `ReviewedAt` | Flagged email pending admin review |
| **AuditRecord** | `ActorId`, `ActorType`, `Action`, `ResourceType`, `ResourceId`, `Timestamp`, `Result`, `DetailJson` | Immutable action log |
| **ProcessedMessage** | `SenderAddress`, `RecipientAddresses`, `Subject`, `SpamScore`, `PhishingScore`, `Direction`, `SecurityCheckResults[]` | Email that passed through security pipeline (referenced by quarantine) |

---

## 5. Mailbox Concept

Mailboxes in Aurora MVP are **shared company identities**, not personal inboxes.

Examples:
```
operations@aurora-logistics.com
sales@aurora-logistics.com
support@aurora-logistics.com
```

A mailbox is a communication channel associated with a domain. Individual staff identity is tracked separately via `SentByUserId` on outbound messages and `PrimaryAssigneeUserId` on threads.

There are **no personal mailboxes** in the current model.

> **Note:** `MailboxMember` / shared access model does **not exist** in current source. If Figma designs membership management screens, mark them as `BACKEND NOT YET IMPLEMENTED`.

---

## 6. Permission Model

Permissions follow a capability-based model. Each permission string represents an action the actor can perform, scoped to the actor's tenant.

**Admin Mail permissions (from `PermissionConstants.cs`):**

| Permission | Grants |
| --- | --- |
| `mail:domain:manage` | Provision domains, view domain config |
| `mail:mailbox:manage` | Create mailboxes, create aliases, reset password |
| `mail:quarantine:read` | View quarantine list and details |
| `mail:quarantine:release` | Release quarantined messages |
| `mail:quarantine:delete` | Permanently delete quarantined messages |
| `mail:audit:read` | View mail audit log |

**Not exposed in Admin Mail UI:**

| Permission | Reason |
| --- | --- |
| `mail:system:manage` | Platform-level only (Stalwart) |
| `mail:read`, `mail:send`, `mail:draft:create` | Staff operational permissions |
| `mail:thread:claim/reassign/unassign` | Staff/Manager thread workflow |

`TENANT_ADMIN` role receives all permissions except `mail:system:manage` and `compliance:platform:ingest`.

Frontend permission checks are **UX convenience only**. Backend remains the authorization authority.

---

## 7. Information Architecture

```
Mail
├── Overview          — Dashboard with summary stats and recent activity
├── Domains           — List and provision tenant email domains
├── Mailboxes         — List and create shared company mailboxes
├── Aliases           — List and create email forwarding aliases
├── Quarantine        — Review, release, or delete flagged emails
└── Audit             — Browse mail security audit trail
```

---

## 8. Main Admin Flows

### Flow 1: View Mail Overview
1. Admin navigates to Mail section
2. Dashboard shows summary cards (domain count, active mailboxes, alias count, pending quarantine)
3. Recent quarantine entries displayed
4. Recent audit activity displayed

### Flow 2: Provision Domain
1. Admin navigates to Domains
2. Clicks "Add Domain"
3. Enters domain FQDN, optional max mailbox count, retention days
4. System provisions domain and generates DKIM TXT record
5. Admin copies DKIM TXT record to configure DNS
6. Domain appears in list

### Flow 3: Create Shared Mailbox
1. Admin navigates to Mailboxes
2. Clicks "Create Mailbox"
3. Selects target domain, enters local part (e.g. `operations`)
4. Preview shows full address: `operations@company.com`
5. Mailbox created and appears in list

### Flow 4: Create Alias
1. Admin navigates to Aliases
2. Clicks "Create Alias"
3. Selects domain, enters alias address, enters target address(es)
4. Alias created and appears in list

### Flow 5: Review Quarantine
1. Admin navigates to Quarantine
2. Filters by status (Pending by default)
3. Clicks a record to open detail drawer
4. Reviews sender, subject, quarantine reason, spam/phishing scores, security checks
5. Chooses to Release (confirm dialog) or Delete Permanently (destructive confirm dialog)

### Flow 6: Review Audit
1. Admin navigates to Audit
2. Optionally filters by resource type, resource ID, date range
3. Browses paginated audit records
4. Clicks a record to view detail in drawer

---

## 9. Key Business Rules

- Admin scope is strictly tenant-isolated; no cross-tenant visibility
- No Stalwart super-admin credentials or server config in Aurora UI
- Mailbox is a shared company identity, not a personal inbox
- Frontend permission visibility is UX convenience; backend enforces authorization
- Quarantine delete is **destructive and irreversible**
- Quarantine release sends the message for delivery/processing
- `ResetPassword` is currently a **stub** — authentication delegated to Cognito OIDC
- Domain provisioning triggers DKIM key generation on Stalwart; admin must manually publish DNS TXT record
- `mail:domain:manage` does **not** grant Stalwart infrastructure access

> **⚠ BACKEND/POLICY REVIEW:** Domain provisioning (`POST /admin/mail/domains`) currently calls Stalwart management API directly. This means Admin can provision domains on the mail server. This behavior exists in source but may need policy review for production.

---

## 10. MVP Scope

### INCLUDE
- Overview dashboard
- Domains (provision + list view)
- Shared Mailboxes (create + list view)
- Aliases (create + list view)
- Quarantine (list, detail, release, delete)
- Audit (list, detail, filter)

### EXCLUDE
- Staff Inbox / email reading
- Thread Claim / Reassign / Unassign
- Draft compose / send
- SLA tracking / Breached
- Teams / Queues / Collaborators
- Auto Assignment
- Personal Mailboxes
- Mailbox membership management (entity not yet implemented)
- Mail server configuration (SMTP, IMAP, JMAP, cluster, TLS)
- Domain security threshold editing (view only if backend supports)

### Backend Gaps Affecting UI

| Feature | Gap |
| --- | --- |
| List Domains | No `GET /admin/mail/domains` endpoint exists |
| List Mailboxes | No `GET /admin/mail/mailboxes` endpoint exists |
| List Aliases | No `GET /admin/mail/aliases` endpoint exists |
| Get Domain Detail | No `GET /admin/mail/domains/{id}` endpoint exists |
| Get Mailbox Detail | No `GET /admin/mail/mailboxes/{id}` endpoint exists |
| Suspend/Activate Mailbox | No `PATCH /admin/mail/mailboxes/{id}/status` endpoint exists |
| Delete Alias | No `DELETE /admin/mail/aliases/{id}` endpoint exists |
| List Quarantine (Admin) | Exists only in Staff.Bff; not yet in Admin.Bff |
| Get Quarantine (Admin) | Exists only in Staff.Bff; not yet in Admin.Bff |
| Release Quarantine (Admin) | Exists only in Staff.Bff; not yet in Admin.Bff |

> Figma should design all screens as specified. Backend endpoints will be added to match the UI. Design represents the **target state**.
