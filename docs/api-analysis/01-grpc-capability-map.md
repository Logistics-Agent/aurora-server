# Aurora Platform - gRPC Capability Map & Protocol Analysis

> **Document ID:** `DOC-API-01`  
> **Status:** Discovery & Inventory Complete  
> **Scope:** Complete backend RPC analysis across .NET 10, Java 21 (Spring Boot 3), and NestJS microservices.  
> **Architecture Reference:** `codex/requirement.md`, `codex/specs/logistics-architecture.md`, `protos/*.proto`

---

## 1. Master gRPC Capability Inventory

The following table summarizes all 120+ RPC definitions and endpoints across the Aurora backend, mapping their protocol definition, server implementation, streaming type, tenant isolation scope, authentication requirements, external exposure candidates, and operational status.

| Service | RPC | Proto File | Server Implementation | Type | Tenant Scope | Auth | Candidate Exposure | Status |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| **ShipmentWorkflowService** | `CreateShipment` | `protos/shipment_workflow.proto` | `src/dotnet/ShipmentWorkflow/GrpcServices/ShipmentGrpcService.cs` | Unary | Isolated (`ICurrentUserService`) | Authenticated User | Staff BFF (`POST /api/v1/shipments`) | Fully Implemented |
| **ShipmentWorkflowService** | `GetShipment` | `protos/shipment_workflow.proto` | `src/dotnet/ShipmentWorkflow/GrpcServices/ShipmentGrpcService.cs` | Unary | Isolated (`ICurrentUserService`) | Authenticated User | Staff BFF (`GET /api/v1/shipments/{id}`) | Fully Implemented |
| **ShipmentWorkflowService** | `ListShipments` | `protos/shipment_workflow.proto` | `src/dotnet/ShipmentWorkflow/GrpcServices/ShipmentGrpcService.cs` | Unary | Isolated (`ICurrentUserService`) | Authenticated User | Staff BFF (`GET /api/v1/shipments`) | Fully Implemented |
| **ShipmentWorkflowService** | `UpdateShipmentStatus` | `protos/shipment_workflow.proto` | `src/dotnet/ShipmentWorkflow/GrpcServices/ShipmentGrpcService.cs` | Unary | Isolated (`ICurrentUserService`) | Authenticated User | Staff BFF (`PATCH /api/v1/shipments/{id}/status`) | Fully Implemented |
| **ShipmentWorkflowService** | `GetShipmentTimeline` | `protos/shipment_workflow.proto` | `src/dotnet/ShipmentWorkflow/GrpcServices/ShipmentGrpcService.cs` | Unary | Isolated (`ICurrentUserService`) | Authenticated User | Staff BFF (`GET /api/v1/shipments/{id}/timeline`) | Fully Implemented |
| **ShipmentWorkflowService** | `SubmitShipment` | `protos/shipment_workflow.proto` | `src/dotnet/ShipmentWorkflow/GrpcServices/ShipmentGrpcService.cs` | Unary | Isolated (`ICurrentUserService`) | Authenticated User | Staff BFF (`POST /api/v1/shipments/{id}/submit`) | Fully Implemented |
| **ShipmentWorkflowService** | `UpdateShipment` | `protos/shipment_workflow.proto` | `src/dotnet/ShipmentWorkflow/GrpcServices/ShipmentGrpcService.cs` | Unary | Isolated (`ICurrentUserService`) | Authenticated User | Staff BFF (`PUT /api/v1/shipments/{id}`) | Fully Implemented |
| **ShipmentWorkflowService** | `CancelShipment` | `protos/shipment_workflow.proto` | `src/dotnet/ShipmentWorkflow/GrpcServices/ShipmentGrpcService.cs` | Unary | Isolated (`ICurrentUserService`) | Authenticated User | Staff BFF (`POST /api/v1/shipments/{id}/cancel`) | Fully Implemented |
| **ShipmentWorkflowService** | `DeleteDraftShipment` | `protos/shipment_workflow.proto` | `src/dotnet/ShipmentWorkflow/GrpcServices/ShipmentGrpcService.cs` | Unary | Isolated (`ICurrentUserService`) | Authenticated User | Staff BFF (`DELETE /api/v1/shipments/{id}`) | Fully Implemented |
| **ShipmentWorkflowService** | `AddCargoItem` | `protos/shipment_workflow.proto` | `src/dotnet/ShipmentWorkflow/GrpcServices/ShipmentGrpcService.cs` | Unary | Isolated (`ICurrentUserService`) | Authenticated User | Staff BFF (`POST /api/v1/shipments/{id}/cargo-items`) | Fully Implemented |
| **ShipmentWorkflowService** | `UpdateCargoItem` | `protos/shipment_workflow.proto` | `src/dotnet/ShipmentWorkflow/GrpcServices/ShipmentGrpcService.cs` | Unary | Isolated (`ICurrentUserService`) | Authenticated User | Staff BFF (`PUT /api/v1/shipments/{id}/cargo-items/{itemId}`) | Fully Implemented |
| **ShipmentWorkflowService** | `RemoveCargoItem` | `protos/shipment_workflow.proto` | `src/dotnet/ShipmentWorkflow/GrpcServices/ShipmentGrpcService.cs` | Unary | Isolated (`ICurrentUserService`) | Authenticated User | Staff BFF (`DELETE /api/v1/shipments/{id}/cargo-items/{itemId}`) | Fully Implemented |
| **ShipmentWorkflowService** | `AddShipmentLocation` | `protos/shipment_workflow.proto` | `src/dotnet/ShipmentWorkflow/GrpcServices/ShipmentGrpcService.cs` | Unary | Isolated (`ICurrentUserService`) | Authenticated User | Staff BFF (`POST /api/v1/shipments/{id}/locations`) | Fully Implemented |
| **ShipmentWorkflowService** | `UpdateShipmentLocation` | `protos/shipment_workflow.proto` | `src/dotnet/ShipmentWorkflow/GrpcServices/ShipmentGrpcService.cs` | Unary | Isolated (`ICurrentUserService`) | Authenticated User | Staff BFF (`PUT /api/v1/shipments/{id}/locations/{locationId}`) | Fully Implemented |
| **ShipmentWorkflowService** | `RemoveShipmentLocation` | `protos/shipment_workflow.proto` | `src/dotnet/ShipmentWorkflow/GrpcServices/ShipmentGrpcService.cs` | Unary | Isolated (`ICurrentUserService`) | Authenticated User | Staff BFF (`DELETE /api/v1/shipments/{id}/locations/{locationId}`) | Fully Implemented |
| **ShipmentWorkflowService** | `AttachShipmentDocument` | `protos/shipment_workflow.proto` | `src/dotnet/ShipmentWorkflow/GrpcServices/ShipmentGrpcService.cs` | Unary | Isolated (`ICurrentUserService`) | Authenticated User | Staff BFF (`POST /api/v1/shipments/{id}/documents`) | Fully Implemented |
| **ShipmentWorkflowService** | `UpdateShipmentDocumentOcr` | `protos/shipment_workflow.proto` | `src/dotnet/ShipmentWorkflow/GrpcServices/ShipmentGrpcService.cs` | Unary | Isolated (`ICurrentUserService`) | Service-to-Service / User | Internal / DocumentOcr Callback | Fully Implemented |
| **ShipmentWorkflowService** | `RemoveShipmentDocument` | `protos/shipment_workflow.proto` | `src/dotnet/ShipmentWorkflow/GrpcServices/ShipmentGrpcService.cs` | Unary | Isolated (`ICurrentUserService`) | Authenticated User | Staff BFF (`DELETE /api/v1/shipments/{id}/documents/{documentId}`) | Fully Implemented |
| **ShipmentWorkflowService** | `AddShipmentMilestone` | `protos/shipment_workflow.proto` | `src/dotnet/ShipmentWorkflow/GrpcServices/ShipmentGrpcService.cs` | Unary | Isolated (`ICurrentUserService`) | Authenticated User | Staff BFF (`POST /api/v1/shipments/{id}/milestones`) | Fully Implemented |
| **ShipmentWorkflowService** | `ImportShipments` | `protos/shipment_workflow.proto` | `src/dotnet/ShipmentWorkflow/GrpcServices/ShipmentGrpcService.cs` | Unary | Isolated (`ICurrentUserService`) | Authenticated User | Staff BFF (`POST /api/v1/shipments/import`) | Fully Implemented |
| **NotificationService** | `ListNotifications` | `protos/notification.proto` | `src/dotnet/Notification/GrpcServices/NotificationGrpcService.cs` | Unary | Isolated (`ICurrentUserService`) | Authenticated User | Staff BFF (`GET /api/v1/notifications`) | Fully Implemented |
| **NotificationService** | `MarkNotificationRead` | `protos/notification.proto` | `src/dotnet/Notification/GrpcServices/NotificationGrpcService.cs` | Unary | Isolated (`ICurrentUserService`) | Authenticated User | Staff BFF (`PATCH /api/v1/notifications/{id}/read`) | Fully Implemented |
| **NotificationService** | `ListNotificationPreferences` | `protos/notification.proto` | `src/dotnet/Notification/GrpcServices/NotificationGrpcService.cs` | Unary | Isolated (`ICurrentUserService`) | Authenticated User | Staff BFF (`GET /api/v1/notifications/preferences`) | Fully Implemented |
| **NotificationService** | `UpsertNotificationPreference` | `protos/notification.proto` | `src/dotnet/Notification/GrpcServices/NotificationGrpcService.cs` | Unary | Isolated (`ICurrentUserService`) | Authenticated User | Staff BFF (`PUT /api/v1/notifications/preferences`) | Fully Implemented |
| **GpsTrackingService** | `IngestPosition` | `protos/gps_tracking.proto` | `src/dotnet/GpsTracking/GrpcServices/GpsTrackingGrpcService.cs` | Unary | Isolated (`ICurrentUserService`) | Device / Edge Gateway | IoT Ingestion Gateway | Fully Implemented |
| **GpsTrackingService** | `GetCurrentLocation` | `protos/gps_tracking.proto` | `src/dotnet/GpsTracking/GrpcServices/GpsTrackingGrpcService.cs` | Unary | Isolated (`ICurrentUserService`) | Authenticated User | Staff BFF (`GET /api/v1/tracking/{id}/current`) | Fully Implemented |
| **GpsTrackingService** | `ListPositionHistory` | `protos/gps_tracking.proto` | `src/dotnet/GpsTracking/GrpcServices/GpsTrackingGrpcService.cs` | Unary | Isolated (`ICurrentUserService`) | Authenticated User | Staff BFF (`GET /api/v1/tracking/{id}/history`) | Fully Implemented |
| **GpsTrackingService** | `CreateGeofence` | `protos/gps_tracking.proto` | `src/dotnet/GpsTracking/GrpcServices/GpsTrackingGrpcService.cs` | Unary | Isolated (`ICurrentUserService`) | Authenticated User | Staff BFF (`POST /api/v1/tracking/geofences`) | Fully Implemented |
| **GpsTrackingService** | `ListGeofences` | `protos/gps_tracking.proto` | `src/dotnet/GpsTracking/GrpcServices/GpsTrackingGrpcService.cs` | Unary | Isolated (`ICurrentUserService`) | Authenticated User | Staff BFF (`GET /api/v1/tracking/geofences`) | Fully Implemented |
| **GpsTrackingService** | `SetGeofenceActive` | `protos/gps_tracking.proto` | `src/dotnet/GpsTracking/GrpcServices/GpsTrackingGrpcService.cs` | Unary | Isolated (`ICurrentUserService`) | Authenticated User | Staff BFF (`PATCH /api/v1/tracking/geofences/{id}/active`) | Fully Implemented |
| **GpsTrackingService** | `ListMonitoringAlerts` | `protos/gps_tracking.proto` | `src/dotnet/GpsTracking/GrpcServices/GpsTrackingGrpcService.cs` | Unary | Isolated (`ICurrentUserService`) | Authenticated User | Staff BFF (`GET /api/v1/tracking/alerts`) | Fully Implemented |
| **GpsTrackingService** | `ResolveMonitoringAlert` | `protos/gps_tracking.proto` | `src/dotnet/GpsTracking/GrpcServices/GpsTrackingGrpcService.cs` | Unary | Isolated (`ICurrentUserService`) | Authenticated User | Staff BFF (`POST /api/v1/tracking/alerts/{id}/resolve`) | Fully Implemented |
| **DocumentOcrService** | `SubmitDocumentJob` | `protos/document_ocr.proto` | `src/dotnet/DocumentOcr/GrpcServices/DocumentOcrGrpcService.cs` | Unary | Isolated (`ICurrentUserService`) | Authenticated User | Staff BFF (`POST /api/v1/documents/submit`) | Fully Implemented |
| **DocumentOcrService** | `SubmitOcrJob` | `protos/document_ocr.proto` | `src/dotnet/DocumentOcr/GrpcServices/DocumentOcrGrpcService.cs` | Unary | Isolated (`ICurrentUserService`) | Authenticated User | Staff BFF (`POST /api/v1/documents/ocr`) | Fully Implemented |
| **DocumentOcrService** | `GetDocumentJob` | `protos/document_ocr.proto` | `src/dotnet/DocumentOcr/GrpcServices/DocumentOcrGrpcService.cs` | Unary | Isolated (`ICurrentUserService`) | Authenticated User | Staff BFF (`GET /api/v1/documents/jobs/{id}`) | Fully Implemented |
| **DocumentOcrService** | `ListDocumentJobs` | `protos/document_ocr.proto` | `src/dotnet/DocumentOcr/GrpcServices/DocumentOcrGrpcService.cs` | Unary | Isolated (`ICurrentUserService`) | Authenticated User | Staff BFF (`GET /api/v1/documents/jobs`) | Fully Implemented |
| **DocumentOcrService** | `CancelDocumentJob` | `protos/document_ocr.proto` | `src/dotnet/DocumentOcr/GrpcServices/DocumentOcrGrpcService.cs` | Unary | Isolated (`ICurrentUserService`) | Authenticated User | Staff BFF (`POST /api/v1/documents/jobs/{id}/cancel`) | Fully Implemented |
| **DocumentOcrService** | `RetryDocumentJob` | `protos/document_ocr.proto` | `src/dotnet/DocumentOcr/GrpcServices/DocumentOcrGrpcService.cs` | Unary | Isolated (`ICurrentUserService`) | Authenticated User | Staff BFF (`POST /api/v1/documents/jobs/{id}/retry`) | Fully Implemented |
| **DocumentOcrService** | `ReviewDocumentJob` | `protos/document_ocr.proto` | `src/dotnet/DocumentOcr/GrpcServices/DocumentOcrGrpcService.cs` | Unary | Isolated (`ICurrentUserService`) | Authenticated User | Staff BFF (`POST /api/v1/documents/jobs/{id}/review`) | Fully Implemented |
| **RegulatoryComplianceService** | `EvaluateCompliance` | `protos/regulatory_compliance.proto` | `src/dotnet/RegulatoryCompliance/GrpcServices/RegulatoryComplianceGrpcService.cs` | Unary | Isolated (`ICurrentUserService`) | Authenticated User / S2S | Staff BFF / RoutePlanningAgent | Fully Implemented |
| **RegulatoryComplianceService** | `GetComplianceEvaluation` | `protos/regulatory_compliance.proto` | `src/dotnet/RegulatoryCompliance/GrpcServices/RegulatoryComplianceGrpcService.cs` | Unary | Isolated (`ICurrentUserService`) | Authenticated User | Staff BFF (`GET /api/v1/compliance/evaluations/{id}`) | Fully Implemented |
| **RegulatoryComplianceService** | `QueryRegulations` | `protos/regulatory_compliance.proto` | `src/dotnet/RegulatoryCompliance/GrpcServices/RegulatoryComplianceGrpcService.cs` | Unary | Isolated + Platform Global | Authenticated User | Staff BFF (`POST /api/v1/compliance/regulations/query`) | Fully Implemented |
| **RegulatoryComplianceService** | `IngestRegulatorySource` | `protos/regulatory_compliance.proto` | `src/dotnet/RegulatoryCompliance/GrpcServices/RegulatoryComplianceGrpcService.cs` | Unary | Platform (Admin/System) / Tenant | Admin / System Role | Admin BFF / System BFF (`POST /api/v1/compliance/sources`) | Fully Implemented |
| **RegulatoryComplianceService** | `IngestKnowledgeDocument` | `protos/regulatory_compliance.proto` | `src/dotnet/RegulatoryCompliance/GrpcServices/RegulatoryComplianceGrpcService.cs` | Unary | Isolated + Platform Global | Admin / System Role | Admin BFF / System BFF (`POST /api/v1/compliance/knowledge`) | Fully Implemented |
| **RegulatoryComplianceService** | `QueryKnowledge` | `protos/regulatory_compliance.proto` | `src/dotnet/RegulatoryCompliance/GrpcServices/RegulatoryComplianceGrpcService.cs` | Unary | Isolated + Platform Global | Authenticated User | Staff BFF (`POST /api/v1/compliance/knowledge/query`) | Fully Implemented |
| **RegulatoryComplianceService** | `GenerateGroundedAnswer` | `protos/regulatory_compliance.proto` | `src/dotnet/RegulatoryCompliance/GrpcServices/RegulatoryComplianceGrpcService.cs` | Unary | Isolated + Platform Global | Authenticated User | Staff BFF / CustomerAssistant | Fully Implemented |
| **RegulatoryComplianceService** | `ValidateGroundedEvidence` | `protos/regulatory_compliance.proto` | `src/dotnet/RegulatoryCompliance/GrpcServices/RegulatoryComplianceGrpcService.cs` | Unary | Stateless Validation | Authenticated S2S | Internal Agent Pipeline | Fully Implemented |
| **ComplianceRag** | `CheckRouteCompliance` | `protos/compliance_rag.proto` | *None* (`RoutePlanningAgent/Infrastructure/Services/ComplianceRagClient.cs` caller) | Unary | Tenant Scope | S2S | Internal Agent Pipeline | **Proto Only / Stub Mismatch** |
| **MailManagement** | `ProvisionDomain` | `protos/mail_platform.proto` | `src/dotnet/MailService/GrpcServices/MailManagementService.cs` | Unary | Isolated (`ICurrentUserService`) | Tenant Admin / System | Admin BFF (`POST /api/v1/admin/mail/domains`) | Fully Implemented |
| **MailManagement** | `CreateMailbox` | `protos/mail_platform.proto` | `src/dotnet/MailService/GrpcServices/MailManagementService.cs` | Unary | Isolated (`ICurrentUserService`) | Tenant Admin / System | Admin BFF (`POST /api/v1/admin/mail/mailboxes`) | Fully Implemented |
| **MailManagement** | `CreateAlias` | `protos/mail_platform.proto` | `src/dotnet/MailService/GrpcServices/MailManagementService.cs` | Unary | Isolated (`ICurrentUserService`) | Tenant Admin / System | Admin BFF (`POST /api/v1/admin/mail/aliases`) | Fully Implemented |
| **MailManagement** | `ResetPassword` | `protos/mail_platform.proto` | `src/dotnet/MailService/GrpcServices/MailManagementService.cs` | Unary | Isolated (`ICurrentUserService`) | Tenant Admin / System | Admin BFF (`POST /api/v1/admin/mail/mailboxes/{id}/reset-password`) | Implemented (Delegated) |
| **MailManagement** | `GetAuditRecords` | `protos/mail_platform.proto` | `src/dotnet/MailService/GrpcServices/MailManagementService.cs` | Unary | Isolated (`ICurrentUserService`) | Tenant Admin / System | Admin BFF / System BFF | Fully Implemented |
| **MailManagement** | `RequeueDeadLetter` | `protos/mail_platform.proto` | `src/dotnet/MailService/GrpcServices/MailManagementService.cs` | Unary | Platform Admin | System Admin | System BFF (`POST /api/v1/system/mail/dead-letters/{id}/requeue`) | Fully Implemented |
| **MailSecurity** | `CreateDraftMessage` | `protos/mail_platform.proto` | `src/dotnet/MailService/GrpcServices/MailSecurityService.cs` | Unary | Isolated (`ICurrentUserService`) | Staff / Manager | Staff BFF (`POST /api/v1/mail/drafts`) | Fully Implemented |
| **MailSecurity** | `GetDraft` | `protos/mail_platform.proto` | `src/dotnet/MailService/GrpcServices/MailSecurityService.cs` | Unary | Isolated (`ICurrentUserService`) | Staff / Manager | Staff BFF (`GET /api/v1/mail/drafts/{id}`) | Fully Implemented |
| **MailSecurity** | `ListDrafts` | `protos/mail_platform.proto` | `src/dotnet/MailService/GrpcServices/MailSecurityService.cs` | Unary | Isolated (`ICurrentUserService`) | Staff / Manager | Staff BFF (`GET /api/v1/mail/drafts`) | Fully Implemented |
| **MailSecurity** | `SubmitOutboundMessage` | `protos/mail_platform.proto` | `src/dotnet/MailService/GrpcServices/MailSecurityService.cs` | Unary | Isolated (`ICurrentUserService`) | Staff / Manager | Staff BFF (`POST /api/v1/mail/send`) | Fully Implemented |
| **MailSecurity** | `GetProcessedMessage` | `protos/mail_platform.proto` | `src/dotnet/MailService/GrpcServices/MailSecurityService.cs` | Unary | Isolated (`ICurrentUserService`) | Staff / Manager | Staff BFF (`GET /api/v1/mail/messages/{id}`) | Fully Implemented |
| **MailSecurity** | `ListProcessedMessages` | `protos/mail_platform.proto` | `src/dotnet/MailService/GrpcServices/MailSecurityService.cs` | Unary | Isolated (`ICurrentUserService`) | Staff / Manager | Staff BFF (`GET /api/v1/mail/messages`) | Fully Implemented |
| **MailSecurity** | `GetQuarantineRecord` | `protos/mail_platform.proto` | `src/dotnet/MailService/GrpcServices/MailSecurityService.cs` | Unary | Isolated (`ICurrentUserService`) | Admin / Security Reviewer | Staff BFF / Admin BFF | Fully Implemented |
| **MailSecurity** | `ListQuarantineRecords` | `protos/mail_platform.proto` | `src/dotnet/MailService/GrpcServices/MailSecurityService.cs` | Unary | Isolated (`ICurrentUserService`) | Admin / Security Reviewer | Staff BFF / Admin BFF | Fully Implemented |
| **MailSecurity** | `ReleaseQuarantine` | `protos/mail_platform.proto` | `src/dotnet/MailService/GrpcServices/MailSecurityService.cs` | Unary | Isolated (`ICurrentUserService`) | Admin / Security Reviewer | Staff BFF (`POST /api/v1/mail/quarantine/{id}/release`) | Fully Implemented |
| **MailSecurity** | `DeleteQuarantine` | `protos/mail_platform.proto` | `src/dotnet/MailService/GrpcServices/MailSecurityService.cs` | Unary | Isolated (`ICurrentUserService`) | Admin / Security Reviewer | Admin BFF (`DELETE /api/v1/admin/mail/quarantine/{id}`) | Fully Implemented |
| **IamService** | `CreateTenant` | `protos/iam_tenant.proto` | `src/dotnet/IamTenant/GrpcServices/IamGrpcService.cs` | Unary | System Multi-tenant | System Admin | System BFF (`POST /api/v1/system/tenants`) | Fully Implemented |
| **IamService** | `GetTenant` | `protos/iam_tenant.proto` | `src/dotnet/IamTenant/GrpcServices/IamGrpcService.cs` | Unary | System Multi-tenant | System Admin | System BFF (`GET /api/v1/system/tenants/{id}`) | Fully Implemented |
| **IamService** | `UpdateTenantStatus` | `protos/iam_tenant.proto` | `src/dotnet/IamTenant/GrpcServices/IamGrpcService.cs` | Unary | System Multi-tenant | System Admin | System BFF (`PATCH /api/v1/system/tenants/{id}/status`) | Fully Implemented |
| **IamService** | `ListTenants` | `protos/iam_tenant.proto` | `src/dotnet/IamTenant/GrpcServices/IamGrpcService.cs` | Unary | System Multi-tenant | System Admin | System BFF (`GET /api/v1/system/tenants`) | Fully Implemented |
| **IamService** | `DeleteTenant` | `protos/iam_tenant.proto` | `src/dotnet/IamTenant/GrpcServices/IamGrpcService.cs` | Unary | System Multi-tenant | System Admin | System BFF (`DELETE /api/v1/system/tenants/{id}`) | Fully Implemented |
| **IamService** | `InviteUser` | `protos/iam_tenant.proto` | `src/dotnet/IamTenant/GrpcServices/IamGrpcService.cs` | Unary | Isolated (`ICurrentUserService`) | Tenant Admin | Admin BFF (`POST /api/v1/admin/staff/invite`) | Fully Implemented |
| **IamService** | `GetUser` | `protos/iam_tenant.proto` | `src/dotnet/IamTenant/GrpcServices/IamGrpcService.cs` | Unary | Isolated (`ICurrentUserService`) | Authenticated User | Staff BFF / Admin BFF | Fully Implemented |
| **IamService** | `GetManyUsers` | `protos/iam_tenant.proto` | `src/dotnet/IamTenant/GrpcServices/IamGrpcService.cs` | Unary | Isolated (`ICurrentUserService`) | Tenant Admin | Admin BFF (`GET /api/v1/admin/staff`) | Fully Implemented |
| **IamService** | `UpdateUser` | `protos/iam_tenant.proto` | `src/dotnet/IamTenant/GrpcServices/IamGrpcService.cs` | Unary | Isolated (`ICurrentUserService`) | Tenant Admin | Admin BFF (`PUT /api/v1/admin/staff/{id}`) | Fully Implemented |
| **IamService** | `ActivateUser` | `protos/iam_tenant.proto` | `src/dotnet/IamTenant/GrpcServices/IamGrpcService.cs` | Unary | Isolated (`ICurrentUserService`) | Tenant Admin | Admin BFF (`POST /api/v1/admin/staff/{id}/activate`) | Fully Implemented |
| **IamService** | `ResetUserPassword` | `protos/iam_tenant.proto` | `src/dotnet/IamTenant/GrpcServices/IamGrpcService.cs` | Unary | Isolated (`ICurrentUserService`) | Tenant Admin | Admin BFF (`POST /api/v1/admin/staff/{id}/reset-password`) | Fully Implemented |
| **IamService** | `AssignRoles` | `protos/iam_tenant.proto` | `src/dotnet/IamTenant/GrpcServices/IamGrpcService.cs` | Unary | Isolated (`ICurrentUserService`) | Tenant Admin | Admin BFF (`POST /api/v1/admin/staff/{id}/roles`) | Fully Implemented |
| **IamService** | `SuspendUser` | `protos/iam_tenant.proto` | `src/dotnet/IamTenant/GrpcServices/IamGrpcService.cs` | Unary | Isolated (`ICurrentUserService`) | Tenant Admin | Admin BFF (`POST /api/v1/admin/staff/{id}/suspend`) | Fully Implemented |
| **IamService** | `CreateCustomRole` | `protos/iam_tenant.proto` | `src/dotnet/IamTenant/GrpcServices/IamGrpcService.cs` | Unary | Isolated | Tenant Admin | N/A (Read-Only Roles) | **Disabled (Unimplemented)** |
| **IamService** | `GetRole` | `protos/iam_tenant.proto` | `src/dotnet/IamTenant/GrpcServices/IamGrpcService.cs` | Unary | Global System / Tenant | Tenant Admin | Admin BFF (`GET /api/v1/admin/roles/{id}`) | Fully Implemented |
| **IamService** | `GetManyRoles` | `protos/iam_tenant.proto` | `src/dotnet/IamTenant/GrpcServices/IamGrpcService.cs` | Unary | Global System / Tenant | Tenant Admin | Admin BFF (`GET /api/v1/admin/roles`) | Fully Implemented |
| **IamService** | `UpdateRole` | `protos/iam_tenant.proto` | `src/dotnet/IamTenant/GrpcServices/IamGrpcService.cs` | Unary | Isolated | Tenant Admin | N/A (Read-Only Roles) | **Disabled (Unimplemented)** |
| **IamService** | `DeleteRole` | `protos/iam_tenant.proto` | `src/dotnet/IamTenant/GrpcServices/IamGrpcService.cs` | Unary | Isolated | Tenant Admin | N/A (Read-Only Roles) | **Disabled (Unimplemented)** |
| **IamService** | `AssignPermissionsToRole`| `protos/iam_tenant.proto` | `src/dotnet/IamTenant/GrpcServices/IamGrpcService.cs` | Unary | Isolated (`ICurrentUserService`) | Tenant Admin | Admin BFF (`POST /api/v1/admin/roles/{id}/permissions`) | Fully Implemented |
| **IamService** | `GetUserPermissions` | `protos/iam_tenant.proto` | `src/dotnet/IamTenant/GrpcServices/IamGrpcService.cs` | Unary | Isolated (`ICurrentUserService`) | Authenticated User | Staff BFF / Internal Interceptor | Fully Implemented |
| **AuthService** | `IdentifyUser` | `protos/auth.proto` | `src/dotnet/IamTenant/GrpcServices/AuthGrpcService.cs` | Unary | Pre-Auth (Global Email Lookup) | Public / Pre-Auth | Staff BFF (`POST /api/v1/auth/identify`) | Fully Implemented |
| **AuthService** | `Login` | `protos/auth.proto` | `src/dotnet/IamTenant/GrpcServices/AuthGrpcService.cs` | Unary | Pre-Auth (Tenant + Cognito Auth) | Public / Pre-Auth | Staff BFF (`POST /api/v1/auth/login`) | Fully Implemented |
| **AuthService** | `CompleteInvitation` | `protos/auth.proto` | `src/dotnet/IamTenant/GrpcServices/AuthGrpcService.cs` | Unary | Pre-Auth (Cognito First Login) | Public / Pre-Auth | Staff BFF (`POST /api/v1/auth/complete-invitation`) | Fully Implemented |
| **AuthService** | `RefreshToken` | `protos/auth.proto` | `src/dotnet/IamTenant/GrpcServices/AuthGrpcService.cs` | Unary | Pre-Auth / Cognito Client Resolve | Public / Token Holder | Staff BFF (`POST /api/v1/auth/refresh`) | Fully Implemented |
| **AuthService** | `Logout` | `protos/auth.proto` | `src/dotnet/IamTenant/GrpcServices/AuthGrpcService.cs` | Unary | Authenticated User | Authenticated User | Staff BFF (`POST /api/v1/auth/logout`) | Fully Implemented |
| **AuthService** | `ForgotPassword` | `protos/auth.proto` | `src/dotnet/IamTenant/GrpcServices/AuthGrpcService.cs` | Unary | Pre-Auth (Global Cognito Dispatch) | Public / Pre-Auth | Staff BFF (`POST /api/v1/auth/forgot-password`) | Fully Implemented |
| **AuthService** | `ConfirmForgotPassword` | `protos/auth.proto` | `src/dotnet/IamTenant/GrpcServices/AuthGrpcService.cs` | Unary | Pre-Auth (Cognito Reset Code) | Public / Pre-Auth | Staff BFF (`POST /api/v1/auth/confirm-forgot-password`) | Fully Implemented |
| **AuthService** | `ValidateToken` | `protos/auth.proto` | *None* (`AuthGrpcService.cs` omits override) | Unary | Stateless Token Validation | Internal S2S | Internal Gateway / BFF | **Proto Only (Omitted)** |
| **RoutePlanningService** | `CreateRoute` | `protos/route-planning-agent.proto` | `src/dotnet/RoutePlanningAgent/GrpcServices/RoutePlanningGrpcService.cs` | Unary | Isolated (`ICurrentUserService`) | Staff / Dispatcher | Staff BFF (`POST /api/v1/routes`) | Fully Implemented |
| **RoutePlanningService** | `GetRoute` | `protos/route-planning-agent.proto` | `src/dotnet/RoutePlanningAgent/GrpcServices/RoutePlanningGrpcService.cs` | Unary | Isolated (`ICurrentUserService`) | Staff / Dispatcher | Staff BFF (`GET /api/v1/routes/{id}`) | Fully Implemented |
| **RoutePlanningService** | `ListRoutes` | `protos/route-planning-agent.proto` | `src/dotnet/RoutePlanningAgent/GrpcServices/RoutePlanningGrpcService.cs` | Unary | Isolated (`ICurrentUserService`) | Staff / Dispatcher | Staff BFF (`GET /api/v1/routes`) | Fully Implemented |
| **RoutePlanningService** | `UpdateRoute` | `protos/route-planning-agent.proto` | `src/dotnet/RoutePlanningAgent/GrpcServices/RoutePlanningGrpcService.cs` | Unary | Isolated (`ICurrentUserService`) | Staff / Dispatcher | Staff BFF (`PUT /api/v1/routes/{id}`) | Fully Implemented |
| **RoutePlanningService** | `DeleteRoute` | `protos/route-planning-agent.proto` | `src/dotnet/RoutePlanningAgent/GrpcServices/RoutePlanningGrpcService.cs` | Unary | Isolated (`ICurrentUserService`) | Staff / Dispatcher | Staff BFF (`DELETE /api/v1/routes/{id}`) | Fully Implemented |
| **RoutePlanningService** | `UpdateRouteStatus` | `protos/route-planning-agent.proto` | `src/dotnet/RoutePlanningAgent/GrpcServices/RoutePlanningGrpcService.cs` | Unary | Isolated (`ICurrentUserService`) | Staff / Dispatcher | Staff BFF (`PATCH /api/v1/routes/{id}/status`) | Fully Implemented |
| **RoutePlanningService** | `OptimizeRoute` | `protos/route-planning-agent.proto` | `src/dotnet/RoutePlanningAgent/GrpcServices/RoutePlanningGrpcService.cs` | Unary | Isolated (`ICurrentUserService`) | Staff / Dispatcher | Staff BFF (`POST /api/v1/routes/{id}/optimize`) | Fully Implemented |
| **RoutePlanningService** | `ApproveRoute` | `protos/route-planning-agent.proto` | `src/dotnet/RoutePlanningAgent/GrpcServices/RoutePlanningGrpcService.cs` | Unary | Isolated (`ICurrentUserService`) | Manager / Approver | Staff BFF (`POST /api/v1/approvals/{id}/approve`) | Fully Implemented |
| **RoutePlanningService** | `RejectRoute` | `protos/route-planning-agent.proto` | `src/dotnet/RoutePlanningAgent/GrpcServices/RoutePlanningGrpcService.cs` | Unary | Isolated (`ICurrentUserService`) | Manager / Approver | Staff BFF (`POST /api/v1/approvals/{id}/reject`) | Fully Implemented |
| **RoutePlanningService** | `ListPendingApprovals` | `protos/route-planning-agent.proto` | `src/dotnet/RoutePlanningAgent/GrpcServices/RoutePlanningGrpcService.cs` | Unary | Isolated (`ICurrentUserService`) | Manager / Approver | Staff BFF (`GET /api/v1/approvals/pending`) | Fully Implemented |
| **RoutePlanningService** | `GetRouteRecommendation` | `protos/route-planning-agent.proto` | `src/dotnet/RoutePlanningAgent/GrpcServices/RoutePlanningGrpcService.cs` | Unary | Isolated (`ICurrentUserService`) | Staff / Dispatcher | Staff BFF (`POST /api/v1/routes/{id}/recommendation`) | Fully Implemented |
| **RoutePlanningService** | `GetTenantAiConfig` | `protos/route-planning-agent.proto` | `src/dotnet/RoutePlanningAgent/GrpcServices/RoutePlanningGrpcService.cs` | Unary | Isolated (`ICurrentUserService`) | Tenant Admin | Admin BFF (`GET /api/v1/admin/ai-config`) | Fully Implemented |
| **RoutePlanningService** | `UpsertTenantAiConfig` | `protos/route-planning-agent.proto` | `src/dotnet/RoutePlanningAgent/GrpcServices/RoutePlanningGrpcService.cs` | Unary | Isolated (`ICurrentUserService`) | Tenant Admin | Admin BFF (`PUT /api/v1/admin/ai-config`) | Fully Implemented |
| **RoutePlanningService** | `UpsertTenantRuleConfig` | `protos/route-planning-agent.proto` | `src/dotnet/RoutePlanningAgent/GrpcServices/RoutePlanningGrpcService.cs` | Unary | Isolated (`ICurrentUserService`) | Tenant Admin | Admin BFF (`PUT /api/v1/admin/rules`) | Fully Implemented |
| **RoutePlanningService** | `ListTenantRuleConfigs` | `protos/route-planning-agent.proto` | `src/dotnet/RoutePlanningAgent/GrpcServices/RoutePlanningGrpcService.cs` | Unary | Isolated (`ICurrentUserService`) | Tenant Admin | Admin BFF (`GET /api/v1/admin/rules`) | Fully Implemented |
| **AiGovernanceService** | `ExecutePolicy` | `protos/ai_governance.proto` | `src/java/ai-governance/.../PolicyGrpcHandler.java` | Unary | Tenant (`CurrentUserContext`) | Authenticated Service (`x-service-id`) | Internal S2S Pre-flight Check | Fully Implemented |
| **AiExecutionService** | `Generate` | `protos/ai_governance.proto` | `src/java/ai-governance/.../AiExecutionGrpcHandler.java` | Unary | Tenant (`CurrentUserContext`) | Authenticated Service (`x-service-id`) | Internal AI Gateway (RoutePlanning, Compliance, OCR) | Fully Implemented |
| **AiExecutionService** | `Embed` | `protos/ai_governance.proto` | `src/java/ai-governance/.../AiExecutionGrpcHandler.java` | Unary | Tenant (`CurrentUserContext`) | Authenticated Service (`x-service-id`) | Internal AI Gateway (RAG Ingestion, Retrieval) | Fully Implemented |
| **AiGovernanceAdminService** | *3 methods* | `protos/ai_governance_admin.proto` | *None* (Commented out in protobuf) | Unary | Platform Scope | Platform Admin | Admin BFF | **Proto Draft Only** |
| **DevOpsIngestionService** | `IngestAlert` | `protos/devops_agent.proto` | `src/java/devops-agent/.../IngestionGrpcHandler.java` | Unary | Platform / Infrastructure | S2S / Monitoring Webhook | Internal SRE Pipeline | Fully Implemented |
| **DevOpsIncidentService** | `ListIncidents` | `protos/devops_agent.proto` | `src/java/devops-agent/.../IncidentGrpcHandler.java` | Unary | Platform / Infrastructure | SRE / DevOps Engineer | Admin BFF / SRE Portal | Fully Implemented |
| **DevOpsIncidentService** | `GetIncident` | `protos/devops_agent.proto` | `src/java/devops-agent/.../IncidentGrpcHandler.java` | Unary | Platform / Infrastructure | SRE / DevOps Engineer | Admin BFF / SRE Portal | Fully Implemented |
| **DevOpsIncidentService** | `ApproveIncident` | `protos/devops_agent.proto` | `src/java/devops-agent/.../IncidentGrpcHandler.java` | Unary | Platform / Infrastructure | Lead SRE / Manager | Admin BFF / SRE Portal | Fully Implemented |
| **DevOpsIncidentService** | `RejectIncident` | `protos/devops_agent.proto` | `src/java/devops-agent/.../IncidentGrpcHandler.java` | Unary | Platform / Infrastructure | Lead SRE / Manager | Admin BFF / SRE Portal | Fully Implemented |
| **DevOpsRuleService** | `ListExistingRules` | `protos/devops_agent.proto` | `src/java/devops-agent/.../RuleGrpcHandler.java` | Unary | Platform / Infrastructure | DevOps Engineer | Admin BFF / SRE Portal | Fully Implemented |
| **DevOpsRuleService** | `CreateRule` | `protos/devops_agent.proto` | `src/java/devops-agent/.../RuleGrpcHandler.java` | Unary | Platform / Infrastructure | DevOps Engineer | Admin BFF / SRE Portal | Fully Implemented |
| **DevOpsRuleService** | `UpdateRule` | `protos/devops_agent.proto` | `src/java/devops-agent/.../RuleGrpcHandler.java` | Unary | Platform / Infrastructure | DevOps Engineer | Admin BFF / SRE Portal | Fully Implemented |
| **DevOpsRuleService** | `DeleteRule` | `protos/devops_agent.proto` | `src/java/devops-agent/.../RuleGrpcHandler.java` | Unary | Platform / Infrastructure | DevOps Engineer | Admin BFF / SRE Portal | Fully Implemented |
| **DevOpsRuleService** | `ListPendingRules` | `protos/devops_agent.proto` | `src/java/devops-agent/.../RuleGrpcHandler.java` | Unary | Platform / Infrastructure | DevOps Engineer | Admin BFF / SRE Portal | Fully Implemented |
| **DevOpsRuleService** | `ApprovePendingRule` | `protos/devops_agent.proto` | `src/java/devops-agent/.../RuleGrpcHandler.java` | Unary | Platform / Infrastructure | Lead SRE | Admin BFF / SRE Portal | Fully Implemented |
| **DevOpsRuleService** | `RejectPendingRule` | `protos/devops_agent.proto` | `src/java/devops-agent/.../RuleGrpcHandler.java` | Unary | Platform / Infrastructure | Lead SRE | Admin BFF / SRE Portal | Fully Implemented |
| **DevOpsConfigService** | `GetSelfConfig` | `protos/devops_agent.proto` | `src/java/devops-agent/.../SelfConfigGrpcHandler.java` | Unary | Platform / Infrastructure | Lead SRE | Admin BFF / SRE Portal | Fully Implemented |
| **DevOpsConfigService** | `UpdateSelfConfig` | `protos/devops_agent.proto` | `src/java/devops-agent/.../SelfConfigGrpcHandler.java` | Unary | Platform / Infrastructure | Lead SRE | Admin BFF / SRE Portal | Fully Implemented |
| **DevOpsRagService** | `QueryKnowledge` | `protos/devops_rag.proto` | *None* (`devops-agent/.../DevOpsRagClient.java` caller) | Unary | Platform SRE | S2S (`x-service-id`) | Internal DevOps Pipeline | **Proto Only / External** |
| **DevOpsRagService** | `IngestKnowledge` | `protos/devops_rag.proto` | *None* | Unary | Platform SRE | S2S | Internal DevOps Pipeline | **Proto Only / Unimplemented** |
| **BillingService** | `GenerateInvoice` | `protos/billing.proto` | `src/nestjs/billing-service/.../billing.controller.ts` | Unary | Isolated (`TenantInterceptor`) | Staff / S2S | Staff BFF (`POST /api/v1/invoices/generate`) | Fully Implemented |
| **BillingService** | `GetInvoiceDetail` | `protos/billing.proto` | `src/nestjs/billing-service/.../billing.controller.ts` | Unary | Isolated (`TenantInterceptor`) | Staff / Finance | Staff BFF (`GET /api/v1/invoices/{id}/detail`) | Fully Implemented |
| **BillingService** | `CheckCustomerCredit` | `protos/billing.proto` | `src/nestjs/billing-service/.../billing.controller.ts` | Unary | Isolated (`TenantInterceptor`) | Staff / S2S | Staff BFF (`POST /api/v1/billing/credit-check`) | Fully Implemented |
| **BillingService** | `CreateInvoice` | `protos/billing.proto` | `src/nestjs/billing-service/.../billing.controller.ts` | Unary | Isolated (`TenantInterceptor`) | Staff / Finance | Staff BFF (`POST /api/v1/invoices`) | Fully Implemented |
| **BillingService** | `GetInvoice` | `protos/billing.proto` | `src/nestjs/billing-service/.../billing.controller.ts` | Unary | Isolated (`TenantInterceptor`) | Staff / Finance | Staff BFF (`GET /api/v1/invoices/{id}`) | Fully Implemented |
| **BillingService** | `ListInvoices` | `protos/billing.proto` | `src/nestjs/billing-service/.../billing.controller.ts` | Unary | Isolated (`TenantInterceptor`) | Staff / Finance | Staff BFF (`GET /api/v1/invoices`) | Fully Implemented |
| **BillingService** | `UpdateInvoiceStatus` | `protos/billing.proto` | `src/nestjs/billing-service/.../billing.controller.ts` | Unary | Isolated (`TenantInterceptor`) | Staff / Finance | Staff BFF (`PATCH /api/v1/invoices/{id}/status`) | Fully Implemented |
| **BillingService** | `CreateEscrowWallet` | `protos/billing.proto` | `src/nestjs/billing-service/.../billing.controller.ts` | Unary | Isolated (`TenantInterceptor`) | Tenant Admin / Carrier | Staff BFF (`POST /api/v1/escrow/wallets`) | Fully Implemented |
| **BillingService** | `GetWalletBalance` | `protos/billing.proto` | `src/nestjs/billing-service/.../billing.controller.ts` | Unary | Isolated (`TenantInterceptor`) | Tenant Staff / Carrier | Staff BFF (`GET /api/v1/escrow/wallets/{id}`) | Fully Implemented |
| **BillingService** | `FreezeEscrowAmount` | `protos/billing.proto` | `src/nestjs/billing-service/.../billing.controller.ts` | Unary | Isolated (`TenantInterceptor`) | Staff / S2S | Staff BFF (`POST /api/v1/escrow/freeze`) | Fully Implemented |
| **BillingService** | `ReleaseEscrowAmount`| `protos/billing.proto` | `src/nestjs/billing-service/.../billing.controller.ts` | Unary | Isolated (`TenantInterceptor`) | Staff / S2S | Staff BFF (`POST /api/v1/escrow/release`) | Fully Implemented |
| **BillingService** | `RefundEscrowAmount` | `protos/billing.proto` | `src/nestjs/billing-service/.../billing.controller.ts` | Unary | Isolated (`TenantInterceptor`) | Staff / S2S | Staff BFF (`POST /api/v1/escrow/refund`) | Fully Implemented |
| **BillingService** | *RecordPayment* | *Missing in proto* | `src/nestjs/billing-service/.../billing.controller.ts` | Unary | Isolated (`TenantInterceptor`) | Staff / Finance | Staff BFF (`POST /api/v1/invoices/payments`) | **Code Only (No Proto Contract)** |
| **BillingService** | *CancelInvoice* | *Missing in proto* | `src/nestjs/billing-service/.../billing.controller.ts` | Unary | Isolated (`TenantInterceptor`) | Staff / Finance | Staff BFF (`POST /api/v1/invoices/{id}/cancel`) | **Code Only (No Proto Contract)** |
| **BillingService** | *IssueDebitNote* | *Missing in proto* | `src/nestjs/billing-service/.../billing.controller.ts` | Unary | Isolated (`TenantInterceptor`) | Staff / Finance | Staff BFF (`POST /api/v1/invoices/debit-notes`) | **Code Only (No Proto Contract)** |
| **BillingService** | *IssueCreditNote* | *Missing in proto* | `src/nestjs/billing-service/.../billing.controller.ts` | Unary | Isolated (`TenantInterceptor`) | Staff / Finance | Staff BFF (`POST /api/v1/invoices/credit-notes`) | **Code Only (No Proto Contract)** |
| **FinancialService** | `EstimateCost` | `protos/financial.proto` | `src/nestjs/financial-service/.../financial.controller.ts` | Unary | Isolated (`TenantInterceptor`) | Staff / S2S (Billing) | Staff BFF (`POST /api/v1/financial/estimate-cost`) | Fully Implemented |
| **FinancialService** | `GetCustomsDuty` | `protos/financial.proto` | `src/nestjs/financial-service/.../financial.controller.ts` | Unary | Isolated (`TenantInterceptor`) | Staff / S2S | Staff BFF (`POST /api/v1/financial/customs-duty`) | Fully Implemented |
| **FinancialService** | `GetMinAcceptableRate` | `protos/financial.proto` | `src/nestjs/financial-service/.../financial.controller.ts` | Unary | Isolated (`TenantInterceptor`) | Staff / S2S | Staff BFF (`POST /api/v1/financial/min-acceptable-rate`) | Fully Implemented |
| **FinancialService** | *GetDynamicMargin* | *Missing in proto* | `src/nestjs/financial-service/.../financial.controller.ts` | Unary | Isolated (`TenantInterceptor`) | Staff / S2S | Staff BFF (`POST /api/v1/financial/dynamic-margin`) | **Code Only (No Proto Contract)** |
| **FinancialService** | *GetExchangeRate* | *Missing in proto* | `src/nestjs/financial-service/.../financial.controller.ts` | Unary | Isolated (`TenantInterceptor`) | Staff / S2S | Staff BFF (`POST /api/v1/financial/exchange-rate`) | **Code Only (No Proto Contract)** |
| **NegotiationService** | *SubmitOffer* | *Missing in proto* | `src/nestjs/negotiation-agent-service/.../negotiation.controller.ts` | Unary | Tenant Scope | Authenticated User | Staff BFF (`POST /api/v1/negotiation/offer`) | **Code Only (No Proto Contract)** |
| **NegotiationService** | *GetSessionHistory* | *Missing in proto* | `src/nestjs/negotiation-agent-service/.../negotiation.controller.ts` | Unary | Tenant Scope | Authenticated User | Staff BFF (`GET /api/v1/negotiation/session/{id}`) | **Code Only (No Proto Contract)** |

---

## 2. Detailed gRPC Capability Analysis per Service

### 2.1. ShipmentWorkflow Service
- **Service Name:** `ShipmentWorkflowService`
- **Proto File:** `protos/shipment_workflow.proto`
- **Fully Qualified Package:** `shipment_workflow.ShipmentWorkflowService`
- **Implementation File:** `src/dotnet/ShipmentWorkflow/GrpcServices/ShipmentGrpcService.cs`
- **Security & Tenancy:** Intercepted by `Shared.Interceptors.AuthInterceptor`. Global EF Core query filters enforce `TenantId == ICurrentUserService.TenantId`.

#### RPCs & Wire Details:
1. **`CreateShipment`**
   - **Method:** `ShipmentGrpcService.CreateShipment(CreateShipmentRequest, ServerCallContext)`
   - **Request:** `CreateShipmentRequest` (Fields: `origin`, `destination`, `estimated_delivery`, `cargo_items`, `locations`, `documents`, `notes`, `transport_mode`, `service_level`, `declared_value`, `declared_value_currency`)
   - **Response:** `ShipmentResponse` (`id`, `reference_number`, `status`, `origin`, `destination`, `created_at`, `updated_at`, `cargo_items`, `locations`, `documents`, `milestones`, etc.)
   - **Streaming:** Unary
   - **Dependencies:** MediatR `CreateShipmentCommand`, `ShipmentWorkflowDbContext`, Outbox publisher (`ShipmentCreatedIntegrationEvent`).
   - **Tenant Requirement:** Strict Tenant Isolation (`ICurrentUserService.TenantId` must not be null/empty).
   - **Status:** Fully Implemented.

2. **`GetShipment`** / **`ListShipments`**
   - **Methods:** `GetShipment(GetShipmentRequest)`, `ListShipments(ListShipmentsRequest)`
   - **Request Fields:** `GetShipmentRequest.id` (UUID string); `ListShipmentsRequest` (`page`, `page_size`, `status_filter`, `search_term`, `date_from`, `date_to`).
   - **Response Fields:** `ShipmentResponse`, `ListShipmentsResponse` (`shipments`, `total_count`, `page`, `page_size`, `total_pages`).
   - **Streaming:** Unary
   - **Dependencies:** MediatR `GetShipmentQuery`, `ListShipmentsQuery`.
   - **Status:** Fully Implemented.

3. **`UpdateShipmentStatus`** / **`SubmitShipment`** / **`CancelShipment`** / **`DeleteDraftShipment`**
   - **State Machine Operations:** Enforces valid status transitions (`Draft` -> `Submitted` -> `Confirmed` -> `InTransit` -> `Delivered` / `Cancelled`).
   - **Streaming:** Unary
   - **Dependencies:** MediatR commands, transactional outbox publishing `ShipmentStatusChangedIntegrationEvent`.
   - **Status:** Fully Implemented.

4. **Child Entity Sub-resource RPCs:**
   - Cargo Items: `AddCargoItem`, `UpdateCargoItem`, `RemoveCargoItem`
   - Locations: `AddShipmentLocation`, `UpdateShipmentLocation`, `RemoveShipmentLocation`
   - Documents: `AttachShipmentDocument`, `UpdateShipmentDocumentOcr`, `RemoveShipmentDocument`
   - Milestones: `AddShipmentMilestone`, `GetShipmentTimeline`
   - Bulk Operations: `ImportShipments`
   - **Streaming:** All Unary.
   - **Status:** Fully Implemented.

---

### 2.2. Notification Service
- **Service Name:** `NotificationService`
- **Proto File:** `protos/notification.proto`
- **Fully Qualified Package:** `notification.NotificationService`
- **Implementation File:** `src/dotnet/Notification/GrpcServices/NotificationGrpcService.cs`
- **Security & Tenancy:** Intercepted by `AuthInterceptor`. Enforces `ICurrentUserService.UserId` and `ICurrentUserService.TenantId`.

#### RPCs & Wire Details:
1. **`ListNotifications`**
   - **Method:** `NotificationGrpcService.ListNotifications(ListNotificationsRequest, ServerCallContext)`
   - **Request:** `ListNotificationsRequest` (`page`, `page_size`, `unread_only`)
   - **Response:** `ListNotificationsResponse` (`notifications`, `total_items`, `page`, `page_size`, `total_pages`)
   - **Streaming:** Unary
   - **Dependencies:** `NotificationDbContext.Notifications` filtered by `RecipientUserId == currentUser.UserId`.
   - **Status:** Fully Implemented.

2. **`MarkNotificationRead`**
   - **Method:** `NotificationGrpcService.MarkNotificationRead(MarkNotificationReadRequest, ServerCallContext)`
   - **Request:** `MarkNotificationReadRequest.id` (UUID string)
   - **Response:** `NotificationResponse` (`id`, `shipment_id`, `event_type`, `channel`, `title`, `body`, `is_read`, `created_at`, `read_at`)
   - **Streaming:** Unary
   - **Status:** Fully Implemented.

3. **`ListNotificationPreferences`** / **`UpsertNotificationPreference`**
   - **Methods:** `ListNotificationPreferences(ListNotificationPreferencesRequest)`, `UpsertNotificationPreference(UpsertNotificationPreferenceRequest)`
   - **Request:** `UpsertNotificationPreferenceRequest` (`event_type`: `SHIPMENT_CREATED`/`STATUS_CHANGED`/etc., `channel`: `IN_APP`/`EMAIL`/`SMS`/`WEBHOOK`, `is_enabled`, `recipient_address`)
   - **Response:** `NotificationPreferenceResponse` (`id`, `event_type`, `channel`, `is_enabled`, `recipient_address`)
   - **Streaming:** Unary
   - **Status:** Fully Implemented.

---

### 2.3. GPS Tracking Service
- **Service Name:** `GpsTrackingService`
- **Proto File:** `protos/gps_tracking.proto`
- **Fully Qualified Package:** `gps_tracking.GpsTrackingService`
- **Implementation File:** `src/dotnet/GpsTracking/GrpcServices/GpsTrackingGrpcService.cs`
- **Security & Tenancy:** Enforces `currentUser.TenantId != Guid.Empty`.

#### RPCs & Wire Details:
1. **`IngestPosition`**
   - **Method:** `GpsTrackingGrpcService.IngestPosition(IngestPositionRequest, ServerCallContext)`
   - **Request:** `IngestPositionRequest` (`external_reading_id`, `device_id`, `vehicle_id`, `latitude`, `longitude`, `speed_kph`, `heading_degrees`, `accuracy_meters`, `recorded_at`)
   - **Response:** `PositionResponse` (`id`, `external_reading_id`, `device_id`, `vehicle_id`, `shipment_id`, `latitude`, `longitude`, `recorded_at`, `received_at`)
   - **Streaming:** Unary
   - **Dependencies:** `IPositionIngestionService` -> checks geofences, updates Redis/PostgreSQL current position, evaluates breaches.
   - **Status:** Fully Implemented.

2. **`GetCurrentLocation`** / **`ListPositionHistory`**
   - **Methods:** Query positions by `VehicleId` or `ShipmentId` selector.
   - **Streaming:** Unary.
   - **Status:** Fully Implemented.

3. **`CreateGeofence`** / **`ListGeofences`** / **`SetGeofenceActive`**
   - **Methods:** Geofence CRUD with circular coordinates (`latitude`, `longitude`, `radius_meters`).
   - **Streaming:** Unary.
   - **Status:** Fully Implemented.

4. **`ListMonitoringAlerts`** / **`ResolveMonitoringAlert`**
   - **Methods:** Alert lifecycle management for geofence breaches, route deviations, and delayed signals.
   - **Streaming:** Unary.
   - **Status:** Fully Implemented.

---

### 2.4. Document OCR Service
- **Service Name:** `DocumentOcrService`
- **Proto File:** `protos/document_ocr.proto`
- **Fully Qualified Package:** `document_ocr.DocumentOcrService`
- **Implementation File:** `src/dotnet/DocumentOcr/GrpcServices/DocumentOcrGrpcService.cs`
- **Security & Tenancy:** Enforces `currentUser.TenantId`.

#### RPCs & Wire Details:
1. **`SubmitOcrJob`** / **`SubmitDocumentJob`**
   - **Methods:** `SubmitOcrJob(SubmitOcrJobRequest)`, `SubmitDocumentJob(SubmitDocumentJobRequest)`
   - **Request Fields:** `idempotency_key`, `storage_reference`, `file_name`, `mime_type`, `size_bytes`, `document_type_hint` (`BILL_OF_LADING`, `COMMERCIAL_INVOICE`, `PACKING_LIST`, `CERTIFICATE_OF_ORIGIN`), `extraction_mode` (`TEXT_ONLY`, `STRUCTURED_DATA`, `MULTIMODAL_ALL`), `external_document_id`, `external_context_id`.
   - **Response Fields:** `DocumentOcrJobResponse` (`job_id`, `status`: `QUEUED`/`PROCESSING`/`COMPLETED`/`FAILED`/`REQUIRES_REVIEW`, `confidence`, `normalized_json`, `detected_document_type`, `needs_review`, `full_text_content`, `artifact_reference`).
   - **Streaming:** Unary
   - **Dependencies:** `IDocumentOcrJobService`, RabbitMQ asynchronous OCR processing pipeline.
   - **Status:** Fully Implemented.

2. **`GetDocumentJob`** / **`ListDocumentJobs`** / **`CancelDocumentJob`** / **`RetryDocumentJob`** / **`ReviewDocumentJob`**
   - **Human-in-the-Loop Review:** `ReviewDocumentJob` allows human operators to supply corrected JSON schemas and approve/reject OCR extractions.
   - **Streaming:** All Unary.
   - **Status:** Fully Implemented.

---

### 2.5. Regulatory Compliance Service & RAG
- **Service Name:** `RegulatoryComplianceService`
- **Proto File:** `protos/regulatory_compliance.proto`
- **Fully Qualified Package:** `regulatory_compliance.RegulatoryComplianceService`
- **Implementation File:** `src/dotnet/RegulatoryCompliance/GrpcServices/RegulatoryComplianceGrpcService.cs`
- **Security & Tenancy:** Dual isolation model — Tenant-owned custom compliance rules vs Platform-wide canonical regulations.

#### RPCs & Wire Details:
1. **`EvaluateCompliance`**
   - **Method:** `RegulatoryComplianceGrpcService.EvaluateCompliance(EvaluateComplianceRequest, ServerCallContext)`
   - **Request:** `EvaluateComplianceRequest` (`idempotency_key`, `external_shipment_id`, `cargo`, `origin_country_code`, `destination_country_code`, `jurisdiction_codes`, `transport_mode`, `documents`, `effective_at`)
   - **Response:** `ComplianceEvaluationResponse` (`evaluation_id`, `external_shipment_id`, `status`, `risk_level`: `LOW`/`MEDIUM`/`HIGH`/`CRITICAL`, `compliance_confidence`, `evidence_sufficiency`, `findings`, `assumptions`, `missing_documents`)
   - **Streaming:** Unary
   - **Dependencies:** `IComplianceEvaluationService`, pgvector hybrid RAG retrieval, rule engine.
   - **Status:** Fully Implemented.

2. **`GetComplianceEvaluation`**
   - **Method:** `GetComplianceEvaluation(GetComplianceEvaluationRequest)`
   - **Streaming:** Unary
   - **Status:** Fully Implemented.

3. **`QueryRegulations`** / **`QueryKnowledge`**
   - **Methods:** Hybrid semantic vector + BM25 keyword query against vectorized legal corpora.
   - **Request:** Query string, jurisdiction code, effective date, top-K, minimum relevance threshold.
   - **Response:** `QueryRegulationsResponse` / `QueryKnowledgeResponse` with ranked citation chunks (`regulatory_document_id`, `document_version_id`, `chunk_id`, `authority`, `excerpt`, `relevance_score`).
   - **Streaming:** Unary.
   - **Status:** Fully Implemented.

4. **`IngestRegulatorySource`** / **`IngestKnowledgeDocument`**
   - **Methods:** Legal document chunking, SHA-256 deduplication, AI embedding generation, and pgvector storage.
   - **Streaming:** Unary.
   - **Status:** Fully Implemented.

5. **`GenerateGroundedAnswer`**
   - **Method:** End-to-end RAG assistant synthesizing citations across regulations and knowledge articles.
   - **Response:** `GenerateGroundedAnswerResponse` with answer text, citation links, conflict detections, and AI governance audit metadata.
   - **Streaming:** Unary.
   - **Status:** Fully Implemented.

6. **`ValidateGroundedEvidence`**
   - **Method:** Deterministic hallucination validator ensuring every citation and statement maps strictly to retrieved chunk IDs.
   - **Streaming:** Unary.
   - **Status:** Fully Implemented.

7. **Discrepancy - `ComplianceRag.CheckRouteCompliance`** (`protos/compliance_rag.proto`):
   - Called by `RoutePlanningAgent` client stub `ComplianceRagClient.cs`.
   - **Status:** Proto Only / Missing dedicated server implementation (RoutePlanningAgent soft-fails if uncontactable).

---

### 2.6. Mail Service (Management & Security)
- **Proto File:** `protos/mail_platform.proto`
- **Services:** `MailManagement`, `MailSecurity`
- **Implementation Files:**
  - `src/dotnet/MailService/GrpcServices/MailManagementService.cs`
  - `src/dotnet/MailService/GrpcServices/MailSecurityService.cs`

#### `MailManagement` RPCs:
1. `ProvisionDomain`: Domain onboarding with DKIM key generation.
2. `CreateMailbox`: Mailbox allocation for tenant staff.
3. `CreateAlias`: Inbound routing alias configuration.
4. `ResetPassword`: Delegated to Cognito OIDC in v1 (acknowledges request).
5. `GetAuditRecords`: Audit log retrieval for administrative actions.
6. `RequeueDeadLetter`: Requeues failed email processing jobs.

#### `MailSecurity` RPCs:
1. `CreateDraftMessage`: Draft creation with content hashing and revision tracking.
2. `GetDraft` / `ListDrafts`: Draft retrieval and pagination.
3. `SubmitOutboundMessage`: Outbound security scan (DLP, anti-spam, recipient validation) and Stalwart SMTP queue injection.
4. `GetProcessedMessage` / `ListProcessedMessages`: Inspection of inbound/outbound email logs with spam/phishing scores.
5. `GetQuarantineRecord` / `ListQuarantineRecords`: Quarantine queue management.
6. `ReleaseQuarantine` / `DeleteQuarantine`: Quarantine release and purging.

---

### 2.7. IAM & Auth Services
- **Proto Files:** `protos/iam_tenant.proto`, `protos/auth.proto`
- **Services:** `IamService`, `AuthService`
- **Implementation Files:**
  - `src/dotnet/IamTenant/GrpcServices/IamGrpcService.cs`
  - `src/dotnet/IamTenant/GrpcServices/AuthGrpcService.cs`

#### `IamService` RPCs:
- **Tenant Management (System Admin):** `CreateTenant`, `GetTenant`, `UpdateTenantStatus`, `ListTenants`, `DeleteTenant`.
- **User / Staff Management (Tenant Admin):** `InviteUser`, `GetUser`, `GetManyUsers`, `UpdateUser`, `ActivateUser`, `ResetUserPassword`, `AssignRoles`, `SuspendUser`.
- **Role & Permission Management:** `GetRole`, `GetManyRoles`, `AssignPermissionsToRole`, `GetUserPermissions`.
- **Disabled Role Mutations (Read-Only Enforcement):** `CreateCustomRole`, `UpdateRole`, `DeleteRole` throw `StatusCode.Unimplemented` as static roles are enforced by domain rules.

#### `AuthService` RPCs:
1. `IdentifyUser`: Public global email pre-lookup returning tenant code and user type.
2. `Login`: Authenticates credentials against AWS Cognito User Pool via Tenant Auth Client.
3. `CompleteInvitation`: Handles `FORCE_CHANGE_PASSWORD` challenge for newly invited users.
4. `RefreshToken`: Renews expired JWT access tokens using Cognito refresh flow.
5. `Logout`: Invalidates session state.
6. `ForgotPassword` / `ConfirmForgotPassword`: Self-service Cognito password recovery.
7. `ValidateToken`: Defined in proto, but omitted from `AuthGrpcService.cs` (validation is handled statelessly via JWT signing keys in Gateway/BFF middleware).

---

### 2.8. Route Planning Agent Service
- **Service Name:** `RoutePlanningService`
- **Proto File:** `protos/route-planning-agent.proto`
- **Implementation File:** `src/dotnet/RoutePlanningAgent/GrpcServices/RoutePlanningGrpcService.cs`

#### RPCs & Wire Details:
1. **Route CRUD & State Machine:** `CreateRoute`, `GetRoute`, `ListRoutes`, `UpdateRoute`, `DeleteRoute`, `UpdateRouteStatus`.
2. **AI & Heuristic Optimization:** `OptimizeRoute` (TSP / VRP heuristic route ordering).
3. **Route Recommendation & Compliance Evaluation:** `GetRouteRecommendation` (orchestrates LLM route analysis, calls Compliance RAG, checks weather/traffic constraints).
4. **Approval Workflow:** `ListPendingApprovals`, `ApproveRoute`, `RejectRoute` (Human-in-the-loop approval gate for high-risk routes).
5. **Tenant AI & Rule Policies:** `GetTenantAiConfig`, `UpsertTenantAiConfig`, `UpsertTenantRuleConfig`, `ListTenantRuleConfigs`.

---

### 2.9. AI Governance Service (Java)
- **Proto File:** `protos/ai_governance.proto`
- **Services:** `AiGovernanceService`, `AiExecutionService`
- **Implementation Files:**
  - `src/java/ai-governance/src/main/java/com/aurora/aigovernance/grpc/governance/PolicyGrpcHandler.java`
  - `src/java/ai-governance/src/main/java/com/aurora/aigovernance/grpc/gateway/AiExecutionGrpcHandler.java`
- **Security & Tenancy:** Intercepts `x-service-id` and `CurrentUserContext.tenantId`. Missing `x-service-id` throws `UNAUTHENTICATED`.

#### RPCs:
1. **`AiGovernanceService.ExecutePolicy`**
   - **Method:** `PolicyGrpcHandler.executePolicy(ExecutePolicyRequest, StreamObserver<ExecutePolicyResponse>)`
   - Evaluates caller rate limits, token budgets, capability authorization, and approved LLM providers.
2. **`AiExecutionService.Generate`**
   - **Method:** `AiExecutionGrpcHandler.generate(AiGenerateRequest, StreamObserver<AiGenerateResponse>)`
   - Governed multimodal LLM text generation with token ceiling validation and storage URI security checks.
3. **`AiExecutionService.Embed`**
   - **Method:** `AiExecutionGrpcHandler.embed(AiEmbedRequest, StreamObserver<AiEmbedResponse>)`
   - Governed text vectorization returning raw float embeddings.

---

### 2.10. DevOps Agent & SRE (Java)
- **Proto File:** `protos/devops_agent.proto`, `protos/devops_rag.proto`
- **Services:** `DevOpsIngestionService`, `DevOpsIncidentService`, `DevOpsRuleService`, `DevOpsConfigService`, `DevOpsRagService`
- **Implementation Files:**
  - `src/java/devops-agent/src/main/java/com/aurora/devopsagent/GrpcServices/IngestionGrpcHandler.java`
  - `src/java/devops-agent/src/main/java/com/aurora/devopsagent/GrpcServices/IncidentGrpcHandler.java`
  - `src/java/devops-agent/src/main/java/com/aurora/devopsagent/GrpcServices/RuleGrpcHandler.java`
  - `src/java/devops-agent/src/main/java/com/aurora/devopsagent/GrpcServices/SelfConfigGrpcHandler.java`

#### RPCs:
1. **Alert Ingestion:** `DevOpsIngestionService.IngestAlert` (deduplicates error signatures from Azure Monitor / Loki).
2. **Incident Management:** `ListIncidents`, `GetIncident`, `ApproveIncident`, `RejectIncident` (automated SRE remediation with approval gates).
3. **Auto-Remediation Rules:** `ListExistingRules`, `CreateRule`, `UpdateRule`, `DeleteRule`, `ListPendingRules`, `ApprovePendingRule`, `RejectPendingRule`.
4. **Agent Configuration:** `GetSelfConfig`, `UpdateSelfConfig`.
5. **DevOps RAG (`devops_rag.proto`):** `DevOpsRagService.QueryKnowledge` called by `DevOpsRagClient.java` (no internal server implementation).

---

### 2.11. Billing & Financial Services (NestJS)
- **Proto Files:** `protos/billing.proto`, `protos/financial.proto`
- **Services:** `BillingService`, `FinancialService`, `NegotiationService`
- **Implementation Files:**
  - `src/nestjs/billing-service/src/interface/controllers/billing.controller.ts`
  - `src/nestjs/financial-service/src/interface/controllers/financial.controller.ts`
  - `src/nestjs/negotiation-agent-service/src/interface/controllers/negotiation.controller.ts`
- **Security & Tenancy:** NestJS `TenantInterceptor` extracts `x-tenant-id` header; `GrpcExceptionFilter` standardizes gRPC status codes.

#### `BillingService` RPCs:
1. **Invoice Lifecycle:** `GenerateInvoice`, `GetInvoiceDetail`, `CheckCustomerCredit`, `CreateInvoice`, `GetInvoice`, `ListInvoices`, `UpdateInvoiceStatus`.
2. **Escrow Wallet Management:** `CreateEscrowWallet`, `GetWalletBalance`, `FreezeEscrowAmount`, `ReleaseEscrowAmount`, `RefundEscrowAmount`.
3. **Uncontracted Controller Methods:** `RecordPayment`, `CancelInvoice`, `IssueDebitNote`, `IssueCreditNote` (implemented in TypeScript controller, but not defined in `billing.proto`).

#### `FinancialService` RPCs:
1. **Cost & Duty Estimation:** `EstimateCost`, `GetCustomsDuty`, `GetMinAcceptableRate`.
2. **Uncontracted Controller Methods:** `GetDynamicMargin`, `GetExchangeRate` (implemented in TypeScript controller, but not defined in `financial.proto`).

#### `NegotiationService` (NestJS):
1. **Uncontracted Service:** `@GrpcMethod('NegotiationService', 'SubmitOffer')` and `@GrpcMethod('NegotiationService', 'GetSessionHistory')` in `negotiation.controller.ts` have no corresponding protobuf definition in `protos/`.

---

## 3. Suspicious / Missing Implementations

1. **`ComplianceRag` Service Stub vs Real Implementation:**
   - [protos/compliance_rag.proto](file:///D:/IT/CD/aurora-server/protos/compliance_rag.proto) declares `service ComplianceRag { rpc CheckRouteCompliance ... }`.
   - [RoutePlanningAgent/Infrastructure/Services/ComplianceRagClient.cs](file:///D:/IT/CD/aurora-server/src/dotnet/RoutePlanningAgent/Infrastructure/Services/ComplianceRagClient.cs) calls this stub.
   - However, the actual compliance service implements `RegulatoryComplianceService` from [protos/regulatory_compliance.proto](file:///D:/IT/CD/aurora-server/protos/regulatory_compliance.proto).
   - *Risk:* `RoutePlanningAgent` will fail or rely on soft-fallback when evaluating route compliance unless aligned with `RegulatoryComplianceService.EvaluateCompliance`.

2. **`AuthService.ValidateToken` Omission:**
   - [protos/auth.proto](file:///D:/IT/CD/aurora-server/protos/auth.proto) line 16 defines `rpc ValidateToken(ValidateTokenRequest) returns (ValidateTokenResponse)`.
   - [IamTenant/GrpcServices/AuthGrpcService.cs](file:///D:/IT/CD/aurora-server/src/dotnet/IamTenant/GrpcServices/AuthGrpcService.cs) does not implement `ValidateToken`.
   - *Rationale:* JWT verification is performed statelessly in BFF/Gateway middleware. The RPC in proto is redundant or incomplete.

3. **NestJS Controller Methods Missing Protobuf Declarations:**
   - `BillingService`: `RecordPayment`, `CancelInvoice`, `IssueDebitNote`, `IssueCreditNote` are decorated with `@GrpcMethod` in [billing.controller.ts](file:///D:/IT/CD/aurora-server/src/nestjs/billing-service/src/interface/controllers/billing.controller.ts), but missing in [billing.proto](file:///D:/IT/CD/aurora-server/protos/billing.proto).
   - `FinancialService`: `GetDynamicMargin`, `GetExchangeRate` are decorated with `@GrpcMethod` in [financial.controller.ts](file:///D:/IT/CD/aurora-server/src/nestjs/financial-service/src/interface/controllers/financial.controller.ts), but missing in [financial.proto](file:///D:/IT/CD/aurora-server/protos/financial.proto).
   - `NegotiationService`: `SubmitOffer`, `GetSessionHistory` are decorated with `@GrpcMethod` in [negotiation.controller.ts](file:///D:/IT/CD/aurora-server/src/nestjs/negotiation-agent-service/src/interface/controllers/negotiation.controller.ts), but no `negotiation.proto` exists in `protos/`.
   - *Risk:* External gRPC clients generating stubs from `protos/` cannot discover or invoke these capabilities.

---

## 4. Proto Defined But Not Implemented

1. **`ai_governance_admin.proto`**:
   - Contains commented-out definitions for `AiGovernanceAdminService` (`GetGlobalAiConfig`, `UpdateGlobalAiConfig`, `ListAiUsageLogs`). No server implementation exists.
2. **`devops_rag.proto` (`DevOpsRagService.IngestKnowledge`)**:
   - `DevOpsRagService` defines `IngestKnowledge` and `QueryKnowledge`. `DevOpsRagClient.java` only calls `QueryKnowledge`. No server implementation exists in the repo.
3. **`IamService` Role Mutation RPCs**:
   - `CreateCustomRole`, `UpdateRole`, `DeleteRole` exist in `iam_tenant.proto`, but intentionally throw `StatusCode.Unimplemented` in `IamGrpcService.cs` because roles are static system constants.

---

## 5. Implemented But Not Clearly Exposed

1. **Core Logistics Subsystems Not Yet Exposed in BFF:**
   - `ShipmentWorkflowService`: All 20 RPCs are fully implemented with unit tests, but `Staff.Bff` has not yet registered a `ShipmentsController` or `ShipmentWorkflowServiceClient`.
   - `GpsTrackingService`: All 8 tracking/geofence RPCs are fully implemented, but BFF does not currently expose GPS endpoints.
   - `NotificationService`: All 4 notification & preference RPCs are fully implemented, but BFF does not currently expose notification endpoints.
2. **Internal Agent Orchestration Methods:**
   - `RegulatoryComplianceService.ValidateGroundedEvidence`: Fully implemented deterministic validator, but used only within internal service logic.

---

## 6. Likely Internal-Only RPCs

The following RPCs should remain strictly internal (never exposed to public internet or user BFFs):

1. **`AiExecutionService.Generate` / `AiExecutionService.Embed`**:
   - Internal AI execution engine requiring `x-service-id` and internal token allocations.
2. **`AiGovernanceService.ExecutePolicy`**:
   - Pre-execution policy check for internal microservices.
3. **`DevOpsIngestionService.IngestAlert`**:
   - Webhook / telemetry receiver for Azure Monitor, Loki, and SRE monitoring agents.
4. **`GpsTrackingService.IngestPosition`**:
   - High-throughput ingestion endpoint for IoT hardware devices and edge tracking gateways.
5. **`ShipmentWorkflowService.UpdateShipmentDocumentOcr`**:
   - Service-to-service callback from `DocumentOcr` / `RabbitMQ` consumer into `ShipmentWorkflow`.
6. **`FinancialService.EstimateCost` / `GetMinAcceptableRate`**:
   - Internal algorithmic cost estimation called by `billing-service` and `negotiation-agent-service`.

---

## 7. Potential User-Facing Capabilities

The following gRPC capabilities map directly to user interactions and should be exposed via BFF endpoints:

1. **Shipment Management (Staff / Portal Users):**
   - Create, list, search, view timeline, submit, cancel shipments.
   - Manage cargo items, tracking locations, documents, and milestones.
2. **Document Processing & Review (Staff Users):**
   - Submit documents for OCR, review low-confidence extractions, retry/cancel OCR jobs.
3. **Compliance Intelligence (Staff & Compliance Officers):**
   - Run compliance pre-checks on shipments (`EvaluateCompliance`).
   - Interactive regulatory assistant (`GenerateGroundedAnswer`, `QueryRegulations`, `QueryKnowledge`).
4. **Route Planning & Approval (Dispatchers & Managers):**
   - Optimize multi-stop routes, request AI route recommendations, approve/reject hazardous or complex routes.
5. **Communication & Invoicing (Staff & Billing Clerks):**
   - Manage draft emails, review quarantined security emails, submit outbound messages.
   - Generate invoices, review escrow wallet balances, approve credit checks.
6. **User & Tenant Administration (Admins):**
   - Invite users, assign roles, update AI/rule threshold configurations, provision mail domains.
