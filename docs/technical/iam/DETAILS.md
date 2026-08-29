# Identity & Access Management (IamTenant) — Deep Technical Details

> **Service Layer**: Architecture, Patterns, Concurrency & Security Engineering  
> **Source-of-Truth**: `src/dotnet/IamTenant`, `IamTenantDbContext`, `UpdateUserPermissionsCommand`, `BulkUpdateUserPermissionsCommand`, `UpdateUserRoleCommand`, `PermissionCacheService`.

---

## 1. Architectural Patterns & Domain Model

The `IamTenant` service is built on **Clean Architecture + CQRS with MediatR** in .NET 10.

```
                    ┌─────────────────────────┐
                    │    IamGrpcService       │
                    └────────────┬────────────┘
                                 │ MediatR Send()
            ┌────────────────────┴────────────────────┐
            ▼                                         ▼
┌─────────────────────────┐               ┌─────────────────────────┐
│   Commands (Writes)     │               │    Queries (Reads)      │
│ - InviteUser            │               │ - GetUser               │
│ - UpdateUserRole        │               │ - GetManyUsers          │
│ - UpdateUserPermissions │               │ - GetUserPermissions    │
│ - BulkUpdatePermissions │               │                         │
└───────────┬─────────────┘               └───────────┬─────────────┘
            │                                         │
            ▼                                         ▼
┌───────────────────────────────────────────────────────────────────┐
│              Domain Entities & EF Core Persistences               │
│     User ──(1:N)──> UserPermission <──(N:1)── Permission          │
│     Tenant ──(1:N)──> User                                        │
└───────────────────────────────────────────────────────────────────┘
```

---

## 2. Deep-Dive: Core Command Implementations

### 2.1 Delta Permission Updates (`UpdateUserPermissionsCommand`)
Permissions are updated via **Delta Semantics** (`Grant` list and `Revoke` list):
1. Resolves caller's `TenantId` from `ICurrentUserService`.
2. Validates that the target `User` exists, belongs to caller's `TenantId`, and is not soft-deleted.
3. Validates all requested grant codes exist in the database table `context.Permissions`.
4. Enforces security invariant: Rejects any attempt to grant system-only permissions from tenant context (`PermissionConstants.IsSystemOnlyPermission`).
5. **Idempotency**: Adds new `UserPermission` records only if absent; removes matching records on revoke; silently skips absent codes.
6. Increments `user.PermissionVersion++` to detect JWT claims staleness.
7. Invalidates Redis cache key `user:{userId}:permissions`.

### 2.2 Bulk Delta Updates & Multi-Tenant Shield (`BulkUpdateUserPermissionsCommand`)
When an Admin selects $N$ users to update permissions:
- Validates **all** user IDs belong to the caller's tenant:
  ```csharp
  var users = await context.Users
      .Where(u => targetUserIds.Contains(u.Id) && u.TenantId == tenantId && !u.IsDeleted)
      .ToListAsync(cancellationToken);

  if (users.Count != targetUserIds.Count)
  {
      throw new DomainException("One or more users not found or belong to another tenant.");
  }
  ```
- If even **one** ID belongs to another tenant or does not exist, the entire transaction aborts fail-closed.
- Applies grants/revokes to all valid users and invalidates Redis cache for every affected ID.

### 2.3 Single Base Role & "Apply Defaults" (`UpdateUserRoleCommand`)
- Changing role `STAFF` $\rightarrow$ `MANAGER` or `MANAGER` $\rightarrow$ `STAFF` **never** mutates permissions by default.
- If `ApplyDefaultPermissions = true` is passed, the handler performs a **UNION** of the target role's default preset template with existing permissions (never silently revokes).
- On downgrade (e.g. `MANAGER` $\rightarrow$ `STAFF`), the handler detects and returns `ElevatedPermissionsRetained` (e.g. `route_planning:approve`, `mail:thread:reassign`) so Admin UI can prompt for cleanup.

---

## 3. Multi-Tenancy & Global Query Filters

EF Core enforces multi-tenant isolation automatically on every query:

```csharp
modelBuilder.Entity<User>(e =>
{
    e.HasQueryFilter(u => _tenantId.HasValue && u.TenantId == _tenantId.Value && !u.IsDeleted);
});
```

- **Fail-Closed**: If `_tenantId` is `null` in a tenant-scoped request, EF Core returns zero records.
- **Cross-Service Database Isolation**: `IamTenant` owns its dedicated PostgreSQL schema. Direct database connections across services are strictly prohibited.

---

## 4. Caching & Performance Architecture

To avoid heavy database joins on every incoming HTTP/gRPC request:
- Permissions are cached in Redis under `user:{userId}:permissions`.
- **Cache Invalidation**: Any permission or role change invalidates the key immediately.
- **Cache-Aside Read**: Queries first check Redis; on cache miss, they query `UserPermissions` and write back to Redis with a 1-hour TTL.

---

## 5. Resilience, Observability & Deployment

- **Resilience**: gRPC clients in `BuildingBlocks.BFF` use Microsoft Standard Resilience Handler (exponential backoff retry + circuit breaker).
- **Observability**: OpenTelemetry tracing instrumented with tenant ID baggage and user ID tags.
- **Database Migrations**: Additive migrations executed via `efbundle` adhering to the Expand-and-Contract pattern.
- **Soft Deletion**: Users are soft-deleted via `IsDeleted = true` and `DeletedAt = DateTimeOffset.UtcNow`, retaining referential integrity for historical audit logs.
