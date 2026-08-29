# Identity & Access Management (IamTenant) — Service Overview

> **Service Layer**: Identity, Tenancy & Access Governance  
> **Target Audience**: Technical Recruiters, System Architects, Interviewers  
> **Source-of-Truth**: `src/dotnet/IamTenant`, `Shared.Security`, `Shared.Constants.PermissionConstants`, `protos/iam_tenant.proto`.

---

## 1. Service Purpose & Problem Solved

In B2B logistics SaaS platforms, user authority is traditionally modeled using either broad, rigid roles (e.g. `Admin`, `Manager`, `Staff`) or overly complex N:N role assignments that lead to privilege escalation and role sprawl. Furthermore, logistics operations frequently require temporary delegations (e.g., operational staff granted approval rights for specific routes, or customs clerks reviewing OCR documents) without promoting them to administrative or managerial roles.

The **IamTenant Service** solves this by establishing a **Simplified RBAC + Direct Capability-Based Permissions + Tenant Isolation** model:
- **Separation of Persona vs. Authority**: A user possesses **exactly ONE Base Role** (`SYSTEM_ADMIN`, `TENANT_ADMIN`, `MANAGER`, `STAFF`) which governs layout shells, top-level navigation, and default dashboards. Base Role is **NEVER** evaluated as operational business authority.
- **Direct Capability Permissions**: Operational authority is derived strictly from explicit capability tokens (e.g. `route_planning:approve`, `mail:thread:reassign`, `ocr:review`).
- **Fail-Closed Multi-Tenancy**: All user identities, groups, and permissions are cryptographically and relationally bounded to their authenticated `TenantId`.

---

## 2. Architecture & Tech Stack

```
[ Frontend / SPA ]
       │
       ▼ (HTTPS Cookie Session / JWT)
[ YARP API Gateway / Admin.Bff / Staff.Bff ]
       │
       ▼ (gRPC Port 5001 - ClientMetadataInterceptor)
┌─────────────────────────────────────────────────────────────┐
│                    IamTenant Microservice                   │
│  ├── Identity & Lifecycle Engine (User Invite, Activation)  │
│  ├── Direct Permission Engine (Delta Grant / Revoke)        │
│  ├── Role & Persona Manager (Single Base Role)              │
│  ├── Tenant Provisioning & Group Manager                    │
│  └── Transactional Outbox (Event Publisher)                 │
└──────────────┬───────────────────────────────┬──────────────┘
               │                               │
               ▼                               ▼
     [ Neon PostgreSQL 16 ]            [ Redis Cache ]
   (Users, Permissions, Tenants)     (user:{id}:permissions)
```

| Layer | Technology |
|---|---|
| **Runtime & Framework** | .NET 10 (C#), ASP.NET Core gRPC |
| **Persistence & ORM** | Entity Framework Core 10, Neon PostgreSQL 16 (Serverless SSL) |
| **Identity Provider** | AWS Cognito User Pools (Tenant-isolated user pools / App Clients) |
| **Caching** | Redis 7 (Cache-Aside pattern for user permissions) |
| **Messaging & Events** | Transactional Outbox Pattern, RabbitMQ (`UserCreatedEvent`, `TenantProvisionedEvent`) |
| **Security & Auth** | JWT Bearer, Claims-based identity, `[RequirePermission]` attribute |

---

## 3. Owned Data & Schema Boundaries

The `IamTenant` service strictly owns all identity and tenancy data:

- **`Tenants`**: Company metadata, domain, status, plan type, AWS Cognito User Pool IDs, and group bindings.
- **`Users`**: Email, names, Cognito Sub ID, single `BaseRole` (`Staff`, `Manager`, `TenantAdmin`, `SystemAdmin`), `PermissionVersion`, status (`Invited`, `Active`, `Suspended`), and soft-delete timestamp.
- **`Permissions`**: System-wide catalog of all authoritative capability codes (e.g. `mail:read`, `route_planning:approve`, `billing_settlement:settlement:manage`).
- **`UserPermissions`**: Relational join entity binding `UserId`, `PermissionId`, `TenantId`, `GrantedByUserId`, and `GrantedAt`.
- **`AuditLogs`**: Immutable log of identity management actions.

---

## 4. API & Contract Surface

Exposed via `protos/iam_tenant.proto` (`IamService`):

- **Tenant Lifecycle**: `CreateTenant`, `GetTenant`, `UpdateTenantStatus`, `ListTenants`, `DeleteTenant`.
- **User Lifecycle**: `InviteUser`, `GetUser`, `GetManyUsers`, `UpdateUser`, `ActivateUser`, `SuspendUser`, `ResetUserPassword`.
- **Role Management**: `UpdateUserRole` (Updates single Base Role with optional template defaults union).
- **Direct Permissions**:
  - `UpdateUserPermissions`: Single-user delta grant/revoke.
  - `BulkUpdateUserPermissions`: Multi-user delta grant/revoke with fail-closed tenant validation.
  - `GetUserPermissions`: Returns active capability tokens and version counter.

---

## 5. Security & Invariants

1. **`ROLE != AUTHORITY`**: Code never performs `if (role == "MANAGER")` on operational endpoints.
2. **Platform vs. Tenant Isolation**: Tenant Admins cannot grant system-only permissions (`mail:system:manage`, `compliance:platform:ingest`) or assign `SYSTEM_ADMIN` roles.
3. **Cache Invalidation & Versioning**: Every permission mutation increments `user.PermissionVersion++` and invalidates Redis key `user:{userId}:permissions`.
4. **Current Maturity**: Production-ready, fully tested with unit and integration suites (`IamAuthorizationTests.cs`).
