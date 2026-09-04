# Aurora Platform — User Capability & API Authorization Matrix

> **Document ID:** `DOC-FE-AUTH-MATRIX`  
> **Source-of-Truth:** Audited against `Shared.Constants.PermissionConstants`, `BuildingBlocks.BFF.Attributes.RequirePermissionAttribute`, and all BFF controllers.  
> **Key Architecture Rule:** `ROLE != AUTHORITY`. Runtime authority is derived **strictly from direct `UserPermissions`**. Base Role defines persona and layout shell only.

---

## 1. Canonical Base Roles & Permission Templates

Aurora defines **exactly four canonical Base Roles**. Base roles act as UI persona anchors and default permission presets:

| Canonical Base Role | Scope Level | Target Persona & UX Shell Focus | Default Permission Preset Template |
|---|---|---|---|
| **`STAFF`** | Tenant / Assigned Work | Day-to-day logistics operations: create shipments, claim & reply to mail threads, optimize routes, view tracking, upload docs. | `GetDefaultStaffPermissions()`: Baseline operational access (`mail:read`, `mail:send`, `shipments:create`, `route_planning:create`, `notifications:access`). |
| **`MANAGER`** | Tenant / Supervisory | Supervisory oversight: team workload review, high-risk route governance, exception handling. | `GetDefaultManagerPermissions()`: Baseline + supervisory extensions (`mail:thread:read_all`, `mail:thread:reassign`, `mail:thread:unassign`, `route_planning:approve`, `documents:review`). |
| **`TENANT_ADMIN`** | Tenant / Administrative | Enterprise administration: staff lifecycle, direct capability permission assignment, mailbox domains, company settings. | `GetTenantAdminPermissions()`: All tenant-scoped operational, supervisory, and IAM management capabilities (`iam:*`, `mail:mailbox:manage`, `route_planning:policy:manage`). |
| **`SYSTEM_ADMIN`** | Global Platform | Platform super-administrator: tenant onboarding & suspension, global regulatory source ingestion, dead-letter queue recovery. | Platform-only capabilities (`mail:system:manage`, `compliance:platform:ingest`). |

> [!IMPORTANT]
> - "Default Template" is used during initial user invitation or when an Admin explicitly clicks **"Apply Role Defaults"**.
> - Default templates are **NOT runtime authorization sources**.
> - Changing a user's role does **NOT** automatically grant or revoke their direct permissions.
> - A user with role `STAFF` who is granted `route_planning:approve` **CAN** approve routes.
> - A user with role `MANAGER` whose permissions lack `route_planning:approve` **CANNOT** approve routes.

---

## 2. Four-Layer Authorization & Execution Pipeline

```text
┌─────────────────────────────────────────────────────────────────────────────┐
│ Layer 1: Authentication Gate (HttpOnly Cookie / Cognito JWT)                │
│ -> Verifies identity and extracts UserId, TenantId, Role, Permissions.      │
└──────────────────────────────────────┬──────────────────────────────────────┘
                                       │ PASSED
                                       ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│ Layer 2: Capability Permission Gate ([RequirePermission])                   │
│ -> Verifies if User possesses the required granular capability token.       │
└──────────────────────────────────────┬──────────────────────────────────────┘
                                       │ PASSED
                                       ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│ Layer 3: Resource Scope Gate (Tenant Isolation & Ownership)                 │
│ -> Multi-tenant isolation (TenantId == CurrentUser.TenantId).               │
│ -> Ownership boundary (e.g. Thread.PrimaryAssignee == CurrentUser.UserId).  │
└──────────────────────────────────────┬──────────────────────────────────────┘
                                       │ PASSED
                                       ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│ Layer 4: Business Governance Gate (Domain Rules & Safety Pipelines)         │
│ -> Optimistic concurrency locking (Thread.Version, Shipment.Version).       │
│ -> Route Planning Risk Assessment (High-Risk Governance Engine).             │
│ -> Outbound Mail Security Pipeline (SPF, DKIM, DMARC, ClamAV, AI Phishing). │
└─────────────────────────────────────────────────────────────────────────────┘
```

---

## 3. Comprehensive User Capability & API Matrix

| Functional Module | BFF HTTP Endpoint | Required Capability Permission | Resource Scope Enforced | Typical Persona *(Info Only)* | Implementation Status |
|---|---|---|---|---|:---:|
| **Auth** | `POST /api/v1/auth/login` | `[AllowAnonymous]` | Public | All | `CURRENT` |
| **Auth** | `POST /api/v1/auth/logout` | `[Authorize]` | Current Session | All | `CURRENT` |
| **Auth** | `GET /api/v1/auth/me` | `[Authorize]` | Current User | All | `CURRENT` |
| **Notifications** | `POST /api/v1/notifications/devices` | `notifications:access` | Current User | All | `CURRENT` |
| **Notifications** | `GET /api/v1/notifications` | `notifications:access` | Current User | All | `CURRENT` |
| **Notifications** | `GET /api/v1/notifications/unread-count` | `notifications:access` | Current User | All | `CURRENT` |
| **Notifications** | `PATCH /api/v1/notifications/{id}/read` | `notifications:access` | Current User | All | `CURRENT` |
| **Mail** | `GET /api/v1/mail/threads` (`scope=MY_WORK`) | `mail:read` | `Assignee == User.Id` | Staff, Manager | `CURRENT` |
| **Mail** | `GET /api/v1/mail/threads` (`scope=UNASSIGNED`)| `mail:read` | `Assignee == null` | Staff, Manager | `CURRENT` |
| **Mail** | `GET /api/v1/mail/threads` (`scope=ALL`) | `mail:thread:read_all` | Tenant-wide | Manager, Supervisor | `CURRENT` |
| **Mail** | `POST /api/v1/mail/threads/{id}/claim` | `mail:thread:claim` | Thread unassigned | Staff, Manager | `CURRENT` |
| **Mail** | `POST /api/v1/mail/threads/{id}/reassign` | `mail:thread:reassign` | Tenant thread | Manager, Lead | `CURRENT` |
| **Mail** | `POST /api/v1/mail/threads/{id}/unassign` | `mail:thread:unassign` | Tenant thread | Manager, Lead | `CURRENT` |
| **Mail** | `GET /api/v1/mail/threads/{id}/assignment-history` | `mail:read` | Tenant thread | Staff, Manager | `CURRENT` |
| **Mail** | `POST /api/v1/mail/drafts` | `mail:draft:create` | Tenant thread | Staff, Manager | `CURRENT` |
| **Mail** | `POST /api/v1/mail/messages/outbound` | `mail:send` | Tenant mailbox | Staff, Manager | `CURRENT` |
| **Mail** | `GET /api/v1/mail/quarantine` | `mail:quarantine:read` | Tenant quarantine | Manager, Security Lead | `CURRENT` |
| **Mail** | `POST /api/v1/mail/quarantine/{id}/release` | `mail:quarantine:release` | Tenant quarantine | Manager, Security Lead | `CURRENT` |
| **Mail Admin**| `POST /api/v1/admin/mail/domains` | `mail:domain:manage` | Tenant Domain | Tenant Admin | `CURRENT_LEGACY` |
| **Mail Admin**| `GET /api/v1/admin/mail/domains` | `mail:domain:manage` | Tenant Domain | Tenant Admin | `TARGET (BACKEND_REQUIRED)` |
| **Mail Admin**| `POST /api/v1/admin/mail/mailboxes` | `mail:mailbox:manage` | Tenant Mailbox | Tenant Admin | `CURRENT` |
| **Mail Admin**| `POST /api/v1/admin/mail/aliases` | `mail:mailbox:manage` | Tenant Mailbox | Tenant Admin | `CURRENT` |
| **Mail Admin**| `DELETE /api/v1/admin/mail/quarantine/{id}` | `mail:quarantine:delete` | Tenant Threat | Tenant Admin | `CURRENT` |
| **Mail Admin**| `GET /api/v1/admin/mail/audit` | `mail:audit:read` | Tenant Audit | Tenant Admin | `CURRENT` |
| **Mail System**| `POST /api/v1/system/mail/dead-letter/{id}/requeue` | `mail:system:manage` | Global Dead Letter | System Admin | `CURRENT` |
| **Shipments** | `POST /api/v1/shipments` | `shipments:create` | Tenant Shipment | Staff, Operator | `CURRENT` |
| **Shipments** | `GET /api/v1/shipments` & `GET /{id}` | `shipments:read` | Tenant Shipment | All Staff Personas | `CURRENT` |
| **Shipments** | `PUT /api/v1/shipments/{id}` | `shipments:update` | Tenant Shipment | Staff, Operator | `CURRENT` |
| **Shipments** | `POST /api/v1/shipments/{id}/submit` | `shipments:submit` | Tenant Shipment | Staff, Operator | `CURRENT` |
| **Shipments** | `POST /api/v1/shipments/{id}/cancel` | `shipments:cancel` | Tenant Shipment | Staff, Operator | `CURRENT` |
| **Shipments** | `POST /api/v1/shipments/{id}/milestones` | `shipments:milestones:update` | Tenant Shipment | Staff, Operator | `CURRENT` |
| **Shipments** | `POST /api/v1/shipments/{id}/documents` | `documents:attach` | Tenant Shipment | Staff, Operator | `CURRENT` |
| **Routes** | `POST /api/v1/routes` | `route_planning:create` | Tenant Route | Staff, Planner | `CURRENT` |
| **Routes** | `GET /api/v1/routes` | `route_planning:read` | Tenant Route | Staff, Planner | `CURRENT` |
| **Routes** | `POST /api/v1/routes/{id}/optimize` | `route_planning:optimize` | Tenant Route | Staff, Planner | `CURRENT` |
| **Routes** | `POST /api/v1/routes/{id}/evaluate-risk` | `route_planning:risk:evaluate` | Tenant Route | Staff, Planner | `CURRENT` |
| **Routes** | `POST /api/v1/routes/{id}/dispatch` | `route_planning:dispatch` | Tenant Route | Staff, Planner | `CURRENT` |
| **Routes** | `GET /api/v1/approvals` | `route_planning:approve` | Tenant Route | Manager, Risk Lead | `CURRENT` |
| **Routes** | `POST /api/v1/approvals/{id}/approve` | `route_planning:approve` | Tenant Route | Authorized Approver | `CURRENT` |
| **Routes** | `POST /api/v1/approvals/{id}/reject` | `route_planning:approve` | Tenant Route | Authorized Approver | `CURRENT` |
| **OCR** | `POST /api/v1/documents/shipment` | `shipments:create` | Tenant Document | Staff, Document Clerk | `CURRENT` |
| **OCR** | `GET /api/v1/documents/jobs/{jobId}` | `shipments:read` | Tenant Document | Staff, Document Clerk | `CURRENT` |
| **OCR** | `POST /api/v1/documents/jobs/{jobId}/review` | `documents:review` | Tenant Document | Document Reviewer | `CURRENT` |
| **Compliance**| `POST /api/v1/compliance/evaluations` | `compliance:evaluate` | Tenant Shipment | Customs Specialist | `CURRENT` |
| **Compliance**| `POST /api/v1/compliance/rag/query` | `compliance:read` | Jurisdiction | Compliance Staff | `CURRENT` |
| **Tracking** | `GET /api/v1/tracking/{id}/current` | `shipments:read` | Tenant Vehicle/Shipment | Staff, Dispatcher | `CURRENT` |
| **Tracking** | `GET /api/v1/tracking/{id}/history` | `shipments:read` | Tenant Vehicle/Shipment | Staff, Dispatcher | `CURRENT` |
| **Tracking** | `POST /api/v1/tracking/geofences` | `shipments:update` | Tenant Geofence | Dispatch Manager | `CURRENT` |
| **Financial** | `POST /api/v1/financial/estimate-cost` | `financial:calculate` | Tenant Cost Table | Staff, Pricing Specialist | `CURRENT` |
| **Financial** | `POST /api/v1/financial/customs-duty` | `financial:calculate` | Tariff Code | Staff, Customs Clerk | `CURRENT` |
| **Billing** | `POST /api/v1/invoices/generate` | `billing:invoice:create` | Tenant Shipment | Billing Clerk | `CURRENT` |
| **Billing** | `GET /api/v1/invoices/{id}` | `billing:invoice:read` | Tenant Invoice | Billing Staff | `CURRENT` |
| **Billing** | `POST /api/v1/invoices/{id}/pay` | `billing:invoice:pay` | Tenant Invoice | Billing Staff | `CURRENT` |
| **Billing** | `GET /api/v1/billing/credit-check` | `billing:credit:read` | Customer Credit | Billing Staff | `CURRENT` |
| **Billing** | `POST /api/v1/escrow/lock` | `billing:escrow:manage` | Escrow Account | Billing Supervisor | `CURRENT` |
| **Billing** | `POST /api/v1/escrow/release` | `billing:escrow:manage` | Escrow Account | Billing Supervisor | `CURRENT` |
| **Negotiation**| `POST /api/v1/negotiations/{id}/mail-draft` | `mail:draft:create` | Negotiation Session | Commercial Staff | `CURRENT` |
| **Admin IAM** | `POST /api/v1/admin/staff` | `iam:user:invite` | Current Tenant | Tenant Admin | `CURRENT` |
| **Admin IAM** | `GET /api/v1/admin/staff` | `iam:user:read` | Current Tenant | Tenant Admin | `CURRENT` |
| **Admin IAM** | `PATCH /api/v1/admin/staff/{id}/role` | `iam:role:manage` | Current Tenant | Tenant Admin | `CURRENT` |
| **Admin IAM** | `PUT /api/v1/admin/staff/{id}/permissions` | `iam:permission:manage` | Current Tenant | Tenant Admin | `CURRENT` |
| **Admin Ops** | `PUT /api/v1/admin/ai-configs/{feature}` | `route_planning:policy:manage` | Current Tenant | Tenant Admin | `CURRENT` |
| **Admin Ops** | `PUT /api/v1/admin/rule-configs/{ruleName}` | `route_planning:policy:manage` | Current Tenant | Tenant Admin | `CURRENT` |
| **Admin Audit**| `GET /api/v1/admin/audit-logs` | `TENANT_ADMIN` role | Current Tenant | Tenant Admin | `CURRENT` |
