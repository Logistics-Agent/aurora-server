# Aurora IAM — Migration Status & Audit Report

> **Audit Date**: August 2026  
> **Source-of-Truth**: Repository Code Audit (C# / .NET 10, Proto Contracts, EF Core Persistences, BFFs).

---

## 1. Executive Implementation Matrix

| Architecture Component | Target Specification | Current Code Status | Evidence / Implementation Location | Notes |
|---|---|:---:|---|---|
| **Base Role Model** | Exactly 1 Base Role (`STAFF`, `MANAGER`, `TENANT_ADMIN`, `SYSTEM_ADMIN`) | `IMPLEMENTED` | `IamTenant.Domain.User.Role` (`BaseRole`), `Shared.Enums.BaseRole.cs` | User entity stores exactly 1 enum mapped to string in PostgreSQL. |
| **Direct User Permissions** | `UserPermission` entity with capability code links | `IMPLEMENTED` | `IamTenant.Domain.UserPermission.cs`, `IamTenantDbContext.cs` | Mapped as `UserPermission(UserId, PermissionId)`. |
| **Permission Catalog & Seeding** | Authoritative capability list seeded in DB | `IMPLEMENTED` | `Shared.Constants.PermissionConstants.cs`, `IamTenantDbContext.SeedSystemData` | Deterministic GUIDs generated from permission codes via MD5. |
| **StaffType Removal** | No `StaffType` in code, DB, or Proto | `IMPLEMENTED` | Grep confirms zero references in production C#, DB, or Proto | Historical references in old markdown docs purged. |
| **Single Permission Update** | Delta `grant` & `revoke` on individual user | `IMPLEMENTED` | `UpdateUserPermissionsCommand.cs`, `StaffController.UpdateStaffPermissions` | Atomic transaction + Redis cache invalidation. |
| **Bulk Permission Update** | Delta `grant` & `revoke` on multiple users | `IMPLEMENTED` | `BulkUpdateUserPermissionsCommand.cs`, `StaffController.BulkUpdateStaffPermissions` | Strict tenant isolation verification across all target user IDs. |
| **Role Change Behavior** | Preserves existing permissions; optional union defaults | `IMPLEMENTED` | `UpdateUserRoleCommand.cs`, `StaffController.UpdateStaffRole` | Returns `ElevatedPermissionsRetained` on downgrade. |
| **BFF Gating (`[RequirePermission]`)** | Zero role-bypass capability checks | `IMPLEMENTED` | `BuildingBlocks.BFF.Attributes.RequirePermissionAttribute` | Evaluates `CurrentUser.HasPermission()`. |
| **Current User Claims (`GET /api/v1/auth/me`)**| Exposes `role` and `permissions: []` | `IMPLEMENTED` | `BuildingBlocks.BFF.Controllers.AuthController.Me` | Returns active permissions list for frontend store. |
| **Realtime / Redis Caching** | Cache-aside for fast permission lookups | `IMPLEMENTED` | `PermissionCacheService.cs`, `UserPermissionCache.cs` | Invalidated automatically upon permission changes. |
| **Permission Versioning** | `PermissionVersion` on User | `IMPLEMENTED` | `User.PermissionVersion`, `UserPermissionsDto.Version` | Bumps on every permission or role mutation. |
| **Frontend Documentation** | Aligned with pure capability model | `PATCHED` | `docs/technical/frontend/` | Patched in this cycle. |

---

## 2. Legacy Remnants & Migration Tracking

### 2.1 `StaffType` Enum
- **Status in Backend**: **100% REMOVED**. `StaffType.cs` and all DB columns have been deleted.
- **Status in Documentation**: Historical text documents (`docs/documents/IamTenant_technical.txt` and legacy `docs/bff-api/admin-api.md`) had mentions of `staffType: 1`. These are marked as obsolete.

### 2.2 `UserRoles` N:N Join Table
- **Status**: **REPLACED**. The database schema uses single column `Role` on table `Users`. The `UserRoles` join table has been retired in favor of `UserPermissions` for granular authorization.

### 2.3 Role-Based Action Bypasses
- **Status**: **ELIMINATED**. All BFF endpoints use `[RequirePermission(PermissionConstants....)]`. There are zero `if (User.IsInRole("MANAGER"))` bypasses on business operations.

---

## 3. Available vs. Missing APIs for Admin Frontend

### 3.1 APIs Fully Available in Admin BFF (`/api/v1/admin/staff`)

| Endpoint | Method | Required Permission | Description |
|---|---|---|---|
| `/api/v1/admin/staff` | `POST` | `iam:user:invite` | Invites staff with specified Base Role and optional template permissions. |
| `/api/v1/admin/staff` | `GET` | `iam:user:read` | Paged listing of staff in current tenant. |
| `/api/v1/admin/staff/{id}` | `GET` | `iam:user:read` | Get staff details including status and role. |
| `/api/v1/admin/staff/{id}` | `PUT` | `iam:user:update` | Update staff basic details (name). |
| `/api/v1/admin/staff/{id}/role` | `PATCH` | `iam:role:manage` | Updates Base Role (STAFF ↔ MANAGER ↔ TENANT_ADMIN) with optional `ApplyDefaultPermissions`. |
| `/api/v1/admin/staff/{id}/permissions` | `GET` | `iam:user:read` | Returns active direct permissions and permission version. |
| `/api/v1/admin/staff/{id}/permissions` | `PATCH` | `iam:permission:manage` | Delta grant/revoke permissions on single user. |
| `/api/v1/admin/staff/permissions` | `PATCH` | `iam:permission:manage` | Bulk delta grant/revoke permissions across multiple user IDs. |
| `/api/v1/admin/roles` | `GET` | `iam:role:read` | Lists canonical base roles (`STAFF`, `MANAGER`, `TENANT_ADMIN`) and their default permission templates. |
| `/api/v1/admin/roles/{code}` | `GET` | `iam:role:read` | Gets specific canonical base role template definition. |
| `/api/v1/auth/me` | `GET` | `[Authorize]` | Returns current user's profile, tenant, role, and direct permissions list. |

### 3.2 Missing / Future Admin Capabilities
1. **Permission Catalog Query Endpoint**: `GET /api/v1/admin/permissions` (Listing all available capability descriptions by module). Currently, the catalog is accessible through `GET /api/v1/admin/roles` default permissions list. A dedicated permission catalog endpoint can be added in a future enhancement.
2. **Permission Audit History Endpoint**: `GET /api/v1/admin/staff/{id}/permission-history` (Historical log of who granted/revoked specific permissions). Backend logs audit entries to `AuditLogs` table, but a dedicated UI query endpoint is planned.

---

## 4. Blockers Before Legacy Decommissioning

There are **NO architectural blockers**. The core C# .NET 10 backend, proto contracts, and database layer have already transitioned to the new model:
- `BaseRole` is single and canonical.
- `UserPermissions` direct mapping is active and covered by unit/integration tests in `IamAuthorizationTests.cs`.
- Frontend documentation has been updated to prevent developers from implementing legacy role checks.
