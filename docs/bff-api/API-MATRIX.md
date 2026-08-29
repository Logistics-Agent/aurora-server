# Aurora Platform - BFF Master API Traceability Matrix

> **Document ID:** `DOC-BFF-MATRIX`  
> **Status:** Implementation & Traceability Complete  
> **Scope:** Comprehensive mapping of all implemented BFF REST endpoints, authorization scopes, backing gRPC services, and CQRS handlers.  
> **Architecture Reference:** `docs/bff-api/README.md`, `docs/bff-api/staff-api.md`, `docs/bff-api/manager-api.md`, `docs/bff-api/admin-api.md`, `docs/bff-api/system-api.md`, `docs/bff-api/shared-api.md`, `docs/bff-api/blocked-api.md`

---

## 1. Master Traceability Table

The table below catalogs every endpoint across the platform, sorted strictly by role classification:
1. `STAFF_ONLY`
2. `MANAGER_ONLY`
3. `ADMIN_ONLY`
4. `SYSTEM_ONLY`
5. `SHARED` (Any API with >= 2 roles)

| Method | Endpoint | Function | Roles | Shared | Service | RPC | BFF File | Backend Files | Status |
| :--- | :--- | :--- | :--- | :---: | :--- | :--- | :--- | :--- | :---: |
| **MANAGER_ONLY** |
| `POST` | `/api/v1/approvals/{id}/approve` | Approve AI Route Recommendation | `[MANAGER]` | `false` | `RoutePlanningAgent` | `ApproveRoute` | `Staff.Bff/Controllers/ApprovalsController.cs` | `route-planning-agent.proto`, `ApproveRouteCommand.cs` | `READY` |
| `POST` | `/api/v1/approvals/{id}/reject` | Reject AI Route Recommendation | `[MANAGER]` | `false` | `RoutePlanningAgent` | `RejectRoute` | `Staff.Bff/Controllers/ApprovalsController.cs` | `route-planning-agent.proto`, `RejectRouteCommand.cs` | `READY` |
| **ADMIN_ONLY** |
| `POST` | `/api/v1/admin/staff/invite` | Invite New Staff User | `[ADMIN]` | `false` | `IamTenant` | `InviteUser` | `Admin.Bff/Controllers/StaffController.cs` | `iam_tenant.proto`, `CreateStaffCommand.cs` | `READY` |
| `GET` | `/api/v1/admin/staff` | List Tenant Staff Directory | `[ADMIN]` | `false` | `IamTenant` | `GetManyUsers` | `Admin.Bff/Controllers/StaffController.cs` | `iam_tenant.proto`, `ListStaffQuery.cs` | `READY` |
| `PUT` | `/api/v1/admin/staff/{id}` | Update Staff Profile | `[ADMIN]` | `false` | `IamTenant` | `UpdateUser` | `Admin.Bff/Controllers/StaffController.cs` | `iam_tenant.proto`, `UpdateStaffCommand.cs` | `READY` |
| `POST` | `/api/v1/admin/staff/{id}/activate` | Reactivate Suspended Staff | `[ADMIN]` | `false` | `IamTenant` | `ActivateUser` | `Admin.Bff/Controllers/StaffController.cs` | `iam_tenant.proto`, `ActivateStaffCommand.cs` | `READY` |
| `POST` | `/api/v1/admin/staff/{id}/suspend` | Suspend Staff Account | `[ADMIN]` | `false` | `IamTenant` | `SuspendUser` | `Admin.Bff/Controllers/StaffController.cs` | `iam_tenant.proto`, `DeactivateStaffCommand.cs` | `READY` |
| `POST` | `/api/v1/admin/staff/{id}/reset-password`| Trigger Admin Password Reset | `[ADMIN]` | `false` | `IamTenant` | `ResetUserPassword` | `Admin.Bff/Controllers/StaffController.cs` | `iam_tenant.proto`, `ResetStaffPasswordCommand.cs` | `READY` |
| `POST` | `/api/v1/admin/staff/{id}/roles` | Assign Roles to User | `[ADMIN]` | `false` | `IamTenant` | `AssignRoles` | `Admin.Bff/Controllers/StaffController.cs` | `iam_tenant.proto`, `AssignRolesCommand.cs` | `READY` |
| `GET` | `/api/v1/admin/roles` | List Tenant System Roles | `[ADMIN]` | `false` | `IamTenant` | `GetManyRoles` | `Admin.Bff/Controllers/RolesController.cs` | `iam_tenant.proto`, `ListRolesQuery.cs` | `READY` |
| `GET` | `/api/v1/admin/roles/{id}` | Get Role Details & Permissions | `[ADMIN]` | `false` | `IamTenant` | `GetRole` | `Admin.Bff/Controllers/RolesController.cs` | `iam_tenant.proto`, `GetRoleQuery.cs` | `READY` |
| `POST` | `/api/v1/admin/roles/{id}/permissions` | Update Role Permission Grants | `[ADMIN]` | `false` | `IamTenant` | `AssignPermissionsToRole`| `Admin.Bff/Controllers/RolesController.cs` | `iam_tenant.proto`, `AssignPermissionsToRoleCommand.cs`| `READY` |
| `GET` | `/api/v1/admin/ai-config` | Get Tenant AI Policy | `[ADMIN]` | `false` | `RoutePlanningAgent` | `GetTenantAiConfig` | `Admin.Bff/Controllers/AiConfigController.cs` | `route-planning-agent.proto`, `GetTenantAiConfigQuery.cs` | `READY` |
| `PUT` | `/api/v1/admin/ai-config` | Update Tenant AI Policy | `[ADMIN]` | `false` | `RoutePlanningAgent` | `UpsertTenantAiConfig`| `Admin.Bff/Controllers/AiConfigController.cs` | `route-planning-agent.proto`, `UpsertTenantAiConfigCommand.cs`| `READY` |
| `GET` | `/api/v1/admin/rules` | List Tenant Dispatch Rules | `[ADMIN]` | `false` | `RoutePlanningAgent` | `ListTenantRuleConfigs`| `Admin.Bff/Controllers/RuleConfigController.cs` | `route-planning-agent.proto`, `ListTenantRuleConfigsQuery.cs` | `READY` |
| `PUT` | `/api/v1/admin/rules` | Configure Tenant Dispatch Rule | `[ADMIN]` | `false` | `RoutePlanningAgent` | `UpsertTenantRuleConfig`| `Admin.Bff/Controllers/RuleConfigController.cs`| `route-planning-agent.proto`, `UpsertTenantRuleConfigCommand.cs`| `READY` |
| `POST` | `/api/v1/admin/mail/domains` | Provision Tenant Mail Domain | `[ADMIN]` | `false` | `MailService` | `ProvisionDomain` | `Admin.Bff/Controllers/MailAdminController.cs` | `mail_platform.proto`, `ProvisionDomainCommand.cs` | `READY` |
| `POST` | `/api/v1/admin/mail/mailboxes` | Create User Mailbox | `[ADMIN]` | `false` | `MailService` | `CreateMailbox` | `Admin.Bff/Controllers/MailAdminController.cs` | `mail_platform.proto`, `CreateMailboxCommand.cs` | `READY` |
| `POST` | `/api/v1/admin/mail/aliases` | Create Inbound Mail Alias | `[ADMIN]` | `false` | `MailService` | `CreateAlias` | `Admin.Bff/Controllers/MailAdminController.cs` | `mail_platform.proto`, `CreateAliasCommand.cs` | `READY` |
| `DELETE`| `/api/v1/admin/mail/quarantine/{id}` | Permanently Purge Quarantined Mail | `[ADMIN]` | `false` | `MailService` | `DeleteQuarantine` | `Admin.Bff/Controllers/MailAdminController.cs` | `mail_platform.proto`, `DeleteQuarantineCommand.cs` | `READY` |
| `GET` | `/api/v1/admin/mail/audit` | Get Tenant Security Audit Log | `[ADMIN]` | `false` | `MailService` | `GetAuditRecords` | `Admin.Bff/Controllers/MailAdminController.cs` | `mail_platform.proto`, `GetAuditRecordsQuery.cs` | `READY` |
| `POST` | `/api/v1/admin/compliance/sources` | Ingest Tenant-Scoped Regulatory Doc | `[ADMIN]` | `false` | `RegulatoryCompliance`| `IngestRegulatorySource`| `Admin.Bff/Controllers/PlatformIngestionController.cs` | `regulatory_compliance.proto`, `RegulatoryIngestionService.cs`| `READY` |
| `POST` | `/api/v1/admin/compliance/knowledge` | Ingest Tenant SOP Knowledge Doc | `[ADMIN]` | `false` | `RegulatoryCompliance`| `IngestKnowledgeDocument`| `Admin.Bff/Controllers/PlatformIngestionController.cs` | `regulatory_compliance.proto`, `KnowledgeIngestionService.cs` | `READY` |
| **SYSTEM_ONLY** |
| `POST` | `/api/v1/system/tenants` | Onboard New Customer Tenant | `[SYSTEM]` | `false` | `IamTenant` | `CreateTenant` | `System.Bff/Controllers/TenantsController.cs` | `iam_tenant.proto`, `CreateTenantCommand.cs` | `READY` |
| `GET` | `/api/v1/system/tenants` | List All System Tenants (Paged) | `[SYSTEM]` | `false` | `IamTenant` | `ListTenants` | `System.Bff/Controllers/TenantsController.cs` | `iam_tenant.proto`, `ListTenantsQuery.cs` | `READY` |
| `GET` | `/api/v1/system/tenants/{id}` | Get System Tenant Details | `[SYSTEM]` | `false` | `IamTenant` | `GetTenant` | `System.Bff/Controllers/TenantsController.cs` | `iam_tenant.proto`, `GetTenantQuery.cs` | `READY` |
| `PATCH`| `/api/v1/system/tenants/{id}/status` | Suspend / Activate Tenant | `[SYSTEM]` | `false` | `IamTenant` | `UpdateTenantStatus` | `System.Bff/Controllers/TenantsController.cs` | `iam_tenant.proto`, `UpdateTenantStatusCommand.cs` | `READY` |
| `DELETE`| `/api/v1/system/tenants/{id}` | Purge / Offboard Tenant | `[SYSTEM]` | `false` | `IamTenant` | `DeleteTenant` | `System.Bff/Controllers/TenantsController.cs` | `iam_tenant.proto`, `DeleteTenantCommand.cs` | `READY` |
| `POST` | `/api/v1/system/compliance/sources` | Ingest Global Trade Law | `[SYSTEM]` | `false` | `RegulatoryCompliance`| `IngestRegulatorySource`| `System.Bff/Controllers/SystemIngestionController.cs` | `regulatory_compliance.proto`, `RegulatoryIngestionService.cs`| `READY` |
| `POST` | `/api/v1/system/compliance/knowledge` | Ingest Global Platform Knowledge | `[SYSTEM]` | `false` | `RegulatoryCompliance`| `IngestKnowledgeDocument`| `System.Bff/Controllers/SystemIngestionController.cs` | `regulatory_compliance.proto`, `KnowledgeIngestionService.cs` | `READY` |
| `POST` | `/api/v1/system/mail/dead-letters/{id}/requeue` | Reprocess Failed Outbox Email | `[SYSTEM]` | `false` | `MailService` | `RequeueDeadLetter` | `System.Bff/Controllers/MailSystemController.cs` | `mail_platform.proto`, `RequeueDeadLetterCommand.cs` | `READY` |
| **SHARED (>= 2 Roles)** |
| `POST` | `/api/v1/shipments` | Create Draft Shipment | `[STAFF, MANAGER]` | `true` | `ShipmentWorkflow` | `CreateShipment` | `Staff.Bff/Controllers/ShipmentsController.cs` | `shipment_workflow.proto`, `CreateShipmentCommand.cs` | `READY` |
| `GET` | `/api/v1/shipments/{id}` | Get Shipment Details | `[STAFF, MANAGER, ADMIN]` | `true` | `ShipmentWorkflow` | `GetShipment` | `Staff.Bff/Controllers/ShipmentsController.cs` | `shipment_workflow.proto`, `GetShipmentQuery.cs` | `READY` |
| `GET` | `/api/v1/shipments` | List / Search Shipments | `[STAFF, MANAGER, ADMIN]` | `true` | `ShipmentWorkflow` | `ListShipments` | `Staff.Bff/Controllers/ShipmentsController.cs` | `shipment_workflow.proto`, `ListShipmentsQuery.cs` | `READY` |
| `PUT` | `/api/v1/shipments/{id}` | Update Shipment Details | `[STAFF, MANAGER]` | `true` | `ShipmentWorkflow` | `UpdateShipment` | `Staff.Bff/Controllers/ShipmentsController.cs` | `shipment_workflow.proto`, `UpdateShipmentCommand.cs` | `READY` |
| `POST` | `/api/v1/shipments/{id}/submit` | Submit Shipment Workflow | `[STAFF, MANAGER]` | `true` | `ShipmentWorkflow` | `SubmitShipment` | `Staff.Bff/Controllers/ShipmentsController.cs` | `shipment_workflow.proto`, `SubmitShipmentCommand.cs` | `READY` |
| `PATCH`| `/api/v1/shipments/{id}/status` | Update Shipment Status | `[STAFF, MANAGER]` | `true` | `ShipmentWorkflow` | `UpdateShipmentStatus` | `Staff.Bff/Controllers/ShipmentsController.cs` | `shipment_workflow.proto`, `UpdateShipmentStatusCommand.cs` | `READY` |
| `POST` | `/api/v1/shipments/{id}/cancel` | Cancel Active Shipment | `[STAFF, MANAGER]` | `true` | `ShipmentWorkflow` | `CancelShipment` | `Staff.Bff/Controllers/ShipmentsController.cs` | `shipment_workflow.proto`, `CancelShipmentCommand.cs` | `READY` |
| `DELETE`| `/api/v1/shipments/{id}` | Delete Draft Shipment | `[STAFF, MANAGER]` | `true` | `ShipmentWorkflow` | `DeleteDraftShipment` | `Staff.Bff/Controllers/ShipmentsController.cs` | `shipment_workflow.proto`, `DeleteDraftShipmentCommand.cs` | `READY` |
| `POST` | `/api/v1/shipments/import` | Bulk Ingest Shipments | `[STAFF, MANAGER]` | `true` | `ShipmentWorkflow` | `ImportShipments` | `Staff.Bff/Controllers/ShipmentsController.cs` | `shipment_workflow.proto`, `ImportShipmentsCommand.cs` | `READY` |
| `POST` | `/api/v1/shipments/{id}/cargo` | Add Cargo Item Line | `[STAFF, MANAGER]` | `true` | `ShipmentWorkflow` | `AddCargoItem` | `Staff.Bff/Controllers/ShipmentsController.cs` | `shipment_workflow.proto`, `AddCargoItemCommand.cs` | `READY` |
| `PUT` | `/api/v1/shipments/{id}/cargo/{itemId}` | Update Cargo Item | `[STAFF, MANAGER]` | `true` | `ShipmentWorkflow` | `UpdateCargoItem` | `Staff.Bff/Controllers/ShipmentsController.cs` | `shipment_workflow.proto`, `UpdateCargoItemCommand.cs` | `READY` |
| `DELETE`| `/api/v1/shipments/{id}/cargo/{itemId}`| Remove Cargo Item | `[STAFF, MANAGER]` | `true` | `ShipmentWorkflow` | `RemoveCargoItem` | `Staff.Bff/Controllers/ShipmentsController.cs` | `shipment_workflow.proto`, `RemoveCargoItemCommand.cs` | `READY` |
| `POST` | `/api/v1/shipments/{id}/locations` | Add Route Checkpoint | `[STAFF, MANAGER]` | `true` | `ShipmentWorkflow` | `AddShipmentLocation` | `Staff.Bff/Controllers/ShipmentsController.cs` | `shipment_workflow.proto`, `AddShipmentLocationCommand.cs` | `READY` |
| `PUT` | `/api/v1/shipments/{id}/locations/{locId}` | Update Route Checkpoint | `[STAFF, MANAGER]` | `true` | `ShipmentWorkflow` | `UpdateShipmentLocation`| `Staff.Bff/Controllers/ShipmentsController.cs` | `shipment_workflow.proto`, `UpdateShipmentLocationCommand.cs`| `READY` |
| `DELETE`| `/api/v1/shipments/{id}/locations/{locId}`| Remove Route Checkpoint | `[STAFF, MANAGER]` | `true` | `ShipmentWorkflow` | `RemoveShipmentLocation`| `Staff.Bff/Controllers/ShipmentsController.cs` | `shipment_workflow.proto`, `RemoveShipmentLocationCommand.cs`| `READY` |
| `POST` | `/api/v1/shipments/{id}/documents` | Attach Compliance Document | `[STAFF, MANAGER]` | `true` | `ShipmentWorkflow` | `AttachShipmentDocument`| `Staff.Bff/Controllers/ShipmentsController.cs` | `shipment_workflow.proto`, `AttachShipmentDocumentCommand.cs`| `READY` |
| `DELETE`| `/api/v1/shipments/{id}/documents/{docId}` | Detach Document | `[STAFF, MANAGER]` | `true` | `ShipmentWorkflow` | `RemoveShipmentDocument`| `Staff.Bff/Controllers/ShipmentsController.cs` | `shipment_workflow.proto`, `RemoveShipmentDocumentCommand.cs`| `READY` |
| `POST` | `/api/v1/shipments/{id}/milestones` | Record Event Milestone | `[STAFF, MANAGER]` | `true` | `ShipmentWorkflow` | `AddShipmentMilestone` | `Staff.Bff/Controllers/ShipmentsController.cs` | `shipment_workflow.proto`, `RecordShipmentMilestoneCommand.cs`| `READY` |
| `GET` | `/api/v1/shipments/{id}/timeline` | Get Shipment Event Timeline | `[STAFF, MANAGER, ADMIN]` | `true` | `ShipmentWorkflow` | `GetShipmentTimeline` | `Staff.Bff/Controllers/ShipmentsController.cs` | `shipment_workflow.proto`, `GetShipmentTimelineQuery.cs` | `READY` |
| `GET` | `/api/v1/tracking/{id}/current` | Get Live GPS Coordinates | `[STAFF, MANAGER, ADMIN]` | `true` | `GpsTracking` | `GetCurrentLocation` | `Staff.Bff/Controllers/TrackingController.cs` | `gps_tracking.proto`, `LocationQueryService.cs` | `READY` |
| `GET` | `/api/v1/tracking/{id}/history` | Get GPS Historical Replay | `[STAFF, MANAGER, ADMIN]` | `true` | `GpsTracking` | `ListPositionHistory` | `Staff.Bff/Controllers/TrackingController.cs` | `gps_tracking.proto`, `LocationQueryService.cs` | `READY` |
| `POST` | `/api/v1/tracking/geofences` | Create Warehouse Geofence | `[STAFF, MANAGER, ADMIN]` | `true` | `GpsTracking` | `CreateGeofence` | `Staff.Bff/Controllers/TrackingController.cs` | `gps_tracking.proto`, `MonitoringManagementService.cs` | `READY` |
| `GET` | `/api/v1/tracking/geofences` | List Warehouse Geofences | `[STAFF, MANAGER, ADMIN]` | `true` | `GpsTracking` | `ListGeofences` | `Staff.Bff/Controllers/TrackingController.cs` | `gps_tracking.proto`, `MonitoringManagementService.cs` | `READY` |
| `PATCH`| `/api/v1/tracking/geofences/{id}/active` | Toggle Geofence Active State | `[STAFF, MANAGER, ADMIN]` | `true` | `GpsTracking` | `SetGeofenceActive` | `Staff.Bff/Controllers/TrackingController.cs` | `gps_tracking.proto`, `MonitoringManagementService.cs` | `READY` |
| `GET` | `/api/v1/tracking/alerts` | List Exception Alerts | `[STAFF, MANAGER, ADMIN]` | `true` | `GpsTracking` | `ListMonitoringAlerts` | `Staff.Bff/Controllers/TrackingController.cs` | `gps_tracking.proto`, `MonitoringManagementService.cs` | `READY` |
| `POST` | `/api/v1/tracking/alerts/{id}/resolve` | Resolve Monitoring Alert | `[MANAGER, ADMIN]` | `true` | `GpsTracking` | `ResolveMonitoringAlert` | `Staff.Bff/Controllers/TrackingController.cs` | `gps_tracking.proto`, `MonitoringManagementService.cs` | `READY` |
| `GET` | `/api/v1/notifications` | List User Notifications | `[STAFF, MANAGER, ADMIN]` | `true` | `Notification` | `ListNotifications` | `Staff.Bff/Controllers/NotificationsController.cs` | `notification.proto`, `NotificationDbContext` | `READY` |
| `PATCH`| `/api/v1/notifications/{id}/read` | Mark Notification As Read | `[STAFF, MANAGER, ADMIN]` | `true` | `Notification` | `MarkNotificationRead` | `Staff.Bff/Controllers/NotificationsController.cs` | `notification.proto`, `NotificationDbContext` | `READY` |
| `GET` | `/api/v1/notifications/preferences` | Get Notification Channels | `[STAFF, MANAGER, ADMIN]` | `true` | `Notification` | `ListNotificationPreferences` | `Staff.Bff/Controllers/NotificationsController.cs` | `notification.proto`, `NotificationDbContext` | `READY` |
| `PUT` | `/api/v1/notifications/preferences` | Update Notification Channels | `[STAFF, MANAGER, ADMIN]` | `true` | `Notification` | `UpsertNotificationPreference`| `Staff.Bff/Controllers/NotificationsController.cs` | `notification.proto`, `NotificationDbContext` | `READY` |
| `POST` | `/api/v1/documents/ocr` | Submit Document for OCR | `[STAFF, MANAGER]` | `true` | `DocumentOcr` | `SubmitOcrJob` | `Staff.Bff/Controllers/DocumentsController.cs` | `document_ocr.proto`, `DocumentOcrJobService.cs` | `READY` |
| `GET` | `/api/v1/documents/jobs/{id}` | Get OCR Extraction Result | `[STAFF, MANAGER, ADMIN]` | `true` | `DocumentOcr` | `GetDocumentJob` | `Staff.Bff/Controllers/DocumentsController.cs` | `document_ocr.proto`, `DocumentOcrJobService.cs` | `READY` |
| `GET` | `/api/v1/documents/jobs` | List Active OCR Jobs | `[STAFF, MANAGER, ADMIN]` | `true` | `DocumentOcr` | `ListDocumentJobs` | `Staff.Bff/Controllers/DocumentsController.cs` | `document_ocr.proto`, `DocumentOcrJobService.cs` | `READY` |
| `POST` | `/api/v1/documents/jobs/{id}/review` | Human Review of Low-Confidence OCR | `[MANAGER, ADMIN]` | `true` | `DocumentOcr` | `ReviewDocumentJob` | `Staff.Bff/Controllers/DocumentsController.cs` | `document_ocr.proto`, `DocumentOcrJobService.cs` | `READY` |
| `POST` | `/api/v1/documents/jobs/{id}/cancel` | Cancel OCR Processing | `[STAFF, MANAGER]` | `true` | `DocumentOcr` | `CancelDocumentJob` | `Staff.Bff/Controllers/DocumentsController.cs` | `document_ocr.proto`, `DocumentOcrJobService.cs` | `READY` |
| `POST` | `/api/v1/documents/jobs/{id}/retry` | Retry Failed OCR Job | `[STAFF, MANAGER]` | `true` | `DocumentOcr` | `RetryDocumentJob` | `Staff.Bff/Controllers/DocumentsController.cs` | `document_ocr.proto`, `DocumentOcrJobService.cs` | `READY` |
| `POST` | `/api/v1/compliance/evaluations` | Evaluate Shipment Compliance | `[STAFF, MANAGER]` | `true` | `RegulatoryCompliance`| `EvaluateCompliance` | `Staff.Bff/Controllers/ComplianceController.cs` | `regulatory_compliance.proto`, `ComplianceEvaluationService.cs`| `READY` |
| `GET` | `/api/v1/compliance/evaluations/{id}` | Get Compliance Report | `[STAFF, MANAGER, ADMIN]` | `true` | `RegulatoryCompliance`| `GetComplianceEvaluation` | `Staff.Bff/Controllers/ComplianceController.cs` | `regulatory_compliance.proto`, `ComplianceEvaluationService.cs`| `READY` |
| `POST` | `/api/v1/compliance/copilot/ask` | Ask AI Legal Regulations Copilot | `[STAFF, MANAGER, ADMIN]` | `true` | `RegulatoryCompliance`| `GenerateGroundedAnswer` | `Staff.Bff/Controllers/ComplianceController.cs` | `regulatory_compliance.proto`, `GroundedAnswerService.cs` | `READY` |
| `POST` | `/api/v1/compliance/regulations/query` | Search Global Regulations RAG | `[STAFF, MANAGER, ADMIN]` | `true` | `RegulatoryCompliance`| `QueryRegulations` | `Staff.Bff/Controllers/SearchController.cs` | `regulatory_compliance.proto`, `RegulationRetrievalService.cs` | `READY` |
| `POST` | `/api/v1/compliance/knowledge/query` | Search Internal SOP Knowledge | `[STAFF, MANAGER, ADMIN]` | `true` | `RegulatoryCompliance`| `QueryKnowledge` | `Staff.Bff/Controllers/SearchController.cs` | `regulatory_compliance.proto`, `KnowledgeIngestionService.cs` | `READY` |
| `POST` | `/api/v1/routes` | Create Multi-stop Route | `[STAFF, MANAGER]` | `true` | `RoutePlanningAgent` | `CreateRoute` | `Staff.Bff/Controllers/RoutesController.cs` | `route-planning-agent.proto`, `CreateRouteCommand.cs` | `READY` |
| `GET` | `/api/v1/routes/{id}` | Get Route Details | `[STAFF, MANAGER, ADMIN]` | `true` | `RoutePlanningAgent` | `GetRoute` | `Staff.Bff/Controllers/RoutesController.cs` | `route-planning-agent.proto`, `GetRouteQuery.cs` | `READY` |
| `GET` | `/api/v1/routes` | List Dispatch Routes | `[STAFF, MANAGER, ADMIN]` | `true` | `RoutePlanningAgent` | `ListRoutes` | `Staff.Bff/Controllers/RoutesController.cs` | `route-planning-agent.proto`, `ListRoutesQuery.cs` | `READY` |
| `PUT` | `/api/v1/routes/{id}` | Update Dispatch Route | `[STAFF, MANAGER]` | `true` | `RoutePlanningAgent` | `UpdateRoute` | `Staff.Bff/Controllers/RoutesController.cs` | `route-planning-agent.proto`, `UpdateRouteCommand.cs` | `READY` |
| `DELETE`| `/api/v1/routes/{id}` | Delete Draft Route | `[STAFF, MANAGER]` | `true` | `RoutePlanningAgent` | `DeleteRoute` | `Staff.Bff/Controllers/RoutesController.cs` | `route-planning-agent.proto`, `DeleteRouteCommand.cs` | `READY` |
| `PATCH`| `/api/v1/routes/{id}/status` | Update Route Dispatch Status | `[STAFF, MANAGER]` | `true` | `RoutePlanningAgent` | `UpdateRouteStatus` | `Staff.Bff/Controllers/RoutesController.cs` | `route-planning-agent.proto`, `UpdateRouteStatusCommand.cs` | `READY` |
| `POST` | `/api/v1/routes/{id}/optimize` | Run TSP Route Optimization | `[STAFF, MANAGER]` | `true` | `RoutePlanningAgent` | `OptimizeRoute` | `Staff.Bff/Controllers/RoutesController.cs` | `route-planning-agent.proto`, `OptimizeRouteCommand.cs` | `READY` |
| `POST` | `/api/v1/routes/{id}/recommendation`| AI Route Recommendation | `[STAFF, MANAGER]` | `true` | `RoutePlanningAgent` | `GetRouteRecommendation`| `Staff.Bff/Controllers/RoutesController.cs` | `route-planning-agent.proto`, `RequestRouteRecommendationCommand.cs`| `READY` |
| `GET` | `/api/v1/approvals/pending` | List Pending Route Approvals | `[MANAGER, ADMIN]` | `true` | `RoutePlanningAgent` | `ListPendingApprovals` | `Staff.Bff/Controllers/ApprovalsController.cs` | `route-planning-agent.proto`, `ListPendingApprovalsQuery.cs` | `READY` |
| `POST` | `/api/v1/auth/identify` | Pre-Auth Email Existence Check | `[STAFF, MANAGER, ADMIN]` | `true` | `IamTenant` | `IdentifyUser` | `BuildingBlocks.BFF/AuthController.cs` | `auth.proto`, `IdentifyUserQuery.cs` | `READY` |
| `POST` | `/api/v1/auth/login` | User Password Login | `[STAFF, MANAGER, ADMIN]` | `true` | `IamTenant` | `Login` | `BuildingBlocks.BFF/AuthController.cs` | `auth.proto`, `LoginCommand.cs` | `READY` |
| `POST` | `/api/v1/auth/complete-invitation` | Initial Login & Password Set | `[STAFF, MANAGER, ADMIN]` | `true` | `IamTenant` | `CompleteInvitation` | `BuildingBlocks.BFF/AuthController.cs` | `auth.proto`, `CompleteInvitationCommand.cs` | `READY` |
| `POST` | `/api/v1/auth/refresh` | Refresh JWT Access Token | `[STAFF, MANAGER, ADMIN]` | `true` | `IamTenant` | `RefreshToken` | `BuildingBlocks.BFF/AuthController.cs` | `auth.proto`, `ICognitoAuthService` | `READY` |
| `POST` | `/api/v1/auth/logout` | Revoke User Session | `[STAFF, MANAGER, ADMIN]` | `true` | `IamTenant` | `Logout` | `BuildingBlocks.BFF/AuthController.cs` | `auth.proto`, Stateless | `READY` |
| `POST` | `/api/v1/auth/forgot-password` | Request Password Reset Code | `[STAFF, MANAGER, ADMIN]` | `true` | `IamTenant` | `ForgotPassword` | `BuildingBlocks.BFF/AuthController.cs` | `auth.proto`, `ICognitoAuthService` | `READY` |
| `GET` | `/api/v1/admin/staff/{id}` | View Staff Profile Card | `[STAFF, MANAGER, ADMIN]` | `true` | `IamTenant` | `GetUser` | `Admin.Bff/Controllers/StaffController.cs` | `iam_tenant.proto`, `GetStaffQuery.cs` | `READY` |
| `POST` | `/api/v1/mail/drafts` | Create Email Draft | `[STAFF, MANAGER]` | `true` | `MailService` | `CreateDraftMessage` | `Staff.Bff/Controllers/MailController.cs` | `mail_platform.proto`, `CreateDraftMessageCommand.cs` | `READY` |
| `GET` | `/api/v1/mail/drafts/{id}` | Get Email Draft | `[STAFF, MANAGER]` | `true` | `MailService` | `GetDraft` | `Staff.Bff/Controllers/MailController.cs` | `mail_platform.proto`, `GetDraftQuery.cs` | `READY` |
| `GET` | `/api/v1/mail/drafts` | List Email Drafts | `[STAFF, MANAGER]` | `true` | `MailService` | `ListDrafts` | `Staff.Bff/Controllers/MailController.cs` | `mail_platform.proto`, `ListDraftsQuery.cs` | `READY` |
| `POST` | `/api/v1/mail/send` | Dispatch Outbound Email | `[STAFF, MANAGER]` | `true` | `MailService` | `SubmitOutboundMessage` | `Staff.Bff/Controllers/MailController.cs` | `mail_platform.proto`, `SubmitOutboundMessageCommand.cs`| `READY` |
| `GET` | `/api/v1/mail/messages/{id}` | Get Processed Email Details | `[STAFF, MANAGER, ADMIN]` | `true` | `MailService` | `GetProcessedMessage` | `Staff.Bff/Controllers/MailController.cs` | `mail_platform.proto`, `GetProcessedMessageQuery.cs` | `READY` |
| `GET` | `/api/v1/mail/messages` | List Processed Emails | `[STAFF, MANAGER, ADMIN]` | `true` | `MailService` | `ListProcessedMessages` | `Staff.Bff/Controllers/MailController.cs` | `mail_platform.proto`, `ListProcessedMessagesQuery.cs` | `READY` |
| `GET` | `/api/v1/mail/quarantine/{id}` | Get Quarantined Email Details | `[MANAGER, ADMIN]` | `true` | `MailService` | `GetQuarantineRecord` | `Staff.Bff/Controllers/MailController.cs` | `mail_platform.proto`, `GetQuarantineRecordQuery.cs` | `READY` |
| `GET` | `/api/v1/mail/quarantine` | List Quarantined Emails | `[MANAGER, ADMIN]` | `true` | `MailService` | `ListQuarantineRecords` | `Staff.Bff/Controllers/MailController.cs` | `mail_platform.proto`, `ListQuarantineRecordsQuery.cs`| `READY` |
| `POST` | `/api/v1/mail/quarantine/{id}/release` | Release Quarantined Email | `[MANAGER, ADMIN]` | `true` | `MailService` | `ReleaseQuarantine` | `Staff.Bff/Controllers/MailController.cs` | `mail_platform.proto`, `ReleaseQuarantineCommand.cs` | `READY` |
| `POST` | `/api/v1/invoices/generate` | Auto-Generate Invoices | `[STAFF, MANAGER]` | `true` | `billing-service` | `GenerateInvoice` | `Staff.Bff/Controllers/BillingController.cs` | `billing.proto`, `GenerateInvoiceUseCase.ts` | `READY` |
| `POST` | `/api/v1/invoices` | Create Manual Invoice | `[STAFF, MANAGER]` | `true` | `billing-service` | `CreateInvoice` | `Staff.Bff/Controllers/BillingController.cs` | `billing.proto`, `billing.service.ts` | `READY` |
| `GET` | `/api/v1/invoices/{id}` | Get Invoice Details | `[STAFF, MANAGER, ADMIN]` | `true` | `billing-service` | `GetInvoiceDetail` | `Staff.Bff/Controllers/BillingController.cs` | `billing.proto`, `billing.service.ts` | `READY` |
| `GET` | `/api/v1/invoices` | List Invoices (Paged) | `[STAFF, MANAGER, ADMIN]` | `true` | `billing-service` | `ListInvoices` | `Staff.Bff/Controllers/BillingController.cs` | `billing.proto`, `billing.service.ts` | `READY` |
| `PATCH`| `/api/v1/invoices/{id}/status` | Update Invoice Status | `[STAFF, MANAGER]` | `true` | `billing-service` | `UpdateInvoiceStatus` | `Staff.Bff/Controllers/BillingController.cs` | `billing.proto`, `billing.service.ts` | `READY` |
| `POST` | `/api/v1/billing/credit-check` | Check Customer Credit Limits | `[STAFF, MANAGER]` | `true` | `billing-service` | `CheckCustomerCredit` | `Staff.Bff/Controllers/BillingController.cs` | `billing.proto`, `billing.service.ts` | `READY` |
| `GET` | `/api/v1/escrow/wallets/{id}` | Get Escrow Wallet Balance | `[STAFF, MANAGER, ADMIN]` | `true` | `billing-service` | `GetWalletBalance` | `Staff.Bff/Controllers/BillingController.cs` | `billing.proto`, `billing.service.ts` | `READY` |
| `POST` | `/api/v1/financial/estimate-cost` | Estimate Freight Cost | `[STAFF, MANAGER]` | `true` | `financial-service` | `EstimateCost` | `Staff.Bff/Controllers/FinancialController.cs` | `financial.proto`, `financial.service.ts` | `READY` |
| `POST` | `/api/v1/financial/customs-duty` | Calculate HS Code Import Duties | `[STAFF, MANAGER]` | `true` | `financial-service` | `GetCustomsDuty` | `Staff.Bff/Controllers/FinancialController.cs` | `financial.proto`, `financial.service.ts` | `READY` |
| `POST` | `/api/v1/chat/message` | Send Customer Support AI Chat | `[STAFF, MANAGER, ADMIN]` | `true` | `customer-assistant`| `handleMessage` | `Staff.Bff/Controllers/ChatController.cs` | REST Bridge, `ConversationalAssistantOrchestrator.ts`| `READY` |

---

## 2. Coverage Summary

### STAFF
- **Exclusive APIs:** 0 (Frontline operations allow supervisory co-management).
- **Shared APIs Accessible:** 52 endpoints (Shipments, Cargo, Tracking, Geofences, Alerts, Notifications, OCR, Compliance, Routes, Mail Drafts/Send, Invoices, Escrow Balance, Freight/Duty Estimates, Chat).
- **Blocked APIs:** 2 (`POST /api/v1/negotiation/offer`, `POST /api/v1/invoices/{id}/payments`).

### MANAGER
- **Exclusive APIs:** 2 (`POST /api/v1/approvals/{id}/approve`, `POST /api/v1/approvals/{id}/reject`).
- **Shared APIs Accessible:** 54 endpoints (All Staff operations + Pending Approvals, Quarantine Release, OCR Review, Alert Resolution).
- **Blocked APIs:** 4 (`POST /api/v1/invoices/{id}/cancel`, `POST /api/v1/invoices/{id}/debit-notes`, `POST /api/v1/invoices/{id}/credit-notes`, `POST /api/v1/negotiation/offer`).

### ADMIN
- **Exclusive APIs:** 21 (Staff User CRUD/Activation/Suspend/Password, Role/Permission Grants, AI Provider Config, Dispatch Rules, Mail Domain/Mailbox/Alias Provisioning, Quarantine Purge, Audit Log, Tenant Compliance Ingestion).
- **Shared APIs Accessible:** 26 endpoints (Read access to Shipments, Tracking, Alerts, Notifications, OCR results, Routes, Quarantine, Invoices, Escrow Balances, Auth).
- **Blocked APIs:** 3 (`POST /api/v1/invoices/{id}/debit-notes`, `POST /api/v1/invoices/{id}/credit-notes`, `GET /api/v1/financial/exchange-rate`).

### SYSTEM
- **Exclusive APIs:** 8 (`POST /api/v1/system/tenants`, `GET /api/v1/system/tenants`, `GET /api/v1/system/tenants/{id}`, `PATCH /api/v1/system/tenants/{id}/status`, `DELETE /api/v1/system/tenants/{id}`, `POST /api/v1/system/compliance/sources`, `POST /api/v1/system/compliance/knowledge`, `POST /api/v1/system/mail/dead-letters/{id}/requeue`).
- **Shared APIs Accessible:** 0 (SYSTEM access is strictly isolated from human tenant web controllers).
- **Blocked APIs:** 1 (`PUT /api/v1/system/tenants/{id}`).

---

## 3. Backend Gaps (Blocked from BFF Implementation)

The following APIs are blocked and omitted from BFF controllers until backend engineering completes the required Protobuf contracts:

1. `PUT /api/v1/system/tenants/{id}`: Requires `UpdateTenant` in `protos/iam_tenant.proto` and `IamGrpcService.cs`.
2. `POST /api/v1/invoices/{id}/payments`: Requires `RecordPayment` in `protos/billing.proto`.
3. `POST /api/v1/invoices/{id}/cancel`: Requires `CancelInvoice` in `protos/billing.proto`.
4. `POST /api/v1/invoices/{id}/debit-notes` & `POST /api/v1/invoices/{id}/credit-notes`: Requires `IssueDebitNote` & `IssueCreditNote` in `protos/billing.proto`.
5. `GET /api/v1/financial/exchange-rate`: Requires `GetExchangeRate` in `protos/financial.proto`.
6. `POST /api/v1/negotiation/offer` & `GET /api/v1/negotiation/session/{id}`: Requires new contract `protos/negotiation.proto`.

---

## 4. Security Review Required Endpoints

> [!CAUTION]
> The following high-privilege endpoints require dedicated security audit review:

1. **Cross-Tenant Operations (`SYSTEM_ADMIN` in `System.Bff`)**:
   - `POST /api/v1/system/tenants`, `PATCH /api/v1/system/tenants/{id}/status`, `DELETE /api/v1/system/tenants/{id}`.
   - *Review Criteria:* Enforce strict IP whitelisting and multi-factor authentication on `System.Bff`.
2. **Staff Identity & Role Permission Administration (`TENANT_ADMIN` in `Admin.Bff`)**:
   - `POST /api/v1/admin/staff/invite`, `POST /api/v1/admin/staff/{id}/roles`, `POST /api/v1/admin/roles/{id}/permissions`.
   - *Review Criteria:* Verify that Tenant Admins cannot grant cross-tenant permissions or assign roles outside their tenant boundary.
3. **Dual-Control Route Approval Gates (`MANAGER` in `Staff.Bff`)**:
   - `POST /api/v1/approvals/{id}/approve`, `POST /api/v1/approvals/{id}/reject`.
   - *Review Criteria:* Verify that the approver's `UserId` is distinct from the route creator's `UserId` (dual-control principle).
4. **Tenant AI Provider & Token Expenditure Configuration (`TENANT_ADMIN` in `Admin.Bff`)**:
   - `PUT /api/v1/admin/ai-config`.
   - *Review Criteria:* Ensure token limit mutations trigger audit log events and validate provider API keys.
5. **Bulk Data Ingestion (`STAFF, MANAGER` in `Staff.Bff`)**:
   - `POST /api/v1/shipments/import`.
   - *Review Criteria:* Enforce rate limiting and max row count validation (capped at 500 rows per request) to prevent denial-of-service.
