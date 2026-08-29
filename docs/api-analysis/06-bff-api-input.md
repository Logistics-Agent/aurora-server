# Aurora Platform - Canonical BFF API Input Contract

> **Document ID:** `DOC-API-06`  
> **Status:** Canonical BFF Specification Baseline Complete  
> **Purpose:** Single source of truth for the BFF API generation phase. Specifies all approved, ready-to-expose capabilities (`G0` and `G1`), explicitly blocks uncontracted/unimplemented capabilities (`G2`, `G3`, `G4`), and strictly seals internal machine-to-machine boundaries (`G5`).  
> **Architecture Reference:** `codex/requirement.md`, `codex/specs/logistics-architecture.md`, `docs/api-analysis/01-grpc-capability-map.md`, `docs/api-analysis/02-cqrs-capability-map.md`, `docs/api-analysis/03-business-capability-map.md`, `docs/api-analysis/04-role-capability-matrix.md`, `docs/api-analysis/05-api-gap-analysis.md`

---

## 1. BFF Integration Architecture Rules

1. **Authentication & Identity Propagation**:
   - All inbound web requests must be authenticated via AWS Cognito JWT bearer tokens.
   - BFF middleware strips all incoming `x-*` client headers (preventing header injection/spoofing).
   - [ClientMetadataInterceptor.cs](file:///D:/IT/CD/aurora-server/src/dotnet/shared/Interceptors/ClientMetadataInterceptor.cs) attaches trusted metadata (`x-user-id`, `x-tenant-id`, `x-role-ids`, `x-permission-version`, `x-trace-id`) to all downstream gRPC channel calls.
2. **Tenant Isolation Guarantees**:
   - `TenantId` is **NEVER** accepted from request body or client query parameters for normal tenant users.
   - All downstream microservices resolve `TenantId` from `ICurrentUserService` and enforce global EF Core / JPA query filters.
   - `SYSTEM_ADMIN` role is strictly separated in `System.Bff` from tenant `STAFF`, `MANAGER`, and `ADMIN` roles.
3. **Resilience & Fault Tolerance**:
   - Downstream gRPC calls in BFF must use Polly resilience pipelines (timeouts, circuit breakers, and retries on transient errors `Unavailable`/`DeadlineExceeded`) configured in `BuildingBlocks.BFF`.

---

## 2. Canonical BFF-Ready Capabilities (G0 & G1)

---

### Section 2.1: Shipment Management (`ShipmentWorkflow`)

#### `BFF-SHIP-01`: Create Shipment
- **CapabilityId:** `BFF-SHIP-01`
- **Resource:** `Shipment`
- **Action:** `CREATE`
- **Roles:** `[STAFF, MANAGER]`
- **Shared:** `true`
- **Service:** `ShipmentWorkflow`
- **Proto:** `protos/shipment_workflow.proto`
- **RPC:** `ShipmentWorkflowService.CreateShipment`
- **Request Message:** `CreateShipmentRequest`
- **Response Message:** `ShipmentResponse`
- **Command/Query:** `CreateShipmentCommand`
- **Handler:** `CreateShipmentCommandHandler`
- **Tenant Scope:** Strict Tenant Isolation (`ICurrentUserService.TenantId`)
- **Suggested HTTP Method:** `POST`
- **Suggested REST Resource:** `/api/v1/shipments`
- **Security Considerations:** Enforce tenant assignment from authenticated JWT context.
- **Validation Considerations:** Require valid `origin`, `destination`, estimated delivery date > UTC now, valid transport mode enum.
- **Source Files:**
  - [protos/shipment_workflow.proto](file:///D:/IT/CD/aurora-server/protos/shipment_workflow.proto)
  - [src/dotnet/ShipmentWorkflow/GrpcServices/ShipmentGrpcService.cs](file:///D:/IT/CD/aurora-server/src/dotnet/ShipmentWorkflow/GrpcServices/ShipmentGrpcService.cs)
  - [src/dotnet/ShipmentWorkflow/Application/Commands/Shipments/CreateShipmentCommand.cs](file:///D:/IT/CD/aurora-server/src/dotnet/ShipmentWorkflow/Application/Commands/Shipments/CreateShipmentCommand.cs)
- **Gap Status:** `G1` (Ready for BFF Controller)

#### `BFF-SHIP-02`: Get Shipment Details
- **CapabilityId:** `BFF-SHIP-02`
- **Resource:** `Shipment`
- **Action:** `GET`
- **Roles:** `[STAFF, MANAGER, ADMIN]`
- **Shared:** `true`
- **Service:** `ShipmentWorkflow`
- **Proto:** `protos/shipment_workflow.proto`
- **RPC:** `ShipmentWorkflowService.GetShipment`
- **Request Message:** `GetShipmentRequest` (`id`)
- **Response Message:** `ShipmentResponse`
- **Command/Query:** `GetShipmentQuery`
- **Handler:** `GetShipmentQueryHandler`
- **Tenant Scope:** Strict Tenant Isolation (`ICurrentUserService.TenantId`)
- **Suggested HTTP Method:** `GET`
- **Suggested REST Resource:** `/api/v1/shipments/{id}`
- **Security Considerations:** Tenant filter prevents cross-tenant data leakage (fails closed with 404).
- **Validation Considerations:** `id` must be valid UUID GUID format.
- **Source Files:**
  - [src/dotnet/ShipmentWorkflow/Application/Queries/Shipments/GetShipmentQuery.cs](file:///D:/IT/CD/aurora-server/src/dotnet/ShipmentWorkflow/Application/Queries/Shipments/GetShipmentQuery.cs)
- **Gap Status:** `G1`

#### `BFF-SHIP-03`: List / Search Shipments
- **CapabilityId:** `BFF-SHIP-03`
- **Resource:** `Shipment`
- **Action:** `LIST / SEARCH`
- **Roles:** `[STAFF, MANAGER, ADMIN]`
- **Shared:** `true`
- **Service:** `ShipmentWorkflow`
- **Proto:** `protos/shipment_workflow.proto`
- **RPC:** `ShipmentWorkflowService.ListShipments`
- **Request Message:** `ListShipmentsRequest` (`page`, `page_size`, `status_filter`, `search_term`, `date_from`, `date_to`)
- **Response Message:** `ListShipmentsResponse`
- **Command/Query:** `ListShipmentsQuery`
- **Handler:** `ListShipmentsQueryHandler`
- **Tenant Scope:** Strict Tenant Isolation
- **Suggested HTTP Method:** `GET`
- **Suggested REST Resource:** `/api/v1/shipments`
- **Security Considerations:** Global EF Core query filters enforce tenant isolation across all pages.
- **Validation Considerations:** Enforce bounded pagination (`pageSize <= 100`).
- **Source Files:**
  - [src/dotnet/ShipmentWorkflow/Application/Queries/Shipments/ListShipmentsQuery.cs](file:///D:/IT/CD/aurora-server/src/dotnet/ShipmentWorkflow/Application/Queries/Shipments/ListShipmentsQuery.cs)
- **Gap Status:** `G1`

#### `BFF-SHIP-04`: Update Shipment
- **CapabilityId:** `BFF-SHIP-04`
- **Resource:** `Shipment`
- **Action:** `UPDATE`
- **Roles:** `[STAFF, MANAGER]`
- **Shared:** `true`
- **Service:** `ShipmentWorkflow`
- **Proto:** `protos/shipment_workflow.proto`
- **RPC:** `ShipmentWorkflowService.UpdateShipment`
- **Request Message:** `UpdateShipmentRequest`
- **Response Message:** `ShipmentResponse`
- **Command/Query:** `UpdateShipmentCommand`
- **Handler:** `UpdateShipmentCommandHandler`
- **Tenant Scope:** Strict Tenant Isolation
- **Suggested HTTP Method:** `PUT`
- **Suggested REST Resource:** `/api/v1/shipments/{id}`
- **Security Considerations:** Mutations allowed only while shipment is in editable states (`Draft`, `Submitted`).
- **Validation Considerations:** Total cargo weight/volume consistency check.
- **Source Files:**
  - [src/dotnet/ShipmentWorkflow/Application/Commands/Shipments/UpdateShipmentCommand.cs](file:///D:/IT/CD/aurora-server/src/dotnet/ShipmentWorkflow/Application/Commands/Shipments/UpdateShipmentCommand.cs)
- **Gap Status:** `G1`

#### `BFF-SHIP-05`: Submit Shipment Workflow
- **CapabilityId:** `BFF-SHIP-05`
- **Resource:** `Shipment`
- **Action:** `SUBMIT`
- **Roles:** `[STAFF, MANAGER]`
- **Shared:** `true`
- **Service:** `ShipmentWorkflow`
- **Proto:** `protos/shipment_workflow.proto`
- **RPC:** `ShipmentWorkflowService.SubmitShipment`
- **Request Message:** `SubmitShipmentRequest` (`id`)
- **Response Message:** `ShipmentResponse`
- **Command/Query:** `SubmitShipmentCommand`
- **Handler:** `SubmitShipmentCommandHandler`
- **Tenant Scope:** Strict Tenant Isolation
- **Suggested HTTP Method:** `POST`
- **Suggested REST Resource:** `/api/v1/shipments/{id}/submit`
- **Security Considerations:** Enforces state machine transition from `Draft` -> `Submitted`.
- **Validation Considerations:** Validates that shipment contains at least one cargo item and destination address.
- **Source Files:**
  - [src/dotnet/ShipmentWorkflow/Application/Commands/Shipments/SubmitShipmentCommand.cs](file:///D:/IT/CD/aurora-server/src/dotnet/ShipmentWorkflow/Application/Commands/Shipments/SubmitShipmentCommand.cs)
- **Gap Status:** `G1`

#### `BFF-SHIP-06`: Update Shipment Status
- **CapabilityId:** `BFF-SHIP-06`
- **Resource:** `Shipment`
- **Action:** `UPDATE_STATUS`
- **Roles:** `[STAFF, MANAGER]`
- **Shared:** `true`
- **Service:** `ShipmentWorkflow`
- **Proto:** `protos/shipment_workflow.proto`
- **RPC:** `ShipmentWorkflowService.UpdateShipmentStatus`
- **Request Message:** `UpdateShipmentStatusRequest` (`id`, `new_status`, `reason`)
- **Response Message:** `ShipmentResponse`
- **Command/Query:** `UpdateShipmentStatusCommand`
- **Handler:** `UpdateShipmentStatusCommandHandler`
- **Tenant Scope:** Strict Tenant Isolation
- **Suggested HTTP Method:** `PATCH`
- **Suggested REST Resource:** `/api/v1/shipments/{id}/status`
- **Security Considerations:** Prevents invalid state regressions.
- **Source Files:**
  - [src/dotnet/ShipmentWorkflow/Application/Commands/Shipments/UpdateShipmentStatusCommand.cs](file:///D:/IT/CD/aurora-server/src/dotnet/ShipmentWorkflow/Application/Commands/Shipments/UpdateShipmentStatusCommand.cs)
- **Gap Status:** `G1`

#### `BFF-SHIP-07`: Cancel Shipment
- **CapabilityId:** `BFF-SHIP-07`
- **Resource:** `Shipment`
- **Action:** `CANCEL`
- **Roles:** `[STAFF, MANAGER]`
- **Shared:** `true`
- **Service:** `ShipmentWorkflow`
- **Proto:** `protos/shipment_workflow.proto`
- **RPC:** `ShipmentWorkflowService.CancelShipment`
- **Request Message:** `CancelShipmentRequest` (`id`, `reason`)
- **Response Message:** `ShipmentResponse`
- **Command/Query:** `CancelShipmentCommand`
- **Handler:** `CancelShipmentCommandHandler`
- **Tenant Scope:** Strict Tenant Isolation
- **Suggested HTTP Method:** `POST`
- **Suggested REST Resource:** `/api/v1/shipments/{id}/cancel`
- **Security Considerations:** Cannot cancel already `Delivered` shipments. Emits outbox cancellation event.
- **Source Files:**
  - [src/dotnet/ShipmentWorkflow/Application/Commands/Shipments/CancelShipmentCommand.cs](file:///D:/IT/CD/aurora-server/src/dotnet/ShipmentWorkflow/Application/Commands/Shipments/CancelShipmentCommand.cs)
- **Gap Status:** `G1`

#### `BFF-SHIP-08`: Delete Draft Shipment
- **CapabilityId:** `BFF-SHIP-08`
- **Resource:** `Shipment`
- **Action:** `DELETE_DRAFT`
- **Roles:** `[STAFF, MANAGER]`
- **Shared:** `true`
- **Service:** `ShipmentWorkflow`
- **Proto:** `protos/shipment_workflow.proto`
- **RPC:** `ShipmentWorkflowService.DeleteDraftShipment`
- **Request Message:** `DeleteDraftShipmentRequest` (`id`)
- **Response Message:** `DeleteDraftShipmentResponse` (`success`)
- **Command/Query:** `DeleteDraftShipmentCommand`
- **Handler:** `DeleteDraftShipmentCommandHandler`
- **Tenant Scope:** Strict Tenant Isolation
- **Suggested HTTP Method:** `DELETE`
- **Suggested REST Resource:** `/api/v1/shipments/{id}`
- **Security Considerations:** Hard delete is strictly rejected if status != `Draft`.
- **Source Files:**
  - [src/dotnet/ShipmentWorkflow/Application/Commands/Shipments/DeleteDraftShipmentCommand.cs](file:///D:/IT/CD/aurora-server/src/dotnet/ShipmentWorkflow/Application/Commands/Shipments/DeleteDraftShipmentCommand.cs)
- **Gap Status:** `G1`

#### `BFF-SHIP-09`: Bulk Import Shipments
- **CapabilityId:** `BFF-SHIP-09`
- **Resource:** `Shipment`
- **Action:** `IMPORT_BULK`
- **Roles:** `[STAFF, MANAGER]`
- **Shared:** `true`
- **Service:** `ShipmentWorkflow`
- **Proto:** `protos/shipment_workflow.proto`
- **RPC:** `ShipmentWorkflowService.ImportShipments`
- **Request Message:** `ImportShipmentsRequest` (`shipments`)
- **Response Message:** `ImportShipmentsResponse` (`imported_count`, `failed_count`, `errors`)
- **Command/Query:** `ImportShipmentsCommand`
- **Handler:** `ImportShipmentsCommandHandler`
- **Tenant Scope:** Strict Tenant Isolation
- **Suggested HTTP Method:** `POST`
- **Suggested REST Resource:** `/api/v1/shipments/import`
- **Validation Considerations:** Batch limit of max 500 rows per request.
- **Source Files:**
  - [src/dotnet/ShipmentWorkflow/Application/Commands/Shipments/ImportShipmentsCommand.cs](file:///D:/IT/CD/aurora-server/src/dotnet/ShipmentWorkflow/Application/Commands/Shipments/ImportShipmentsCommand.cs)
- **Gap Status:** `G1`

#### `BFF-SHIP-10` to `BFF-SHIP-15`: Child Entities (Cargo, Locations, Documents, Milestones)
- **Resource:** `CargoItem`, `ShipmentLocation`, `ShipmentDocument`, `ShipmentMilestone`
- **Roles:** `[STAFF, MANAGER]`
- **Suggested REST Routes:**
  - `POST /api/v1/shipments/{id}/cargo` (`AddCargoItem`)
  - `PUT /api/v1/shipments/{id}/cargo/{itemId}` (`UpdateCargoItem`)
  - `DELETE /api/v1/shipments/{id}/cargo/{itemId}` (`RemoveCargoItem`)
  - `POST /api/v1/shipments/{id}/locations` (`AddShipmentLocation`)
  - `PUT /api/v1/shipments/{id}/locations/{locationId}` (`UpdateShipmentLocation`)
  - `DELETE /api/v1/shipments/{id}/locations/{locationId}` (`RemoveShipmentLocation`)
  - `POST /api/v1/shipments/{id}/documents` (`AttachShipmentDocument`)
  - `DELETE /api/v1/shipments/{id}/documents/{documentId}` (`RemoveShipmentDocument`)
  - `POST /api/v1/shipments/{id}/milestones` (`AddShipmentMilestone`)
  - `GET /api/v1/shipments/{id}/timeline` (`GetShipmentTimeline`)
- **Gap Status:** `G1` (Ready for inclusion in `ShipmentsController.cs`)

---

### Section 2.2: GPS Tracking & Live Monitoring (`GpsTracking`)

#### `BFF-GPS-01`: Get Current Live Position
- **CapabilityId:** `BFF-GPS-01`
- **Resource:** `GpsPosition`
- **Action:** `GET_CURRENT`
- **Roles:** `[STAFF, MANAGER, ADMIN]`
- **Shared:** `true`
- **Service:** `GpsTracking`
- **Proto:** `protos/gps_tracking.proto`
- **RPC:** `GpsTrackingService.GetCurrentLocation`
- **Request Message:** `GetCurrentLocationRequest` (`selector_case`: `VehicleId` or `ShipmentId`)
- **Response Message:** `CurrentLocationResponse` (`latitude`, `longitude`, `speed_kph`, `heading_degrees`, `recorded_at`)
- **Command/Query:** `LocationQueryService.GetCurrentAsync`
- **Tenant Scope:** Strict Tenant Isolation
- **Suggested HTTP Method:** `GET`
- **Suggested REST Resource:** `/api/v1/tracking/{id}/current?type=vehicle|shipment`
- **Gap Status:** `G1`

#### `BFF-GPS-02`: List Position Breadcrumb History
- **CapabilityId:** `BFF-GPS-02`
- **Resource:** `GpsPosition`
- **Action:** `LIST_HISTORY`
- **Roles:** `[STAFF, MANAGER, ADMIN]`
- **Shared:** `true`
- **Service:** `GpsTracking`
- **Proto:** `protos/gps_tracking.proto`
- **RPC:** `GpsTrackingService.ListPositionHistory`
- **Request Message:** `ListPositionHistoryRequest` (`selector`, `from`, `to`, `page`, `page_size`)
- **Response Message:** `ListPositionHistoryResponse`
- **Command/Query:** `LocationQueryService.ListHistoryAsync`
- **Tenant Scope:** Strict Tenant Isolation
- **Suggested HTTP Method:** `GET`
- **Suggested REST Resource:** `/api/v1/tracking/{id}/history`
- **Gap Status:** `G1`

#### `BFF-GPS-03` to `BFF-GPS-06`: Geofences & Monitoring Alerts
- **Resource:** `Geofence`, `MonitoringAlert`
- **Roles:** `[STAFF, MANAGER, ADMIN]` (Resolve Alert: `[MANAGER, ADMIN]`)
- **Suggested REST Routes:**
  - `POST /api/v1/tracking/geofences` (`CreateGeofence`)
  - `GET /api/v1/tracking/geofences` (`ListGeofences`)
  - `PATCH /api/v1/tracking/geofences/{id}/active` (`SetGeofenceActive`)
  - `GET /api/v1/tracking/alerts` (`ListMonitoringAlerts`)
  - `POST /api/v1/tracking/alerts/{id}/resolve` (`ResolveMonitoringAlert`)
- **Gap Status:** `G1` (Ready for `TrackingController.cs`)

---

### Section 2.3: Notifications & Preferences (`Notification`)

#### `BFF-NOTIF-01` to `BFF-NOTIF-04`: Notification Center
- **Resource:** `Notification`, `NotificationPreference`
- **Roles:** `[STAFF, MANAGER, ADMIN]`
- **Suggested REST Routes:**
  - `GET /api/v1/notifications` (`ListNotifications`)
  - `PATCH /api/v1/notifications/{id}/read` (`MarkNotificationRead`)
  - `GET /api/v1/notifications/preferences` (`ListNotificationPreferences`)
  - `PUT /api/v1/notifications/preferences` (`UpsertNotificationPreference`)
- **Security Considerations:** Must isolate strictly by `RecipientUserId == CurrentUser.UserId`.
- **Gap Status:** `G1` (Ready for `NotificationsController.cs`)

---

### Section 2.4: Document OCR Management (`DocumentOcr`)

#### `BFF-OCR-01` to `BFF-OCR-06`: Document OCR Processing
- **Resource:** `DocumentOcrJob`
- **Roles:** `[STAFF, MANAGER]` (Review: `[MANAGER, ADMIN]`)
- **Existing REST Routes:** Fully mapped in [src/dotnet/BFF/Staff.Bff/Controllers/DocumentsController.cs](file:///D:/IT/CD/aurora-server/src/dotnet/BFF/Staff.Bff/Controllers/DocumentsController.cs):
  - `POST /api/v1/documents/ocr` (`SubmitOcrJob`)
  - `GET /api/v1/documents/jobs/{id}` (`GetDocumentJob`)
  - `GET /api/v1/documents/jobs` (`ListDocumentJobs`)
  - `POST /api/v1/documents/jobs/{id}/review` (`ReviewDocumentJob`)
  - `POST /api/v1/documents/jobs/{id}/cancel` (`CancelDocumentJob`)
  - `POST /api/v1/documents/jobs/{id}/retry` (`RetryDocumentJob`)
- **Gap Status:** `G0` (Fully Mapped & Operational)

---

### Section 2.5: Compliance Intelligence & Copilot (`RegulatoryCompliance`)

#### `BFF-COMP-01`: Evaluate Shipment Compliance
- **CapabilityId:** `BFF-COMP-01`
- **Resource:** `ComplianceEvaluation`
- **Action:** `EVALUATE`
- **Roles:** `[STAFF, MANAGER]`
- **Shared:** `true`
- **Service:** `RegulatoryCompliance`
- **Proto:** `protos/regulatory_compliance.proto`
- **RPC:** `RegulatoryComplianceService.EvaluateCompliance`
- **Request Message:** `EvaluateComplianceRequest`
- **Response Message:** `ComplianceEvaluationResponse`
- **Command/Query:** `ComplianceEvaluationService.EvaluateAsync`
- **Tenant Scope:** Strict Tenant Isolation
- **Suggested HTTP Method:** `POST`
- **Suggested REST Resource:** `/api/v1/compliance/evaluations`
- **Gap Status:** `G1`

#### `BFF-COMP-02`: AI Compliance Copilot (Grounded Assistant)
- **CapabilityId:** `BFF-COMP-02`
- **Resource:** `ComplianceCopilot`
- **Action:** `GENERATE_ANSWER`
- **Roles:** `[STAFF, MANAGER, ADMIN]`
- **Shared:** `true`
- **Service:** `RegulatoryCompliance`
- **Proto:** `protos/regulatory_compliance.proto`
- **RPC:** `RegulatoryComplianceService.GenerateGroundedAnswer`
- **Request Message:** `GenerateGroundedAnswerRequest`
- **Response Message:** `GenerateGroundedAnswerResponse`
- **Command/Query:** `GroundedAnswerService.GenerateAnswerAsync`
- **Tenant Scope:** Tenant + Platform Global RAG
- **Suggested HTTP Method:** `POST`
- **Suggested REST Resource:** `/api/v1/compliance/copilot/ask`
- **Gap Status:** `G1`

#### `BFF-COMP-03` to `BFF-COMP-05`: Legal Knowledge Search & Ingestion
- **Resource:** `RegulatorySource`, `KnowledgeDocument`
- **Roles:** `[STAFF, ADMIN, SYSTEM]`
- **Existing REST Routes:**
  - `POST /api/v1/compliance/regulations/query` (Staff.Bff / SearchController -> `G0`)
  - `POST /api/v1/compliance/knowledge/query` (Staff.Bff / SearchController -> `G0`)
  - `POST /api/v1/admin/compliance/sources` (Admin.Bff / PlatformIngestionController -> `G0`)
  - `POST /api/v1/system/compliance/sources` (System.Bff / SystemIngestionController -> `G0`)
- **Gap Status:** `G0`

---

### Section 2.6: Route Planning & Approvals (`RoutePlanningAgent`)

#### `BFF-ROUTE-01` to `BFF-ROUTE-06`: Route Planning & AI Recommendations
- **Resource:** `RoutePlan`, `RouteApproval`, `TenantAiConfig`, `TenantRuleConfig`
- **Roles:** `[STAFF, MANAGER, ADMIN]`
- **Existing REST Routes:** Fully mapped in [Staff.Bff](file:///D:/IT/CD/aurora-server/src/dotnet/BFF/Staff.Bff/Controllers/RoutesController.cs) and [Admin.Bff](file:///D:/IT/CD/aurora-server/src/dotnet/BFF/Admin.Bff/Controllers/AiConfigController.cs):
  - `POST /api/v1/routes`, `GET /api/v1/routes`, `GET /api/v1/routes/{id}`, `PUT /api/v1/routes/{id}`, `DELETE /api/v1/routes/{id}`, `POST /api/v1/routes/{id}/optimize`, `POST /api/v1/routes/{id}/recommendation` -> `G0`
  - `GET /api/v1/approvals/pending`, `POST /api/v1/approvals/{id}/approve`, `POST /api/v1/approvals/{id}/reject` -> `G0`
  - `GET /api/v1/admin/ai-config`, `PUT /api/v1/admin/ai-config` -> `G0`
  - `GET /api/v1/admin/rules`, `PUT /api/v1/admin/rules` -> `G0`
- **Gap Status:** `G0`

---

### Section 2.7: IAM, Roles & Authentication (`IamTenant`)

#### `BFF-IAM-01` to `BFF-IAM-06`: Staff Identity & Roles
- **Resource:** `StaffUser`, `Role`, `Tenant`
- **Roles:** `[ADMIN, SYSTEM]`
- **Existing REST Routes:** Fully mapped in `Admin.Bff`, `System.Bff`, and `Staff.Bff`:
  - `POST /api/v1/admin/staff/invite`, `GET /api/v1/admin/staff`, `GET /api/v1/admin/staff/{id}`, `PUT /api/v1/admin/staff/{id}`, `POST /api/v1/admin/staff/{id}/activate`, `POST /api/v1/admin/staff/{id}/suspend`, `POST /api/v1/admin/staff/{id}/reset-password`, `POST /api/v1/admin/staff/{id}/roles` -> `G0`
  - `GET /api/v1/admin/roles`, `GET /api/v1/admin/roles/{id}` -> `G0`
  - `POST /api/v1/system/tenants`, `GET /api/v1/system/tenants`, `GET /api/v1/system/tenants/{id}`, `PATCH /api/v1/system/tenants/{id}/status`, `DELETE /api/v1/system/tenants/{id}` -> `G0`
  - `POST /api/v1/auth/identify`, `POST /api/v1/auth/login`, `POST /api/v1/auth/complete-invitation`, `POST /api/v1/auth/refresh`, `POST /api/v1/auth/logout`, `POST /api/v1/auth/forgot-password` -> `G0`
- **Ready for Addition (`G1`):**
  - `POST /api/v1/admin/roles/{id}/permissions` (`AssignPermissionsToRole`) -> `G1`
  - `GET /api/v1/auth/permissions` (`GetUserPermissions`) -> `G1`

---

### Section 2.8: Corporate Mail Platform (`MailService`)

#### `BFF-MAIL-01` to `BFF-MAIL-05`: Email Drafts, Outbound & Quarantine
- **Resource:** `EmailDraft`, `OutboundEmail`, `MailQuarantine`, `MailDomain`, `Mailbox`
- **Roles:** `[STAFF, MANAGER, ADMIN]`
- **Existing REST Routes:** Fully mapped in `Staff.Bff`, `Admin.Bff`, and `System.Bff`:
  - `POST /api/v1/mail/drafts`, `GET /api/v1/mail/drafts`, `GET /api/v1/mail/drafts/{id}`, `POST /api/v1/mail/send`, `GET /api/v1/mail/messages`, `GET /api/v1/mail/messages/{id}`, `GET /api/v1/mail/quarantine`, `GET /api/v1/mail/quarantine/{id}`, `POST /api/v1/mail/quarantine/{id}/release` -> `G0`
  - `POST /api/v1/admin/mail/domains`, `POST /api/v1/admin/mail/mailboxes`, `POST /api/v1/admin/mail/aliases`, `DELETE /api/v1/admin/mail/quarantine/{id}`, `GET /api/v1/admin/mail/audit` -> `G0`
  - `POST /api/v1/system/mail/dead-letters/{id}/requeue` -> `G0`
- **Gap Status:** `G0`

---

### Section 2.9: Billing, Financials & Escrow (`billing-service`, `financial-service`)

#### `BFF-BILL-01`: Generate Invoice
- **CapabilityId:** `BFF-BILL-01`
- **Resource:** `Invoice`
- **Action:** `GENERATE`
- **Roles:** `[STAFF, MANAGER]`
- **Service:** `billing-service`
- **Proto:** `protos/billing.proto`
- **RPC:** `BillingService.GenerateInvoice`
- **Suggested REST Route:** `POST /api/v1/invoices/generate`
- **Gap Status:** `G1`

#### `BFF-BILL-02` to `BFF-BILL-05`: Invoices, Customer Credit & Escrow Balance
- **Resource:** `Invoice`, `CustomerCredit`, `EscrowWallet`
- **Roles:** `[STAFF, MANAGER, ADMIN]`
- **Suggested REST Routes:**
  - `POST /api/v1/invoices` (`CreateInvoice`) -> `G1`
  - `GET /api/v1/invoices/{id}` (`GetInvoiceDetail`) -> `G1`
  - `GET /api/v1/invoices` (`ListInvoices`) -> `G1`
  - `PATCH /api/v1/invoices/{id}/status` (`UpdateInvoiceStatus`) -> `G1`
  - `POST /api/v1/billing/credit-check` (`CheckCustomerCredit`) -> `G1`
  - `GET /api/v1/escrow/wallets/{id}` (`GetWalletBalance`) -> `G1`
- **Gap Status:** `G1`

#### `BFF-FIN-01` & `BFF-FIN-02`: Cost & Customs Duty Estimation
- **Resource:** `CostEstimation`, `CustomsDuty`
- **Roles:** `[STAFF, MANAGER]`
- **Service:** `financial-service`
- **Proto:** `protos/financial.proto`
- **RPCs:** `FinancialService.EstimateCost`, `FinancialService.GetCustomsDuty`
- **Suggested REST Routes:**
  - `POST /api/v1/financial/estimate-cost` -> `G1`
  - `POST /api/v1/financial/customs-duty` -> `G1`
- **Gap Status:** `G1`

---

## 3. BLOCKED APIs (Backend Contract / Implementation Required First)

> [!WARNING]
> The following capabilities are **BLOCKED** from BFF generation. They MUST NOT be implemented in BFF until their backend contracts or server stubs are completed.

### 3.1. Blocked G2 Capabilities (Proto Contract Required)

1. **`Tenant.UpdateProfile` (`IamTenant`)**:
   - **Command:** `UpdateTenantCommand` exists in [UpdateTenantCommand.cs](file:///D:/IT/CD/aurora-server/src/dotnet/IamTenant/Application/Commands/Tenants/UpdateTenantCommand.cs).
   - **Required Backend Work:** Add `rpc UpdateTenant (UpdateTenantRequest) returns (TenantResponse);` to [protos/iam_tenant.proto](file:///D:/IT/CD/aurora-server/protos/iam_tenant.proto) and implement override in [IamGrpcService.cs](file:///D:/IT/CD/aurora-server/src/dotnet/IamTenant/GrpcServices/IamGrpcService.cs).
   - **Planned REST Route:** `PUT /api/v1/system/tenants/{id}`.

2. **`Invoice.RecordPayment` (`billing-service`)**:
   - **Service Method:** `BillingService.recordPayment` exists in [billing.service.ts](file:///D:/IT/CD/aurora-server/src/nestjs/billing-service/src/application/services/billing.service.ts).
   - **Required Backend Work:** Add `rpc RecordPayment (RecordPaymentRequest) returns (RecordPaymentResponse);` to [protos/billing.proto](file:///D:/IT/CD/aurora-server/protos/billing.proto).
   - **Planned REST Route:** `POST /api/v1/invoices/{id}/payments`.

3. **`Invoice.Cancel` & `AdjustmentNote.Issue` (`billing-service`)**:
   - **Service Methods:** `BillingService.cancelInvoice`, `issueDebitNote`, `issueCreditNote` exist in [billing.service.ts](file:///D:/IT/CD/aurora-server/src/nestjs/billing-service/src/application/services/billing.service.ts).
   - **Required Backend Work:** Add `CancelInvoice`, `IssueDebitNote`, `IssueCreditNote` to [protos/billing.proto](file:///D:/IT/CD/aurora-server/protos/billing.proto).
   - **Planned REST Route:** `POST /api/v1/invoices/{id}/cancel`, `POST /api/v1/invoices/{id}/debit-notes`, `POST /api/v1/invoices/{id}/credit-notes`.

4. **`ExchangeRate.GetRate` (`financial-service`)**:
   - **Service Method:** `FinancialService.getExchangeRate` exists in [financial.service.ts](file:///D:/IT/CD/aurora-server/src/nestjs/financial-service/src/application/services/financial.service.ts).
   - **Required Backend Work:** Add `rpc GetExchangeRate (GetExchangeRateRequest) returns (GetExchangeRateResponse);` to [protos/financial.proto](file:///D:/IT/CD/aurora-server/protos/financial.proto).
   - **Planned REST Route:** `GET /api/v1/financial/exchange-rate`.

5. **`Negotiation.SubmitOffer` & `GetHistory` (`negotiation-agent-service`)**:
   - **Service Methods:** `NegotiationService.submitOffer`, `getSessionHistory` exist in [negotiation.service.ts](file:///D:/IT/CD/aurora-server/src/nestjs/negotiation-agent-service/src/application/services/negotiation.service.ts).
   - **Required Backend Work:** Create formal contract `protos/negotiation.proto`.
   - **Planned REST Route:** `POST /api/v1/negotiation/offer`, `GET /api/v1/negotiation/session/{id}`.

---

### 3.2. Blocked G4 Capabilities (Client Stub Mismatch / Implementation Required)

1. **`ComplianceRag.CheckRouteCompliance` (`RoutePlanningAgent`)**:
   - **Issue:** [ComplianceRagClient.cs](file:///D:/IT/CD/aurora-server/src/dotnet/RoutePlanningAgent/Infrastructure/Services/ComplianceRagClient.cs) calls legacy `ComplianceRag` service instead of `RegulatoryComplianceService.EvaluateCompliance`.
   - **Required Backend Work:** Refactor `RoutePlanningAgent` to consume `RegulatoryComplianceService` via standard `regulatory_compliance.proto` contract.

2. **`DevOpsRagService` & `AiGovernanceAdminService`**:
   - **Issue:** Stubs exist in proto, but server implementations do not exist in the repository.

---

## 4. INTERNAL ONLY — DO NOT EXPOSE (G5)

> [!CAUTION]
> The following capabilities are **STRICTLY CONFIDENTIAL / MACHINE-TO-MACHINE ONLY**. Under no circumstances should any BFF controller or public REST route expose these RPCs.

1. **`AiExecutionService.Generate` & `AiExecutionService.Embed` (`ai-governance`)**:
   - **Why Internal:** Core internal LLM token gateway; requires `x-service-id` internal service credentials.
2. **`AiGovernanceService.ExecutePolicy` (`ai-governance`)**:
   - **Why Internal:** Inter-service pre-execution rate limit and token ceiling validator.
3. **`GpsTrackingService.IngestPosition` (`GpsTracking`)**:
   - **Why Internal:** High-throughput streaming endpoint for IoT hardware edge telemetry.
4. **`DevOpsIngestionService.IngestAlert` (`devops-agent`)**:
   - **Why Internal:** Internal monitoring webhook for Azure Monitor / Loki SRE alerts.
5. **`BillingService.FreezeEscrowAmount`, `ReleaseEscrowAmount`, `RefundEscrowAmount` (`billing-service`)**:
   - **Why Internal:** Critical financial fund movements; must be triggered strictly by verified transactional integration event consumers upon shipment delivery or cancellation.
6. **`FinancialService.GetMinAcceptableRate` & `GetDynamicMargin` (`financial-service`)**:
   - **Why Internal:** Proprietary pricing floors and dynamic margin decay calculations used internally by the automated rate negotiation agent.
7. **`ShipmentWorkflowService.UpdateShipmentDocumentOcr` (`ShipmentWorkflow`)**:
   - **Why Internal:** Asynchronous callback from document OCR worker to enrich shipment document records.
8. **`RegulatoryComplianceService.ValidateGroundedEvidence` (`RegulatoryCompliance`)**:
   - **Why Internal:** Internal deterministic hallucination filter inside the RAG assistant pipeline.
9. **`IamService.ResolveTenantAuthClient` (`IamTenant`)**:
   - **Why Internal:** Internal Cognito App Client routing query.
