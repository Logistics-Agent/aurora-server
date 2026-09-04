# Aurora Platform — Frontend Integration & Contract Implementation Status

> **Document ID:** `DOC-FE-STATUS`  
> **Status:** Canonical Implementation Status Matrix  
> **Audited Against:** Frontend Contracts, .NET 10 BFF Controllers, Protobuf Definitions, Microservices.

---

## 1. Feature Implementation Matrix

| Feature | UI Component / View | BFF Endpoint | Downstream RPC | Backend Implementation | Status |
|---|---|---|---|---|:---:|
| **Authentication: Login** | Login Form (`/login`) | `POST /api/v1/auth/login` | `AuthService.Login` | `IamTenant` / Cognito | `READY` |
| **Authentication: Identity** | Bootstrap Session Check | `GET /api/v1/auth/me` | `AuthService.GetCurrentUser` | `IamTenant` / JWT Claims | `READY` |
| **Notifications: FCM Web** | Browser Web Push / Toast | `POST /api/v1/notifications/devices` | `NotificationService.RegisterDevice` | `Notification` (EF Core / FCM) | `READY` |
| **Notifications: List** | Header Bell Dropdown | `GET /api/v1/notifications` | `NotificationService.ListNotifications` | `Notification` | `READY` |
| **Admin: Staff Directory** | Admin Console -> Users | `GET /api/v1/admin/staff` | `IamService.GetManyUsers` | `IamTenant` | `READY` |
| **Admin: Staff Invite** | Admin Console -> Invite Modal | `POST /api/v1/admin/staff` | `IamService.InviteUser` | `IamTenant` / Cognito | `READY` |
| **Admin: Change Role** | Admin Console -> Role Selector | `PATCH /api/v1/admin/staff/{id}/role` | `IamService.UpdateUserRole` | `IamTenant` | `READY` |
| **Admin: Set Capabilities** | Admin Console -> Permission Matrix | `PUT /api/v1/admin/staff/{id}/permissions` | `IamService.SetUserPermissions` | `IamTenant` | `READY` |
| **Admin: AI Automation Policy** | Admin Console -> AI Governance | `PUT /api/v1/admin/ai-configs/{feature}` | `RoutePlanningService.UpsertTenantAiConfig` | `RoutePlanningAgent` | `READY` |
| **Admin: Route Risk Rules** | Admin Console -> Dispatch Rules | `PUT /api/v1/admin/rule-configs/{ruleName}` | `RoutePlanningService.UpsertTenantRuleConfig` | `RoutePlanningAgent` | `READY` |
| **Admin: Mail Domains** | Admin Console -> Domain List | `GET /api/v1/admin/mail/domains` | `MailManagement.ListDomains` | Missing in Proto & Service | `BACKEND_REQUIRED` |
| **Admin: Mail Domain Legacy** | Admin Console -> Domain Modal | `POST /api/v1/admin/mail/domains` | `MailManagement.ProvisionDomain` | `MailService` | `CURRENT_LEGACY` |
| **Admin: Shared Mailboxes** | Admin Console -> Mailboxes | `POST /api/v1/admin/mail/mailboxes` | `MailManagement.CreateMailbox` | `MailService` | `READY` |
| **Admin: Default Shared Mailbox**| Admin Console -> Default Badge | `TenantMailConfig.DefaultMailboxId` | N/A | Missing in DB & Proto | `BACKEND_REQUIRED` |
| **Admin: Mail Aliases** | Admin Console -> Aliases Drawer | `POST /api/v1/admin/mail/aliases` | `MailManagement.CreateAlias` | `MailService` | `READY (Refactor to 1:1 Target)` |
| **Admin: Quarantine Purge** | Admin Console -> Quarantine List | `DELETE /api/v1/admin/mail/quarantine/{id}` | `MailSecurity.DeleteQuarantine` | `MailService` | `READY` |
| **Admin: Mail Audit Trail** | Admin Console -> Mail Audit | `GET /api/v1/admin/mail/audit` | `MailManagement.GetAuditRecords` | `MailService` | `READY` |
| **Staff Mail: Triage Queues** | Operations Workspace -> Mail | `GET /api/v1/mail/threads` | `MailSecurity.ListThreads` | `MailService` | `READY` |
| **Staff Mail: Claim Thread** | Operations Workspace -> Take Button| `POST /api/v1/mail/threads/{id}/claim` | `MailSecurity.ClaimThread` | `MailService` (Version Lock) | `READY` |
| **Staff Mail: Reassign Thread** | Operations Workspace -> Reassign Modal | `POST /api/v1/mail/threads/{id}/reassign` | `MailSecurity.ReassignThread` | `MailService` | `READY` |
| **Staff Mail: Unassign Thread** | Operations Workspace -> Return Modal | `POST /api/v1/mail/threads/{id}/unassign` | `MailSecurity.UnassignThread` | `MailService` | `READY` |
| **Staff Mail: Draft Email** | Operations Workspace -> Composer | `POST /api/v1/mail/drafts` | `MailSecurity.CreateDraftMessage` | `MailService` | `READY` |
| **Staff Mail: Send Outbound** | Operations Workspace -> Send Button| `POST /api/v1/mail/messages/outbound` | `MailSecurity.SubmitOutboundMessage` | `MailService` (Stalwart Relay) | `READY` |
| **Staff Mail: Quarantine Release**| Operations Workspace -> Security | `POST /api/v1/mail/quarantine/{id}/release` | `MailSecurity.ReleaseQuarantine` | `MailService` | `READY` |
| **Shipments: List & Filter** | Operations Workspace -> Shipments | `GET /api/v1/shipments` | `ShipmentWorkflowService.ListShipments` | `ShipmentWorkflow` | `READY` |
| **Shipments: Create & Submit** | Operations Workspace -> New Shipment | `POST /api/v1/shipments`, `/submit` | `ShipmentWorkflowService.SubmitShipment` | `ShipmentWorkflow` | `READY` |
| **Routes: Plan & Optimize** | Operations Workspace -> Route Map | `POST /api/v1/routes`, `/optimize` | `RoutePlanningService.OptimizeRoute` | `RoutePlanningAgent` | `READY` |
| **Routes: Risk Approval** | Operations Workspace -> Approvals | `POST /api/v1/approvals/{id}/approve` | `RoutePlanningService.ApproveRoute` | `RoutePlanningAgent` | `READY` |
| **OCR: Document Ingestion** | Operations Workspace -> Documents | `POST /api/v1/documents/shipment` | `DocumentOcrService.SubmitOcrJob` | `DocumentOcr` | `READY` |
| **OCR: Review Disputed Data** | Operations Workspace -> OCR Review | `POST /api/v1/documents/jobs/{id}/review` | `DocumentOcrService.ReviewOcrJob` | `DocumentOcr` | `READY` |
| **Compliance: Evaluation** | Operations Workspace -> Compliance | `POST /api/v1/compliance/evaluations` | `RegulatoryComplianceService.EvaluateCompliance` | `RegulatoryCompliance` | `READY` |
| **Tracking: Live GPS Fleet** | Operations Workspace -> Tracking | `GET /api/v1/tracking/{id}/current` | `GpsTrackingService.GetCurrentLocation` | `GpsTracking` | `READY` |
| **Billing: Generate Invoice** | Operations Workspace -> Billing | `POST /api/v1/invoices/generate` | `BillingService.GenerateInvoice` | `billing-service` | `READY` |
| **Billing: Escrow Lock** | Operations Workspace -> Escrow | `POST /api/v1/escrow/lock` | `BillingService.LockEscrow` | `billing-service` | `READY` |
| **Negotiations: AI Draft** | Operations Workspace -> Negotiation | `POST /api/v1/negotiations/{id}/mail-draft` | `NegotiationService.GetDraftSuggestion` + Mail | `negotiation-agent-service` | `READY` |
