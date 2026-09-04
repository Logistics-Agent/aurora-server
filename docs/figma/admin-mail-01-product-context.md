# Aurora Admin Mail UI — Product Context

> **Design Target:** Figma AI / UI Designer Reference Specification  
> **Source of Truth:** Audited against `.NET 10` `MailService`, `Admin.Bff`, `Staff.Bff`, `System.Bff`, `protos/mail_platform.proto`, and `PermissionConstants.cs`.

---

## 1. Product Summary

**Aurora** is an enterprise multi-tenant B2B SaaS logistics and freight execution platform. Each customer organization operates as an isolated **Tenant** with dedicated staff, custom roles, and isolated data boundaries.

The Aurora Mail Platform replaces uncoordinated personal inboxes with **Shared Company Mailboxes** (e.g. `operations@acmelogistics.com`, `customs@acmelogistics.com`). Inbound and outbound communications pass through a multi-stage security pipeline (SPF, DKIM, DMARC, ClamAV antivirus, SpamAssassin, AI phishing detection) before surfacing in operational queues.

The **Mail Administration Module** is an integrated section within the **Aurora Admin Console** (`/admin/mail/*`). It is the control plane for the Tenant Administrator (`TENANT_ADMIN`) to view assigned email domains, manage shared company mailboxes, configure 1:1 email forwarding aliases, review security quarantine threats, and inspect compliance audit trails.

---

## 2. Mail Domain & Mailbox Model

```text
Tenant (Acme Logistics)
  │
  ├── 1..N Assigned Domains (e.g. acmelogistics.com)
  │     ├── Provisioned & Assigned by SYSTEM_ADMIN (Stalwart Server UI / System API)
  │     └── Viewed in Aurora Admin Console with DKIM DNS instructions
  │
  ├── 1..N Shared Mailboxes
  │     ├── EXACTLY ONE Default Operational Shared Mailbox (operations@acmelogistics.com)
  │     │     └── Primary customer inquiry intake
  │     └── 0..N Specialized Mailboxes (customs@acmelogistics.com, pricing@acmelogistics.com)
  │
  ├── 0..N Forwarding Aliases (e.g. contact@acmelogistics.com ──► operations@acmelogistics.com)
  │     └── Target Invariant: Exactly 1 target shared mailbox (no fan-out)
  │
  └── Quarantine Threat Records
        └── Flagged inbound threats awaiting review or permanent purge
```

---

## 3. Key Design Invariants

1. **Integrated Module in Aurora Admin**: Admin Mail is not a separate application. It lives under `Mail Administration` in the Aurora Admin sidebar.
2. **Domain Ownership Policy**:
   - `SYSTEM_ADMIN` provisions mail domains in Stalwart and assigns them to tenants.
   - `TENANT_ADMIN` views assigned domains and DNS verification records.
   - Arbitrary domain creation (`+ Add Domain`) is disallowed in target UX.
3. **Default Operational Mailbox**:
   - Every tenant maintains exactly one **Default Shared Mailbox** for incoming inquiries that do not match specialized routing rules.
4. **Single-Target Alias Semantics**:
   - An alias is an alternate public address (e.g. `sales@`, `info@`) that routes strictly to **one canonical shared mailbox**. Aliases do not have independent queues or logins.
5. **No Mailbox Passwords in UI**:
   - Humans authenticate via AWS Cognito OIDC. Mailbox accounts are internal Stalwart routing identities. Ordinary Admin UI does not expose mailbox password reset buttons.
