# Aurora Platform — Tenant Admin Console API Catalog

> **Document ID:** `DOC-BFF-ADMIN`  
> **Status:** Canonical Specification (Synchronized with `Admin.Bff` C# Source)  
> **Scope:** HTTP REST APIs consumed exclusively by the **Aurora Admin Console** (`Admin.Bff`).  
> **Base Controller:** `[Authorize(Roles = "TENANT_ADMIN")]` via [AdminControllerBase.cs](file:///d:/IT/CD/aurora-server/src/dotnet/BFF/Admin.Bff/Controllers/AdminControllerBase.cs).  
> **Source Precedence:** Source Code & Protos > docs/technical/frontend > docs/bff-api > Figma UI Specs.

---

## 1. Tenant Admin API Catalog Table

| Method | Path | Purpose | Permission | Scope | Backend RPC | Implementation Status |
|---|---|---|---|---|---|:---:|
| `POST` | `/api/v1/admin/staff` | Invite new staff & assign baseline permissions | `iam:user:invite` | Current Tenant | `IamService.InviteUser` | `CURRENT` |
| `GET` | `/api/v1/admin/staff` | List staff directory with pagination | `iam:user:read` | Current Tenant | `IamService.GetManyUsers` | `CURRENT` |
| `GET` | `/api/v1/admin/staff/{id}` | Get staff member profile & capabilities | `iam:user:read` | Current Tenant | `IamService.GetUser` | `CURRENT` |
| `PUT` | `/api/v1/admin/staff/{id}` | Update staff profile (name, phone) | `iam:user:update` | Current Tenant | `IamService.UpdateUser` | `CURRENT` |
| `PATCH`| `/api/v1/admin/staff/{id}/role` | Change base persona role (`STAFF` ↔ `MANAGER` ↔ `TENANT_ADMIN`) | `iam:role:manage` | Current Tenant | `IamService.UpdateUserRole` | `CURRENT` |
| `PUT` | `/api/v1/admin/staff/{id}/permissions` | Overwrite direct capability permissions | `iam:permission:manage` | Current Tenant | `IamService.SetUserPermissions` | `CURRENT` |
| `POST` | `/api/v1/admin/staff/{id}/permissions/bulk-assign` | Add list of capability permissions | `iam:permission:manage` | Current Tenant | `IamService.BulkAssignPermissions` | `CURRENT` |
| `POST` | `/api/v1/admin/staff/{id}/permissions/bulk-revoke` | Remove list of capability permissions | `iam:permission:manage` | Current Tenant | `IamService.BulkRevokePermissions` | `CURRENT` |
| `DELETE`| `/api/v1/admin/staff/{id}` | Deactivate staff member | `iam:user:delete` | Current Tenant | `IamService.DeleteUser` | `CURRENT` |
| `GET` | `/api/v1/admin/roles` | List canonical base roles & default templates | `iam:role:read` | Platform Template | Static Catalog | `CURRENT` |
| `GET` | `/api/v1/admin/roles/{code}` | Get role definition & default template | `iam:role:read` | Platform Template | Static Catalog | `CURRENT` |
| `GET` | `/api/v1/admin/ai-configs/{feature}` | Get tenant AI automation policy for feature | `route_planning:policy:manage` | Current Tenant | `RoutePlanningService.GetTenantAiConfig` | `CURRENT` |
| `PUT` | `/api/v1/admin/ai-configs/{feature}` | Upsert tenant AI automation policy | `route_planning:policy:manage` | Current Tenant | `RoutePlanningService.UpsertTenantAiConfig` | `CURRENT` |
| `GET` | `/api/v1/admin/rule-configs` | List tenant route risk rule thresholds | `route_planning:policy:manage` | Current Tenant | `RoutePlanningService.ListTenantRuleConfigs` | `CURRENT` |
| `PUT` | `/api/v1/admin/rule-configs/{ruleName}` | Upsert threshold for a specific risk rule | `route_planning:policy:manage` | Current Tenant | `RoutePlanningService.UpsertTenantRuleConfig` | `CURRENT` |
| `POST` | `/api/v1/admin/mail/domains` | Provision arbitrary mail domain *(Legacy)* | `mail:domain:manage` | Current Tenant | `MailManagement.ProvisionDomain` | `CURRENT_LEGACY` *(Target requires System Admin provisioning)* |
| `GET` | `/api/v1/admin/mail/domains` | List assigned domains for current tenant | `mail:domain:manage` | Current Tenant | `MailManagement.ListDomains` | `TARGET (BACKEND_REQUIRED)` |
| `POST` | `/api/v1/admin/mail/mailboxes` | Create shared department mailbox | `mail:mailbox:manage` | Current Tenant | `MailManagement.CreateMailbox` | `CURRENT` |
| `POST` | `/api/v1/admin/mail/aliases` | Create inbound mail forwarding alias | `mail:mailbox:manage` | Current Tenant | `MailManagement.CreateAlias` | `CURRENT` |
| `POST` | `/api/v1/admin/mail/mailboxes/{id}/reset-password` | Reset mailbox password *(No-op in v1)* | `mail:mailbox:manage` | Current Tenant | `MailManagement.ResetPassword` | `CURRENT_NOOP` *(Removed from target UI)* |
| `DELETE`| `/api/v1/admin/mail/quarantine/{id}` | Permanently purge quarantined threat | `mail:quarantine:delete` | Current Tenant | `MailSecurity.DeleteQuarantine` | `CURRENT` |
| `GET` | `/api/v1/admin/mail/audit` | Query immutable mail security audit log | `mail:audit:read` | Current Tenant | `MailManagement.GetAuditRecords` | `CURRENT` |
| `POST` | `/api/v1/admin/ingestion/regulatory-sources` | Ingest regulatory source document | `compliance:platform:ingest` | Platform/Tenant | `RegulatoryComplianceService.IngestRegulatorySource` | `CURRENT` |
| `POST` | `/api/v1/admin/ingestion/knowledge-documents` | Ingest tenant SOP knowledge document | `compliance:platform:ingest` | Tenant SOP | `RegulatoryComplianceService.IngestKnowledgeDocument` | `CURRENT` |
| `GET` | `/api/v1/admin/audit-logs` | Query tenant security audit logs | `TENANT_ADMIN` role | Current Tenant | `AuditLogService.GetAdminAuditLogs` | `CURRENT` |

---

## 2. Granular Endpoint Specifications

### `POST /api/v1/admin/staff`
- **Purpose:** Invites a new staff member to the organization with assigned base role and direct capability permissions.
- **Permission:** `iam:user:invite`
- **Scope:** Current Tenant (`ICurrentUserService.TenantId`)
- **Backend RPC:** `IamService.InviteUser`
- **Request Body:**
  ```json
  {
    "firstName": "Alex",
    "lastName": "Nguyen",
    "email": "alex.nguyen@company.com",
    "phoneNumber": "+84901234567",
    "role": "STAFF",
    "applyDefaultPermissions": true,
    "permissions": ["shipments:read", "shipments:create", "mail:read", "mail:thread:claim"]
  }
  ```
- **Response (`201 Created`):**
  ```json
  {
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "firstName": "Alex",
    "lastName": "Nguyen",
    "email": "alex.nguyen@company.com",
    "phoneNumber": "+84901234567",
    "status": "INVITED",
    "role": "STAFF",
    "permissions": ["shipments:read", "shipments:create", "mail:read", "mail:thread:claim"],
    "permissionVersion": 1,
    "tenantId": "e5b8ba84-0000-0000-0000-000000000001",
    "createdAt": "2026-09-04T12:00:00Z"
  }
  ```

---

### `PATCH /api/v1/admin/staff/{id}/role`
- **Purpose:** Updates the user's base persona role without altering direct capabilities unless `applyDefaultPermissions` is explicitly true.
- **Permission:** `iam:role:manage`
- **Scope:** Current Tenant
- **Backend RPC:** `IamService.UpdateUserRole`
- **Request Body:**
  ```json
  {
    "role": "MANAGER",
    "applyDefaultPermissions": false
  }
  ```

---

### `PUT /api/v1/admin/ai-configs/{feature}`
- **Purpose:** Configures AI automation policy and provider per feature (e.g. `route_planning`).
- **Permission:** `route_planning:policy:manage`
- **Scope:** Current Tenant
- **Backend RPC:** `RoutePlanningService.UpsertTenantAiConfig`
- **Request Body:**
  ```json
  {
    "policy": "RulesAndLlm",
    "aiProvider": "Gemini",
    "isActive": true
  }
  ```

---

### `PUT /api/v1/admin/rule-configs/{ruleName}`
- **Purpose:** Tunes operational dispatch risk thresholds (e.g. `HeavyWeightRule`, `LargeVolumeRule`, `RouteStopCountRule`).
- **Permission:** `route_planning:policy:manage`
- **Scope:** Current Tenant
- **Backend RPC:** `RoutePlanningService.UpsertTenantRuleConfig`
- **Request Body:**
  ```json
  {
    "isEnabled": true,
    "thresholds": {
      "MaxWeightKg": 15000.0,
      "HighRiskScore": 75.0
    }
  }
  ```

---

### `POST /api/v1/admin/mail/mailboxes`
- **Purpose:** Creates a shared company mailbox under an assigned domain.
- **Permission:** `mail:mailbox:manage`
- **Scope:** Current Tenant
- **Backend RPC:** `MailManagement.CreateMailbox`
- **Request Body:**
  ```json
  {
    "domainId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "localPart": "operations",
    "userId": null
  }
  ```
- **Response (`201 Created`):**
  ```json
  {
    "mailboxId": "4ba85f64-5717-4562-b3fc-2c963f66afa6",
    "fullAddress": "operations@acmelogistics.com",
    "createdAt": "2026-09-04T12:00:00Z"
  }
  ```

---

### `POST /api/v1/admin/mail/aliases`
- **Purpose:** Creates an inbound email alias that forwards incoming mail to a canonical shared mailbox.
- **Permission:** `mail:mailbox:manage`
- **Scope:** Current Tenant
- **Backend RPC:** `MailManagement.CreateAlias`
- **Target Invariant:** 1 Alias routes to exactly 1 Shared Mailbox (no multi-target fan-out).
- **Request Body:**
  ```json
  {
    "domainId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "aliasAddress": "contact@acmelogistics.com",
    "targetAddresses": ["operations@acmelogistics.com"]
  }
  ```
