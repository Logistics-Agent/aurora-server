# Aurora Platform - Admin Exclusive API Catalog (ADMIN_ONLY)

> **Document ID:** `DOC-BFF-ADMIN`  
> **Status:** Canonical Specification Complete  
> **Scope:** HTTP REST APIs exclusively accessible by the `ADMIN` role (`Admin.Bff`).  
> **Base Controller:** `[Authorize(Roles = "TENANT_ADMIN")]` via [AdminControllerBase.cs](file:///D:/IT/CD/aurora-server/src/dotnet/BFF/Admin.Bff/Controllers/AdminControllerBase.cs).

---

## 1. Admin Exclusive API Table

| Method | Endpoint | Function | Service | RPC | Main Source File |
| :--- | :--- | :--- | :--- | :--- | :--- |
| `POST` | `/api/v1/admin/staff/invite` | Invite New Staff User | `IamTenant` | `IamService.InviteUser` | `Admin.Bff/Controllers/StaffController.cs` |
| `GET` | `/api/v1/admin/staff` | List Tenant Staff Directory | `IamTenant` | `IamService.GetManyUsers` | `Admin.Bff/Controllers/StaffController.cs` |
| `PUT` | `/api/v1/admin/staff/{id}` | Update Staff Profile | `IamTenant` | `IamService.UpdateUser` | `Admin.Bff/Controllers/StaffController.cs` |
| `POST` | `/api/v1/admin/staff/{id}/activate` | Reactivate Suspended Staff | `IamTenant` | `IamService.ActivateUser` | `Admin.Bff/Controllers/StaffController.cs` |
| `POST` | `/api/v1/admin/staff/{id}/suspend` | Suspend Staff Account | `IamTenant` | `IamService.SuspendUser` | `Admin.Bff/Controllers/StaffController.cs` |
| `POST` | `/api/v1/admin/staff/{id}/reset-password` | Trigger Admin Password Reset | `IamTenant` | `IamService.ResetUserPassword` | `Admin.Bff/Controllers/StaffController.cs` |
| `POST` | `/api/v1/admin/staff/{id}/roles` | Assign Roles to User | `IamTenant` | `IamService.AssignRoles` | `Admin.Bff/Controllers/StaffController.cs` |
| `GET` | `/api/v1/admin/roles` | List Tenant System Roles | `IamTenant` | `IamService.GetManyRoles` | `Admin.Bff/Controllers/RolesController.cs` |
| `GET` | `/api/v1/admin/roles/{id}` | Get Role Details & Permissions | `IamTenant` | `IamService.GetRole` | `Admin.Bff/Controllers/RolesController.cs` |
| `POST` | `/api/v1/admin/roles/{id}/permissions` | Update Role Permission Grants | `IamTenant` | `IamService.AssignPermissionsToRole` | `Admin.Bff/Controllers/RolesController.cs` |
| `GET` | `/api/v1/admin/ai-config` | Get Tenant AI Provider Policy | `RoutePlanningAgent` | `RoutePlanningService.GetTenantAiConfig` | `Admin.Bff/Controllers/AiConfigController.cs` |
| `PUT` | `/api/v1/admin/ai-config` | Update Tenant AI Provider Policy | `RoutePlanningAgent` | `RoutePlanningService.UpsertTenantAiConfig` | `Admin.Bff/Controllers/AiConfigController.cs` |
| `GET` | `/api/v1/admin/rules` | List Tenant Dispatch Rules | `RoutePlanningAgent` | `RoutePlanningService.ListTenantRuleConfigs` | `Admin.Bff/Controllers/RuleConfigController.cs` |
| `PUT` | `/api/v1/admin/rules` | Configure Tenant Dispatch Rule | `RoutePlanningAgent` | `RoutePlanningService.UpsertTenantRuleConfig` | `Admin.Bff/Controllers/RuleConfigController.cs` |
| `POST` | `/api/v1/admin/mail/domains` | Provision Tenant Mail Domain | `MailService` | `MailManagement.ProvisionDomain` | `Admin.Bff/Controllers/MailAdminController.cs` |
| `POST` | `/api/v1/admin/mail/mailboxes` | Create User Mailbox | `MailService` | `MailManagement.CreateMailbox` | `Admin.Bff/Controllers/MailAdminController.cs` |
| `POST` | `/api/v1/admin/mail/aliases` | Create Inbound Mail Alias | `MailService` | `MailManagement.CreateAlias` | `Admin.Bff/Controllers/MailAdminController.cs` |
| `DELETE`| `/api/v1/admin/mail/quarantine/{id}` | Permanently Purge Quarantined Mail | `MailService` | `MailSecurity.DeleteQuarantine` | `Admin.Bff/Controllers/MailAdminController.cs` |
| `GET` | `/api/v1/admin/mail/audit` | Get Tenant Security Audit Log | `MailService` | `MailManagement.GetAuditRecords` | `Admin.Bff/Controllers/MailAdminController.cs` |
| `POST` | `/api/v1/admin/compliance/sources` | Ingest Tenant-Scoped Regulatory Doc | `RegulatoryCompliance`| `RegulatoryComplianceService.IngestRegulatorySource` | `Admin.Bff/Controllers/PlatformIngestionController.cs` |
| `POST` | `/api/v1/admin/compliance/knowledge` | Ingest Tenant SOP Knowledge Doc | `RegulatoryCompliance`| `RegulatoryComplianceService.IngestKnowledgeDocument` | `Admin.Bff/Controllers/PlatformIngestionController.cs` |

---

## 2. Granular API Specifications (Samples)

### `POST /api/v1/admin/staff/invite`
- **Function:** Invites a new staff member to the organization, creating a local IAM record and Cognito account.
- **Role:** `ADMIN_ONLY`
- **Tenant Scope:** Strict Tenant Isolation (`ICurrentUserService.TenantId`)
- **Backend Service:** `IamTenant`
- **RPC:** `IamService.InviteUser`
- **Request:**
  ```json
  {
    "email": "dispatcher@carrier.com",
    "name": "Alex Mercer",
    "staffType": 1,
    "roleIds": ["5fa85f64-5717-4562-b3fc-2c963f66afa6"]
  }
  ```
- **Response:**
  ```json
  {
    "id": "1fa85f64-5717-4562-b3fc-2c963f66afa6",
    "email": "dispatcher@carrier.com",
    "fullName": "Alex Mercer",
    "status": "INVITED",
    "roles": ["OPERATOR"],
    "createdAt": "2026-08-24T12:00:00.000Z"
  }
  ```
- **Source Flow:**
  ```text
  Admin.Bff (POST /api/v1/admin/staff/invite)
      -> GrpcClient (IamService.InviteUserAsync)
      -> RPC (InviteUser)
      -> Command (CreateStaffCommand)
      -> Handler (CreateStaffCommandHandler)
      -> AWS Cognito (AdminCreateUser) & IamTenantDbContext (Save User & UserRoles)
  ```
- **Status:** `READY` (G0)

---

### `PUT /api/v1/admin/ai-config`
- **Function:** Configures AI provider credentials, model selection, temperature, and daily token budget for the tenant.
- **Role:** `ADMIN_ONLY`
- **Tenant Scope:** Strict Tenant Isolation
- **Backend Service:** `RoutePlanningAgent`
- **RPC:** `RoutePlanningService.UpsertTenantAiConfig`
- **Request:**
  ```json
  {
    "provider": "ANTHROPIC",
    "model": "claude-3-5-sonnet-20241022",
    "temperature": 0.2,
    "maxTokens": 4096,
    "monthlyTokenLimit": 5000000
  }
  ```
- **Response:**
  ```json
  {
    "tenantId": "a0000000-0000-0000-0000-000000000001",
    "provider": "ANTHROPIC",
    "model": "claude-3-5-sonnet-20241022",
    "updatedAt": "2026-08-24T12:00:00.000Z"
  }
  ```
- **Status:** `READY` (G0)
