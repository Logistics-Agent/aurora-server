# Identity & Access Management (IamTenant) — Interview Q&A Guide

> **Target Audience**: Junior, Mid-level, Senior & System Design Interviewers  
> **Source-of-Truth**: Grounded 100% in Aurora `IamTenant` implementation.

---

### Q1 (Junior): Why did the architecture separate Persona Roles from Authority Permissions?
**Answer**:  
In enterprise SaaS, treating roles as direct authority causes role explosion and security risks. A user needed route approval authority without needing full manager visibility into all employee performance or financial queues. In Aurora, a user has **exactly one Base Role** (`STAFF`, `MANAGER`, `TENANT_ADMIN`, `SYSTEM_ADMIN`) which controls the UI layout shell, navigation menu, and landing dashboard, while **direct capability permissions** (`UserPermissions`) define actual operational execution rights.

---

### Q2 (Mid): How does Aurora prevent cross-tenant data leakage in IAM queries?
**Answer**:  
Multi-tenant isolation is enforced at three levels:
1. **Authenticated Context**: `ICurrentUserService` extracts the `TenantId` securely from the validated JWT claims. Client-supplied tenant parameters are rejected.
2. **EF Core Global Query Filters**: `IamTenantDbContext` registers `e.HasQueryFilter(u => u.TenantId == _tenantId.Value && !u.IsDeleted)`, automatically appending tenant scoping to all generated SQL queries.
3. **Fail-Closed Bulk Operations**: In `BulkUpdateUserPermissionsHandler`, if any target `UserId` does not belong to the caller's tenant, the entire request throws a domain exception immediately.

---

### Q3 (Mid): Why do bulk permission updates use Delta Semantics (`Grant`/`Revoke`) instead of Full Replacement?
**Answer**:  
In real-world operations, users in a bulk selection often possess distinct baseline capabilities (e.g. User A has `ocr:review`, User B has `compliance:override`). If an Admin selects both users to grant `route_planning:approve`, a full replacement payload (`{ "permissions": ["route_planning:approve"] }`) would unintentionally wipe out User A's OCR rights and User B's compliance rights. Delta semantics apply additive grants and explicit revokes without mutating unrelated permissions.

---

### Q4 (Senior): What happens when an Admin changes a user's role from `MANAGER` to `STAFF`?
**Answer**:  
Role changes do **not** silently strip user permissions. In Aurora, the role change updates `user.Role = BaseRole.Staff` while leaving existing `UserPermissions` intact. However, the handler inspects active capabilities and returns `ElevatedPermissionsRetained` (e.g. `route_planning:approve`, `mail:thread:reassign`) in the response DTO. This enables the Admin UI to clearly inform the administrator of remaining supervisory capabilities and offer an explicit cleanup workflow if desired.

---

### Q5 (Senior / System Design): How is permission cache consistency handled across microservices?
**Answer**:  
Permissions are cached in Redis under `user:{userId}:permissions`. When an Admin modifies permissions or roles:
1. The database transaction writes new `UserPermission` records and increments `user.PermissionVersion++`.
2. Upon transaction commit, `permissionCache.InvalidateAsync(userId)` deletes the Redis key.
3. Subsequent requests encounter a cache miss, reload from the database, and write to Redis with a 1-hour TTL.
4. Downstream microservices compare the JWT's `permission_version` claim with the user's active version to detect and refresh stale sessions.

---

### Q6 (System Design): What are the architectural tradeoffs of using AWS Cognito alongside a custom PostgreSQL IAM service?
**Answer**:  
- **Benefit**: AWS Cognito offloads MFA, password hashing, brute-force protection, and OAuth2/OIDC compliance, while PostgreSQL owns rich relational domain concepts (tenants, capability trees, bulk delta operations, audit trails).
- **Tradeoff**: Dual-write complexity during user registration. Aurora mitigates this using the **Transactional Outbox Pattern**: Cognito user creation is executed first, followed by atomic database insertion with an outbox event.
