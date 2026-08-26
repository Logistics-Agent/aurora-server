# Aurora Platform - BFF API Architecture & Catalog

> **Document ID:** `DOC-BFF-00`  
> **Status:** Canonical BFF Architecture & API Catalog Baseline  
> **Scope:** Architecture blueprint and master index for the Aurora Backend-for-Frontend (BFF) layer across `Staff.Bff`, `Admin.Bff`, and `System.Bff`.  
> **Architecture Reference:** `codex/requirement.md`, `docs/api-analysis/06-bff-api-input.md`

---

## 1. Architecture Principles

The Aurora BFF layer acts as the single external gateway for web portals and mobile applications, mediating between external HTTP REST/WebSocket clients and internal gRPC microservices.

```text
Client (Web / Mobile)
    -> Cloudflare / Gateway (Rate Limiting, SSL Termination)
    -> BFF (Authentication, Role Authorization, Request Validation, DTO Mapping)
    -> Inter-service gRPC with ClientMetadataInterceptor (x-user-id, x-tenant-id, x-role-ids)
    -> Internal Microservices (.NET, Java, NestJS)
```

### 1.1. Core Responsibilities

1. **Authentication & Identity Propagation**:
   - Validates incoming AWS Cognito JWT Bearer tokens.
   - Strips client-supplied `x-*` headers to eliminate header injection attacks.
   - Injects trusted identity metadata (`x-user-id`, `x-tenant-id`, `x-role-ids`, `x-permission-version`, `x-trace-id`) via `ClientMetadataInterceptor`.
2. **Tenant Isolation**:
   - `TenantId` is **NEVER** accepted from client query strings or payload bodies for tenant-scoped operations.
   - Normal users fail closed (401/403) if tenant identity context is missing.
3. **Resilience & Fault Tolerance**:
   - Integrates Polly resilience pipelines with standard retry, circuit breaker, and timeout policies.
4. **Protobuf & DTO Translation**:
   - Maps strongly typed JSON payloads to Protobuf requests and Protobuf responses back to JSON DTOs.
   - Translates gRPC status codes (`NotFound` -> 404, `InvalidArgument` -> 400, `PermissionDenied` -> 403, `AlreadyExists` -> 409) into standardized RFC 7807 problem details.

---

## 2. Platform Role Separation & Catalog Index

The BFF API catalog is strictly partitioned to eliminate duplicate endpoint implementations and enforce least-privilege security boundaries:

```text
docs/bff-api/
├── README.md           # This document - Architecture & Master Index
├── staff-api.md        # STAFF_ONLY exclusive operational APIs
├── manager-api.md      # MANAGER_ONLY exclusive supervisory & approval APIs
├── admin-api.md        # ADMIN_ONLY exclusive tenant administration APIs
├── system-api.md       # SYSTEM_ONLY exclusive platform provisioning & SRE APIs
├── shared-api.md       # SHARED APIs accessible across >= 2 platform roles
├── blocked-api.md      # BLOCKED APIs requiring backend contract/implementation work
└── API-MATRIX.md       # Comprehensive Post-Implementation Traceability Matrix
```

### 2.1. Role Taxonomy

| Role | Target BFF Gateway | Scope & Responsibility | Catalog Reference |
| :--- | :--- | :--- | :--- |
| **`STAFF`** | `Staff.Bff` | Daily operational execution within tenant (Shipments, Cargo, Routes, OCR, Mail). | [staff-api.md](file:///D:/IT/CD/aurora-server/docs/bff-api/staff-api.md) |
| **`MANAGER`** | `Staff.Bff` | Supervisory gates, dual-control approvals, exception resolutions, financial adjustments. | [manager-api.md](file:///D:/IT/CD/aurora-server/docs/bff-api/manager-api.md) |
| **`ADMIN`** | `Admin.Bff` | Intra-tenant organization governance, staff provisioning, AI & rule configuration. | [admin-api.md](file:///D:/IT/CD/aurora-server/docs/bff-api/admin-api.md) |
| **`SYSTEM`** | `System.Bff` / Internal | Cross-tenant provisioning, platform administration, SRE automation, dead-letter recovery. | [system-api.md](file:///D:/IT/CD/aurora-server/docs/bff-api/system-api.md) |
| **`SHARED`** | `Staff/Admin/System` | Multi-role access (Shipment viewing, tracking, notifications, auth). | [shared-api.md](file:///D:/IT/CD/aurora-server/docs/bff-api/shared-api.md) |
| **`BLOCKED`** | N/A | Missing backend protobuf contracts or server implementations. | [blocked-api.md](file:///D:/IT/CD/aurora-server/docs/bff-api/blocked-api.md) |
