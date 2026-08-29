# Aurora Server — User Capability & API Authorization Matrix

> **Source-of-Truth**: Audited against `Shared.Constants.PermissionConstants`, `BuildingBlocks.BFF.Attributes.RequirePermissionAttribute`, and all BFF controllers.  
> **Key Architecture Rule**: `ROLE != AUTHORITY`. Runtime authority is derived **strictly from direct `UserPermissions`**. Base Role defines persona and layout shell only.

---

## 1. Canonical Base Roles & Permission Templates

Aurora defines **exactly four canonical Base Roles**. Base roles act as UI persona anchors and default permission presets:

| Canonical Base Role | Scope Level | Target Persona & UX Shell Focus | Default Permission Preset Template |
|---|---|---|---|
| **`STAFF`** | Tenant / Assigned Work | Day-to-day logistics operations: create shipments, claim & reply to mail threads, optimize routes, view tracking, upload docs. | `GetDefaultStaffPermissions()`: Baseline operational access (`mail:read`, `mail:send`, `shipments:create`, `route_planning:create`, `financial_tax:read`, `billing_settlement:read`). |
| **`MANAGER`** | Tenant / Supervisory | Supervisory oversight: team workload review, high-risk route governance, exception handling. | `GetDefaultManagerPermissions()`: Baseline + supervisory extensions (`mail:thread:reassign`, `route_planning:approve`, `ocr:review`, `compliance:override`, `billing_settlement:settlement:manage`). |
| **`TENANT_ADMIN`** | Tenant / Administrative | Enterprise administration: staff lifecycle, direct capability permission assignment, mailbox domains, company settings. | `GetTenantAdminPermissions()`: All tenant-scoped operational, supervisory, and IAM management capabilities. |
| **`SYSTEM_ADMIN`** | Global Platform | Platform super-administrator: tenant onboarding & suspension, global regulatory source ingestion, dead-letter queue recovery. | Platform-only capabilities (`mail:system:manage`, `compliance:platform:ingest`). |

> [!IMPORTANT]
> - "Default Template" is used during initial user invitation or when an Admin explicitly clicks **"Apply Role Defaults"**.
> - Default templates are **NOT runtime authorization sources**.
> - Changing a user's role does **NOT** automatically grant or revoke their direct permissions.
> - A user with role `STAFF` who is granted `route_planning:approve` **CAN** approve routes.
> - A user with role `MANAGER` whose permissions lack `route_planning:approve` **CANNOT** approve routes.

---

## 2. Four-Layer Authorization & Execution Pipeline

```
+─────────────────────────────────────────────────────────────────────────────+
| Layer 1: Authentication Gate (Session Cookie / Cognito JWT)                 |
| -> Verifies identity and extracts UserId, TenantId, Role, Permissions.      |
+──────────────────────────────────────┬──────────────────────────────────────+
                                       │ PASSED
                                       v
+─────────────────────────────────────────────────────────────────────────────+
| Layer 2: Capability Permission Gate ([RequirePermission])                   |
| -> Verifies if User possesses the required granular capability token.       |
+──────────────────────────────────────┬──────────────────────────────────────+
                                       │ PASSED
                                       v
+─────────────────────────────────────────────────────────────────────────────+
| Layer 3: Resource Scope Gate (Tenant Isolation & Ownership)                 |
| -> Multi-tenant isolation (TenantId == CurrentUser.TenantId).               |
| -> Ownership boundary (e.g. Thread.PrimaryAssignee == CurrentUser.UserId).  |
+──────────────────────────────────────┬──────────────────────────────────────+
                                       │ PASSED
                                       v
+─────────────────────────────────────────────────────────────────────────────+
| Layer 4: Business Governance Gate (Domain Rules & Safety Pipelines)         |
| -> Finite state machine transitions (e.g. DRAFT -> SUBMITTED).              |
| -> Route Planning Risk Assessment & Execution Authorization.                |
| -> Outbound Mail Security Pipeline (SPF, DKIM, DMARC, ClamAV, AI Phishing). |
+─────────────────────────────────────────────────────────────────────────────+
```

---

## 3. Comprehensive User Capability & API Matrix

| Functional Module | BFF HTTP Endpoint | Required Capability Permission | Resource Scope Enforced | Typical Persona *(Info Only)* | Default Template | Status |
|---|---|---|---|---|---|:---:|
| **Auth** | `GET /api/v1/auth/login` | `[AllowAnonymous]` | Public | All | All | `READY` |
| **Auth** | `POST /api/v1/auth/logout` | `[Authorize]` | Current Session | All | All | `READY` |
| **Auth** | `GET /api/v1/auth/me` | `[Authorize]` | Current User | All | All | `READY` |
| **Mail** | `GET /api/v1/mail/threads` (`MY_WORK`) | `mail:read` | `AssignedUserId == User.Id` | Staff, Manager | `STAFF`, `MANAGER`, `TENANT_ADMIN` | `READY` |
| **Mail** | `GET /api/v1/mail/threads` (`UNASSIGNED`)| `mail:read` | `AssignedUserId == null` | Staff, Manager | `STAFF`, `MANAGER`, `TENANT_ADMIN` | `READY` |
| **Mail** | `GET /api/v1/mail/threads` (`ALL`) | `mail:thread:read_all` | Tenant-wide | Manager, Supervisor | `MANAGER`, `TENANT_ADMIN` | `READY` |
| **Mail** | `POST /api/v1/mail/threads/{id}/claim` | `mail:thread:claim` | Thread unassigned | Staff, Manager | `STAFF`, `MANAGER`, `TENANT_ADMIN` | `READY` |
| **Mail** | `POST /api/v1/mail/threads/{id}/reassign` | `mail:thread:reassign` | Tenant thread | Manager, Lead | `MANAGER`, `TENANT_ADMIN` | `READY` |
| **Mail** | `POST /api/v1/mail/threads/{id}/unassign` | `mail:thread:unassign` | Tenant thread | Manager, Lead | `MANAGER`, `TENANT_ADMIN` | `READY` |
| **Mail** | `GET /api/v1/mail/threads/{id}/assignment-history` | `mail:read` | Tenant thread | Staff, Manager | `STAFF`, `MANAGER`, `TENANT_ADMIN` | `READY` |
| **Mail** | `POST /api/v1/mail/drafts` | `mail:draft:create` | Tenant thread | Staff, Manager | `STAFF`, `MANAGER`, `TENANT_ADMIN` | `READY` |
| **Mail** | `POST /api/v1/mail/messages/outbound` | `mail:send` | `AssignedUserId == User.Id` | Staff, Manager | `STAFF`, `MANAGER`, `TENANT_ADMIN` | `READY` |
| **Mail** | `GET /api/v1/mail/quarantine` | `mail:quarantine:read` | Tenant quarantine | Manager, Security Lead | `MANAGER`, `TENANT_ADMIN` | `READY` |
| **Mail** | `POST /api/v1/mail/quarantine/{id}/release` | `mail:quarantine:release` | Tenant quarantine | Manager, Security Lead | `MANAGER`, `TENANT_ADMIN` | `READY` |
| **Mail Admin**| `POST /api/v1/admin/mail/domains` | `mail:domain:manage` | Tenant Domain | Tenant Admin | `TENANT_ADMIN` | `READY` |
| **Mail Admin**| `POST /api/v1/admin/mail/mailboxes` | `mail:mailbox:manage` | Tenant Mailbox | Tenant Admin | `TENANT_ADMIN` | `READY` |
| **Mail System**| `POST /api/v1/system/mail/dead-letter/{id}/requeue` | `mail:system:manage` | Global Dead Letter | System Admin | `SYSTEM_ADMIN` | `READY` |
| **Shipments** | `POST /api/v1/shipments` | `shipments:create` | Tenant Shipment | Staff, Operator | `STAFF`, `MANAGER`, `TENANT_ADMIN` | `READY` |
| **Shipments** | `GET /api/v1/shipments` & `GET /{id}` | `shipments:read` | Tenant Shipment | All Staff Personas | `STAFF`, `MANAGER`, `TENANT_ADMIN` | `READY` |
| **Shipments** | `PUT /api/v1/shipments/{id}` | `shipments:update` | Tenant Shipment | Staff, Operator | `STAFF`, `MANAGER`, `TENANT_ADMIN` | `READY` |
| **Shipments** | `POST /api/v1/shipments/{id}/submit` | `shipments:submit` | Tenant Shipment | Staff, Operator | `STAFF`, `MANAGER`, `TENANT_ADMIN` | `READY` |
| **Shipments** | `POST /api/v1/shipments/{id}/cancel` | `shipments:cancel` | Tenant Shipment | Manager, Supervisor | `MANAGER`, `TENANT_ADMIN` | `READY` |
| **Shipments** | `DELETE /api/v1/shipments/{id}` | `shipments:delete` | Tenant Shipment | Manager, Supervisor | `MANAGER`, `TENANT_ADMIN` | `READY` |
| **Shipments** | `POST /api/v1/shipments/import` | `shipments:import` | Tenant Shipment | Manager, Data Lead | `MANAGER`, `TENANT_ADMIN` | `READY` |
| **Routes** | `POST /api/v1/routes` | `route_planning:create` | Tenant Route | Staff, Planner | `STAFF`, `MANAGER`, `TENANT_ADMIN` | `READY` |
| **Routes** | `POST /api/v1/routes/{id}/optimize` | `route_planning:optimize` | Tenant Route | Staff, Planner | `STAFF`, `MANAGER`, `TENANT_ADMIN` | `READY` |
| **Routes** | `GET /api/v1/approvals/routes` | `route_planning:approval:read`| Tenant Route | Manager, Risk Lead | `MANAGER`, `TENANT_ADMIN` | `READY` |
| **Routes** | `POST /api/v1/approvals/routes/{id}/approve`| `route_planning:approve` | Tenant Route | Authorized Approver | `MANAGER`, `TENANT_ADMIN` | `READY` |
| **Routes** | `POST /api/v1/approvals/routes/{id}/reject` | `route_planning:reject` | Tenant Route | Authorized Approver | `MANAGER`, `TENANT_ADMIN` | `READY` |
| **OCR** | `POST /api/v1/documents/ocr/jobs` | `documents:ingest` | Tenant Document | Staff, Document Clerk | `MANAGER`, `TENANT_ADMIN` | `READY` |
| **OCR** | `POST /api/v1/documents/ocr/jobs/{id}/review` | `ocr:review` | Tenant Document | Document Reviewer | `MANAGER`, `TENANT_ADMIN` | `READY` |
| **Compliance**| `POST /api/v1/compliance/evaluate` | `compliance:override` *(if overriding)* | Tenant Shipment | Customs Specialist | `MANAGER`, `TENANT_ADMIN` | `READY` |
| **Compliance**| `POST /api/v1/compliance/grounded-answer` | `documents:manage` *(or read)* | Tenant / Public | Compliance Staff | `MANAGER`, `TENANT_ADMIN` | `READY` |
| **Ingestion** | `POST /api/v1/admin/ingestion/regulatory-sources` | `compliance:platform:ingest` | Platform / Tenant | Platform Ingestion Lead | `SYSTEM_ADMIN` | `READY` |
| **Tracking** | `GET /api/v1/tracking/{id}/current` | `shipments:read` | Tenant Vehicle/Shipment | Staff, Dispatcher | `STAFF`, `MANAGER`, `TENANT_ADMIN` | `READY` |
| **Tracking** | `POST /api/v1/tracking/geofences` | `gps_tracking:geofence:manage` | Tenant Geofence | Dispatch Manager | `MANAGER`, `TENANT_ADMIN` | `READY` |
| **Financial** | `POST /api/v1/financial/estimate-cost` | `financial_tax:calculate` | Tenant Cost Table | Staff, Pricing Specialist | `STAFF`, `MANAGER`, `TENANT_ADMIN` | `READY` |
| **Financial** | `POST /api/v1/financial/customs-duty` | `financial_tax:calculate` | Tenant Tariff | Customs Specialist | `STAFF`, `MANAGER`, `TENANT_ADMIN` | `READY` |
| **Billing** | `POST /api/v1/billing/invoices/generate`| `billing_settlement:invoice:create` | Tenant Invoice | Billing Specialist | `MANAGER`, `TENANT_ADMIN` | `READY` |
| **Billing** | `POST /api/v1/billing/escrow/release` | `billing_settlement:settlement:manage` | Tenant Escrow Wallet | Finance Lead, Manager | `MANAGER`, `TENANT_ADMIN` | `READY` |
| **Admin IAM** | `POST /api/v1/admin/staff` | `iam:user:invite` | Tenant Staff User | Tenant Admin | `TENANT_ADMIN` | `READY` |
| **Admin IAM** | `PATCH /api/v1/admin/staff/{id}/role` | `iam:role:manage` | Tenant Staff User | Tenant Admin | `TENANT_ADMIN` | `READY` |
| **Admin IAM** | `GET /api/v1/admin/staff/{id}/permissions`| `iam:user:read` | Tenant Staff User | Tenant Admin | `TENANT_ADMIN` | `READY` |
| **Admin IAM** | `PATCH /api/v1/admin/staff/{id}/permissions`| `iam:permission:manage` | Tenant Staff User | Tenant Admin | `TENANT_ADMIN` | `READY` |
| **Admin IAM** | `PATCH /api/v1/admin/staff/permissions` | `iam:permission:manage` | Tenant Staff Users (Bulk) | Tenant Admin | `TENANT_ADMIN` | `READY` |
| **Admin IAM** | `GET /api/v1/admin/roles` | `iam:role:read` | Tenant Roles Catalog | Tenant Admin | `TENANT_ADMIN` | `READY` |
| **System IAM**| `POST /api/v1/system/tenants` | Role: `SYSTEM_ADMIN` | Global Tenants | System Admin | `SYSTEM_ADMIN` | `READY` |
| **System IAM**| `PATCH /api/v1/system/tenants/{id}/status`| Role: `SYSTEM_ADMIN` | Global Tenants | System Admin | `SYSTEM_ADMIN` | `READY` |
