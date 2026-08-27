# Aurora IAM — User Permission Management & Administration

> **Status**: AUTHORITATIVE / TARGET ARCHITECTURE  
> **Source-of-Truth**: Audited against `StaffController.cs`, `UpdateUserPermissionsCommand.cs`, `BulkUpdateUserPermissionsCommand.cs`, and `UpdateUserRoleCommand.cs`.

---

## 1. Overview & Workflows

Tenant Administrators manage user identities and direct permissions through the Admin BFF (`/api/v1/admin/staff`).

```
Tenant Admin UI
  │
  ├── Single User Management
  │     ├── Invite User with preset permissions
  │     ├── Change Base Role (STAFF ↔ MANAGER ↔ TENANT_ADMIN)
  │     ├── Grant / Revoke individual capabilities (Delta)
  │     └── Explicitly "Apply Role Defaults" (Union template)
  │
  └── Bulk User Management
        ├── Select N users in table
        ├── Bulk Grant capabilities (Delta)
        └── Bulk Revoke capabilities (Delta)
```

---

## 2. Single-User Permission Administration

### 2.1 Viewing User Permissions (`GET /api/v1/admin/staff/{id}/permissions`)
- **Required Permission**: `iam:user:read` (or legacy `iam:read`)
- **Response**:
  ```json
  {
    "userId": "9a3c7e81-7788-4221-9988-112233445566",
    "role": "STAFF",
    "permissions": [
      "mail:read",
      "mail:send",
      "route_planning:read",
      "route_planning:optimize",
      "route_planning:approve"
    ],
    "permissionVersion": 4
  }
  ```

### 2.2 Updating User Permissions with Delta Semantics (`PATCH /api/v1/admin/staff/{id}/permissions`)
- **Required Permission**: `iam:permission:manage` (or legacy `iam:assign`)
- **Request Payload**:
  ```json
  {
    "grant": [
      "route_planning:approve",
      "ocr:review"
    ],
    "revoke": [
      "route_planning:reject"
    ]
  }
  ```
- **Semantics**:
  - **Idempotent Grant**: Adds `route_planning:approve` and `ocr:review` if not already present.
  - **Idempotent Revoke**: Removes `route_planning:reject` if present; silently ignores if absent.
  - **Unrelated Permissions**: Any existing permissions not listed in `grant` or `revoke` remain completely untouched.
  - **Cache & Versioning**: Bumps `user.PermissionVersion++` and invalidates Redis cache key `user:{userId}:permissions`.

---

## 3. Bulk Permission Administration & Delta Semantics

### 3.1 Why Delta Semantics Are Mandatory for Bulk Updates
Bulk permission updates **MUST NEVER** use replacement semantics (`{ "permissions": [...] }`). Different users in a bulk selection often possess distinct baseline or specialized capabilities.

#### Concrete Scenario
- **User A** has: `mail:send`, `route_planning:read`
- **User B** has: `mail:send`, `ocr:review`

If an Admin selects **User A + User B** and grants `route_planning:approve`:

```json
PATCH /api/v1/admin/staff/permissions
{
  "userIds": [
    "9a3c7e81-...", 
    "b41c2299-..."
  ],
  "grant": [
    "route_planning:approve"
  ],
  "revoke": []
}
```

#### Resulting State
- **User A**: `mail:send`, `route_planning:read`, `route_planning:approve` (Retains `route_planning:read`)
- **User B**: `mail:send`, `ocr:review`, `route_planning:approve` (Retains `ocr:review`)

Replacement semantics would have wiped out `route_planning:read` from User A and `ocr:review` from User B, causing severe operational disruptions.

### 3.2 Bulk Response Schema
```json
{
  "updatedUsersCount": 2,
  "affectedUserIds": [
    "9a3c7e81-7788-4221-9988-112233445566",
    "b41c2299-1122-3344-5566-778899aabbcc"
  ]
}
```

---

## 4. Role Change Semantics & Explicit "Apply Defaults"

### 4.1 Changing Base Role (`PATCH /api/v1/admin/staff/{id}/role`)
- **Required Permission**: `iam:role:manage` (or legacy `iam:assign`)

```json
PATCH /api/v1/admin/staff/9a3c7e81-.../role
{
  "role": "MANAGER",
  "applyDefaultPermissions": false
}
```

### 4.2 Promotion / Downgrade Invariants
1. **Role Change Alone Preserves Permissions**: Changing `STAFF` $\rightarrow$ `MANAGER` or `MANAGER` $\rightarrow$ `STAFF` with `applyDefaultPermissions: false` **DOES NOT** mutate existing `UserPermissions`.
2. **Explicit Apply Defaults (`applyDefaultPermissions: true`)**:
   - Performs a **UNION** of the target role's template permissions with the user's existing permissions.
   - **Never revokes** existing capabilities.
3. **Downgrade Visibility (`ElevatedPermissionsRetained`)**:
   - When downgrading from `MANAGER` $\rightarrow$ `STAFF` or `TENANT_ADMIN` $\rightarrow$ `STAFF`, the backend detects and returns any elevated permissions the user still possesses (e.g. `route_planning:approve`, `mail:thread:reassign`, `compliance:override`).
   - Admin UI displays these retained elevated capabilities and offers an explicit cleanup action if desired.

```json
{
  "userId": "9a3c7e81-...",
  "role": "STAFF",
  "permissions": [
    "mail:read",
    "mail:send",
    "route_planning:approve"
  ],
  "permissionVersion": 5,
  "elevatedPermissionsRetained": [
    "route_planning:approve"
  ]
}
```

---

## 5. Security Invariants & Isolation Rules

1. **Platform Isolation**: Tenant Admins cannot grant system-only permissions (`mail:system:manage`, `compliance:platform:ingest`). Any attempt throws a domain exception.
2. **System Admin Assignment**: `SYSTEM_ADMIN` role cannot be assigned within tenant context. Assignable tenant roles are strictly `STAFF`, `MANAGER`, and `TENANT_ADMIN`.
3. **Tenant Isolation on Bulk Updates**: If any `userId` in `BulkUpdateUserPermissionsRequest` does not belong to the caller's authenticated `TenantId`, the entire operation aborts fail-closed (`DomainException`), preventing cross-tenant privilege escalation.
4. **Catalog Validation**: All codes in `grant` must exist in the system permissions database table (`context.Permissions`). Unknown codes are rejected immediately.
