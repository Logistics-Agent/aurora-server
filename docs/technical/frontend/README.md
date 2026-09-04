# Aurora Platform — Frontend Technical Documentation & Standards

> **Status:** Canonical Frontend Engineering Reference Baseline  
> **Target Consumer:** Frontend Engineering Team (`aurora-client` Next.js App)  
> **Source Precedence:** Source Code & Protos > docs/technical/frontend > docs/bff-api > Figma UI Specs.

---

## 1. Documentation Source-of-Truth Rule

When integrating features, resolving contract ambiguities, or verifying behavior, frontend engineers must adhere to the strict precedence hierarchy:

```text
1. Current Source Code / Tests  (C# .NET 10 BFF Controllers, Java/NestJS Services)
2. Protobuf Contracts           (protos/*.proto)
3. BFF Implementation           (Admin.Bff, Staff.Bff, System.Bff)
4. Technical Frontend Docs      (docs/technical/frontend/*)
5. BFF API Documentation        (docs/bff-api/*)
6. Figma / UI Specifications    (docs/figma/* — Target UX Specifications)
7. Historical Reverse Reports   (docs/reverse/* — Labeled SUPERSEDED / Historical)
```

> [!IMPORTANT]
> Figma design documents represent **Product Target UX**. Where backend code does not yet implement a target capability (e.g. `ListDomains`, `DefaultMailboxId`), the frontend documentation and API catalog clearly distinguish between `CURRENT` and `TARGET (BACKEND_REQUIRED)`.

---

## 2. Core Architecture Rules for Frontend

1. **One Aurora Application Experience**:
   - **Aurora Admin Console** (`/admin/*`) is the shell for `TENANT_ADMIN`.
   - **Aurora Operations Workspace** (`/ops/*` or root) is the unified shell for `STAFF` and `MANAGER`.
   - Mail is an integrated first-class module inside both shells, **not a standalone application**.
2. **Role != Authority**:
   - `BaseRole` (`TENANT_ADMIN`, `MANAGER`, `STAFF`, `SYSTEM_ADMIN`) dictates the visual layout and initial dashboard view.
   - `UserPermissions` array dictates runtime authority (e.g. button visibility, action gating).
   - Legacy `StaffType` (Operations, Documentation, CS, Finance) is **100% removed**.
3. **Session Authentication & Cookies**:
   - Authentication uses AWS Cognito OIDC via `POST /api/v1/auth/login`.
   - Access and refresh tokens are stored in `HttpOnly; Secure; SameSite=Lax/Strict` cookies.
   - Axios client must set `withCredentials: true`. No tokens are stored in `localStorage` or React state.
4. **Optimistic Concurrency & Error Contract**:
   - Mail threads, shipments, and routes include optimistic concurrency tokens (`Version`).
   - Mutations resulting in `409 Conflict` (e.g. `THREAD_ALREADY_ASSIGNED`) must prompt the user with a real-time status update.

---

## 3. Directory Index

- [API_CATALOG.md](file:///d:/IT/CD/aurora-server/docs/technical/frontend/API_CATALOG.md) — Comprehensive frontend API catalog with request/response schemas, permissions, query params, error codes, and concurrency rules.
- [IMPLEMENTATION_STATUS.md](file:///d:/IT/CD/aurora-server/docs/technical/frontend/IMPLEMENTATION_STATUS.md) — End-to-end feature matrix mapping UI, BFF, RPC, Backend, and Implementation Status.
- [ROLE_PERMISSION_API_MATRIX.md](file:///d:/IT/CD/aurora-server/docs/technical/frontend/ROLE_PERMISSION_API_MATRIX.md) — Direct capability permissions and persona shell mapping.
- [FE_INTEGRATION_GUIDE.md](file:///d:/IT/CD/aurora-server/docs/technical/frontend/FE_INTEGRATION_GUIDE.md) — Step-by-step cookbook for auth, state management, and real-time push integration.
- [NOTIFICATION-FE-INTEGRATION.md](file:///d:/IT/CD/aurora-server/docs/technical/frontend/NOTIFICATION-FE-INTEGRATION.md) — Firebase Cloud Messaging (FCM) web push integration guide.
