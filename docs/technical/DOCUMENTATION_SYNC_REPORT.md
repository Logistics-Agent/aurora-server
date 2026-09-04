# Documentation Synchronization Report

> **Document ID:** `DOC-SYNC-REPORT-2026-09-04`  
> **Audited By:** Aurora Platform Architecture & Review Agent  
> **Scope:** Full-codebase reverse audit, documentation synchronization, contract alignment, UI specification overhaul, and backend gap analysis.

---

## 1. Superpowers Plans Summary

### Plan 1: Notification FCM Push (`docs/superpowers/plans/2026-08-29-notification-fcm-push.md`)
- **Status:** `IMPLEMENTED` (Backend C# / gRPC / BFF / MassTransit Consumers).
- **Core Architecture:**
  - Browser interacts strictly via `Staff.Bff` with session cookies.
  - BFF enforces `[Authorize]` and `[RequirePermission(PermissionConstants.Notification.Access)]` (`notifications:access`).
  - `Staff.Bff` authenticates to `Notification` gRPC using `x-service-id: staff-bff` and `x-service-api-key`, verified with fixed-time SHA-256 comparison before `AuthInterceptor` runs.
  - Producers emit domain events via Transactional Outbox to RabbitMQ.
  - Audience resolves strictly via `ShipmentSubscription` (`(TenantId, UserId, ShipmentId)`). When no audience is found, it persists a duplicate-safe `NoRecipient` outcome and acknowledges the message without tenant broadcast.
  - Firebase Admin JSON remains on server; client uses public Web SDK credentials.

### Plan 2: Frontend FCM Popup (`docs/superpowers/plans/2026-08-30-frontend-fcm-popup.md`)
- **Status:** `PARTIAL` (Implemented in sibling repository `aurora-client`; runtime acceptance requires active Firebase project credentials and live gateway).
- **Core Architecture:**
  - Next.js 16 / React 19 client with Axios `withCredentials: true`.
  - Public Firebase Web SDK initialization and VAPID key.
  - Foreground Sonner toasts, background `firebase-messaging-sw.js` notifications, unread count polling, and safe route navigation (`/shipments/{id}`, `/notifications`).

---

## 2. Current Architecture Verified

1. **Role != Authority Invariant**:
   - Exactly four canonical personas (`SYSTEM_ADMIN`, `TENANT_ADMIN`, `MANAGER`, `STAFF`).
   - Legacy `StaffType` (Operations, Documentation, CS, Finance) is 100% eliminated from backend code, database, protos, and documentation.
   - All authorization gates enforce direct capability permissions (`[RequirePermission]`) and resource scopes (`TenantId`, `MailboxId`, `PrimaryAssigneeUserId`).
2. **One Aurora Application Experience**:
   - Aurora Admin Console (`/admin/*`) for `TENANT_ADMIN`.
   - Aurora Operations Workspace (`/ops/*` or `/`) for `STAFF` and `MANAGER`.
   - Mail is an integrated module within both persona shells, not a standalone application.
3. **Mail Architecture**:
   - Stalwart is the underlying mail server infrastructure managed by System Admins.
   - Mailbox represents company identity (e.g. `operations@acmelogistics.com`).
   - `EmailThread` is the operational work item, assigned to a single `PrimaryAssigneeUserId` with optimistic concurrency locking (`Version`).
   - Outbound sending logs authenticated human author (`SentByUserId`).

---

## 3. Documentation Drift Found & Resolved

| Drift Area | Historical / Documentation Assumption | Actual Source / Target Reality | Resolution |
|---|---|---|---|
| **Mail Domain Provisioning** | Tenant Admin can provision arbitrary domains via `POST /api/v1/admin/mail/domains`. | Target policy mandates System Admin provisioning on Stalwart and assignment to tenants. | Marked current endpoint as `CURRENT_LEGACY`, removed `+ Add Domain` from target UI, and documented `GET /api/v1/admin/mail/domains` as `BACKEND_REQUIRED`. |
| **Default Mailbox** | Assumed existing `DefaultMailboxId` / `IsDefault`. | Missing in current `Mailbox` entity and database schema. | Documented as `BACKEND_REQUIRED` (recommended `TenantMailConfig.DefaultMailboxId`). |
| **Alias Targets** | Assumed multi-target fan-out (`List<string> Targets`). | Target MVP requires 1 Alias -> exactly 1 canonical Shared Mailbox to avoid duplicate thread processing. | Documented as `TARGET_CHANGE_REQUIRED` / `BACKEND_REFACTOR_REQUIRED`. |
| **Mailbox Password Reset** | Admin Mail UI had "Reset Mailbox Credentials" actions. | Human auth is Cognito OIDC; Stalwart password reset is no-op in v1. | Removed password reset actions from standard Admin Mail UI. |
| **StaffType Residue** | Legacy `staffType: 1` in old API samples. | Zero `StaffType` in backend code. | Purged all `staffType` references across all API catalogs and Figma specs. |
| **Standalone Mail App** | Mail described as separate application. | Mail is an integrated module inside Aurora Admin Console and Aurora Operations Workspace. | Restructured Figma UI specifications and BFF documentation. |

---

## 4. Files Updated

### Superpowers Plans Status
- `docs/superpowers/plans/PLAN_STATUS.md` [NEW]

### BFF API Documentation (`docs/bff-api/`)
- `docs/bff-api/README.md` [MODIFIED]
- `docs/bff-api/admin-api.md` [MODIFIED]
- `docs/bff-api/staff-api.md` [MODIFIED]
- `docs/bff-api/manager-api.md` [MODIFIED]
- `docs/bff-api/shared-api.md` [MODIFIED]
- `docs/bff-api/system-api.md` [MODIFIED]
- `docs/bff-api/blocked-api.md` [MODIFIED]
- `docs/bff-api/API-MATRIX.md` [MODIFIED]

### Frontend Technical Documentation (`docs/technical/frontend/`)
- `docs/technical/frontend/README.md` [NEW]
- `docs/technical/frontend/API_CATALOG.md` [MODIFIED]
- `docs/technical/frontend/IMPLEMENTATION_STATUS.md` [NEW]
- `docs/technical/frontend/ROLE_PERMISSION_API_MATRIX.md` [MODIFIED]

### Figma UI Specifications (`docs/figma/`)
- `docs/figma/admin-ops-01-product-context.md` [MODIFIED]
- `docs/figma/admin-ops-02-ui-spec.md` [MODIFIED]
- `docs/figma/admin-mail-01-product-context.md` [MODIFIED]
- `docs/figma/admin-mail-02-ui-spec.md` [MODIFIED]
- `docs/figma/staff-mail-01-product-context.md` [MODIFIED]
- `docs/figma/staff-mail-02-ui-spec.md` [MODIFIED]

### Root Documentation
- `README.md` [MODIFIED]
- `README.vi.md` [MODIFIED]

### Final Report
- `docs/technical/DOCUMENTATION_SYNC_REPORT.md` [NEW]

---

## 5. BFF/API Contract Changes

1. **Authentication Session**: Documented HttpOnly cookie session endpoints (`/api/v1/auth/identify`, `/api/v1/auth/login`, `/api/v1/auth/refresh`, `/api/v1/auth/logout`, `/api/v1/auth/me`).
2. **Notification Center**: Fully specified `POST /api/v1/notifications/devices`, `DELETE /api/v1/notifications/devices/{id}`, `GET /api/v1/notifications`, `GET /api/v1/notifications/unread-count`, `PATCH /api/v1/notifications/{id}/read`, `PATCH /api/v1/notifications/read-all` with `notifications:access` permission.
3. **Mail Operations**: Standardized `GET /api/v1/mail/threads?scope=UNASSIGNED|MY_WORK|ALL`, `POST /api/v1/mail/threads/{id}/claim`, `POST /api/v1/mail/threads/{id}/reassign`, `POST /api/v1/mail/threads/{id}/unassign`, and `POST /api/v1/mail/messages/outbound`.
4. **Admin IAM**: Synchronized `POST /api/v1/admin/staff`, `PATCH /api/v1/admin/staff/{id}/role`, `PUT /api/v1/admin/staff/{id}/permissions`, `GET /api/v1/admin/roles`, `GET /api/v1/admin/ai-configs/{feature}`, and `GET /api/v1/admin/rule-configs`.

---

## 6. Mail Architecture Corrections

1. **Stalwart Infrastructure vs Aurora SaaS**: Clarified that Stalwart is platform mail infrastructure managed by System Admins. Aurora provides the SaaS collaborative thread workspace.
2. **Company Mailbox vs Personal Inbox**: Clarified that human users do not have individual personal inboxes. All communication is routed to shared company mailboxes with human attribution (`SentByUserId`).
3. **Default Mailbox Intake**: Defined target requirement for exactly one Default Operational Shared Mailbox per tenant.
4. **1:1 Alias Target**: Mandated 1:1 alias-to-mailbox mapping to prevent duplicate message fan-out and concurrency race conditions.

---

## 7. UI Shell Consolidation

- **Admin Console (`TENANT_ADMIN`)**: Unified People & Access, Operations Configuration, Mail Administration, and Audit & Security under one sidebar.
- **Operations Workspace (`STAFF` & `MANAGER`)**: Unified Shipments, Routes, Documents OCR, Compliance, Tracking, and 3-pane Mail Workspace with dynamic capability gating.

---

## 8. Backend Gaps (Marked `BACKEND_REQUIRED`)

1. `GET /api/v1/admin/mail/domains` — List assigned domains for tenant (`MailManagement.ListDomains`).
2. `POST /api/v1/system/mail/domains/assign` — System Admin domain assignment (`MailManagement.AssignDomain`).
3. `TenantMailConfig.DefaultMailboxId` — Explicit default operational mailbox configuration.
4. `Alias 1:1 Target Refactor` — Schema and validator update to enforce single target mailbox.
5. `PUT /api/v1/system/tenants/{id}` — Tenant company profile update RPC in `protos/iam_tenant.proto`.
6. Billing RPCs — `RecordPayment`, `CancelInvoice`, `IssueDebitNote`, `IssueCreditNote` in `protos/billing.proto`.

---

## 9. Remaining Decisions & Policy Notes

- **Stalwart IMAP/JMAP Direct Access**: If external desktop mail clients (e.g. Thunderbird, Apple Mail) are ever supported for human staff in future versions, a dedicated credential issuance workflow must be introduced. For MVP, all access is via Aurora Web Workspace.
- **Cognito MFA**: Tenant-level MFA policy enforcement can be configured in Cognito User Pool settings.

---

## 10. Validation Performed

1. **Targeted Repository Grep**: Verified removal of `StaffType`, `OperationsStaff`, `CustomerServiceStaff`, `DocumentationStaff`, `FinanceStaff`, `role == MANAGER` authorization bypasses, and mailbox password reset buttons from UI specs.
2. **Git Diff & Whitespace Verification**: Verified all modified markdown documents.
3. **Link & Precedence Integrity**: Verified cross-references and source-of-truth precedence across all updated documentation.
