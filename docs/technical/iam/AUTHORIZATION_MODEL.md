# Aurora IAM — Authoritative Authorization Model

> **Status**: AUTHORITATIVE / TARGET ARCHITECTURE  
> **Source-of-Truth**: Audited against `IamTenantDbContext`, `PermissionConstants`, `BaseRole`, `RequirePermissionAttribute`, and BFF Controllers.

---

## 1. Executive Summary & Core Architectural Invariants

Aurora implements a **Simplified RBAC + Direct Capability-Based User Permissions + Resource Scope + Business Governance** authorization model.

```
User
 ├── exactly ONE Base Role (Persona & App Shell)
 │     ├── SYSTEM_ADMIN  (Platform Super-Admin)
 │     ├── TENANT_ADMIN  (Tenant Administrator)
 │     ├── MANAGER       (Operations Supervisor)
 │     └── STAFF         (Operational Staff)
 │
 └── N UserPermissions (Runtime Capability Authority)
       ├── mail:thread:reassign
       ├── route_planning:approve
       ├── ocr:review
       ├── compliance:override
       ├── billing_settlement:settlement:manage
       └── ...
```

### The Seven Authoritative Invariants

1. **`ROLE != AUTHORITY`**: Base Role describes user **persona, shell layout, default navigation, and dashboard view type only**. Base Role is **NEVER** evaluated as operational business authority.
2. **`ROLE != PERMISSION SET`**: A role does not dynamically grant runtime permissions. Runtime authority is derived **strictly from `UserPermissions`**.
3. **`ROLE DEFAULTS != RUNTIME AUTHORIZATION`**: Default permission templates (`GetDefaultStaffPermissions()`, `GetDefaultManagerPermissions()`, `GetTenantAdminPermissions()`) exist purely as **onboarding presets / templates**. Changing a user's role does **NOT** automatically grant or revoke direct permissions.
4. **`STAFFTYPE DOES NOT EXIST`**: Legacy classifications (`Operations`, `Documentation`, `CustomerService`, `Finance`) and business-specific roles (`ROUTE_OPERATOR`, `CUSTOMS_OFFICER`, `FINANCE_OFFICER`) are **completely removed**.
5. **`EXACTLY ONE BASE ROLE`**: Each user belongs to exactly one Base Role (`STAFF`, `MANAGER`, `TENANT_ADMIN`, `SYSTEM_ADMIN`). The legacy `User <-> N:N <-> Roles` model is retired.
6. **`PERMISSIONS ARE CAPABILITY TOKENS`**: Granular capability permissions (e.g. `route_planning:approve`, `mail:send`) represent the exact authority to perform actions. A `STAFF` user **MAY** have `route_planning:approve` (e.g. delegated authority), and a `MANAGER` user **MAY NOT** have `route_planning:approve`. Both states are completely valid.
7. **`FOUR-LAYER DEFENSE-IN-DEPTH`**: Authorization does not stop at permissions. Execution requires passing Authentication, Capability Permission, Resource Scope, and Business Governance.

---

## 2. Canonical Base Roles

Aurora recognizes **exactly four canonical Base Roles**:

| Base Role Code | Scope | Target Persona & UX Shell | Typical Responsibility |
|---|---|---|---|
| **`SYSTEM_ADMIN`** | Global Platform | Platform Administration Shell | Platform provisioning, tenant onboarding, global regulatory source ingestion, system maintenance. |
| **`TENANT_ADMIN`** | Tenant-Wide | Tenant Settings & IAM Shell | Tenant staff lifecycle, direct capability permission assignment, company domain & mailbox setup. |
| **`MANAGER`** | Tenant-Wide | Supervisory & Exception Dashboard | Team oversight, operational queues, supervision dashboards, team workload rebalancing. |
| **`STAFF`** | Tenant / Assigned Work | Operational Work Shell | Day-to-day operations, My Work inbox, shipments processing, route creation & optimization. |

> [!IMPORTANT]
> **Role determines UX presentation, not business authorization.**  
> - A user with role `STAFF` lands on the "My Work" dashboard.  
> - A user with role `MANAGER` lands on the "Supervisory Overview" dashboard.  
> - However, whether either user can click **"Approve Route"** or **"Reassign Email Thread"** depends **strictly** on possessing `route_planning:approve` or `mail:thread:reassign` in their direct `UserPermissions`.

---

## 3. Four-Layer Authorization & Execution Pipeline

Runtime authorization in Aurora is evaluated through four successive, non-bypassable gates:

```mermaid
flowchart TD
    A[Incoming Request] --> Gate1{1. Authentication Gate}
    Gate1 -->|Unauthenticated| E1[401 Unauthorized]
    Gate1 -->|Valid Session Cookie / JWT| Gate2{2. Permission Gate\n[RequirePermission]}
    
    Gate2 -->|Missing Capability Token| E2[403 Forbidden\nMissing required permission]
    Gate2 -->|Has Capability Token| Gate3{3. Resource Scope Gate\nTenant Isolation & Ownership}
    
    Gate3 -->|Cross-Tenant or Unassigned Violation| E3[404 Not Found / 403 Forbidden]
    Gate3 -->|Scope Verified| Gate4{4. Business Governance Gate\nPolicy Engine & Stale Protection}
    
    Gate4 -->|Stale Route / Stale Policy / Blocked Risk| E4[422 Unprocessable / 403 Blocked]
    Gate4 -->|Governance Cleared| Exec[Execute Business Operation]
```

### Layer 1: Authentication Gate
- Validates the `.AspNetCore.Cookies` session or Cognito JWT.
- Populates `CurrentUser` context (`UserId`, `TenantId`, `Role`, `Permissions`, `PermissionVersion`).

### Layer 2: Capability Permission Gate (`[RequirePermission]`)
- Enforced at BFF API endpoints via `[RequirePermission(PermissionConstants.Module.Action)]`.
- Evaluates `CurrentUser.HasPermission(requiredCode)` against the user's active direct permissions.
- **Zero Role Bypasses**: Even a `MANAGER` or `TENANT_ADMIN` cannot execute an endpoint if their `UserPermissions` lack the required capability.

### Layer 3: Resource Scope Gate
- **Tenant Isolation**: All queries enforce global query filter `TenantId == CurrentUser.TenantId` (Fail-closed).
- **Ownership Scope**: Operational endpoints enforce ownership boundaries (e.g. `mail:send` requires `thread.PrimaryAssigneeUserId == CurrentUser.UserId`).

### Layer 4: Business Governance Gate
- Evaluates dynamic risk levels, finite state machine transitions, and regulatory rules.
- **Example (Route Planning)**: Even with `route_planning:execute`, if the route is assessed as `HIGH` risk, the Governance Decision becomes `ManagerApprovalRequired`. The route cannot be activated until an approved `ApprovalRequest` matching the exact `RouteVersion` and `PolicyVersion` exists.
- **Example (Mail Platform)**: Even with `mail:send`, outbound mail must pass the Outbound Security Pipeline (SPF/DKIM/DMARC validation, attachment ClamAV scan, and prompt-injection check).

---

## 4. Permission Catalog Taxonomy

Capability permissions are categorized by domain module under strict colon notation (`module:resource:action` or `module:action`):

```
Shared.Constants.PermissionConstants
├── Mail (mail:read, mail:send, mail:thread:reassign, mail:quarantine:release, ...)
├── Shipment (shipments:create, shipments:read, shipments:submit, shipments:cancel, ...)
├── RoutePlanning (route_planning:read, route_planning:optimize, route_planning:approve, ...)
├── Ocr (ocr:review)
├── Documents (documents:ingest, documents:manage)
├── Compliance (compliance:override, compliance:platform:ingest)
├── Financial (financial_tax:read, financial_tax:calculate)
├── Billing (billing_settlement:read, billing_settlement:invoice:create, billing_settlement:settlement:manage)
├── Gps (gps_tracking:geofence:manage)
└── Iam (iam:user:read, iam:user:invite, iam:user:update, iam:role:manage, iam:permission:manage)
```

---

## 5. Summary Comparison: Old vs. Target Model

| Dimension | Legacy Model (Deprecated) | Target Aurora IAM Model (Authoritative) |
|---|---|---|
| **Role Assignment** | User $\leftrightarrow$ N:N $\leftrightarrow$ Roles | User $\rightarrow$ **Exactly ONE Base Role** (`STAFF`, `MANAGER`, `TENANT_ADMIN`, `SYSTEM_ADMIN`) |
| **Business Authority** | Inferred from Role (`role == MANAGER`) | Inferred **strictly from `UserPermissions`** (`hasPermission("...")`) |
| **Role Meaning** | Grant of broad system privileges | **Persona, layout shell, default dashboard type** |
| **Staff Classification** | `StaffType` enum (Operations, Docs, CS, Finance) | **REMOVED**. Granular capability permissions used instead. |
| **Role Change Effect** | Overwrote user permissions with role defaults | **Permissions UNCHANGED**. Role change is purely persona change. |
| **Permission Defaults** | Runtime authorization source | **Creation preset / optional template ONLY**. |
| **Permission Updates** | Full replacement (`permissions: [...]`) | **Delta Updates** (`grant: [...], revoke: [...]`) |
