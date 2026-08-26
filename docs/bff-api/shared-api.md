# Aurora Platform - Shared API Catalog (SHARED)

> **Document ID:** `DOC-BFF-SHARED`  
> **Status:** Canonical Specification Complete  
> **Scope:** All HTTP REST APIs utilized by two or more platform roles (`STAFF`, `MANAGER`, `ADMIN`, `SYSTEM`).  
> **Rule:** An API used by >= 2 roles MUST have exactly ONE implementation and is recorded here without duplication in single-role catalogs.

---

## 1. Shared API Summary Table

| Method | Endpoint | Function | Roles | Service | RPC | Main Source File |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| `POST` | `/api/v1/shipments` | Create Draft Shipment | `[STAFF, MANAGER]` | `ShipmentWorkflow` | `CreateShipment` | `Staff.Bff/Controllers/ShipmentsController.cs` |
| `GET` | `/api/v1/shipments/{id}` | Get Shipment Details | `[STAFF, MANAGER, ADMIN]` | `ShipmentWorkflow` | `GetShipment` | `Staff.Bff/Controllers/ShipmentsController.cs` |
| `GET` | `/api/v1/shipments` | List / Search Shipments | `[STAFF, MANAGER, ADMIN]` | `ShipmentWorkflow` | `ListShipments` | `Staff.Bff/Controllers/ShipmentsController.cs` |
| `PUT` | `/api/v1/shipments/{id}` | Update Shipment Details | `[STAFF, MANAGER]` | `ShipmentWorkflow` | `UpdateShipment` | `Staff.Bff/Controllers/ShipmentsController.cs` |
| `POST` | `/api/v1/shipments/{id}/submit` | Submit Shipment Workflow | `[STAFF, MANAGER]` | `ShipmentWorkflow` | `SubmitShipment` | `Staff.Bff/Controllers/ShipmentsController.cs` |
| `PATCH`| `/api/v1/shipments/{id}/status` | Update Shipment Status | `[STAFF, MANAGER]` | `ShipmentWorkflow` | `UpdateShipmentStatus` | `Staff.Bff/Controllers/ShipmentsController.cs` |
| `POST` | `/api/v1/shipments/{id}/cancel` | Cancel Active Shipment | `[STAFF, MANAGER]` | `ShipmentWorkflow` | `CancelShipment` | `Staff.Bff/Controllers/ShipmentsController.cs` |
| `DELETE`| `/api/v1/shipments/{id}` | Delete Draft Shipment | `[STAFF, MANAGER]` | `ShipmentWorkflow` | `DeleteDraftShipment` | `Staff.Bff/Controllers/ShipmentsController.cs` |
| `POST` | `/api/v1/shipments/import` | Bulk Ingest Shipments | `[STAFF, MANAGER]` | `ShipmentWorkflow` | `ImportShipments` | `Staff.Bff/Controllers/ShipmentsController.cs` |
| `POST` | `/api/v1/shipments/{id}/cargo` | Add Cargo Item Line | `[STAFF, MANAGER]` | `ShipmentWorkflow` | `AddCargoItem` | `Staff.Bff/Controllers/ShipmentsController.cs` |
| `PUT` | `/api/v1/shipments/{id}/cargo/{itemId}` | Update Cargo Item | `[STAFF, MANAGER]` | `ShipmentWorkflow` | `UpdateCargoItem` | `Staff.Bff/Controllers/ShipmentsController.cs` |
| `DELETE`| `/api/v1/shipments/{id}/cargo/{itemId}`| Remove Cargo Item | `[STAFF, MANAGER]` | `ShipmentWorkflow` | `RemoveCargoItem` | `Staff.Bff/Controllers/ShipmentsController.cs` |
| `POST` | `/api/v1/shipments/{id}/locations` | Add Route Checkpoint | `[STAFF, MANAGER]` | `ShipmentWorkflow` | `AddShipmentLocation` | `Staff.Bff/Controllers/ShipmentsController.cs` |
| `PUT` | `/api/v1/shipments/{id}/locations/{locId}` | Update Route Checkpoint | `[STAFF, MANAGER]` | `ShipmentWorkflow` | `UpdateShipmentLocation`| `Staff.Bff/Controllers/ShipmentsController.cs` |
| `DELETE`| `/api/v1/shipments/{id}/locations/{locId}`| Remove Route Checkpoint | `[STAFF, MANAGER]` | `ShipmentWorkflow` | `RemoveShipmentLocation`| `Staff.Bff/Controllers/ShipmentsController.cs` |
| `POST` | `/api/v1/shipments/{id}/documents` | Attach Compliance Document | `[STAFF, MANAGER]` | `ShipmentWorkflow` | `AttachShipmentDocument`| `Staff.Bff/Controllers/ShipmentsController.cs` |
| `DELETE`| `/api/v1/shipments/{id}/documents/{docId}` | Detach Document | `[STAFF, MANAGER]` | `ShipmentWorkflow` | `RemoveShipmentDocument`| `Staff.Bff/Controllers/ShipmentsController.cs` |
| `POST` | `/api/v1/shipments/{id}/milestones` | Record Event Milestone | `[STAFF, MANAGER]` | `ShipmentWorkflow` | `AddShipmentMilestone` | `Staff.Bff/Controllers/ShipmentsController.cs` |
| `GET` | `/api/v1/shipments/{id}/timeline` | Get Shipment Event Timeline | `[STAFF, MANAGER, ADMIN]` | `ShipmentWorkflow` | `GetShipmentTimeline` | `Staff.Bff/Controllers/ShipmentsController.cs` |


| `GET` | `/api/v1/tracking/{id}/current` | Get Live GPS Coordinates | `[STAFF, MANAGER, ADMIN]` | `GpsTracking` | `GetCurrentLocation` | `Staff.Bff/Controllers/TrackingController.cs` |
| `GET` | `/api/v1/tracking/{id}/history` | Get GPS Historical Replay | `[STAFF, MANAGER, ADMIN]` | `GpsTracking` | `ListPositionHistory` | `Staff.Bff/Controllers/TrackingController.cs` |
| `POST` | `/api/v1/tracking/geofences` | Create Warehouse Geofence | `[STAFF, MANAGER, ADMIN]` | `GpsTracking` | `CreateGeofence` | `Staff.Bff/Controllers/TrackingController.cs` |
| `GET` | `/api/v1/tracking/geofences` | List Warehouse Geofences | `[STAFF, MANAGER, ADMIN]` | `GpsTracking` | `ListGeofences` | `Staff.Bff/Controllers/TrackingController.cs` |
| `PATCH`| `/api/v1/tracking/geofences/{id}/active` | Toggle Geofence Active State | `[STAFF, MANAGER, ADMIN]` | `GpsTracking` | `SetGeofenceActive` | `Staff.Bff/Controllers/TrackingController.cs` |
| `GET` | `/api/v1/tracking/alerts` | List Exception Alerts | `[STAFF, MANAGER, ADMIN]` | `GpsTracking` | `ListMonitoringAlerts` | `Staff.Bff/Controllers/TrackingController.cs` |
| `POST` | `/api/v1/tracking/alerts/{id}/resolve` | Resolve Monitoring Alert | `[MANAGER, ADMIN]` | `GpsTracking` | `ResolveMonitoringAlert` | `Staff.Bff/Controllers/TrackingController.cs` |


| `GET` | `/api/v1/notifications` | List User In-App Notifications | `[STAFF, MANAGER, ADMIN]` | `Notification` | `ListNotifications` | `Staff.Bff/Controllers/NotificationsController.cs` |
| `PATCH`| `/api/v1/notifications/{id}/read` | Mark Notification As Read | `[STAFF, MANAGER, ADMIN]` | `Notification` | `MarkNotificationRead` | `Staff.Bff/Controllers/NotificationsController.cs` |
| `GET` | `/api/v1/notifications/preferences` | Get Notification Channels | `[STAFF, MANAGER, ADMIN]` | `Notification` | `ListNotificationPreferences` | `Staff.Bff/Controllers/NotificationsController.cs` |
| `PUT` | `/api/v1/notifications/preferences` | Update Notification Channels | `[STAFF, MANAGER, ADMIN]` | `Notification` | `UpsertNotificationPreference`| `Staff.Bff/Controllers/NotificationsController.cs` |


| `POST` | `/api/v1/documents/ocr` | Submit Document for OCR | `[STAFF, MANAGER]` | `DocumentOcr` | `SubmitOcrJob` | `Staff.Bff/Controllers/DocumentsController.cs` |
| `GET` | `/api/v1/documents/jobs/{id}` | Get OCR Extraction Result | `[STAFF, MANAGER, ADMIN]` | `DocumentOcr` | `GetDocumentJob` | `Staff.Bff/Controllers/DocumentsController.cs` |
| `GET` | `/api/v1/documents/jobs` | List Active OCR Jobs | `[STAFF, MANAGER, ADMIN]` | `DocumentOcr` | `ListDocumentJobs` | `Staff.Bff/Controllers/DocumentsController.cs` |
| `POST` | `/api/v1/documents/jobs/{id}/review` | Human Review of Low-Confidence OCR | `[MANAGER, ADMIN]` | `DocumentOcr` | `ReviewDocumentJob` | `Staff.Bff/Controllers/DocumentsController.cs` |
| `POST` | `/api/v1/documents/jobs/{id}/cancel` | Cancel OCR Processing | `[STAFF, MANAGER]` | `DocumentOcr` | `CancelDocumentJob` | `Staff.Bff/Controllers/DocumentsController.cs` |
| `POST` | `/api/v1/documents/jobs/{id}/retry` | Retry Failed OCR Job | `[STAFF, MANAGER]` | `DocumentOcr` | `RetryDocumentJob` | `Staff.Bff/Controllers/DocumentsController.cs` |


| `POST` | `/api/v1/compliance/evaluations` | Evaluate Shipment Compliance | `[STAFF, MANAGER]` | `RegulatoryCompliance`| `EvaluateCompliance` | `Staff.Bff/Controllers/ComplianceController.cs` |
| `GET` | `/api/v1/compliance/evaluations/{id}` | Get Compliance Report | `[STAFF, MANAGER, ADMIN]` | `RegulatoryCompliance`| `GetComplianceEvaluation` | `Staff.Bff/Controllers/ComplianceController.cs` |
| `POST` | `/api/v1/compliance/copilot/ask` | Ask AI Legal Regulations Copilot | `[STAFF, MANAGER, ADMIN]` | `RegulatoryCompliance`| `GenerateGroundedAnswer` | `Staff.Bff/Controllers/ComplianceController.cs` |
| `POST` | `/api/v1/compliance/regulations/query` | Search Global Regulations RAG | `[STAFF, MANAGER, ADMIN]` | `RegulatoryCompliance`| `QueryRegulations` | `Staff.Bff/Controllers/SearchController.cs` |
| `POST` | `/api/v1/compliance/knowledge/query` | Search Internal SOP Knowledge | `[STAFF, MANAGER, ADMIN]` | `RegulatoryCompliance`| `QueryKnowledge` | `Staff.Bff/Controllers/SearchController.cs` |


| `POST` | `/api/v1/routes` | Create Multi-stop Route | `[STAFF, MANAGER]` | `RoutePlanningAgent` | `CreateRoute` | `Staff.Bff/Controllers/RoutesController.cs` |
| `GET` | `/api/v1/routes/{id}` | Get Route Details | `[STAFF, MANAGER, ADMIN]` | `RoutePlanningAgent` | `GetRoute` | `Staff.Bff/Controllers/RoutesController.cs` |
| `GET` | `/api/v1/routes` | List Dispatch Routes | `[STAFF, MANAGER, ADMIN]` | `RoutePlanningAgent` | `ListRoutes` | `Staff.Bff/Controllers/RoutesController.cs` |
| `PUT` | `/api/v1/routes/{id}` | Update Dispatch Route | `[STAFF, MANAGER]` | `RoutePlanningAgent` | `UpdateRoute` | `Staff.Bff/Controllers/RoutesController.cs` |
| `DELETE`| `/api/v1/routes/{id}` | Delete Draft Route | `[STAFF, MANAGER]` | `RoutePlanningAgent` | `DeleteRoute` | `Staff.Bff/Controllers/RoutesController.cs` |
| `PATCH`| `/api/v1/routes/{id}/status` | Update Route Dispatch Status | `[STAFF, MANAGER]` | `RoutePlanningAgent` | `UpdateRouteStatus` | `Staff.Bff/Controllers/RoutesController.cs` |
| `POST` | `/api/v1/routes/{id}/optimize` | Run TSP Route Optimization | `[STAFF, MANAGER]` | `RoutePlanningAgent` | `OptimizeRoute` | `Staff.Bff/Controllers/RoutesController.cs` |
| `POST` | `/api/v1/routes/{id}/recommendation`| AI Route Recommendation | `[STAFF, MANAGER]` | `RoutePlanningAgent` | `GetRouteRecommendation`| `Staff.Bff/Controllers/RoutesController.cs` |
| `GET` | `/api/v1/approvals/pending` | List Pending Route Approvals | `[MANAGER, ADMIN]` | `RoutePlanningAgent` | `ListPendingApprovals` | `Staff.Bff/Controllers/ApprovalsController.cs` |


| `POST` | `/api/v1/auth/identify` | Pre-Auth Email Existence Check | `[STAFF, MANAGER, ADMIN]` | `IamTenant` | `IdentifyUser` | `BuildingBlocks.BFF/AuthController.cs` |
| `POST` | `/api/v1/auth/login` | User Password Login | `[STAFF, MANAGER, ADMIN]` | `IamTenant` | `Login` | `BuildingBlocks.BFF/AuthController.cs` |
| `POST` | `/api/v1/auth/complete-invitation` | Initial Login & Password Set | `[STAFF, MANAGER, ADMIN]` | `IamTenant` | `CompleteInvitation` | `BuildingBlocks.BFF/AuthController.cs` |
| `POST` | `/api/v1/auth/refresh` | Refresh JWT Access Token | `[STAFF, MANAGER, ADMIN]` | `IamTenant` | `RefreshToken` | `BuildingBlocks.BFF/AuthController.cs` |
| `POST` | `/api/v1/auth/logout` | Revoke User Session | `[STAFF, MANAGER, ADMIN]` | `IamTenant` | `Logout` | `BuildingBlocks.BFF/AuthController.cs` |
| `POST` | `/api/v1/auth/forgot-password` | Request Password Reset Code | `[STAFF, MANAGER, ADMIN]` | `IamTenant` | `ForgotPassword` | `BuildingBlocks.BFF/AuthController.cs` |


| `GET` | `/api/v1/admin/staff/{id}` | View Staff Profile Card | `[STAFF, MANAGER, ADMIN]` | `IamTenant` | `GetUser` | `Admin.Bff/Controllers/StaffController.cs` |

| `POST` | `/api/v1/mail/drafts` | Create Email Draft | `[STAFF, MANAGER]` | `MailService` | `CreateDraftMessage` | `Staff.Bff/Controllers/MailController.cs` |
| `GET` | `/api/v1/mail/drafts/{id}` | Get Email Draft | `[STAFF, MANAGER]` | `MailService` | `GetDraft` | `Staff.Bff/Controllers/MailController.cs` |
| `GET` | `/api/v1/mail/drafts` | List Email Drafts | `[STAFF, MANAGER]` | `MailService` | `ListDrafts` | `Staff.Bff/Controllers/MailController.cs` |
| `POST` | `/api/v1/mail/send` | Dispatch Outbound Email | `[STAFF, MANAGER]` | `MailService` | `SubmitOutboundMessage` | `Staff.Bff/Controllers/MailController.cs` |
| `GET` | `/api/v1/mail/messages/{id}` | Get Processed Email Details | `[STAFF, MANAGER, ADMIN]` | `MailService` | `GetProcessedMessage` | `Staff.Bff/Controllers/MailController.cs` |
| `GET` | `/api/v1/mail/messages` | List Processed Emails | `[STAFF, MANAGER, ADMIN]` | `MailService` | `ListProcessedMessages` | `Staff.Bff/Controllers/MailController.cs` |
| `GET` | `/api/v1/mail/quarantine/{id}` | Get Quarantined Email Details | `[MANAGER, ADMIN]` | `MailService` | `GetQuarantineRecord` | `Staff.Bff/Controllers/MailController.cs` |
| `GET` | `/api/v1/mail/quarantine` | List Quarantined Emails | `[MANAGER, ADMIN]` | `MailService` | `ListQuarantineRecords` | `Staff.Bff/Controllers/MailController.cs` |
| `POST` | `/api/v1/mail/quarantine/{id}/release` | Release Quarantined Email | `[MANAGER, ADMIN]` | `MailService` | `ReleaseQuarantine` | `Staff.Bff/Controllers/MailController.cs` |


| `POST` | `/api/v1/invoices/generate` | Auto-Generate Invoices | `[STAFF, MANAGER]` | `billing-service` | `GenerateInvoice` | `Staff.Bff/Controllers/BillingController.cs` |
| `POST` | `/api/v1/invoices` | Create Manual Invoice | `[STAFF, MANAGER]` | `billing-service` | `CreateInvoice` | `Staff.Bff/Controllers/BillingController.cs` |
| `GET` | `/api/v1/invoices/{id}` | Get Invoice Details | `[STAFF, MANAGER, ADMIN]` | `billing-service` | `GetInvoiceDetail` | `Staff.Bff/Controllers/BillingController.cs` |
| `GET` | `/api/v1/invoices` | List Invoices (Paged) | `[STAFF, MANAGER, ADMIN]` | `billing-service` | `ListInvoices` | `Staff.Bff/Controllers/BillingController.cs` |
| `PATCH`| `/api/v1/invoices/{id}/status` | Update Invoice Status | `[STAFF, MANAGER]` | `billing-service` | `UpdateInvoiceStatus` | `Staff.Bff/Controllers/BillingController.cs` |
| `POST` | `/api/v1/billing/credit-check` | Check Customer Credit Limits | `[STAFF, MANAGER]` | `billing-service` | `CheckCustomerCredit` | `Staff.Bff/Controllers/BillingController.cs` |
| `GET` | `/api/v1/escrow/wallets/{id}` | Get Escrow Wallet Balance | `[STAFF, MANAGER, ADMIN]` | `billing-service` | `GetWalletBalance` | `Staff.Bff/Controllers/BillingController.cs` |
| `POST` | `/api/v1/financial/estimate-cost` | Estimate Multi-modal Freight Cost| `[STAFF, MANAGER]` | `financial-service` | `EstimateCost` | `Staff.Bff/Controllers/FinancialController.cs` |
| `POST` | `/api/v1/financial/customs-duty` | Calculate HS Code Import Duties | `[STAFF, MANAGER]` | `financial-service` | `GetCustomsDuty` | `Staff.Bff/Controllers/FinancialController.cs` |

| `POST` | `/api/v1/chat/message` | Send Customer Support AI Chat | `[STAFF, MANAGER, ADMIN]` | `customer-assistant`| `handleMessage` | `Staff.Bff/Controllers/ChatController.cs` |

---

## 2. Granular Multi-Role Tenant Behaviors

### `POST /api/v1/shipments`
- **Roles:** `[STAFF, MANAGER]`
- **Function:** Initiates a new shipment lifecycle aggregate root in `Draft` state.
- **Tenant Behavior per Role:**
  - **STAFF:** Created under operator's authenticated `TenantId`. Assigned to operator's queue.
  - **MANAGER:** Created under supervisor's `TenantId`. Bypasses initial triage queues for expedited handling.
  - **ADMIN:** Forbidden (System Admin / Tenant Admin do not create daily operational freight loads).
  - **SYSTEM:** Forbidden (Must use internal event ingestion).
- **Backend Files:**
  - Proto: [protos/shipment_workflow.proto](file:///D:/IT/CD/aurora-server/protos/shipment_workflow.proto)
  - Implementation: [src/dotnet/ShipmentWorkflow/GrpcServices/ShipmentGrpcService.cs](file:///D:/IT/CD/aurora-server/src/dotnet/ShipmentWorkflow/GrpcServices/ShipmentGrpcService.cs)
  - Command: `CreateShipmentCommand.cs` & `CreateShipmentCommandHandler`
- **BFF Files:**
  - Controller: `src/dotnet/BFF/Staff.Bff/Controllers/ShipmentsController.cs`
  - Client: `ShipmentWorkflowService.ShipmentWorkflowServiceClient`

---

### `GET /api/v1/tracking/{id}/current`
- **Roles:** `[STAFF, MANAGER, ADMIN]`
- **Function:** Queries real-time latitude, longitude, heading, and speed telemetry.
- **Tenant Behavior per Role:**
  - **STAFF:** Views live telemetry for assigned shipments/vehicles.
  - **MANAGER:** Views live telemetry across all team dispatches.
  - **ADMIN:** Views fleet-wide live telemetry within company organization.
  - **SYSTEM:** Machine-to-machine internal monitoring.
- **Backend Files:**
  - Proto: [protos/gps_tracking.proto](file:///D:/IT/CD/aurora-server/protos/gps_tracking.proto)
  - Implementation: [src/dotnet/GpsTracking/GrpcServices/GpsTrackingGrpcService.cs](file:///D:/IT/CD/aurora-server/src/dotnet/GpsTracking/GrpcServices/GpsTrackingGrpcService.cs)
- **BFF Files:**
  - Controller: `src/dotnet/BFF/Staff.Bff/Controllers/TrackingController.cs`
  - Client: `GpsTrackingService.GpsTrackingServiceClient`
