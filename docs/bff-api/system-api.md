# Aurora Platform — System Admin Control Plane API Catalog

> **Document ID:** `DOC-BFF-SYS`  
> **Status:** Canonical Specification (Synchronized with `System.Bff` C# Source)  
> **Scope:** HTTP REST APIs consumed exclusively by the **Aurora System Control Plane** (`System.Bff`).  
> **Base Controller:** `[Authorize(Roles = "SYSTEM_ADMIN")]` via [SystemControllerBase.cs](file:///d:/IT/CD/aurora-server/src/dotnet/BFF/System.Bff/Controllers/SystemControllerBase.cs).  
> **Source Precedence:** Source Code & Protos > docs/technical/frontend > docs/bff-api > Figma UI Specs.

---

## 1. System Control Plane APIs Table

| Module | Method | Path | Purpose | Required Permission / Role | Backend RPC | Status |
|---|---|---|---|---|---|:---:|
| **Tenant** | `POST` | `/api/v1/system/tenants` | Onboard new enterprise tenant | `SYSTEM_ADMIN` role | `IamService.CreateTenant` | `CURRENT` |
| **Tenant** | `GET` | `/api/v1/system/tenants` | List all platform tenants | `SYSTEM_ADMIN` role | `IamService.GetManyTenants` | `CURRENT` |
| **Tenant** | `GET` | `/api/v1/system/tenants/{id}` | Get tenant details & subscription | `SYSTEM_ADMIN` role | `IamService.GetTenant` | `CURRENT` |
| **Tenant** | `PUT` | `/api/v1/system/tenants/{id}/status` | Activate/suspend tenant organization | `SYSTEM_ADMIN` role | `IamService.UpdateTenantStatus` | `CURRENT` |
| **Tenant** | `POST` | `/api/v1/system/tenants/{id}/admin` | Provision first Tenant Administrator | `SYSTEM_ADMIN` role | `IamService.CreateTenantAdmin` | `CURRENT` |
| **Mail Infra** | `POST` | `/api/v1/system/mail/dead-letter/{id}/requeue` | Requeue failed pipeline email | `mail:system:manage` | `MailManagement.RequeueDeadLetter` | `CURRENT` |
| **Mail Infra** | `GET` | `/api/v1/system/mail/audit` | Platform-wide mail audit trail | `SYSTEM_ADMIN` role | `MailManagement.GetAuditRecords` | `CURRENT` |
| **Mail Infra** | `POST` | `/api/v1/system/mail/domains/assign` | Assign pre-configured domain to tenant | `mail:system:domain:assign` | `MailManagement.AssignDomain` | `TARGET (BACKEND_REQUIRED)` |
| **Mail Infra** | `GET` | `/api/v1/system/mail/domains` | List all domains across all tenants | `SYSTEM_ADMIN` role | `MailManagement.ListAllDomains` | `TARGET (BACKEND_REQUIRED)` |
| **Ingestion** | `POST` | `/api/v1/system/ingestion/regulatory-sources` | Ingest national/global legal statutes (`PLATFORM` scope) | `compliance:platform:ingest` | `RegulatoryComplianceService.IngestRegulatorySource` | `CURRENT` |
| **Ingestion** | `POST` | `/api/v1/system/ingestion/knowledge-documents` | Ingest global knowledge bases (`PLATFORM` scope) | `compliance:platform:ingest` | `RegulatoryComplianceService.IngestKnowledgeDocument` | `CURRENT` |
| **Audit** | `GET` | `/api/v1/system/audit-logs` | Platform-wide security audit logs | `SYSTEM_ADMIN` role | `AuditLogService.GetAdminAuditLogs` | `CURRENT` |

---

## 2. Mail Domain Ownership Target Architecture

```text
SYSTEM_ADMIN
    ↓
Stalwart Admin UI / System API
    ↓
1. Provision/Configure Mail Domain & DKIM in Stalwart
2. Assign Domain to Aurora Tenant (POST /api/v1/system/mail/domains/assign)
    ↓
TENANT_ADMIN (Aurora Admin Console)
    ↓
1. View Assigned Domains (GET /api/v1/admin/mail/domains)
2. Provision Shared Mailboxes & Forwarding Aliases under assigned domain
3. MUST NOT provision arbitrary unassigned domains
```
