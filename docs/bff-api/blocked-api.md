# Aurora Platform — Blocked & Target Backend Gap Inventory

> **Document ID:** `DOC-BFF-BLOCKED`  
> **Status:** Canonical Gap & Target Architecture Inventory  
> **Scope:** Capabilities currently blocked from frontend integration or requiring backend contract refactoring.  
> **Rule:** Distinguish `BACKEND_REQUIRED`, `TARGET_CHANGE_REQUIRED`, and `CURRENT_LEGACY`.

---

## 1. Backend Gap & Target Architecture Table

| Desired Endpoint / Capability | Persona / Role | Required Backend Change | Status | Priority |
|---|---|---|:---:|:---:|
| `GET /api/v1/admin/mail/domains` | `TENANT_ADMIN` | Add `ListDomains` RPC to `mail_platform.proto` and `MailService` to allow Tenant Admin to view assigned domains. | `BACKEND_REQUIRED` | **P0** |
| `POST /api/v1/system/mail/domains/assign` | `SYSTEM_ADMIN` | Add domain assignment RPC to `mail_platform.proto` and `System.Bff` for platform domain allocation. | `BACKEND_REQUIRED` | **P0** |
| `TenantMailConfig.DefaultMailboxId` | System / Admin | Add `DefaultMailboxId` or `Mailbox.IsDefault` to identify tenant's primary operational email intake. | `BACKEND_REQUIRED` | **P0** |
| `Alias 1:1 Canonical Target` | `TENANT_ADMIN` | Restrict `Alias.Targets` from multi-target fan-out (`List<string>`) to single canonical `MailboxId`. | `TARGET_CHANGE_REQUIRED` | **P1** |
| `POST /api/v1/admin/mail/domains` | `TENANT_ADMIN` | Deprecate arbitrary tenant domain creation in favor of System Admin provisioning & assignment. | `CURRENT_LEGACY` | **P1** |
| `PUT /api/v1/system/tenants/{id}` | `SYSTEM_ADMIN` | Add `UpdateTenant` RPC to `protos/iam_tenant.proto` and `IamGrpcService.cs`. | `BACKEND_REQUIRED` | **P1** |
| `POST /api/v1/invoices/{id}/payments` | `STAFF`, `MANAGER` | Add `RecordPayment` RPC in `protos/billing.proto`. | `BACKEND_REQUIRED` | **P1** |
| `POST /api/v1/invoices/{id}/cancel` | `MANAGER`, `ADMIN` | Add `CancelInvoice` RPC in `protos/billing.proto`. | `BACKEND_REQUIRED` | **P1** |
| `POST /api/v1/invoices/{id}/debit-notes` | `MANAGER`, `ADMIN` | Add `IssueDebitNote` RPC in `protos/billing.proto`. | `BACKEND_REQUIRED` | **P2** |
| `POST /api/v1/invoices/{id}/credit-notes`| `MANAGER`, `ADMIN` | Add `IssueCreditNote` RPC in `protos/billing.proto`. | `BACKEND_REQUIRED` | **P2** |
| `GET /api/v1/financial/exchange-rate` | `STAFF`, `MANAGER` | Add `GetExchangeRate` RPC in `protos/financial.proto`. | `BACKEND_REQUIRED` | **P2** |

---

## 2. Mail Architecture Gaps Detail

### 1. Mail Domain Assignment & Listing
- **Current State:** `Admin.Bff` allows `TENANT_ADMIN` to invoke `POST /api/v1/admin/mail/domains` with any FQDN.
- **Target Policy:** `SYSTEM_ADMIN` provisions domains on Stalwart and assigns them to tenants via `System.Bff`. `TENANT_ADMIN` in `Admin.Bff` only reads assigned domains (`GET /api/v1/admin/mail/domains`) and configures mailboxes/aliases within them.
- **Required Action:**
  - Add `ListDomainsRequest` / `ListDomainsResponse` to `mail_platform.proto`.
  - Implement `ListDomains` in `MailService` and expose `GET /api/v1/admin/mail/domains` in `Admin.Bff`.
  - Deprecate `POST /api/v1/admin/mail/domains` in `Admin.Bff`.

### 2. Default Operational Shared Mailbox
- **Current State:** `Mailbox` entity contains no default flag. `TenantMailConfig` does not exist in backend.
- **Target Policy:** Each tenant must possess exactly one Default Operational Shared Mailbox (e.g. `operations@acmelogistics.com`) as primary customer intake.
- **Required Action:**
  - Recommended aggregate: Add `DefaultMailboxId` (Guid?) on `TenantMailConfig` or `IsDefault` boolean on `Mailbox`.

### 3. Single-Target Alias Semantics
- **Current State:** `Alias` entity has `List<string> Targets` and proto has `repeated string target_addresses`.
- **Target Policy:** 1 Alias routes to exactly 1 canonical Shared Mailbox to avoid message fan-out and duplicate thread processing.
- **Required Action:**
  - Update `CreateAliasRequestValidator` and database schema to enforce 1:1 alias-to-mailbox mapping.
