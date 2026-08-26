# Aurora Platform - Comprehensive API Gap Analysis

> **Document ID:** `DOC-API-05`  
> **Status:** API Gap Classification & Architectural Remediation Plan Complete  
> **Scope:** End-to-end audit of all platform capabilities to determine exposure readiness, missing contracts, missing implementations, and internal-only boundaries across all 16 microservices.  
> **Architecture Reference:** `codex/requirement.md`, `docs/api-analysis/01-grpc-capability-map.md`, `docs/api-analysis/02-cqrs-capability-map.md`, `docs/api-analysis/03-business-capability-map.md`, `docs/api-analysis/04-role-capability-matrix.md`

---

## 1. Gap Classification Taxonomy

Every discovered platform capability is classified into one of six distinct gap states:

- **`G0` (Ready for BFF / Fully Mapped)**: Backend gRPC/REST and BFF endpoints already exist and align with architecture rules.
- **`G1` (gRPC Ready, BFF Missing)**: gRPC service and backend application logic are fully implemented and tested, but BFF controller endpoint is missing.
- **`G2` (Backend Ready, Proto Contract Missing)**: Application logic/command handler exists in code, but gRPC `.proto` contract or service override is missing.
- **`G3` (Partially Implemented)**: Domain/Application layer has partial logic, but formal CQRS Command/Query or Handler is missing.
- **`G4` (Missing Implementation / Stub Mismatch)**: Required capability exists as a stub or business requirement, but no backend implementation exists.
- **`G5` (Must Stay Internal)**: Critical internal/M2M capability that MUST NOT be exposed to public web portals or user-facing BFFs.

---

## 2. Master API Gap Summary Table

| ID | Capability | Service | Roles | Gap | Existing Backend | Missing Layer | Priority |
| :--- | :--- | :--- | :--- | :---: | :--- | :--- | :---: |
| **GAP-01** | Shipment Lifecycle (CRUD, Submit, Cancel, Import) | `ShipmentWorkflow` | `[STAFF, MANAGER]` | **G1** | `ShipmentGrpcService.cs` (20 RPCs) | `[x] BFF` | **P1** |
| **GAP-02** | Shipment Cargo, Locations & Documents | `ShipmentWorkflow` | `[STAFF, MANAGER]` | **G1** | `ShipmentGrpcService.cs` (Child RPCs) | `[x] BFF` | **P1** |
| **GAP-03** | Shipment Milestones & Timeline | `ShipmentWorkflow` | `[STAFF, MANAGER, ADMIN]` | **G1** | `ShipmentGrpcService.cs` | `[x] BFF` | **P1** |
| **GAP-04** | Live GPS Location & Position History | `GpsTracking` | `[STAFF, MANAGER, ADMIN]` | **G1** | `GpsTrackingGrpcService.cs` | `[x] BFF` | **P1** |
| **GAP-05** | Geofence Management & Alerts | `GpsTracking` | `[STAFF, MANAGER, ADMIN]` | **G1** | `GpsTrackingGrpcService.cs` | `[x] BFF` | **P1** |
| **GAP-06** | User Notifications & Preferences | `Notification` | `[STAFF, MANAGER, ADMIN]` | **G1** | `NotificationGrpcService.cs` | `[x] BFF` | **P1** |
| **GAP-07** | Document OCR Management & Review | `DocumentOcr` | `[STAFF, MANAGER]` | **G0** | `DocumentOcrGrpcService.cs` | *None (Mapped in Staff.Bff)* | **P1** |
| **GAP-08** | Compliance Evaluation & Grounded Copilot | `RegulatoryCompliance` | `[STAFF, MANAGER]` | **G1** | `RegulatoryComplianceGrpcService.cs` | `[x] BFF` | **P1** |
| **GAP-09** | Regulatory Law & Knowledge Query/Ingestion | `RegulatoryCompliance` | `[STAFF, ADMIN, SYSTEM]` | **G0** | `RegulatoryComplianceGrpcService.cs` | *None (Mapped in Staff/Admin BFF)* | **P1** |
| **GAP-10** | Route Planning, Optimization & Recommendation | `RoutePlanningAgent` | `[STAFF, MANAGER]` | **G0** | `RoutePlanningGrpcService.cs` | *None (Mapped in Staff.Bff)* | **P1** |
| **GAP-11** | Dual-Control Route Approvals | `RoutePlanningAgent` | `[MANAGER]` | **G0** | `RoutePlanningGrpcService.cs` | *None (Mapped in Staff.Bff)* | **P1** |
| **GAP-12** | Tenant AI & Rule Configuration | `RoutePlanningAgent` | `[ADMIN]` | **G0** | `RoutePlanningGrpcService.cs` | *None (Mapped in Admin.Bff)* | **P1** |
| **GAP-13** | Staff Identity Administration | `IamTenant` | `[ADMIN]` | **G0** | `IamGrpcService.cs` | *None (Mapped in Admin.Bff)* | **P1** |
| **GAP-14** | Role & Permission Viewing | `IamTenant` | `[ADMIN]` | **G0** | `IamGrpcService.cs` | *None (Mapped in Admin.Bff)* | **P1** |
| **GAP-15** | Role Permission Assignment | `IamTenant` | `[ADMIN]` | **G1** | `IamGrpcService.AssignPermissionsToRole` | `[x] BFF` | **P2** |
| **GAP-16** | User Permission Context Matrix | `IamTenant` | `[STAFF, MANAGER, ADMIN]`| **G1** | `IamGrpcService.GetUserPermissions` | `[x] BFF` | **P1** |
| **GAP-17** | Tenant Company Profile Update | `IamTenant` | `[SYSTEM]` | **G2** | `UpdateTenantCommand.cs` | `[x] Proto [x] gRPC [x] BFF` | **P1** |
| **GAP-18** | Public User Authentication & Onboarding | `IamTenant` | `[STAFF, MANAGER, ADMIN]`| **G0** | `AuthGrpcService.cs` | *None (Mapped in Staff.Bff)* | **P1** |
| **GAP-19** | Mail Domain & Mailbox Administration | `MailService` | `[ADMIN]` | **G0** | `MailManagementService.cs` | *None (Mapped in Admin.Bff)* | **P1** |
| **GAP-20** | Email Drafts & Outbound Send | `MailService` | `[STAFF, MANAGER]` | **G0** | `MailSecurityService.cs` | *None (Mapped in Staff.Bff)* | **P1** |
| **GAP-21** | Email Quarantine Inspection & Release | `MailService` | `[MANAGER, ADMIN]` | **G0** | `MailSecurityService.cs` | *None (Mapped in Staff/Admin BFF)* | **P1** |
| **GAP-22** | Invoice Generation & Detail Viewing | `billing-service` | `[STAFF, MANAGER, ADMIN]`| **G1** | `billing.controller.ts` | `[x] BFF` | **P1** |
| **GAP-23** | Invoice Payment Recording | `billing-service` | `[STAFF, MANAGER]` | **G2** | `billing.service.ts` | `[x] Proto [x] BFF` | **P1** |
| **GAP-24** | Invoice Cancellation & Adjustment Notes | `billing-service` | `[MANAGER, ADMIN]` | **G2** | `billing.service.ts` | `[x] Proto [x] BFF` | **P1** |
| **GAP-25** | Escrow Wallet Balance Viewing | `billing-service` | `[STAFF, MANAGER, ADMIN]`| **G1** | `billing.controller.ts` | `[x] BFF` | **P1** |
| **GAP-26** | Customer Credit Limit Check | `billing-service` | `[STAFF, MANAGER]` | **G1** | `billing.controller.ts` | `[x] BFF` | **P1** |
| **GAP-27** | Freight Cost & Customs Duty Calculation | `financial-service` | `[STAFF, MANAGER]` | **G1** | `financial.controller.ts` | `[x] BFF` | **P1** |
| **GAP-28** | Currency Exchange Rate Lookup | `financial-service` | `[STAFF, MANAGER, ADMIN]`| **G2** | `financial.service.ts` | `[x] Proto [x] BFF` | **P2** |
| **GAP-29** | AI Freight Rate Negotiation | `negotiation-agent`| `[STAFF, MANAGER]` | **G2** | `negotiation.service.ts` | `[x] Proto [x] BFF` | **P1** |
| **GAP-30** | SRE Incident & Auto-Remediation Rule Ops | `devops-agent` | `[SYSTEM]` | **G1** | `IncidentGrpcHandler.java`, `RuleGrpcHandler.java` | `[x] BFF (SRE Portal)` | **P2** |
| **GAP-31** | Compliance RAG Service Alignment | `RoutePlanningAgent` | `[SYSTEM]` | **G4** | `ComplianceRagClient.cs` stub mismatch | `[x] gRPC Client Alignment` | **P0** |
| **GAP-32** | AI Governance Execution Gateway | `ai-governance` | `[SYSTEM]` | **G5** | `AiExecutionGrpcHandler.java` | *None (Strictly Internal)* | **P0** |
| **GAP-33** | Escrow Fund Movement (`Freeze/Release/Refund`)| `billing-service` | `[SYSTEM]` | **G5** | `billing.service.ts` | *None (Strictly Internal)* | **P0** |
| **GAP-34** | IoT GPS Telemetry Ingestion | `GpsTracking` | `[SYSTEM]` | **G5** | `GpsTrackingGrpcService.cs` | *None (Strictly Internal)* | **P0** |
| **GAP-35** | Document OCR Shipment Enrichment Callback | `ShipmentWorkflow` | `[SYSTEM]` | **G5** | `UpdateShipmentDocumentOcrCommand`| *None (Strictly Internal)* | **P0** |

---

## 3. Granular Gap Breakdown

### GAP-01: Shipment Lifecycle Management
- **Service:** `ShipmentWorkflow`
- **Business Capability:** Shipment creation, editing, submission, status transitions, cancellation, draft purging, and bulk import.
- **Required Roles:** `[STAFF, MANAGER]`
- **Classification:** **`G1` (gRPC Ready, BFF Missing)**
- **Existing Backend:**
  - File: [src/dotnet/ShipmentWorkflow/GrpcServices/ShipmentGrpcService.cs](file:///D:/IT/CD/aurora-server/src/dotnet/ShipmentWorkflow/GrpcServices/ShipmentGrpcService.cs)
  - RPCs: `CreateShipment`, `GetShipment`, `ListShipments`, `UpdateShipment`, `SubmitShipment`, `UpdateShipmentStatus`, `CancelShipment`, `DeleteDraftShipment`, `ImportShipments`.
  - Commands/Queries: `CreateShipmentCommand`, `SubmitShipmentCommand`, `UpdateShipmentCommand`, `UpdateShipmentStatusCommand`, `CancelShipmentCommand`, `DeleteDraftShipmentCommand`, `ImportShipmentsCommand`, `GetShipmentQuery`, `ListShipmentsQuery`.
- **Missing Layer:** `[x] BFF`
- **Suggested Files to Add:**
  - `src/dotnet/BFF/Staff.Bff/Controllers/ShipmentsController.cs`
  - `src/dotnet/BFF/BuildingBlocks.BFF/Extensions/GrpcClientExtensions.cs` (Register `ShipmentWorkflowService.ShipmentWorkflowServiceClient`).
- **Reason:** Core logistics functionality is fully tested and operational in backend microservice, but lacks web portal endpoints in `Staff.Bff`.
- **Security/Tenant Concerns:** Must resolve `TenantId` strictly from authenticated JWT context in BFF middleware; reject client-supplied tenant headers.

---

### GAP-02: Shipment Cargo, Locations & Documents
- **Service:** `ShipmentWorkflow`
- **Business Capability:** Sub-resource management for cargo items, route stops/locations, and attached compliance documents.
- **Required Roles:** `[STAFF, MANAGER]`
- **Classification:** **`G1` (gRPC Ready, BFF Missing)**
- **Existing Backend:**
  - File: [src/dotnet/ShipmentWorkflow/GrpcServices/ShipmentGrpcService.cs](file:///D:/IT/CD/aurora-server/src/dotnet/ShipmentWorkflow/GrpcServices/ShipmentGrpcService.cs)
  - RPCs: `AddCargoItem`, `UpdateCargoItem`, `RemoveCargoItem`, `AddShipmentLocation`, `UpdateShipmentLocation`, `RemoveShipmentLocation`, `AttachShipmentDocument`, `RemoveShipmentDocument`.
- **Missing Layer:** `[x] BFF`
- **Suggested Files to Add:**
  - Include sub-resource routes inside `src/dotnet/BFF/Staff.Bff/Controllers/ShipmentsController.cs` (`/api/v1/shipments/{id}/cargo`, `/locations`, `/documents`).
- **Reason:** Operators need granular endpoints to modify cargo and route waypoints on active shipments.
- **Security/Tenant Concerns:** Enforce aggregate root validation ensuring parent `Shipment.TenantId == CurrentUser.TenantId`.

---

### GAP-04 & GAP-05: Real-time GPS Tracking, Geofences & Monitoring Alerts
- **Service:** `GpsTracking`
- **Business Capability:** Real-time map position lookup, historical breadcrumb replay, warehouse geofence configuration, and exception alert monitoring.
- **Required Roles:** `[STAFF, MANAGER, ADMIN]`
- **Classification:** **`G1` (gRPC Ready, BFF Missing)**
- **Existing Backend:**
  - File: [src/dotnet/GpsTracking/GrpcServices/GpsTrackingGrpcService.cs](file:///D:/IT/CD/aurora-server/src/dotnet/GpsTracking/GrpcServices/GpsTrackingGrpcService.cs)
  - RPCs: `GetCurrentLocation`, `ListPositionHistory`, `CreateGeofence`, `ListGeofences`, `SetGeofenceActive`, `ListMonitoringAlerts`, `ResolveMonitoringAlert`.
- **Missing Layer:** `[x] BFF`
- **Suggested Files to Add:**
  - `src/dotnet/BFF/Staff.Bff/Controllers/TrackingController.cs`
  - `src/dotnet/BFF/BuildingBlocks.BFF/Extensions/GrpcClientExtensions.cs` (Register `GpsTrackingService.GpsTrackingServiceClient`).
- **Reason:** Dispatchers and managers require real-time map visualization and alert resolution tools in the web UI.
- **Security/Tenant Concerns:** Position queries must be scoped to vehicles/shipments owned by the calling tenant.

---

### GAP-06: User Notification Center & Event Preferences
- **Service:** `Notification`
- **Business Capability:** In-app notification feed, read-state dismissal, and notification channel preference toggles (InApp, Email, SMS, Webhook).
- **Required Roles:** `[STAFF, MANAGER, ADMIN]`
- **Classification:** **`G1` (gRPC Ready, BFF Missing)**
- **Existing Backend:**
  - File: [src/dotnet/Notification/GrpcServices/NotificationGrpcService.cs](file:///D:/IT/CD/aurora-server/src/dotnet/Notification/GrpcServices/NotificationGrpcService.cs)
  - RPCs: `ListNotifications`, `MarkNotificationRead`, `ListNotificationPreferences`, `UpsertNotificationPreference`.
- **Missing Layer:** `[x] BFF`
- **Suggested Files to Add:**
  - `src/dotnet/BFF/Staff.Bff/Controllers/NotificationsController.cs`
  - `src/dotnet/BFF/BuildingBlocks.BFF/Extensions/GrpcClientExtensions.cs` (Register `NotificationService.NotificationServiceClient`).
- **Reason:** Web portal header notification bell and settings modal require HTTP endpoints.
- **Security/Tenant Concerns:** Must strictly query `RecipientUserId == CurrentUser.UserId` to prevent cross-user notification leakage.

---

### GAP-08: Shipment Compliance Evaluation & AI Copilot
- **Service:** `RegulatoryCompliance`
- **Business Capability:** Pre-dispatch compliance checks against international trade laws and interactive legal RAG queries.
- **Required Roles:** `[STAFF, MANAGER]`
- **Classification:** **`G1` (gRPC Ready, BFF Missing)**
- **Existing Backend:**
  - File: [src/dotnet/RegulatoryCompliance/GrpcServices/RegulatoryComplianceGrpcService.cs](file:///D:/IT/CD/aurora-server/src/dotnet/RegulatoryCompliance/GrpcServices/RegulatoryComplianceGrpcService.cs)
  - RPCs: `EvaluateCompliance`, `GetComplianceEvaluation`, `GenerateGroundedAnswer`.
- **Missing Layer:** `[x] BFF`
- **Suggested Files to Add:**
  - `src/dotnet/BFF/Staff.Bff/Controllers/ComplianceController.cs`
- **Reason:** Compliance officers and dispatchers need dedicated UI actions to trigger compliance audits and consult the AI regulation copilot.
- **Security/Tenant Concerns:** RAG queries must isolate tenant-specific internal policies from platform-wide regulations.

---

### GAP-17: Tenant Organization Profile Update
- **Service:** `IamTenant`
- **Business Capability:** Update tenant company name, tax ID code, and subscription plan tier.
- **Required Roles:** `[SYSTEM]` (Platform System Admin)
- **Classification:** **`G2` (Backend Ready, Proto Contract Missing)**
- **Existing Backend:**
  - Command: `UpdateTenantCommand.cs` in [src/dotnet/IamTenant/Application/Commands/Tenants/UpdateTenantCommand.cs](file:///D:/IT/CD/aurora-server/src/dotnet/IamTenant/Application/Commands/Tenants/UpdateTenantCommand.cs)
  - Handler: `UpdateTenantHandler`
- **Missing Layer:** `[x] Proto [x] gRPC Implementation [x] BFF`
- **Suggested Files to Add/Change:**
  - `protos/iam_tenant.proto`: Add `rpc UpdateTenant (UpdateTenantRequest) returns (TenantResponse);`.
  - `src/dotnet/IamTenant/GrpcServices/IamGrpcService.cs`: Implement `UpdateTenant` override delegating to `UpdateTenantCommand`.
  - `src/dotnet/BFF/System.Bff/Controllers/TenantsController.cs`: Add `PUT /api/v1/system/tenants/{id}`.
- **Reason:** System Admin portal cannot currently edit tenant metadata because `IamGrpcService` only exposes `UpdateTenantStatus`.
- **Security/Tenant Concerns:** High-privilege operation restricted strictly to `SYSTEM_ADMIN` role.

---

### GAP-22 to GAP-26: Invoicing, Payments, Adjustments & Escrow
- **Service:** `billing-service` (NestJS)
- **Business Capability:** Post-delivery invoice generation, payment entry, debit/credit note adjustments, customer credit check, and escrow wallet balance inspection.
- **Required Roles:** `[STAFF, MANAGER, ADMIN]`
- **Classification:** **`G1` / `G2`**
  - Invoicing, Credit Check, Wallet Balance: **`G1`** (gRPC exists, BFF missing).
  - Payment Recording, Cancellation, Debit/Credit Notes: **`G2`** (Implemented in TypeScript service, missing from `billing.proto`).
- **Existing Backend:**
  - Files: [src/nestjs/billing-service/src/interface/controllers/billing.controller.ts](file:///D:/IT/CD/aurora-server/src/nestjs/billing-service/src/interface/controllers/billing.controller.ts), `billing.service.ts`.
- **Missing Layer:** `[x] Proto (for Adjustments & Payments) [x] BFF`
- **Suggested Files to Add/Change:**
  - `protos/billing.proto`: Add `RecordPayment`, `CancelInvoice`, `IssueDebitNote`, `IssueCreditNote`.
  - `src/dotnet/BFF/Staff.Bff/Controllers/BillingController.cs` (Expose invoicing and escrow balance).
- **Reason:** Complete accounts receivable workflows require formal contract alignment and BFF routing.
- **Security/Tenant Concerns:** Debit/Credit adjustments and invoice cancellations are financial mutations requiring `MANAGER` role approval.

---

### GAP-27 to GAP-29: Financial Estimation, Exchange Rates & Rate Bidding
- **Services:** `financial-service`, `negotiation-agent-service` (NestJS)
- **Business Capability:** Freight cost calculation, import duties, currency exchange rates, and interactive freight rate negotiation.
- **Required Roles:** `[STAFF, MANAGER]`
- **Classification:** **`G1` / `G2`**
  - `EstimateCost`, `GetCustomsDuty`: **`G1`** (gRPC ready, BFF missing).
  - `GetExchangeRate`: **`G2`** (Missing in `financial.proto`).
  - `SubmitOffer`, `GetSessionHistory`: **`G2`** (Missing `negotiation.proto`).
- **Missing Layer:** `[x] Proto (for Negotiation & ExchangeRate) [x] BFF`
- **Suggested Files to Add/Change:**
  - `protos/financial.proto`: Add `GetExchangeRate`.
  - `protos/negotiation.proto`: Create contract for `NegotiationService`.
  - `src/dotnet/BFF/Staff.Bff/Controllers/FinancialController.cs` and `NegotiationController.cs`.
- **Reason:** Dispatchers need real-time freight estimation tools and interactive AI pricing dialogues.
- **Security/Tenant Concerns:** Floor rates (`GetMinAcceptableRate`) must remain strictly internal (G5) and never exposed to customers.

---

### GAP-31: Compliance RAG Client Protocol Mismatch
- **Service:** `RoutePlanningAgent` -> `RegulatoryCompliance`
- **Business Capability:** Machine-to-machine route compliance pre-check during route recommendation.
- **Required Roles:** `[SYSTEM]`
- **Classification:** **`G4` (Stub Mismatch / Blocker)**
- **Existing Backend:**
  - Caller: [src/dotnet/RoutePlanningAgent/Infrastructure/Services/ComplianceRagClient.cs](file:///D:/IT/CD/aurora-server/src/dotnet/RoutePlanningAgent/Infrastructure/Services/ComplianceRagClient.cs) (Calls legacy `ComplianceRag.CheckRouteCompliance`).
  - Target: [src/dotnet/RegulatoryCompliance/GrpcServices/RegulatoryComplianceGrpcService.cs](file:///D:/IT/CD/aurora-server/src/dotnet/RegulatoryCompliance/GrpcServices/RegulatoryComplianceGrpcService.cs) (Implements `RegulatoryComplianceService.EvaluateCompliance`).
- **Missing Layer:** `[x] Client Alignment`
- **Suggested Files to Change:**
  - Refactor `RoutePlanningAgent/Infrastructure/Services/ComplianceRagClient.cs` to invoke `RegulatoryComplianceService.EvaluateCompliance` with route waypoint and cargo parameters.
- **Reason:** Prevents `RoutePlanningAgent` from soft-failing during compliance audits.
- **Security/Tenant Concerns:** Must pass caller `TenantId` across service boundary via `ClientMetadataInterceptor`.

---

## 4. Remediation Categories

### 4.1. Ready for BFF (G0 & G1)
*(Backend microservices are 100% complete and tested. Only BFF controller mapping and GrpcClient registration required)*

1. **Shipment Core (`GAP-01`, `GAP-02`, `GAP-03`)**: Create `ShipmentsController.cs` in `Staff.Bff`.
2. **GPS Tracking & Geofences (`GAP-04`, `GAP-05`)**: Create `TrackingController.cs` in `Staff.Bff`.
3. **Notifications & Preferences (`GAP-06`)**: Create `NotificationsController.cs` in `Staff.Bff`.
4. **Compliance Copilot (`GAP-08`)**: Create `ComplianceController.cs` in `Staff.Bff`.
5. **Role Permission Matrix (`GAP-15`, `GAP-16`)**: Register permission endpoints in `Admin.Bff` / `Staff.Bff`.
6. **Invoicing & Escrow Balance (`GAP-22`, `GAP-25`, `GAP-26`)**: Create `BillingController.cs` in `Staff.Bff`.
7. **Cost & Duty Estimation (`GAP-27`)**: Create `FinancialController.cs` in `Staff.Bff`.

---

### 4.2. Backend Contract Required First (G2)
*(Application logic exists in code, but protobuf contract or gRPC override must be added before BFF exposure)*

1. **`GAP-17` (Tenant Profile Update)**: Add `UpdateTenant` to [protos/iam_tenant.proto](file:///D:/IT/CD/aurora-server/protos/iam_tenant.proto) and wire to [UpdateTenantCommand.cs](file:///D:/IT/CD/aurora-server/src/dotnet/IamTenant/Application/Commands/Tenants/UpdateTenantCommand.cs).
2. **`GAP-23` & `GAP-24` (Payment Entry & Adjustments)**: Add `RecordPayment`, `CancelInvoice`, `IssueDebitNote`, `IssueCreditNote` to [protos/billing.proto](file:///D:/IT/CD/aurora-server/protos/billing.proto).
3. **`GAP-28` (Exchange Rates)**: Add `GetExchangeRate` to [protos/financial.proto](file:///D:/IT/CD/aurora-server/protos/financial.proto).
4. **`GAP-29` (AI Rate Negotiation)**: Create `protos/negotiation.proto` for `NegotiationService`.

---

### 4.3. Backend Implementation / Alignment Required (G4)

1. **`GAP-31` (Compliance RAG Client Alignment)**:
   - Align [RoutePlanningAgent/Infrastructure/Services/ComplianceRagClient.cs](file:///D:/IT/CD/aurora-server/src/dotnet/RoutePlanningAgent/Infrastructure/Services/ComplianceRagClient.cs) to call `RegulatoryComplianceService.EvaluateCompliance` directly.

---

### 4.4. Must Stay Internal (G5)
*(Strictly machine-to-machine; MUST NEVER be exposed to BFF controllers)*

1. **`GAP-32` (`AiExecutionService.Generate / Embed`)**: Internal LLM inference engine with service token ceiling enforcement.
2. **`GAP-33` (`BillingService.Freeze / Release / Refund Escrow`)**: Automated carrier escrow fund movements triggered strictly by milestone event consumers.
3. **`GAP-34` (`GpsTrackingService.IngestPosition`)**: IoT hardware edge gateway telemetry stream.
4. **`GAP-35` (`ShipmentWorkflowService.UpdateShipmentDocumentOcr`)**: Internal asynchronous callback from OCR worker.
5. **Pricing Floor (`FinancialService.GetMinAcceptableRate / GetDynamicMargin`)**: Confidential internal rate calculations.
