# Aurora Platform - System Exclusive API Catalog (SYSTEM_ONLY)

> **Document ID:** `DOC-BFF-SYS`  
> **Status:** Canonical Specification Complete  
> **Scope:** HTTP REST APIs exclusively accessible by the `SYSTEM` role (`System.Bff`).  
> **Base Controller:** `[Authorize(Roles = "SYSTEM_ADMIN")]` via [SystemControllerBase.cs](file:///D:/IT/CD/aurora-server/src/dotnet/BFF/System.Bff/Controllers/SystemControllerBase.cs).

---

## 1. System Exclusive API Table

| Method | Endpoint | Function | Service | RPC | Main Source File |
| :--- | :--- | :--- | :--- | :--- | :--- |
| `POST` | `/api/v1/system/tenants` | Onboard New Customer Tenant | `IamTenant` | `IamService.CreateTenant` | `System.Bff/Controllers/TenantsController.cs` |
| `GET` | `/api/v1/system/tenants` | List All System Tenants (Paged) | `IamTenant` | `IamService.ListTenants` | `System.Bff/Controllers/TenantsController.cs` |
| `GET` | `/api/v1/system/tenants/{id}` | Get System Tenant Details | `IamTenant` | `IamService.GetTenant` | `System.Bff/Controllers/TenantsController.cs` |
| `PATCH`| `/api/v1/system/tenants/{id}/status`| Suspend / Activate Tenant | `IamTenant` | `IamService.UpdateTenantStatus` | `System.Bff/Controllers/TenantsController.cs` |
| `DELETE`| `/api/v1/system/tenants/{id}` | Purge / Offboard Tenant | `IamTenant` | `IamService.DeleteTenant` | `System.Bff/Controllers/TenantsController.cs` |
| `POST` | `/api/v1/system/compliance/sources` | Ingest Global Trade Law | `RegulatoryCompliance`| `RegulatoryComplianceService.IngestRegulatorySource` | `System.Bff/Controllers/SystemIngestionController.cs` |
| `POST` | `/api/v1/system/compliance/knowledge` | Ingest Global Platform Knowledge | `RegulatoryCompliance`| `RegulatoryComplianceService.IngestKnowledgeDocument` | `System.Bff/Controllers/SystemIngestionController.cs` |
| `POST` | `/api/v1/system/mail/dead-letters/{id}/requeue` | Reprocess Failed Outbox Email | `MailService` | `MailManagement.RequeueDeadLetter` | `System.Bff/Controllers/MailSystemController.cs` |

---

## 2. Granular API Specifications (Samples)

### `POST /api/v1/system/tenants`
- **Function:** Provisions a brand new enterprise customer tenant, sets up Cognito App Client, allocates default roles, and initializes data partition.
- **Role:** `SYSTEM_ONLY`
- **Tenant Scope:** System Multi-tenant Administrator Scope
- **Backend Service:** `IamTenant`
- **RPC:** `IamService.CreateTenant`
- **Request:**
  ```json
  {
    "name": "Pacific Logistics Corp",
    "tenantCode": "PACIFIC_LOG",
    "planType": 2,
    "adminEmail": "admin@pacificlog.com",
    "adminFullName": "Chief Administrator"
  }
  ```
- **Response:**
  ```json
  {
    "id": "a0000000-0000-0000-0000-000000000002",
    "name": "Pacific Logistics Corp",
    "tenantCode": "PACIFIC_LOG",
    "planType": "ENTERPRISE",
    "status": "ACTIVE",
    "createdAt": "2026-08-24T12:00:00.000Z"
  }
  ```
- **Source Flow:**
  ```text
  System.Bff (POST /api/v1/system/tenants)
      -> GrpcClient (IamService.CreateTenantAsync)
      -> RPC (CreateTenant)
      -> Command (CreateTenantCommand)
      -> Handler (CreateTenantCommandHandler)
      -> AWS Cognito (CreateUserPoolClient) & IamTenantDbContext (Insert Tenant & Initial Admin)
  ```
- **Status:** `READY` (G0)

---

### `POST /api/v1/system/mail/dead-letters/{id}/requeue`
- **Function:** Requeues an email dispatch job that previously failed delivery and landed in the dead-letter queue.
- **Role:** `SYSTEM_ONLY`
- **Tenant Scope:** Platform SRE Scope
- **Backend Service:** `MailService`
- **RPC:** `MailManagement.RequeueDeadLetter`
- **Response:**
  ```json
  {
    "success": true,
    "message": "Dead letter message successfully requeued for dispatch"
  }
  ```
- **Status:** `READY` (G0)
