# Aurora Platform - Machine-Readable BFF API Implementation Plan

> **Document ID:** `DOC-BFF-PLAN`  
> **Status:** Final Execution Blueprint  
> **Rule of Action:** Actions are strictly one of: `KEEP`, `IMPLEMENT`, `BLOCK`, `INTERNAL_ONLY`, `SECURITY_REVIEW`.

---

## 1. Master API Implementation Plan

| ID | Method | Route | BFF | BFF Actors | Internal Callers | Service | RPC | Gap | Existing File | Planned File | Action |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- | :---: | :--- | :--- | :---: |
| **API-01** | `POST` | `/api/v1/approvals/{id}/approve` | `Staff.Bff` | `[MANAGER]` | `None` | `RoutePlanningAgent` | `ApproveRoute` | `G0` | `Staff.Bff/Controllers/ApprovalsController.cs` | `NONE` | `KEEP` |
| **API-02** | `POST` | `/api/v1/approvals/{id}/reject` | `Staff.Bff` | `[MANAGER]` | `None` | `RoutePlanningAgent` | `RejectRoute` | `G0` | `Staff.Bff/Controllers/ApprovalsController.cs` | `NONE` | `KEEP` |
| **API-03** | `POST` | `/api/v1/admin/staff/invite` | `Admin.Bff` | `[TENANT_ADMIN]` | `None` | `IamTenant` | `InviteUser` | `G0` | `Admin.Bff/Controllers/StaffController.cs` | `NONE` | `KEEP` |
| **API-04** | `GET` | `/api/v1/admin/staff` | `Admin.Bff` | `[TENANT_ADMIN]` | `None` | `IamTenant` | `GetManyUsers` | `G0` | `Admin.Bff/Controllers/StaffController.cs` | `NONE` | `KEEP` |
| **API-05** | `PUT` | `/api/v1/admin/staff/{id}` | `Admin.Bff` | `[TENANT_ADMIN]` | `None` | `IamTenant` | `UpdateUser` | `G0` | `Admin.Bff/Controllers/StaffController.cs` | `NONE` | `KEEP` |
| **API-06** | `POST` | `/api/v1/admin/staff/{id}/activate` | `Admin.Bff` | `[TENANT_ADMIN]` | `None` | `IamTenant` | `ActivateUser` | `G0` | `Admin.Bff/Controllers/StaffController.cs` | `NONE` | `KEEP` |
| **API-07** | `POST` | `/api/v1/admin/staff/{id}/suspend` | `Admin.Bff` | `[TENANT_ADMIN]` | `None` | `IamTenant` | `SuspendUser` | `G0` | `Admin.Bff/Controllers/StaffController.cs` | `NONE` | `KEEP` |
| **API-08** | `POST` | `/api/v1/admin/staff/{id}/reset-password` | `Admin.Bff` | `[TENANT_ADMIN]` | `None` | `IamTenant` | `ResetUserPassword` | `G0` | `Admin.Bff/Controllers/StaffController.cs` | `NONE` | `KEEP` |
| **API-09** | `POST` | `/api/v1/admin/staff/{id}/roles` | `Admin.Bff` | `[TENANT_ADMIN]` | `None` | `IamTenant` | `AssignRoles` | `G0` | `Admin.Bff/Controllers/StaffController.cs` | `NONE` | `KEEP` |
| **API-10** | `GET` | `/api/v1/admin/roles` | `Admin.Bff` | `[TENANT_ADMIN]` | `None` | `IamTenant` | `GetManyRoles` | `G0` | `Admin.Bff/Controllers/RolesController.cs` | `NONE` | `KEEP` |
| **API-11** | `GET` | `/api/v1/admin/roles/{id}` | `Admin.Bff` | `[TENANT_ADMIN]` | `None` | `IamTenant` | `GetRole` | `G0` | `Admin.Bff/Controllers/RolesController.cs` | `NONE` | `KEEP` |
| **API-12** | `POST` | `/api/v1/admin/roles/{id}/permissions` | `Admin.Bff` | `[TENANT_ADMIN]` | `None` | `IamTenant` | `AssignPermissionsToRole` | `G0` | `Admin.Bff/Controllers/RolesController.cs` | `NONE` | `KEEP` |
| **API-13** | `GET` | `/api/v1/admin/ai-config` | `Admin.Bff` | `[TENANT_ADMIN]` | `None` | `RoutePlanningAgent` | `GetTenantAiConfig` | `G0` | `Admin.Bff/Controllers/AiConfigController.cs` | `NONE` | `KEEP` |
| **API-14** | `PUT` | `/api/v1/admin/ai-config` | `Admin.Bff` | `[TENANT_ADMIN]` | `None` | `RoutePlanningAgent` | `UpsertTenantAiConfig` | `G0` | `Admin.Bff/Controllers/AiConfigController.cs` | `NONE` | `KEEP` |
| **API-15** | `GET` | `/api/v1/admin/rules` | `Admin.Bff` | `[TENANT_ADMIN]` | `None` | `RoutePlanningAgent` | `ListTenantRuleConfigs`| `G0` | `Admin.Bff/Controllers/RuleConfigController.cs`| `NONE` | `KEEP` |
| **API-16** | `PUT` | `/api/v1/admin/rules` | `Admin.Bff` | `[TENANT_ADMIN]` | `None` | `RoutePlanningAgent` | `UpsertTenantRuleConfig`| `G0` | `Admin.Bff/Controllers/RuleConfigController.cs`| `NONE` | `KEEP` |
| **API-17** | `POST` | `/api/v1/admin/mail/domains` | `Admin.Bff` | `[TENANT_ADMIN]` | `None` | `MailService` | `ProvisionDomain` | `G0` | `Admin.Bff/Controllers/MailAdminController.cs` | `NONE` | `KEEP` |
| **API-18** | `POST` | `/api/v1/admin/mail/mailboxes` | `Admin.Bff` | `[TENANT_ADMIN]` | `None` | `MailService` | `CreateMailbox` | `G0` | `Admin.Bff/Controllers/MailAdminController.cs` | `NONE` | `KEEP` |
| **API-19** | `POST` | `/api/v1/admin/mail/aliases` | `Admin.Bff` | `[TENANT_ADMIN]` | `None` | `MailService` | `CreateAlias` | `G0` | `Admin.Bff/Controllers/MailAdminController.cs` | `NONE` | `KEEP` |
| **API-20** | `DELETE`| `/api/v1/admin/mail/quarantine/{id}` | `Admin.Bff` | `[TENANT_ADMIN]` | `None` | `MailService` | `DeleteQuarantine` | `G0` | `Admin.Bff/Controllers/MailAdminController.cs` | `NONE` | `KEEP` |
| **API-21** | `GET` | `/api/v1/admin/mail/audit` | `Admin.Bff` | `[TENANT_ADMIN]` | `None` | `MailService` | `GetAuditRecords` | `G0` | `Admin.Bff/Controllers/MailAdminController.cs` | `NONE` | `KEEP` |
| **API-22** | `POST` | `/api/v1/admin/compliance/sources` | `Admin.Bff` | `[TENANT_ADMIN]` | `None` | `RegulatoryCompliance`| `IngestRegulatorySource`| `G0` | `Admin.Bff/Controllers/PlatformIngestionController.cs` | `NONE` | `KEEP` |
| **API-23** | `POST` | `/api/v1/admin/compliance/knowledge` | `Admin.Bff` | `[TENANT_ADMIN]` | `None` | `RegulatoryCompliance`| `IngestKnowledgeDocument`| `G0` | `Admin.Bff/Controllers/PlatformIngestionController.cs` | `NONE` | `KEEP` |
| **API-24** | `POST` | `/api/v1/system/tenants` | `System.Bff` | `[SYSTEM_ADMIN]` | `None` | `IamTenant` | `CreateTenant` | `G0` | `System.Bff/Controllers/TenantsController.cs` | `NONE` | `SECURITY_REVIEW` |
| **API-25** | `GET` | `/api/v1/system/tenants` | `System.Bff` | `[SYSTEM_ADMIN]` | `None` | `IamTenant` | `ListTenants` | `G0` | `System.Bff/Controllers/TenantsController.cs` | `NONE` | `KEEP` |
| **API-26** | `GET` | `/api/v1/system/tenants/{id}` | `System.Bff` | `[SYSTEM_ADMIN]` | `None` | `IamTenant` | `GetTenant` | `G0` | `System.Bff/Controllers/TenantsController.cs` | `NONE` | `KEEP` |
| **API-27** | `PATCH`| `/api/v1/system/tenants/{id}/status` | `System.Bff` | `[SYSTEM_ADMIN]` | `None` | `IamTenant` | `UpdateTenantStatus` | `G0` | `System.Bff/Controllers/TenantsController.cs` | `NONE` | `SECURITY_REVIEW` |
| **API-28** | `DELETE`| `/api/v1/system/tenants/{id}` | `System.Bff` | `[SYSTEM_ADMIN]` | `None` | `IamTenant` | `DeleteTenant` | `G0` | `System.Bff/Controllers/TenantsController.cs` | `NONE` | `SECURITY_REVIEW` |
| **API-29** | `POST` | `/api/v1/system/compliance/sources` | `System.Bff` | `[SYSTEM_ADMIN]` | `None` | `RegulatoryCompliance`| `IngestRegulatorySource`| `G0` | `System.Bff/Controllers/SystemIngestionController.cs` | `NONE` | `KEEP` |
| **API-30** | `POST` | `/api/v1/system/compliance/knowledge` | `System.Bff` | `[SYSTEM_ADMIN]` | `None` | `RegulatoryCompliance`| `IngestKnowledgeDocument`| `G0` | `System.Bff/Controllers/SystemIngestionController.cs` | `NONE` | `KEEP` |
| **API-31** | `POST` | `/api/v1/system/mail/dead-letters/{id}/requeue` | `System.Bff` | `[SYSTEM_ADMIN]` | `None` | `MailService` | `RequeueDeadLetter` | `G0` | `System.Bff/Controllers/MailSystemController.cs` | `NONE` | `KEEP` |
| **API-32** | `POST` | `/api/v1/shipments` | `Staff.Bff` | `[STAFF, MANAGER]` | `None` | `ShipmentWorkflow` | `CreateShipment` | `G1` | `NONE` | `Staff.Bff/Controllers/ShipmentsController.cs` | `IMPLEMENT` |
| **API-33** | `GET` | `/api/v1/shipments/{id}` | `Staff.Bff` | `[STAFF, MANAGER, TENANT_ADMIN]`| `None` | `ShipmentWorkflow` | `GetShipment` | `G1` | `NONE` | `Staff.Bff/Controllers/ShipmentsController.cs` | `IMPLEMENT` |
| **API-34** | `GET` | `/api/v1/shipments` | `Staff.Bff` | `[STAFF, MANAGER, TENANT_ADMIN]`| `None` | `ShipmentWorkflow` | `ListShipments` | `G1` | `NONE` | `Staff.Bff/Controllers/ShipmentsController.cs` | `IMPLEMENT` |
| **API-35** | `PUT` | `/api/v1/shipments/{id}` | `Staff.Bff` | `[STAFF, MANAGER]` | `None` | `ShipmentWorkflow` | `UpdateShipment` | `G1` | `NONE` | `Staff.Bff/Controllers/ShipmentsController.cs` | `IMPLEMENT` |
| **API-36** | `POST` | `/api/v1/shipments/{id}/submit` | `Staff.Bff` | `[STAFF, MANAGER]` | `None` | `ShipmentWorkflow` | `SubmitShipment` | `G1` | `NONE` | `Staff.Bff/Controllers/ShipmentsController.cs` | `IMPLEMENT` |
| **API-37** | `PATCH`| `/api/v1/shipments/{id}/status` | `Staff.Bff` | `[STAFF, MANAGER]` | `[SYSTEM, WORKER]` | `ShipmentWorkflow` | `UpdateShipmentStatus` | `G1` | `NONE` | `Staff.Bff/Controllers/ShipmentsController.cs` | `IMPLEMENT` |
| **API-38** | `POST` | `/api/v1/shipments/{id}/cancel` | `Staff.Bff` | `[STAFF, MANAGER]` | `None` | `ShipmentWorkflow` | `CancelShipment` | `G1` | `NONE` | `Staff.Bff/Controllers/ShipmentsController.cs` | `IMPLEMENT` |
| **API-39** | `DELETE`| `/api/v1/shipments/{id}` | `Staff.Bff` | `[STAFF, MANAGER]` | `None` | `ShipmentWorkflow` | `DeleteDraftShipment` | `G1` | `NONE` | `Staff.Bff/Controllers/ShipmentsController.cs` | `IMPLEMENT` |
| **API-40** | `POST` | `/api/v1/shipments/import` | `Staff.Bff` | `[STAFF, MANAGER]` | `None` | `ShipmentWorkflow` | `ImportShipments` | `G1` | `NONE` | `Staff.Bff/Controllers/ShipmentsController.cs` | `IMPLEMENT` |
| **API-41** | `POST` | `/api/v1/shipments/{id}/cargo` | `Staff.Bff` | `[STAFF, MANAGER]` | `None` | `ShipmentWorkflow` | `AddCargoItem` | `G1` | `NONE` | `Staff.Bff/Controllers/ShipmentsController.cs` | `IMPLEMENT` |
| **API-42** | `PUT` | `/api/v1/shipments/{id}/cargo/{itemId}` | `Staff.Bff` | `[STAFF, MANAGER]` | `None` | `ShipmentWorkflow` | `UpdateCargoItem` | `G1` | `NONE` | `Staff.Bff/Controllers/ShipmentsController.cs` | `IMPLEMENT` |
| **API-43** | `DELETE`| `/api/v1/shipments/{id}/cargo/{itemId}`| `Staff.Bff` | `[STAFF, MANAGER]` | `None` | `ShipmentWorkflow` | `RemoveCargoItem` | `G1` | `NONE` | `Staff.Bff/Controllers/ShipmentsController.cs` | `IMPLEMENT` |
| **API-44** | `POST` | `/api/v1/shipments/{id}/locations` | `Staff.Bff` | `[STAFF, MANAGER]` | `None` | `ShipmentWorkflow` | `AddShipmentLocation` | `G1` | `NONE` | `Staff.Bff/Controllers/ShipmentsController.cs` | `IMPLEMENT` |
| **API-45** | `PUT` | `/api/v1/shipments/{id}/locations/{locId}` | `Staff.Bff` | `[STAFF, MANAGER]` | `None` | `ShipmentWorkflow` | `UpdateShipmentLocation`| `G1` | `NONE` | `Staff.Bff/Controllers/ShipmentsController.cs` | `IMPLEMENT` |
| **API-46** | `DELETE`| `/api/v1/shipments/{id}/locations/{locId}`| `Staff.Bff` | `[STAFF, MANAGER]` | `None` | `ShipmentWorkflow` | `RemoveShipmentLocation`| `G1` | `NONE` | `Staff.Bff/Controllers/ShipmentsController.cs` | `IMPLEMENT` |
| **API-47** | `POST` | `/api/v1/shipments/{id}/documents` | `Staff.Bff` | `[STAFF, MANAGER]` | `None` | `ShipmentWorkflow` | `AttachShipmentDocument`| `G1` | `NONE` | `Staff.Bff/Controllers/ShipmentsController.cs` | `IMPLEMENT` |
| **API-48** | `DELETE`| `/api/v1/shipments/{id}/documents/{docId}` | `Staff.Bff` | `[STAFF, MANAGER]` | `None` | `ShipmentWorkflow` | `RemoveShipmentDocument`| `G1` | `NONE` | `Staff.Bff/Controllers/ShipmentsController.cs` | `IMPLEMENT` |
| **API-49** | `POST` | `/api/v1/shipments/{id}/milestones` | `Staff.Bff` | `[STAFF, MANAGER]` | `[SYSTEM, GpsTracking]` | `ShipmentWorkflow` | `AddShipmentMilestone` | `G1` | `NONE` | `Staff.Bff/Controllers/ShipmentsController.cs` | `IMPLEMENT` |
| **API-50** | `GET` | `/api/v1/shipments/{id}/timeline` | `Staff.Bff` | `[STAFF, MANAGER, TENANT_ADMIN]`| `None` | `ShipmentWorkflow` | `GetShipmentTimeline` | `G1` | `NONE` | `Staff.Bff/Controllers/ShipmentsController.cs` | `IMPLEMENT` |
| **API-51** | `GET` | `/api/v1/tracking/{id}/current` | `Staff.Bff` | `[STAFF, MANAGER, TENANT_ADMIN]`| `None` | `GpsTracking` | `GetCurrentLocation` | `G1` | `NONE` | `Staff.Bff/Controllers/TrackingController.cs` | `IMPLEMENT` |
| **API-52** | `GET` | `/api/v1/tracking/{id}/history` | `Staff.Bff` | `[STAFF, MANAGER, TENANT_ADMIN]`| `None` | `GpsTracking` | `ListPositionHistory` | `G1` | `NONE` | `Staff.Bff/Controllers/TrackingController.cs` | `IMPLEMENT` |
| **API-53** | `POST` | `/api/v1/tracking/geofences` | `Staff.Bff` | `[STAFF, MANAGER, TENANT_ADMIN]`| `None` | `GpsTracking` | `CreateGeofence` | `G1` | `NONE` | `Staff.Bff/Controllers/TrackingController.cs` | `IMPLEMENT` |
| **API-54** | `GET` | `/api/v1/tracking/geofences` | `Staff.Bff` | `[STAFF, MANAGER, TENANT_ADMIN]`| `None` | `GpsTracking` | `ListGeofences` | `G1` | `NONE` | `Staff.Bff/Controllers/TrackingController.cs` | `IMPLEMENT` |
| **API-55** | `PATCH`| `/api/v1/tracking/geofences/{id}/active` | `Staff.Bff` | `[STAFF, MANAGER, TENANT_ADMIN]`| `None` | `GpsTracking` | `SetGeofenceActive` | `G1` | `NONE` | `Staff.Bff/Controllers/TrackingController.cs` | `IMPLEMENT` |
| **API-56** | `GET` | `/api/v1/tracking/alerts` | `Staff.Bff` | `[STAFF, MANAGER, TENANT_ADMIN]`| `None` | `GpsTracking` | `ListMonitoringAlerts` | `G1` | `NONE` | `Staff.Bff/Controllers/TrackingController.cs` | `IMPLEMENT` |
| **API-57** | `POST` | `/api/v1/tracking/alerts/{id}/resolve` | `Staff.Bff` | `[MANAGER, TENANT_ADMIN]` | `None` | `GpsTracking` | `ResolveMonitoringAlert` | `G1` | `NONE` | `Staff.Bff/Controllers/TrackingController.cs` | `IMPLEMENT` |
| **API-58** | `GET` | `/api/v1/notifications` | `Staff.Bff` | `[STAFF, MANAGER, TENANT_ADMIN]`| `None` | `Notification` | `ListNotifications` | `G1` | `NONE` | `Staff.Bff/Controllers/NotificationsController.cs` | `IMPLEMENT` |
| **API-59** | `PATCH`| `/api/v1/notifications/{id}/read` | `Staff.Bff` | `[STAFF, MANAGER, TENANT_ADMIN]`| `None` | `Notification` | `MarkNotificationRead` | `G1` | `NONE` | `Staff.Bff/Controllers/NotificationsController.cs` | `IMPLEMENT` |
| **API-60** | `GET` | `/api/v1/notifications/preferences` | `Staff.Bff` | `[STAFF, MANAGER, TENANT_ADMIN]`| `None` | `Notification` | `ListNotificationPreferences` | `G1` | `NONE` | `Staff.Bff/Controllers/NotificationsController.cs` | `IMPLEMENT` |
| **API-61** | `PUT` | `/api/v1/notifications/preferences` | `Staff.Bff` | `[STAFF, MANAGER, TENANT_ADMIN]`| `None` | `Notification` | `UpsertNotificationPreference`| `G1` | `NONE` | `Staff.Bff/Controllers/NotificationsController.cs` | `IMPLEMENT` |
| **API-62** | `POST` | `/api/v1/compliance/evaluations` | `Staff.Bff` | `[STAFF, MANAGER]` | `[SYSTEM, RoutePlanningAgent]` | `RegulatoryCompliance`| `EvaluateCompliance` | `G1` | `NONE` | `Staff.Bff/Controllers/ComplianceController.cs` | `IMPLEMENT` |
| **API-63** | `GET` | `/api/v1/compliance/evaluations/{id}` | `Staff.Bff` | `[STAFF, MANAGER, TENANT_ADMIN]`| `None` | `RegulatoryCompliance`| `GetComplianceEvaluation` | `G1` | `NONE` | `Staff.Bff/Controllers/ComplianceController.cs` | `IMPLEMENT` |
| **API-64** | `POST` | `/api/v1/compliance/copilot/ask` | `Staff.Bff` | `[STAFF, MANAGER, TENANT_ADMIN]`| `None` | `RegulatoryCompliance`| `GenerateGroundedAnswer` | `G1` | `NONE` | `Staff.Bff/Controllers/ComplianceController.cs` | `IMPLEMENT` |
| **API-65** | `POST` | `/api/v1/invoices/generate` | `Staff.Bff` | `[STAFF, MANAGER]` | `[SYSTEM, WORKER]` | `billing-service` | `GenerateInvoice` | `G1` | `NONE` | `Staff.Bff/Controllers/BillingController.cs` | `IMPLEMENT` |
| **API-66** | `POST` | `/api/v1/invoices` | `Staff.Bff` | `[STAFF, MANAGER]` | `None` | `billing-service` | `CreateInvoice` | `G1` | `NONE` | `Staff.Bff/Controllers/BillingController.cs` | `IMPLEMENT` |
| **API-67** | `GET` | `/api/v1/invoices/{id}` | `Staff.Bff` | `[STAFF, MANAGER, TENANT_ADMIN]`| `None` | `billing-service` | `GetInvoiceDetail` | `G1` | `NONE` | `Staff.Bff/Controllers/BillingController.cs` | `IMPLEMENT` |
| **API-68** | `GET` | `/api/v1/invoices` | `Staff.Bff` | `[STAFF, MANAGER, TENANT_ADMIN]`| `None` | `billing-service` | `ListInvoices` | `G1` | `NONE` | `Staff.Bff/Controllers/BillingController.cs` | `IMPLEMENT` |
| **API-69** | `PATCH`| `/api/v1/invoices/{id}/status` | `Staff.Bff` | `[STAFF, MANAGER]` | `[SYSTEM, WORKER]` | `billing-service` | `UpdateInvoiceStatus` | `G1` | `NONE` | `Staff.Bff/Controllers/BillingController.cs` | `IMPLEMENT` |
| **API-70** | `POST` | `/api/v1/billing/credit-check` | `Staff.Bff` | `[STAFF, MANAGER]` | `[SYSTEM, BillingService]` | `billing-service` | `CheckCustomerCredit` | `G1` | `NONE` | `Staff.Bff/Controllers/BillingController.cs` | `IMPLEMENT` |
| **API-71** | `GET` | `/api/v1/escrow/wallets/{id}` | `Staff.Bff` | `[STAFF, MANAGER, TENANT_ADMIN]`| `None` | `billing-service` | `GetWalletBalance` | `G1` | `NONE` | `Staff.Bff/Controllers/BillingController.cs` | `IMPLEMENT` |
| **API-72** | `POST` | `/api/v1/financial/estimate-cost` | `Staff.Bff` | `[STAFF, MANAGER]` | `[SYSTEM, BillingService]` | `financial-service` | `EstimateCost` | `G1` | `NONE` | `Staff.Bff/Controllers/FinancialController.cs` | `IMPLEMENT` |
| **API-73** | `POST` | `/api/v1/financial/customs-duty` | `Staff.Bff` | `[STAFF, MANAGER]` | `None` | `financial-service` | `GetCustomsDuty` | `G1` | `NONE` | `Staff.Bff/Controllers/FinancialController.cs` | `IMPLEMENT` |
| **API-74** | `PUT` | `/api/v1/system/tenants/{id}` | `System.Bff` | `[SYSTEM_ADMIN]` | `None` | `IamTenant` | *Missing* (`UpdateTenant`) | `G2` | `NONE` | `NONE` | `BLOCK` |
| **API-75** | `POST` | `/api/v1/invoices/{id}/payments` | `Staff.Bff` | `[STAFF, MANAGER]` | `None` | `billing-service` | *Missing* (`RecordPayment`) | `G2` | `NONE` | `NONE` | `BLOCK` |
| **API-76** | `POST` | `/api/v1/invoices/{id}/cancel` | `Staff.Bff` | `[MANAGER, TENANT_ADMIN]` | `None` | `billing-service` | *Missing* (`CancelInvoice`) | `G2` | `NONE` | `NONE` | `BLOCK` |
| **API-77** | `POST` | `/api/v1/invoices/{id}/debit-notes` | `Staff.Bff` | `[MANAGER, TENANT_ADMIN]` | `None` | `billing-service` | *Missing* (`IssueDebitNote`)| `G2` | `NONE` | `NONE` | `BLOCK` |
| **API-78** | `POST` | `/api/v1/invoices/{id}/credit-notes`| `Staff.Bff` | `[MANAGER, TENANT_ADMIN]` | `None` | `billing-service` | *Missing* (`IssueCreditNote`)| `G2` | `NONE` | `NONE` | `BLOCK` |
| **API-79** | `GET` | `/api/v1/financial/exchange-rate` | `Staff.Bff` | `[STAFF, MANAGER, TENANT_ADMIN]`| `None` | `financial-service` | *Missing* (`GetExchangeRate`)| `G2` | `NONE` | `NONE` | `BLOCK` |
| **API-80** | `POST` | `/api/v1/negotiation/offer` | `Staff.Bff` | `[STAFF, MANAGER]` | `None` | `negotiation-agent` | *Missing* (`SubmitOffer`) | `G2` | `NONE` | `NONE` | `BLOCK` |
| **API-81** | `GET` | `/api/v1/negotiation/session/{id}` | `Staff.Bff` | `[STAFF, MANAGER, TENANT_ADMIN]`| `None` | `negotiation-agent` | *Missing* (`GetSessionHistory`)| `G2` | `NONE` | `NONE` | `BLOCK` |
| **API-82** | `POST` | `/api/v1/compliance/route-check` | `Staff.Bff` | `[STAFF, MANAGER]` | `[SYSTEM, RoutePlanningAgent]` | `RegulatoryCompliance`| *Mismatch* | `G4` | `NONE` | `NONE` | `BLOCK` |
| **M2M-01** | `RPC` | `IngestPosition` | `Internal` | `None` | `[SYSTEM, IOT_GATEWAY]` | `GpsTracking` | `IngestPosition` | `G5` | `src/dotnet/GpsTracking/GrpcServices/GpsTrackingGrpcService.cs` | `NONE` | `INTERNAL_ONLY` |
| **M2M-02** | `RPC` | `Generate` / `Embed` | `Internal` | `None` | `[SYSTEM, RoutePlanningAgent, Compliance]` | `ai-governance` | `Generate` / `Embed` | `G5` | `src/java/ai-governance/.../AiExecutionGrpcHandler.java` | `NONE` | `INTERNAL_ONLY` |
| **M2M-03** | `RPC` | `ExecutePolicy` | `Internal` | `None` | `[SYSTEM, ai-governance]` | `ai-governance` | `ExecutePolicy` | `G5` | `src/java/ai-governance/.../PolicyGrpcHandler.java` | `NONE` | `INTERNAL_ONLY` |
| **M2M-04** | `RPC` | `FreezeEscrowAmount` | `Internal` | `None` | `[SYSTEM, WORKER]` | `billing-service` | `FreezeEscrowAmount` | `G5` | `src/nestjs/billing-service/.../billing.controller.ts` | `NONE` | `INTERNAL_ONLY` |
| **M2M-05** | `RPC` | `ReleaseEscrowAmount` | `Internal` | `None` | `[SYSTEM, WORKER]` | `billing-service` | `ReleaseEscrowAmount` | `G5` | `src/nestjs/billing-service/.../billing.controller.ts` | `NONE` | `INTERNAL_ONLY` |
| **M2M-06** | `RPC` | `RefundEscrowAmount` | `Internal` | `None` | `[SYSTEM, WORKER]` | `billing-service` | `RefundEscrowAmount` | `G5` | `src/nestjs/billing-service/.../billing.controller.ts` | `NONE` | `INTERNAL_ONLY` |
| **M2M-07** | `RPC` | `UpdateShipmentDocumentOcr` | `Internal` | `None` | `[SYSTEM, WORKER]` | `ShipmentWorkflow` | `UpdateShipmentDocumentOcr` | `G5` | `src/dotnet/ShipmentWorkflow/GrpcServices/ShipmentGrpcService.cs` | `NONE` | `INTERNAL_ONLY` |
| **M2M-08** | `RPC` | `ValidateGroundedEvidence` | `Internal` | `None` | `[SYSTEM, Compliance]` | `RegulatoryCompliance`| `ValidateGroundedEvidence` | `G5` | `src/dotnet/RegulatoryCompliance/.../RegulatoryComplianceGrpcService.cs` | `NONE` | `INTERNAL_ONLY` |
| **M2M-09** | `RPC` | `GetMinAcceptableRate` | `Internal` | `None` | `[SYSTEM, NegotiationAgent]` | `financial-service` | `GetMinAcceptableRate` | `G5` | `src/nestjs/financial-service/.../financial.controller.ts` | `NONE` | `INTERNAL_ONLY` |
| **M2M-10** | `RPC` | `IngestAlert` | `Internal` | `None` | `[SYSTEM, Prometheus/Loki]` | `devops-agent` | `IngestAlert` | `G5` | `src/java/devops-agent/.../IngestionGrpcHandler.java` | `NONE` | `INTERNAL_ONLY` |

---

## 2. Execution Summary by Action Category

- **`KEEP`**: 28 verified G0 endpoints.
- **`IMPLEMENT`**: 30 verified G1 endpoints ready for immediate production mapping in `Staff.Bff`.
- **`BLOCK`**: 9 uncontracted G2/G4 endpoints deferred until backend contracts are committed.
- **`INTERNAL_ONLY`**: 10 G5 capabilities sealed behind inter-service mTLS and event queues.
- **`SECURITY_REVIEW`**: 3 tenant lifecycle management endpoints (`CreateTenant`, `UpdateTenantStatus`, `DeleteTenant`) flagged for elevated security audit.
