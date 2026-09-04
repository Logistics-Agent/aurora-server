# Aurora Platform — BFF API Architecture & Catalog

> **Document ID:** `DOC-BFF-00`  
> **Status:** Canonical BFF Architecture & Master Index (Synchronized with .NET 10 BFF Source)  
> **Scope:** Architecture blueprint and master catalog across `Admin.Bff`, `Staff.Bff`, and `System.Bff`.  
> **Source Precedence:** Source Code & Protos > docs/technical/frontend > docs/bff-api > Figma UI Specs.

---

## 1. Architecture Principles

The Aurora Backend-for-Frontend (BFF) layer serves as the single entry point for web clients and mobile applications, mediating between external HTTP REST requests and internal gRPC microservices.

```text
Browser / Web Client
    ↓ (HTTPS + HttpOnly Session Cookies)
Cloudflare / YARP Gateway (SSL Termination, Rate Limiting)
    ↓
Aurora BFF Layer (Admin.Bff | Staff.Bff | System.Bff)
    ├── Authenticates Session (AWS Cognito OIDC JWT)
    ├── Enforces Direct Capability Permissions ([RequirePermission])
    ├── Validates & Maps DTOs
    └── Injects ClientMetadataInterceptor (x-user-id, x-tenant-id, x-trace-id)
    ↓ (Internal gRPC + Private Service Network)
Internal Microservices (.NET 10, Java 21, NestJS)
```

### 1.1. Core Security & Persona Model

1. **Role != Authority**:
   - **Base Role** (`TENANT_ADMIN`, `MANAGER`, `STAFF`, `SYSTEM_ADMIN`) defines the application shell and default layout persona.
   - **Direct Capability Permission** (e.g. `mail:thread:claim`, `route_planning:approve`) grants actual runtime authority.
   - **Resource Scope** (e.g. `TenantId`, `ShipmentId`, `MailboxId`) restricts the operational boundary.
   - Legacy `StaffType` (Operations, Documentation, CS, Finance) is **100% removed**.
2. **Strict Multi-Tenant Isolation**:
   - `TenantId` is derived exclusively from the authenticated JWT session context (`ICurrentUserService.TenantId`).
   - Client requests cannot supply or override `TenantId`.
3. **Session Cookies & Token Protection**:
   - Tokens (`access_token`, `refresh_token`) are stored in secure `HttpOnly` cookies. They are never exposed in JSON response bodies.
4. **Protobuf & DTO Mapping**:
   - Strongly typed JSON DTOs are mapped to Protobuf requests, translating gRPC status codes into standardized RFC 7807 problem details.

---

## 2. Platform Persona Shells & Catalog Index

```text
docs/bff-api/
├── README.md           # This document - Architecture & Master Index
├── admin-api.md        # Tenant Admin Console APIs (Admin.Bff)
├── staff-api.md        # Operations Workspace APIs (Staff.Bff - Staff execution)
├── manager-api.md      # Operations Workspace Supervisory Gates (Staff.Bff - Manager capabilities)
├── shared-api.md       # Shared APIs (Auth, Notifications, Search, Dashboard)
├── system-api.md       # System Admin Platform & SRE APIs (System.Bff)
├── blocked-api.md      # Gap analysis & target APIs requiring backend implementation
└── API-MATRIX.md       # Comprehensive End-to-End Traceability Matrix
```

### 2.1. Persona Shell Mapping

| Persona | Application Shell | Gateway | Primary Responsibility | Reference |
|---|---|---|---|---|
| **`TENANT_ADMIN`** | **Aurora Admin Console** | `Admin.Bff` | People & Access, Operations Configuration, Mail Administration, Tenant Audit. | [admin-api.md](file:///d:/IT/CD/aurora-server/docs/bff-api/admin-api.md) |
| **`STAFF`** | **Aurora Operations Workspace** | `Staff.Bff` | Daily execution: Shipments, Routes, OCR Documents, Compliance, Mail Triage & Sending, Tracking. | [staff-api.md](file:///d:/IT/CD/aurora-server/docs/bff-api/staff-api.md) |
| **`MANAGER`** | **Aurora Operations Workspace** | `Staff.Bff` | Operations supervision, Route risk approvals, Mail reassignment/unassignment, Supervisory queue (`ALL`). | [manager-api.md](file:///d:/IT/CD/aurora-server/docs/bff-api/manager-api.md) |
| **`SYSTEM_ADMIN`** | **System Admin Control Plane** | `System.Bff` | Cross-tenant provisioning, Platform law ingestion, System dead-letter recovery. | [system-api.md](file:///d:/IT/CD/aurora-server/docs/bff-api/system-api.md) |
| **`MULTI-ROLE`** | **Shared Surface** | `Staff.Bff` / `Admin.Bff` | Authentication session, Notification center, Unified search, Summary dashboard. | [shared-api.md](file:///d:/IT/CD/aurora-server/docs/bff-api/shared-api.md) |
