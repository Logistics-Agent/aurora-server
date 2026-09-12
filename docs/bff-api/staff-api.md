# Aurora Platform — Operations Workspace API Catalog (Staff Execution)

> **Document ID:** `DOC-BFF-STAFF`  
> **Status:** Canonical Specification (Synchronized with `Staff.Bff` C# Source)  
> **Scope:** HTTP REST APIs consumed by the **Aurora Operations Workspace** for operational execution (`Staff.Bff`).  
> **Base Controller:** `[Authorize]` via `src/dotnet/BFF/Staff.Bff/Controllers/StaffControllerBase.cs`.
> **Source Precedence:** Source Code & Protos > docs/technical/frontend > docs/bff-api > Figma UI Specs.

---

## 1. Operational Endpoints Table

| Module | Method | Path | Purpose | Permission | Scope | Backend RPC | Status |
|---|---|---|---|---|---|---|:---:|
| **Shipment** | `POST` | `/api/v1/shipments` | Create draft shipment | `shipments:create` | Tenant | `ShipmentWorkflowService.CreateShipment` | `CURRENT` |
| **Shipment** | `GET` | `/api/v1/shipments` | List shipments with filters | `shipments:read` | Tenant | `ShipmentWorkflowService.ListShipments` | `CURRENT` |
| **Shipment** | `GET` | `/api/v1/shipments/{id}` | Get shipment details | `shipments:read` | Tenant | `ShipmentWorkflowService.GetShipment` | `CURRENT` |
| **Shipment** | `PUT` | `/api/v1/shipments/{id}` | Update shipment draft | `shipments:update` | Tenant | `ShipmentWorkflowService.UpdateShipment` | `CURRENT` |
| **Shipment** | `DELETE`| `/api/v1/shipments/{id}` | Delete shipment draft | `shipments:delete` | Tenant | `ShipmentWorkflowService.DeleteShipment` | `CURRENT` |
| **Shipment** | `POST` | `/api/v1/shipments/{id}/submit` | Submit shipment for execution | `shipments:submit` | Tenant | `ShipmentWorkflowService.SubmitShipment` | `CURRENT` |
| **Shipment** | `POST` | `/api/v1/shipments/{id}/cancel` | Cancel active shipment | `shipments:cancel` | Tenant | `ShipmentWorkflowService.CancelShipment` | `CURRENT` |
| **Shipment** | `POST` | `/api/v1/shipments/{id}/milestones` | Record delivery milestone | `shipments:milestones:update` | Tenant | `ShipmentWorkflowService.UpdateMilestone` | `CURRENT` |
| **Shipment** | `POST` | `/api/v1/shipments/{id}/document-intakes` | Create/replay the authoritative upload → attachment → OCR intake | `documents:ingest` | Tenant shipment + upload | `ShipmentWorkflowService.CreateDocumentIntake` + `DocumentOcrService` | `CURRENT` |
| **Shipment** | `POST` | `/api/v1/shipments/{id}/documents` | Attach a pre-existing document metadata record | `shipments:create` (legacy fallback: `documents:create`) | Tenant shipment | `ShipmentWorkflowService.AttachShipmentDocument` | `CURRENT_LEGACY` |
| **Shipment** | `GET` | `/api/v1/shipments/{id}/events` | Get shipment event audit trail | `shipments:read` | Tenant | `ShipmentWorkflowService.GetShipmentEvents` | `CURRENT` |
| **Routes** | `POST` | `/api/v1/routes` | Create route proposal | `route_planning:create` | Tenant | `RoutePlanningService.CreateRoute` | `CURRENT` |
| **Routes** | `GET` | `/api/v1/routes` | List routes with pagination | `route_planning:read` | Tenant | `RoutePlanningService.ListRoutes` | `CURRENT` |
| **Routes** | `GET` | `/api/v1/routes/{id}` | Get route details & stops | `route_planning:read` | Tenant | `RoutePlanningService.GetRoute` | `CURRENT` |
| **Routes** | `PUT` | `/api/v1/routes/{id}` | Update route parameters | `route_planning:update` | Tenant | `RoutePlanningService.UpdateRoute` | `CURRENT` |
| **Routes** | `DELETE`| `/api/v1/routes/{id}` | Delete draft route | `route_planning:delete` | Tenant | `RoutePlanningService.DeleteRoute` | `CURRENT` |
| **Routes** | `POST` | `/api/v1/routes/{id}/stops` | Add stop to route | `route_planning:update` | Tenant | `RoutePlanningService.AddStop` | `CURRENT` |
| **Routes** | `DELETE`| `/api/v1/routes/{id}/stops/{stopId}` | Remove stop from route | `route_planning:update` | Tenant | `RoutePlanningService.RemoveStop` | `CURRENT` |
| **Routes** | `POST` | `/api/v1/routes/{id}/optimize` | Trigger AI route optimization | `route_planning:optimize` | Tenant | `RoutePlanningService.OptimizeRoute` | `CURRENT` |
| **Routes** | `POST` | `/api/v1/routes/{id}/evaluate-risk` | Evaluate route risk score | `route_planning:risk:evaluate` | Tenant | `RoutePlanningService.EvaluateRisk` | `CURRENT` |
| **Routes** | `POST` | `/api/v1/routes/{id}/dispatch` | Dispatch approved route | `route_planning:dispatch` | Tenant | `RoutePlanningService.DispatchRoute` | `CURRENT` |
| **Mail** | `GET` | `/api/v1/mail/threads` | List email threads (`UNASSIGNED` / `MY_WORK`) | `mail:read` | Mailbox/User | `MailSecurity.ListThreads` | `CURRENT` |
| **Mail** | `GET` | `/api/v1/mail/threads/{id}` | Get thread conversation & history | `mail:read` | Mailbox/User | `MailSecurity.GetThread` | `CURRENT` |
| **Mail** | `POST` | `/api/v1/mail/threads/{id}/claim` | Atomically claim thread (Take Thread) | `mail:thread:claim` | Thread Assignee | `MailSecurity.ClaimThread` | `CURRENT` |
| **Mail** | `GET` | `/api/v1/mail/threads/{id}/assignment-history` | View thread ownership transitions | `mail:read` | Thread | `MailSecurity.GetThreadAssignmentHistory` | `CURRENT` |
| **Mail** | `POST` | `/api/v1/mail/drafts` | Create or update email draft | `mail:draft:create` | Mailbox/User | `MailSecurity.CreateDraftMessage` | `CURRENT` |
| **Mail** | `GET` | `/api/v1/mail/drafts` | List drafts for mailbox | `mail:read` | Mailbox/User | `MailSecurity.ListDrafts` | `CURRENT` |
| **Mail** | `GET` | `/api/v1/mail/drafts/{id}` | Get draft revision content | `mail:read` | Mailbox/User | `MailSecurity.GetDraft` | `CURRENT` |
| **Mail** | `POST` | `/api/v1/mail/messages/outbound` | Submit outbound email | `mail:send` | Mailbox | `MailSecurity.SubmitOutboundMessage` | `CURRENT` |
| **Mail** | `GET` | `/api/v1/mail/messages` | List processed messages | `mail:read` | Mailbox | `MailSecurity.ListProcessedMessages` | `CURRENT` |
| **Mail** | `GET` | `/api/v1/mail/messages/{id}` | Get processed message & security checks | `mail:read` | Mailbox | `MailSecurity.GetProcessedMessage` | `CURRENT` |
| **Mail** | `GET` | `/api/v1/mail/quarantine` | List quarantined emails | `mail:quarantine:read` | Tenant | `MailSecurity.ListQuarantineRecords` | `CURRENT` |
| **Mail** | `GET` | `/api/v1/mail/quarantine/{id}` | Inspect quarantined threat record | `mail:quarantine:read` | Tenant | `MailSecurity.GetQuarantineRecord` | `CURRENT` |
| **Mail** | `POST` | `/api/v1/mail/quarantine/{id}/release` | Release false-positive email to queue | `mail:quarantine:release` | Tenant | `MailSecurity.ReleaseQuarantine` | `CURRENT` |
| **Documents/OCR** | `POST` | `/api/v1/documents/uploads` | Create a short-lived, write-only browser upload session | `documents:ingest` | Tenant upload session | `DocumentOcrService.CreateUploadSession` | `CURRENT` |
| **Documents/OCR** | `PUT` | `{writeUrl returned by /documents/uploads}` | Upload bytes directly to object storage; this is not a Staff BFF route | Upload session capability | Tenant upload object | S3-compatible/local input storage | `CURRENT` |
| **Documents/OCR** | `GET` | `/api/v1/documents/shipment-documents` | List tenant shipment OCR jobs | `documents:read` | Tenant | `DocumentOcrService.ListDocumentJobs` | `CURRENT` |
| **Documents/OCR** | `GET` | `/api/v1/documents/shipment/{id}` | Get shipment document/OCR detail | `documents:read` | Tenant document/job | `DocumentOcrService.GetDocumentJob` | `CURRENT` |
| **Documents/OCR** | `GET` | `/api/v1/documents/shipment-documents/{id}` | Detail route alias retained by the controller | `documents:read` | Tenant document/job | `DocumentOcrService.GetDocumentJob` | `CURRENT` |
| **Documents/OCR** | `GET` | `/api/v1/documents/shipment-documents/{id}/review` | Get field-level human-review payload | `ocr:review` | Tenant document/job | `DocumentOcrService.GetDocumentJob` | `CURRENT` |
| **Documents/OCR** | `POST` | `/api/v1/documents/shipment-documents/{id}/review` | Confirm, correct or reject OCR extraction | `ocr:review` | Tenant document/job | `DocumentOcrService.ReviewDocumentJob` | `CURRENT` |
| **Documents/OCR** | `POST` | `/api/v1/documents/shipment-documents/{id}/cancel` | Cancel an active OCR job | `documents:manage` | Tenant document/job | `DocumentOcrService.CancelDocumentJob` | `CURRENT` |
| **Documents/OCR** | `POST` | `/api/v1/documents/shipment-documents/{id}/retry` | Retry a failed OCR job | `documents:manage` | Tenant document/job | `DocumentOcrService.RetryDocumentJob` | `CURRENT` |
| **Documents/OCR** | `POST` | `/api/v1/documents/shipment` or `/api/v1/documents/shipment-documents` | Submit a storage-reference-based OCR job | `documents:ingest` | Tenant document/job | `DocumentOcrService.SubmitOcrJob` | `CURRENT_LEGACY` |
| **Compliance** | `POST` | `/api/v1/compliance/evaluations` | Evaluate shipment trade compliance | `compliance:evaluate` | Shipment | `RegulatoryComplianceService.EvaluateCompliance` | `CURRENT` |
| **Compliance** | `GET` | `/api/v1/compliance/evaluations/{id}` | Get compliance assessment result | `compliance:read` | Evaluation | `RegulatoryComplianceService.GetEvaluation` | `CURRENT` |
| **Compliance** | `POST` | `/api/v1/compliance/rag/query` | Query regulatory citations | `compliance:read` | Jurisdiction | `RegulatoryComplianceService.QueryRegulations` | `CURRENT` |
| **Tracking** | `GET` | `/api/v1/tracking/{id}/current` | Get latest GPS position | `shipments:read` | Shipment/Vehicle | `GpsTrackingService.GetCurrentLocation` | `CURRENT` |
| **Tracking** | `GET` | `/api/v1/tracking/{id}/history` | Get GPS breadcrumb history | `shipments:read` | Shipment/Vehicle | `GpsTrackingService.ListPositionHistory` | `CURRENT` |
| **Tracking** | `GET` | `/api/v1/tracking/{id}/alerts` | Get geofence & sensor alerts | `shipments:read` | Shipment/Vehicle | `GpsTrackingService.ListAlerts` | `CURRENT` |
| **Tracking** | `POST` | `/api/v1/tracking/geofences` | Create geofence monitoring zone | `shipments:update` | Tenant | `GpsTrackingService.CreateGeofence` | `CURRENT` |
| **Tracking** | `GET` | `/api/v1/tracking/geofences` | List tenant geofences | `shipments:read` | Tenant | `GpsTrackingService.ListGeofences` | `CURRENT` |
| **Financial** | `POST` | `/api/v1/financial/estimate-cost` | Calculate freight cost estimate | `financial:calculate` | Tenant | `FinancialService.EstimateCost` | `CURRENT` |
| **Financial** | `POST` | `/api/v1/financial/customs-duty` | Calculate customs duties & tariffs | `financial:calculate` | Tenant | `FinancialService.GetCustomsDuty` | `CURRENT` |
| **Billing** | `POST` | `/api/v1/invoices/generate` | Auto-generate shipment invoice | `billing:invoice:create` | Shipment | `BillingService.GenerateInvoice` | `CURRENT` |
| **Billing** | `POST` | `/api/v1/invoices` | Create manual invoice | `billing:invoice:create` | Customer | `BillingService.CreateInvoice` | `CURRENT` |
| **Billing** | `GET` | `/api/v1/invoices/{id}` | Get invoice details | `billing:invoice:read` | Invoice | `BillingService.GetInvoice` | `CURRENT` |
| **Billing** | `POST` | `/api/v1/invoices/{id}/pay` | Record invoice settlement | `billing:invoice:pay` | Invoice | `BillingService.PayInvoice` | `CURRENT` |
| **Billing** | `GET` | `/api/v1/billing/credit-check` | Check customer credit limit | `billing:credit:read` | Customer | `BillingService.CheckCreditLimit` | `CURRENT` |
| **Billing** | `GET` | `/api/v1/escrow/accounts/{customerId}` | Check escrow balance | `billing:escrow:read` | Customer | `BillingService.GetEscrowAccount` | `CURRENT` |
| **Billing** | `POST` | `/api/v1/escrow/lock` | Lock funds in escrow | `billing:escrow:manage` | Customer | `BillingService.LockEscrow` | `CURRENT` |
| **Billing** | `POST` | `/api/v1/escrow/release` | Release locked escrow funds | `billing:escrow:manage` | Customer | `BillingService.ReleaseEscrow` | `CURRENT` |
| **Negotiation**| `POST` | `/api/v1/negotiations/{id}/mail-draft` | Generate draft from AI suggestion | `mail:draft:create` | Negotiation | `NegotiationService.GetDraftSuggestion` + `MailSecurity.CreateDraftMessage` | `CURRENT` |
| **Negotiation**| `GET` | `/api/v1/negotiations/{id}/suggestion` | View AI concession counter-offer | `mail:read` | Negotiation | `NegotiationService.GetDraftSuggestion` | `CURRENT` |
| **Assistant** | `POST` | `/api/v1/assistant/query` | Grounded multi-corpus AI assistant | `compliance:read` | Tenant | `RegulatoryComplianceService.GenerateGroundedAnswer` | `CURRENT` |
| **Chat** | `POST` | `/api/v1/chat/conversations` | Customer Assistant conversation | `None (Session)` | Tenant | `CustomerAssistantService (HTTP)` | `CURRENT` |

---

## 2. Secure Documents/OCR Intake Flow

The browser must use the upload-session flow for new shipment documents. The BFF never receives file bytes and never asks the browser to invent a `storageReference` or `externalDocumentId`.

```text
1. POST /api/v1/documents/uploads                       [documents:ingest]
   └─ 201 { uploadId, storageReference, writeUrl, requiredHeaders, expiresAt, ... }
2. PUT {writeUrl}                                       [direct object-storage upload]
   └─ Send the returned required headers and file bytes; writeUrl expires after 15 minutes.
3. POST /api/v1/shipments/{shipmentId}/document-intakes [documents:ingest]
   └─ { uploadId, documentTypeHint, idempotencyKey }
   └─ 202 { intakeId, documentId, ocrJobId, status, stage, ... }
4. GET /api/v1/documents/shipment-documents             [documents:read]
   └─ Poll list/detail until terminal OCR status; use review/manage routes below as needed.
```

The intake endpoint verifies the tenant-scoped upload object, creates or replays one shipment attachment, consumes the upload session, and submits one idempotent OCR job. Replaying the same `idempotencyKey` with the same body resumes the persisted intake; changing the body returns a conflict. Cross-tenant IDs follow the anti-enumeration policy and are not converted into new IDs.

Upload/intake failures use `application/problem+json` with `code` and `retryable` extensions. The canonical statuses are:

| HTTP status | Stable codes/examples | Client behavior |
|---:|---|---|
| `400` | `INVALID_REQUEST`, `INVALID_UPLOAD_REQUEST` | Fix the request; do not retry unchanged. |
| `404` | `UPLOAD_NOT_FOUND`, `UPLOAD_TENANT_MISMATCH`, `DOCUMENT_INTAKE_NOT_FOUND` | Treat as not visible to this tenant. |
| `409` | `UPLOAD_EXPIRED`, `UPLOAD_IDEMPOTENCY_CONFLICT`, `INVALID_STATE_TRANSITION` | Refresh/reconcile the existing resource; preserve idempotency key. |
| `422` | `UPLOAD_CONTENT_MISMATCH`, `UPLOAD_MIME_MISMATCH`, `UPLOAD_SIZE_MISMATCH`, `UPLOAD_HASH_MISMATCH` | Recreate the upload session and upload the correct bytes. |
| `503` | `DOCUMENT_OCR_UNAVAILABLE`, `SHIPMENT_WORKFLOW_UNAVAILABLE` (`retryable: true`) | Retry the same intake request; do not create a second attachment. |

### Retained compatibility routes

The following routes remain for existing clients and are not the new browser upload flow:

- `POST /api/v1/documents/shipment` and `POST /api/v1/documents/shipment-documents` accept a caller-supplied `storageReference` and submit OCR directly. They require `documents:ingest` and return the legacy `200` job status shape.
- `POST /api/v1/shipments/{id}/documents` attaches caller-supplied document metadata through ShipmentWorkflow. Its source permission is `shipments:create` with legacy fallback `documents:create`; new UI code must use `POST /api/v1/shipments/{id}/document-intakes` instead.
- The `/api/v1`-less `api/...` route aliases declared on `DocumentsController` are compatibility aliases. The versioned `/api/v1/...` paths above are the FE contract.

The old `/api/v1/documents/jobs/...` and `/api/v1/documents/ocr/jobs/...` paths are not Staff BFF routes in the current source and must not be used by FE.

The sanitized FE contract fixture is tracked at `docs/contracts/staff-bff-documents.openapi.json`. It is source-derived from the Staff BFF controller DTOs and attributes because the isolated finalization environment could not complete runtime Swagger generation; `DocumentsPublishedContractTests` provides the drift check against those source types and metadata.

---

## 3. Mail Thread Operational Workflow

```text
[Incoming Email Ingestion]
        ↓
[UNASSIGNED Queue] (scope=UNASSIGNED)
        ↓ Staff clicks "Take Thread"
[POST /api/v1/mail/threads/{id}/claim]
        ├── Enforces Optimistic Concurrency (thread.Version)
        ├── Sets PrimaryAssigneeUserId = currentUser.UserId
        └── Transitions Status = IN_PROGRESS
        ↓
[MY_WORK Queue] (scope=MY_WORK)
        ├── Compose Draft: POST /api/v1/mail/drafts
        └── Submit Outbound: POST /api/v1/mail/messages/outbound
```
