# Aurora Platform — BFF Master API Traceability Matrix

> **Document ID:** `DOC-BFF-MATRIX`  
> **Status:** Canonical End-to-End Traceability Matrix (Synchronized with C# BFF Source)  
> **Scope:** Comprehensive mapping of all implemented BFF REST endpoints, authorization scopes, direct capability permissions, and backing gRPC microservices.  
> **Source Precedence:** Source Code & Protos > docs/technical/frontend > docs/bff-api > Figma UI Specs.

---

## 1. Master Traceability Matrix

| Method | Endpoint | Purpose | Required Capability Permission | Scope | Gateway BFF | Backing Service | gRPC RPC | Status |
|---|---|---|---|---|---|---|---|:---:|
| **Authentication & Profile** |
| `POST` | `/api/v1/auth/identify` | Step 1 login: verify account | `AllowAnonymous` | Public | `Staff.Bff` | `IamTenant` | `AuthService.IdentifyUser` | `CURRENT` |
| `POST` | `/api/v1/auth/login` | Authenticate & issue cookies | `AllowAnonymous` | Public | `Staff.Bff` | `IamTenant` | `AuthService.Login` | `CURRENT` |
| `POST` | `/api/v1/auth/refresh` | Rotate access token cookie | Cookie Session | User | `Staff.Bff` | `IamTenant` | `AuthService.RefreshToken` | `CURRENT` |
| `POST` | `/api/v1/auth/logout` | Revoke user session | Cookie Session | User | `Staff.Bff` | `IamTenant` | `AuthService.Logout` | `CURRENT` |
| `GET` | `/api/v1/auth/me` | Current user profile & capabilities | Cookie Session | User | `Staff.Bff` | `IamTenant` | `AuthService.GetCurrentUser` | `CURRENT` |
| `POST` | `/api/v1/auth/forgot-password` | Request password reset code | `AllowAnonymous` | Public | `Staff.Bff` | `IamTenant` | `AuthService.ForgotPassword` | `CURRENT` |
| `POST` | `/api/v1/auth/confirm-forgot-password` | Confirm new password | `AllowAnonymous` | Public | `Staff.Bff` | `IamTenant` | `AuthService.ConfirmForgotPassword` | `CURRENT` |
| `POST` | `/api/v1/auth/complete-invitation` | Set initial invited password | `AllowAnonymous` | Public | `Staff.Bff` | `IamTenant` | `AuthService.CompleteInvitation` | `CURRENT` |
| **Tenant Admin Console (`Admin.Bff`)** |
| `POST` | `/api/v1/admin/staff` | Onboard staff member | `iam:user:invite` | Tenant | `Admin.Bff` | `IamTenant` | `IamService.InviteUser` | `CURRENT` |
| `GET` | `/api/v1/admin/staff` | List staff directory | `iam:user:read` | Tenant | `Admin.Bff` | `IamTenant` | `IamService.GetManyUsers` | `CURRENT` |
| `GET` | `/api/v1/admin/staff/{id}` | Get staff profile | `iam:user:read` | Tenant | `Admin.Bff` | `IamTenant` | `IamService.GetUser` | `CURRENT` |
| `PUT` | `/api/v1/admin/staff/{id}` | Update staff profile | `iam:user:update` | Tenant | `Admin.Bff` | `IamTenant` | `IamService.UpdateUser` | `CURRENT` |
| `PATCH`| `/api/v1/admin/staff/{id}/role` | Change base persona role | `iam:role:manage` | Tenant | `Admin.Bff` | `IamTenant` | `IamService.UpdateUserRole` | `CURRENT` |
| `PUT` | `/api/v1/admin/staff/{id}/permissions` | Set direct capabilities | `iam:permission:manage` | Tenant | `Admin.Bff` | `IamTenant` | `IamService.SetUserPermissions` | `CURRENT` |
| `POST` | `/api/v1/admin/staff/{id}/permissions/bulk-assign` | Bulk add permissions | `iam:permission:manage` | Tenant | `Admin.Bff` | `IamTenant` | `IamService.BulkAssignPermissions` | `CURRENT` |
| `POST` | `/api/v1/admin/staff/{id}/permissions/bulk-revoke` | Bulk revoke permissions | `iam:permission:manage` | Tenant | `Admin.Bff` | `IamTenant` | `IamService.BulkRevokePermissions` | `CURRENT` |
| `DELETE`| `/api/v1/admin/staff/{id}` | Deactivate staff member | `iam:user:delete` | Tenant | `Admin.Bff` | `IamTenant` | `IamService.DeleteUser` | `CURRENT` |
| `GET` | `/api/v1/admin/roles` | List canonical base roles | `iam:role:read` | Platform | `Admin.Bff` | Static | N/A | `CURRENT` |
| `GET` | `/api/v1/admin/roles/{code}` | Get role template | `iam:role:read` | Platform | `Admin.Bff` | Static | N/A | `CURRENT` |
| `GET` | `/api/v1/admin/ai-configs/{feature}` | Get AI automation policy | `route_planning:policy:manage` | Tenant | `Admin.Bff` | `RoutePlanningAgent` | `RoutePlanningService.GetTenantAiConfig` | `CURRENT` |
| `PUT` | `/api/v1/admin/ai-configs/{feature}` | Upsert AI automation policy | `route_planning:policy:manage` | Tenant | `Admin.Bff` | `RoutePlanningAgent` | `RoutePlanningService.UpsertTenantAiConfig` | `CURRENT` |
| `GET` | `/api/v1/admin/rule-configs` | List risk rule thresholds | `route_planning:policy:manage` | Tenant | `Admin.Bff` | `RoutePlanningAgent` | `RoutePlanningService.ListTenantRuleConfigs` | `CURRENT` |
| `PUT` | `/api/v1/admin/rule-configs/{ruleName}` | Upsert risk rule threshold | `route_planning:policy:manage` | Tenant | `Admin.Bff` | `RoutePlanningAgent` | `RoutePlanningService.UpsertTenantRuleConfig` | `CURRENT` |
| `POST` | `/api/v1/admin/mail/domains` | Provision mail domain *(Legacy)* | `mail:domain:manage` | Tenant | `Admin.Bff` | `MailService` | `MailManagement.ProvisionDomain` | `CURRENT_LEGACY` |
| `GET` | `/api/v1/admin/mail/domains` | List assigned mail domains | `mail:domain:manage` | Tenant | `Admin.Bff` | `MailService` | `MailManagement.ListDomains` | `TARGET (BACKEND_REQUIRED)` |
| `POST` | `/api/v1/admin/mail/mailboxes` | Create shared mailbox | `mail:mailbox:manage` | Tenant | `Admin.Bff` | `MailService` | `MailManagement.CreateMailbox` | `CURRENT` |
| `POST` | `/api/v1/admin/mail/aliases` | Create email alias | `mail:mailbox:manage` | Tenant | `Admin.Bff` | `MailService` | `MailManagement.CreateAlias` | `CURRENT` |
| `DELETE`| `/api/v1/admin/mail/quarantine/{id}` | Permanently delete threat | `mail:quarantine:delete` | Tenant | `Admin.Bff` | `MailService` | `MailSecurity.DeleteQuarantine` | `CURRENT` |
| `GET` | `/api/v1/admin/mail/audit` | Query mail security audit | `mail:audit:read` | Tenant | `Admin.Bff` | `MailService` | `MailManagement.GetAuditRecords` | `CURRENT` |
| `POST` | `/api/v1/admin/ingestion/regulatory-sources` | Ingest regulation doc | `compliance:platform:ingest` | Tenant | `Admin.Bff` | `RegulatoryCompliance` | `RegulatoryComplianceService.IngestRegulatorySource` | `CURRENT` |
| `POST` | `/api/v1/admin/ingestion/knowledge-documents` | Ingest SOP doc | `compliance:platform:ingest` | Tenant | `Admin.Bff` | `RegulatoryCompliance` | `RegulatoryComplianceService.IngestKnowledgeDocument` | `CURRENT` |
| `GET` | `/api/v1/admin/audit-logs` | Query tenant audit logs | `TENANT_ADMIN` role | Tenant | `Admin.Bff` | `AuditLogService` | `AuditLogService.GetAdminAuditLogs` | `CURRENT` |
| **Operations Workspace (`Staff.Bff`)** |
| `POST` | `/api/v1/shipments` | Create draft shipment | `shipments:create` | Tenant | `Staff.Bff` | `ShipmentWorkflow` | `ShipmentWorkflowService.CreateShipment` | `CURRENT` |
| `GET` | `/api/v1/shipments` | List shipments | `shipments:read` | Tenant | `Staff.Bff` | `ShipmentWorkflow` | `ShipmentWorkflowService.ListShipments` | `CURRENT` |
| `GET` | `/api/v1/shipments/{id}` | Get shipment details | `shipments:read` | Tenant | `Staff.Bff` | `ShipmentWorkflow` | `ShipmentWorkflowService.GetShipment` | `CURRENT` |
| `PUT` | `/api/v1/shipments/{id}` | Update shipment | `shipments:update` | Tenant | `Staff.Bff` | `ShipmentWorkflow` | `ShipmentWorkflowService.UpdateShipment` | `CURRENT` |
| `DELETE`| `/api/v1/shipments/{id}` | Delete draft shipment | `shipments:delete` | Tenant | `Staff.Bff` | `ShipmentWorkflow` | `ShipmentWorkflowService.DeleteShipment` | `CURRENT` |
| `POST` | `/api/v1/shipments/{id}/submit` | Submit shipment | `shipments:submit` | Tenant | `Staff.Bff` | `ShipmentWorkflow` | `ShipmentWorkflowService.SubmitShipment` | `CURRENT` |
| `POST` | `/api/v1/shipments/{id}/cancel` | Cancel shipment | `shipments:cancel` | Tenant | `Staff.Bff` | `ShipmentWorkflow` | `ShipmentWorkflowService.CancelShipment` | `CURRENT` |
| `POST` | `/api/v1/shipments/{id}/milestones` | Add delivery milestone | `shipments:milestones:update` | Tenant | `Staff.Bff` | `ShipmentWorkflow` | `ShipmentWorkflowService.UpdateMilestone` | `CURRENT` |
| `POST` | `/api/v1/shipments/{id}/documents` | Attach OCR document | `documents:attach` | Tenant | `Staff.Bff` | `ShipmentWorkflow` | `ShipmentWorkflowService.AttachDocument` | `CURRENT` |
| `GET` | `/api/v1/shipments/{id}/events` | Shipment event timeline | `shipments:read` | Tenant | `Staff.Bff` | `ShipmentWorkflow` | `ShipmentWorkflowService.GetShipmentEvents` | `CURRENT` |
| `POST` | `/api/v1/routes` | Create route proposal | `route_planning:create` | Tenant | `Staff.Bff` | `RoutePlanningAgent` | `RoutePlanningService.CreateRoute` | `CURRENT` |
| `GET` | `/api/v1/routes` | List routes | `route_planning:read` | Tenant | `Staff.Bff` | `RoutePlanningAgent` | `RoutePlanningService.ListRoutes` | `CURRENT` |
| `GET` | `/api/v1/routes/{id}` | Get route details | `route_planning:read` | Tenant | `Staff.Bff` | `RoutePlanningAgent` | `RoutePlanningService.GetRoute` | `CURRENT` |
| `PUT` | `/api/v1/routes/{id}` | Update route | `route_planning:update` | Tenant | `Staff.Bff` | `RoutePlanningAgent` | `RoutePlanningService.UpdateRoute` | `CURRENT` |
| `DELETE`| `/api/v1/routes/{id}` | Delete route | `route_planning:delete` | Tenant | `Staff.Bff` | `RoutePlanningAgent` | `RoutePlanningService.DeleteRoute` | `CURRENT` |
| `POST` | `/api/v1/routes/{id}/stops` | Add stop | `route_planning:update` | Tenant | `Staff.Bff` | `RoutePlanningAgent` | `RoutePlanningService.AddStop` | `CURRENT` |
| `DELETE`| `/api/v1/routes/{id}/stops/{stopId}` | Remove stop | `route_planning:update` | Tenant | `Staff.Bff` | `RoutePlanningAgent` | `RoutePlanningService.RemoveStop` | `CURRENT` |
| `POST` | `/api/v1/routes/{id}/optimize` | Optimize waypoint order | `route_planning:optimize` | Tenant | `Staff.Bff` | `RoutePlanningAgent` | `RoutePlanningService.OptimizeRoute` | `CURRENT` |
| `POST` | `/api/v1/routes/{id}/evaluate-risk` | Evaluate route risk | `route_planning:risk:evaluate` | Tenant | `Staff.Bff` | `RoutePlanningAgent` | `RoutePlanningService.EvaluateRisk` | `CURRENT` |
| `POST` | `/api/v1/routes/{id}/dispatch` | Dispatch route | `route_planning:dispatch` | Tenant | `Staff.Bff` | `RoutePlanningAgent` | `RoutePlanningService.DispatchRoute` | `CURRENT` |
| `GET` | `/api/v1/approvals` | List pending route approvals | `route_planning:approve` | Tenant | `Staff.Bff` | `RoutePlanningAgent` | `RoutePlanningService.ListApprovalRequests` | `CURRENT` |
| `POST` | `/api/v1/approvals/{id}/approve` | Approve high-risk route | `route_planning:approve` | Approval | `Staff.Bff` | `RoutePlanningAgent` | `RoutePlanningService.ApproveRoute` | `CURRENT` |
| `POST` | `/api/v1/approvals/{id}/reject` | Reject high-risk route | `route_planning:approve` | Approval | `Staff.Bff` | `RoutePlanningAgent` | `RoutePlanningService.RejectRoute` | `CURRENT` |
| `GET` | `/api/v1/mail/threads` | List operational threads | `mail:read` / `mail:thread:read_all` | Queue | `Staff.Bff` | `MailService` | `MailSecurity.ListThreads` | `CURRENT` |
| `GET` | `/api/v1/mail/threads/{id}` | Get thread conversation | `mail:read` | Thread | `Staff.Bff` | `MailService` | `MailSecurity.GetThread` | `CURRENT` |
| `POST` | `/api/v1/mail/threads/{id}/claim` | Atomically claim thread | `mail:thread:claim` | Thread | `Staff.Bff` | `MailService` | `MailSecurity.ClaimThread` | `CURRENT` |
| `POST` | `/api/v1/mail/threads/{id}/reassign` | Reassign thread to staff | `mail:thread:reassign` | Thread | `Staff.Bff` | `MailService` | `MailSecurity.ReassignThread` | `CURRENT` |
| `POST` | `/api/v1/mail/threads/{id}/unassign` | Release thread to unassigned | `mail:thread:unassign` | Thread | `Staff.Bff` | `MailService` | `MailSecurity.UnassignThread` | `CURRENT` |
| `GET` | `/api/v1/mail/threads/{id}/assignment-history` | Thread assignment history | `mail:read` | Thread | `Staff.Bff` | `MailService` | `MailSecurity.GetThreadAssignmentHistory` | `CURRENT` |
| `POST` | `/api/v1/mail/drafts` | Create / save draft | `mail:draft:create` | Mailbox | `Staff.Bff` | `MailService` | `MailSecurity.CreateDraftMessage` | `CURRENT` |
| `GET` | `/api/v1/mail/drafts` | List drafts | `mail:read` | Mailbox | `Staff.Bff` | `MailService` | `MailSecurity.ListDrafts` | `CURRENT` |
| `GET` | `/api/v1/mail/drafts/{id}` | Get draft revision | `mail:read` | Draft | `Staff.Bff` | `MailService` | `MailSecurity.GetDraft` | `CURRENT` |
| `POST` | `/api/v1/mail/messages/outbound` | Send outbound email | `mail:send` | Mailbox | `Staff.Bff` | `MailService` | `MailSecurity.SubmitOutboundMessage` | `CURRENT` |
| `GET` | `/api/v1/mail/messages` | List processed messages | `mail:read` | Mailbox | `Staff.Bff` | `MailService` | `MailSecurity.ListProcessedMessages` | `CURRENT` |
| `GET` | `/api/v1/mail/messages/{id}` | Get processed message | `mail:read` | Message | `Staff.Bff` | `MailService` | `MailSecurity.GetProcessedMessage` | `CURRENT` |
| `GET` | `/api/v1/mail/quarantine` | List quarantine records | `mail:quarantine:read` | Tenant | `Staff.Bff` | `MailService` | `MailSecurity.ListQuarantineRecords` | `CURRENT` |
| `GET` | `/api/v1/mail/quarantine/{id}` | Get quarantine threat | `mail:quarantine:read` | Tenant | `Staff.Bff` | `MailService` | `MailSecurity.GetQuarantineRecord` | `CURRENT` |
| `POST` | `/api/v1/mail/quarantine/{id}/release` | Release quarantine message | `mail:quarantine:release` | Tenant | `Staff.Bff` | `MailService` | `MailSecurity.ReleaseQuarantine` | `CURRENT` |
| `POST` | `/api/v1/notifications/devices` | Register browser FCM token | `notifications:access` | User | `Staff.Bff` | `Notification` | `NotificationService.RegisterDevice` | `CURRENT` |
| `DELETE`| `/api/v1/notifications/devices/{id}` | Remove FCM device | `notifications:access` | User | `Staff.Bff` | `Notification` | `NotificationService.RemoveDevice` | `CURRENT` |
| `POST` | `/api/v1/notifications/subscriptions/shipments/{shipmentId}` | Subscribe to shipment | `notifications:access` | User | `Staff.Bff` | `Notification` | `NotificationService.SubscribeShipment` | `CURRENT` |
| `GET` | `/api/v1/notifications` | List notifications | `notifications:access` | User | `Staff.Bff` | `Notification` | `NotificationService.ListNotifications` | `CURRENT` |
| `GET` | `/api/v1/notifications/unread-count` | Get unread count | `notifications:access` | User | `Staff.Bff` | `Notification` | `NotificationService.GetUnreadCount` | `CURRENT` |
| `PATCH` | `/api/v1/notifications/{id}/read` | Mark read | `notifications:access` | User | `Staff.Bff` | `Notification` | `NotificationService.MarkNotificationRead` | `CURRENT` |
| `PATCH` | `/api/v1/notifications/read-all` | Mark all read | `notifications:access` | User | `Staff.Bff` | `Notification` | `NotificationService.MarkAllNotificationsRead` | `CURRENT` |
