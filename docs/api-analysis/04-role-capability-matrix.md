# Aurora Platform - Role & Authorization Capability Matrix

> **Document ID:** `DOC-API-04`  
> **Status:** Role Classification & Security Authorization Matrix Complete  
> **Scope:** Granular classification of all platform capabilities across the four foundational roles: `STAFF`, `MANAGER`, `ADMIN`, `SYSTEM`.  
> **Architecture Reference:** `codex/requirement.md`, `codex/specs/logistics-architecture.md`, `docs/api-analysis/01-grpc-capability-map.md`, `docs/api-analysis/02-cqrs-capability-map.md`, `docs/api-analysis/03-business-capability-map.md`

---

## 1. Security Principles & Role Definitions

### 1.1. Role Definitions

1. **`STAFF` (Operator / Specialist)**:
   - **Intent:** Everyday operational execution within tenant boundaries.
   - **Scope:** Creates and manages shipments, cargo items, routes, drafts, documents, OCR submissions, rate queries, and notifications.
   - **Tenancy:** Strictly bound to authenticated `TenantId`.

2. **`MANAGER` (Supervisor / Approver)**:
   - **Intent:** Operational supervision, dual-control approval gates, compliance/exception reviews.
   - **Scope:** Approves/rejects AI route recommendations, reviews OCR extractions, manages quarantine releases, resolves alerts, and inspects supervisor dashboards.
   - **Tenancy:** Strictly bound to authenticated `TenantId`.

3. **`ADMIN` (Tenant Administrator)**:
   - **Intent:** Intra-tenant organization governance, staff identity management, AI & business rule threshold configuration, email domain provisioning.
   - **Scope:** Invites/activates/suspends users, assigns roles/permissions, updates tenant AI provider preferences and rule thresholds, provisions mail domains and mailboxes.
   - **Tenancy:** Strictly isolated to current `TenantId`. Has **NO** access to other tenants' data and **NO** permission to bypass multi-tenant isolation.

4. **`SYSTEM` (Machine-to-Machine / Background Worker / Platform Admin)**:
   - **Intent:** Automated background workers, event-driven projectors, IoT hardware telemetry receivers, SRE automation, inter-service gRPC orchestration, and platform tenant lifecycle provisioning.
   - **Scope:** IoT GPS ingestion, SRE alert deduplication, internal LLM execution gateway, outbox dispatchers, dead-letter reprocessing, cross-tenant ingestion, tenant provisioning.
   - **Tenancy:** Platform scope or machine execution context with explicit non-human identity.

---

## 2. Role Capability Matrix

| Capability | Staff | Manager | Admin | System | Scope | Evidence | Confidence |
| :--- | :---: | :---: | :---: | :---: | :--- | :--- | :--- |
| **Shipment.Create** | ✓ | ✓ | - | - | Tenant Isolated | `ShipmentGrpcService.CreateShipment`, `StaffControllerBase` | `EXISTING` |
| **Shipment.Get** | ✓ | ✓ | ✓ | - | Tenant Isolated | `ShipmentGrpcService.GetShipment`, `StaffControllerBase` | `EXISTING` |
| **Shipment.List** | ✓ | ✓ | ✓ | - | Tenant Isolated | `ShipmentGrpcService.ListShipments`, `StaffControllerBase` | `EXISTING` |
| **Shipment.Update** | ✓ | ✓ | - | - | Tenant Isolated | `ShipmentGrpcService.UpdateShipment` | `STRONGLY_INFERRED` |
| **Shipment.Submit** | ✓ | ✓ | - | - | Tenant Isolated | `ShipmentGrpcService.SubmitShipment` | `EXISTING` |
| **Shipment.UpdateStatus** | ✓ | ✓ | - | ✓ | Tenant Isolated | `ShipmentGrpcService.UpdateShipmentStatus` | `STRONGLY_INFERRED` |
| **Shipment.Cancel** | ✓ | ✓ | - | - | Tenant Isolated | `ShipmentGrpcService.CancelShipment` | `STRONGLY_INFERRED` |
| **Shipment.DeleteDraft** | ✓ | ✓ | - | - | Tenant Isolated | `ShipmentGrpcService.DeleteDraftShipment` | `STRONGLY_INFERRED` |
| **Shipment.ImportBulk** | ✓ | ✓ | - | - | Tenant Isolated | `ShipmentGrpcService.ImportShipments` | `STRONGLY_INFERRED` |
| **CargoItem.Manage** | ✓ | ✓ | - | - | Tenant Isolated | `ShipmentGrpcService.Add/Update/RemoveCargoItem` | `STRONGLY_INFERRED` |
| **Location.Manage** | ✓ | ✓ | - | - | Tenant Isolated | `ShipmentGrpcService.Add/Update/RemoveLocation` | `STRONGLY_INFERRED` |
| **Document.Manage** | ✓ | ✓ | - | - | Tenant Isolated | `ShipmentGrpcService.Attach/RemoveDocument` | `STRONGLY_INFERRED` |
| **Document.UpdateOcrCallback** | - | - | - | ✓ | Tenant Isolated | `ShipmentGrpcService.UpdateShipmentDocumentOcr` | `EXISTING` |
| **Milestone.Record** | ✓ | ✓ | - | ✓ | Tenant Isolated | `ShipmentGrpcService.AddShipmentMilestone` | `STRONGLY_INFERRED` |
| **Milestone.GetTimeline** | ✓ | ✓ | ✓ | - | Tenant Isolated | `ShipmentGrpcService.GetShipmentTimeline` | `EXISTING` |
| **Notification.List** | ✓ | ✓ | ✓ | - | User / Recipient | `NotificationGrpcService.ListNotifications` | `EXISTING` |
| **Notification.MarkRead** | ✓ | ✓ | ✓ | - | User / Recipient | `NotificationGrpcService.MarkNotificationRead` | `EXISTING` |
| **NotificationPreference.Manage** | ✓ | ✓ | ✓ | - | User / Recipient | `NotificationGrpcService.UpsertNotificationPreference` | `EXISTING` |
| **GpsPosition.Ingest** | - | - | - | ✓ | Device / Edge Gate | `GpsTrackingGrpcService.IngestPosition` | `EXISTING` |
| **GpsPosition.GetCurrent** | ✓ | ✓ | ✓ | - | Tenant Isolated | `GpsTrackingGrpcService.GetCurrentLocation` | `EXISTING` |
| **GpsPosition.ListHistory** | ✓ | ✓ | ✓ | - | Tenant Isolated | `GpsTrackingGrpcService.ListPositionHistory` | `EXISTING` |
| **Geofence.Create** | ✓ | ✓ | ✓ | - | Tenant Isolated | `GpsTrackingGrpcService.CreateGeofence` | `STRONGLY_INFERRED` |
| **Geofence.List** | ✓ | ✓ | ✓ | - | Tenant Isolated | `GpsTrackingGrpcService.ListGeofences` | `EXISTING` |
| **Geofence.SetActive** | ✓ | ✓ | ✓ | - | Tenant Isolated | `GpsTrackingGrpcService.SetGeofenceActive` | `STRONGLY_INFERRED` |
| **MonitoringAlert.List** | ✓ | ✓ | ✓ | - | Tenant Isolated | `GpsTrackingGrpcService.ListMonitoringAlerts` | `EXISTING` |
| **MonitoringAlert.Resolve** | - | ✓ | ✓ | - | Tenant Isolated | `GpsTrackingGrpcService.ResolveMonitoringAlert` | `STRONGLY_INFERRED` |
| **OcrJob.Submit** | ✓ | ✓ | - | ✓ | Tenant Isolated | `DocumentOcrGrpcService.SubmitOcrJob` | `EXISTING` |
| **OcrJob.Get** | ✓ | ✓ | ✓ | - | Tenant Isolated | `DocumentOcrGrpcService.GetDocumentJob` | `EXISTING` |
| **OcrJob.List** | ✓ | ✓ | ✓ | - | Tenant Isolated | `DocumentOcrGrpcService.ListDocumentJobs` | `EXISTING` |
| **OcrJob.Cancel** | ✓ | ✓ | - | - | Tenant Isolated | `DocumentOcrGrpcService.CancelDocumentJob` | `STRONGLY_INFERRED` |
| **OcrJob.Retry** | ✓ | ✓ | - | ✓ | Tenant Isolated | `DocumentOcrGrpcService.RetryDocumentJob` | `STRONGLY_INFERRED` |
| **OcrJob.Review** | - | ✓ | ✓ | - | Tenant Isolated | `DocumentOcrGrpcService.ReviewDocumentJob` | `EXISTING` |
| **Compliance.Evaluate** | ✓ | ✓ | - | ✓ | Tenant Isolated | `RegulatoryComplianceGrpcService.EvaluateCompliance` | `EXISTING` |
| **Compliance.GetEvaluation** | ✓ | ✓ | ✓ | - | Tenant Isolated | `RegulatoryComplianceGrpcService.GetComplianceEvaluation` | `EXISTING` |
| **RegulatorySource.Ingest** | - | - | - | ✓ | Platform Global | `RegulatoryComplianceGrpcService.IngestRegulatorySource` | `EXISTING` |
| **RegulatorySource.Query** | ✓ | ✓ | ✓ | ✓ | Platform Global | `RegulatoryComplianceGrpcService.QueryRegulations` | `EXISTING` |
| **KnowledgeDoc.Ingest** | - | - | ✓ | ✓ | Tenant + Global | `RegulatoryComplianceGrpcService.IngestKnowledgeDocument` | `EXISTING` |
| **KnowledgeDoc.Query** | ✓ | ✓ | ✓ | ✓ | Tenant + Global | `RegulatoryComplianceGrpcService.QueryKnowledge` | `EXISTING` |
| **ComplianceCopilot.Ask** | ✓ | ✓ | ✓ | - | Tenant + Global | `RegulatoryComplianceGrpcService.GenerateGroundedAnswer` | `EXISTING` |
| **CitationValidator.Validate** | - | - | - | ✓ | Stateless Service | `RegulatoryComplianceGrpcService.ValidateGroundedEvidence` | `EXISTING` |
| **Tenant.Create** | - | - | - | ✓ | System Multi-tenant | `IamGrpcService.CreateTenant`, `SystemControllerBase` | `EXISTING` |
| **Tenant.Get** | - | - | - | ✓ | System Multi-tenant | `IamGrpcService.GetTenant`, `SystemControllerBase` | `EXISTING` |
| **Tenant.List** | - | - | - | ✓ | System Multi-tenant | `IamGrpcService.ListTenants`, `SystemControllerBase` | `EXISTING` |
| **Tenant.UpdateStatus** | - | - | - | ✓ | System Multi-tenant | `IamGrpcService.UpdateTenantStatus`, `SystemControllerBase` | `EXISTING` |
| **Tenant.UpdateProfile** | - | - | - | ✓ | System Multi-tenant | `UpdateTenantCommand` (Unexposed RPC) | `PROPOSED` |
| **Tenant.Delete** | - | - | - | ✓ | System Multi-tenant | `IamGrpcService.DeleteTenant`, `SystemControllerBase` | `EXISTING` |
| **StaffUser.Invite** | - | - | ✓ | - | Tenant Isolated | `IamGrpcService.InviteUser`, `AdminControllerBase` | `EXISTING` |
| **StaffUser.Get** | ✓ | ✓ | ✓ | - | Tenant Isolated | `IamGrpcService.GetUser`, `AdminControllerBase` | `EXISTING` |
| **StaffUser.List** | - | - | ✓ | - | Tenant Isolated | `IamGrpcService.GetManyUsers`, `AdminControllerBase` | `EXISTING` |
| **StaffUser.Update** | - | - | ✓ | - | Tenant Isolated | `IamGrpcService.UpdateUser`, `AdminControllerBase` | `EXISTING` |
| **StaffUser.Activate** | - | - | ✓ | - | Tenant Isolated | `IamGrpcService.ActivateUser`, `AdminControllerBase` | `EXISTING` |
| **StaffUser.Suspend** | - | - | ✓ | - | Tenant Isolated | `IamGrpcService.SuspendUser`, `AdminControllerBase` | `EXISTING` |
| **StaffUser.ResetPassword** | - | - | ✓ | - | Tenant Isolated | `IamGrpcService.ResetUserPassword`, `AdminControllerBase` | `EXISTING` |
| **StaffUser.AssignRoles** | - | - | ✓ | - | Tenant Isolated | `IamGrpcService.AssignRoles`, `AdminControllerBase` | `EXISTING` |
| **Role.Get** | - | - | ✓ | - | Global / Tenant | `IamGrpcService.GetRole`, `AdminControllerBase` | `EXISTING` |
| **Role.List** | - | - | ✓ | - | Global / Tenant | `IamGrpcService.GetManyRoles`, `AdminControllerBase` | `EXISTING` |
| **Role.AssignPermissions** | - | - | ✓ | - | Tenant Isolated | `IamGrpcService.AssignPermissionsToRole`, `AdminControllerBase` | `EXISTING` |
| **Role.GetUserPermissions** | ✓ | ✓ | ✓ | ✓ | Tenant Isolated | `IamGrpcService.GetUserPermissions` | `EXISTING` |
| **Auth.IdentifyUser** | ✓ | ✓ | ✓ | - | Pre-Auth Public | `AuthGrpcService.IdentifyUser`, Public endpoint | `EXISTING` |
| **Auth.Login** | ✓ | ✓ | ✓ | - | Pre-Auth Public | `AuthGrpcService.Login`, Public endpoint | `EXISTING` |
| **Auth.CompleteInvite** | ✓ | ✓ | ✓ | - | Pre-Auth Public | `AuthGrpcService.CompleteInvitation`, Public endpoint | `EXISTING` |
| **Auth.RefreshToken** | ✓ | ✓ | ✓ | - | Token Context | `AuthGrpcService.RefreshToken`, Public endpoint | `EXISTING` |
| **Auth.Logout** | ✓ | ✓ | ✓ | - | Authenticated User | `AuthGrpcService.Logout`, `[Authorize]` | `EXISTING` |
| **Auth.ForgotPassword** | ✓ | ✓ | ✓ | - | Pre-Auth Public | `AuthGrpcService.ForgotPassword`, Public endpoint | `EXISTING` |
| **Route.Create** | ✓ | ✓ | - | - | Tenant Isolated | `RoutePlanningGrpcService.CreateRoute` | `EXISTING` |
| **Route.Get** | ✓ | ✓ | ✓ | - | Tenant Isolated | `RoutePlanningGrpcService.GetRoute` | `EXISTING` |
| **Route.List** | ✓ | ✓ | ✓ | - | Tenant Isolated | `RoutePlanningGrpcService.ListRoutes` | `EXISTING` |
| **Route.Update** | ✓ | ✓ | - | - | Tenant Isolated | `RoutePlanningGrpcService.UpdateRoute` | `EXISTING` |
| **Route.Delete** | ✓ | ✓ | - | - | Tenant Isolated | `RoutePlanningGrpcService.DeleteRoute` | `EXISTING` |
| **Route.UpdateStatus** | ✓ | ✓ | - | ✓ | Tenant Isolated | `RoutePlanningGrpcService.UpdateRouteStatus` | `STRONGLY_INFERRED` |
| **Route.Optimize** | ✓ | ✓ | - | - | Tenant Isolated | `RoutePlanningGrpcService.OptimizeRoute` | `EXISTING` |
| **Route.RecommendAi** | ✓ | ✓ | - | - | Tenant Isolated | `RoutePlanningGrpcService.GetRouteRecommendation` | `EXISTING` |
| **RouteApproval.ListPending**| - | ✓ | ✓ | - | Tenant Isolated | `RoutePlanningGrpcService.ListPendingApprovals` | `EXISTING` |
| **RouteApproval.Approve** | - | ✓ | - | - | Tenant Isolated | `RoutePlanningGrpcService.ApproveRoute` | `EXISTING` |
| **RouteApproval.Reject** | - | ✓ | - | - | Tenant Isolated | `RoutePlanningGrpcService.RejectRoute` | `EXISTING` |
| **TenantAiConfig.Get** | - | - | ✓ | - | Tenant Isolated | `RoutePlanningGrpcService.GetTenantAiConfig`, `AdminControllerBase` | `EXISTING` |
| **TenantAiConfig.Upsert** | - | - | ✓ | - | Tenant Isolated | `RoutePlanningGrpcService.UpsertTenantAiConfig`, `AdminControllerBase` | `EXISTING` |
| **TenantRuleConfig.List** | - | - | ✓ | - | Tenant Isolated | `RoutePlanningGrpcService.ListTenantRuleConfigs`, `AdminControllerBase` | `EXISTING` |
| **TenantRuleConfig.Upsert** | - | - | ✓ | - | Tenant Isolated | `RoutePlanningGrpcService.UpsertTenantRuleConfig`, `AdminControllerBase` | `EXISTING` |
| **MailDomain.Provision** | - | - | ✓ | - | Tenant Isolated | `MailManagementService.ProvisionDomain`, `AdminControllerBase` | `EXISTING` |
| **Mailbox.Create** | - | - | ✓ | - | Tenant Isolated | `MailManagementService.CreateMailbox`, `AdminControllerBase` | `EXISTING` |
| **MailAlias.Create** | - | - | ✓ | - | Tenant Isolated | `MailManagementService.CreateAlias`, `AdminControllerBase` | `EXISTING` |
| **EmailDraft.Create** | ✓ | ✓ | - | - | Tenant Isolated | `MailSecurityService.CreateDraftMessage` | `EXISTING` |
| **EmailDraft.Get** | ✓ | ✓ | - | - | Tenant Isolated | `MailSecurityService.GetDraft` | `EXISTING` |
| **EmailDraft.List** | ✓ | ✓ | - | - | Tenant Isolated | `MailSecurityService.ListDrafts` | `EXISTING` |
| **OutboundEmail.SubmitSend** | ✓ | ✓ | - | - | Tenant Isolated | `MailSecurityService.SubmitOutboundMessage` | `EXISTING` |
| **ProcessedEmail.Get** | ✓ | ✓ | ✓ | - | Tenant Isolated | `MailSecurityService.GetProcessedMessage` | `EXISTING` |
| **ProcessedEmail.List** | ✓ | ✓ | ✓ | - | Tenant Isolated | `MailSecurityService.ListProcessedMessages` | `EXISTING` |
| **MailQuarantine.Get** | - | ✓ | ✓ | - | Tenant Isolated | `MailSecurityService.GetQuarantineRecord` | `EXISTING` |
| **MailQuarantine.List** | - | ✓ | ✓ | - | Tenant Isolated | `MailSecurityService.ListQuarantineRecords` | `EXISTING` |
| **MailQuarantine.Release** | - | ✓ | ✓ | - | Tenant Isolated | `MailSecurityService.ReleaseQuarantine` | `EXISTING` |
| **MailQuarantine.Delete** | - | - | ✓ | - | Tenant Isolated | `MailSecurityService.DeleteQuarantine`, `AdminControllerBase` | `EXISTING` |
| **MailAudit.GetRecords** | - | - | ✓ | ✓ | Tenant / System | `MailManagementService.GetAuditRecords` | `EXISTING` |
| **DeadLetter.Requeue** | - | - | - | ✓ | Platform Admin | `MailManagementService.RequeueDeadLetter`, `SystemControllerBase` | `EXISTING` |
| **AiPolicy.Evaluate** | - | - | - | ✓ | Internal M2M | `PolicyGrpcHandler.executePolicy` | `EXISTING` |
| **AiExecution.Generate** | - | - | - | ✓ | Internal M2M | `AiExecutionGrpcHandler.generate` | `EXISTING` |
| **AiExecution.Embed** | - | - | - | ✓ | Internal M2M | `AiExecutionGrpcHandler.embed` | `EXISTING` |
| **DevOpsAlert.Ingest** | - | - | - | ✓ | Platform Webhook | `IngestionGrpcHandler.ingestAlert` | `EXISTING` |
| **DevOpsIncident.Get/List** | - | - | - | ✓ | Platform SRE | `IncidentGrpcHandler.getIncident/listIncidents` | `EXISTING` |
| **DevOpsIncident.Approve/Reject**| - | - | - | ✓ | Platform SRE Lead | `IncidentGrpcHandler.approveIncident/rejectIncident` | `EXISTING` |
| **DevOpsRule.Manage** | - | - | - | ✓ | Platform SRE | `RuleGrpcHandler.*` | `EXISTING` |
| **DevOpsConfig.Manage** | - | - | - | ✓ | Platform SRE | `SelfConfigGrpcHandler.*` | `EXISTING` |
| **Invoice.Generate** | ✓ | ✓ | - | ✓ | Tenant Isolated | `BillingService.generateInvoice` | `STRONGLY_INFERRED` |
| **Invoice.CreateManual** | ✓ | ✓ | - | - | Tenant Isolated | `BillingService.createInvoice` | `STRONGLY_INFERRED` |
| **Invoice.GetDetail** | ✓ | ✓ | ✓ | - | Tenant Isolated | `BillingService.getInvoiceDetail` | `EXISTING` |
| **Invoice.List** | ✓ | ✓ | ✓ | - | Tenant Isolated | `BillingService.listInvoices` | `EXISTING` |
| **Invoice.UpdateStatus** | ✓ | ✓ | - | ✓ | Tenant Isolated | `BillingService.updateInvoiceStatus` | `STRONGLY_INFERRED` |
| **Invoice.RecordPayment** | ✓ | ✓ | - | - | Tenant Isolated | `BillingService.recordPayment` | `STRONGLY_INFERRED` |
| **Invoice.Cancel** | - | ✓ | ✓ | - | Tenant Isolated | `BillingService.cancelInvoice` | `STRONGLY_INFERRED` |
| **AdjustmentNote.Issue** | - | ✓ | ✓ | - | Tenant Isolated | `BillingService.issueDebitNote/issueCreditNote` | `STRONGLY_INFERRED` |
| **CustomerCredit.Check** | ✓ | ✓ | - | ✓ | Tenant Isolated | `BillingService.checkCustomerCredit` | `EXISTING` |
| **EscrowWallet.Create** | - | - | ✓ | ✓ | Tenant Isolated | `BillingService.createEscrowWallet` | `STRONGLY_INFERRED` |
| **EscrowWallet.GetBalance** | ✓ | ✓ | ✓ | - | Tenant Isolated | `BillingService.getWalletBalance` | `EXISTING` |
| **EscrowTransaction.Freeze** | - | - | - | ✓ | Tenant Isolated | `BillingService.freezeEscrowAmount` | `EXISTING` |
| **EscrowTransaction.Release** | - | - | - | ✓ | Tenant Isolated | `BillingService.releaseEscrowAmount` | `EXISTING` |
| **EscrowTransaction.Refund** | - | - | - | ✓ | Tenant Isolated | `BillingService.refundEscrowAmount` | `EXISTING` |
| **CostEstimation.Estimate** | ✓ | ✓ | - | ✓ | Tenant Isolated | `FinancialService.estimateCost` | `EXISTING` |
| **CustomsDuty.Calculate** | ✓ | ✓ | - | ✓ | Tenant Isolated | `FinancialService.getCustomsDuty` | `EXISTING` |
| **PricingRate.GetFloorRate** | - | - | - | ✓ | Tenant Isolated | `FinancialService.getMinAcceptableRate` | `EXISTING` |
| **PricingRate.GetDynamicMargin**| - | - | - | ✓ | Tenant Isolated | `FinancialService.getDynamicMargin` | `EXISTING` |
| **ExchangeRate.GetRate** | ✓ | ✓ | ✓ | ✓ | Global / Tenant | `FinancialService.getExchangeRate` | `EXISTING` |
| **Negotiation.SubmitOffer** | ✓ | ✓ | - | - | Tenant Isolated | `NegotiationService.submitOffer` | `EXISTING` |
| **Negotiation.GetHistory** | ✓ | ✓ | ✓ | - | Tenant Isolated | `NegotiationService.getSessionHistory` | `EXISTING` |
| **AssistantChat.SendMessage** | ✓ | ✓ | ✓ | - | Tenant Isolated | `ConversationalAssistantOrchestrator.handleMessage` | `EXISTING` |

---

## 3. Dedicated Role Breakdowns

### 3.1. Staff-Only Capabilities
*(Operational actions that are purely execution-focused and not relevant for higher administrative tiers)*

1. **`Negotiation.SubmitOffer`**:
   - **Danger Level:** `MEDIUM`
   - **Reason:** Frontline rate negotiation with carrier/customer.
2. **`Shipment.Create` / `Shipment.Update` / `Shipment.Cancel`**:
   - **Danger Level:** `MEDIUM`
   - **Reason:** Everyday operational payload modifications.

---

### 3.2. Manager-Only Capabilities
*(Supervisory and dual-control approval actions restricted strictly to management)*

1. **`RouteApproval.Approve` / `RouteApproval.Reject`**:
   - **Allowed Roles:** `[MANAGER]`
   - **Danger Level:** `HIGH`
   - **Reason:** Authorization of high-risk, expensive, or hazardous cargo routes recommended by AI.
2. **`MonitoringAlert.Resolve`**:
   - **Allowed Roles:** `[MANAGER, ADMIN]`
   - **Danger Level:** `MEDIUM`
   - **Reason:** Acknowledging and closing critical geofence breach or signal loss alerts.
3. **`OcrJob.Review`**:
   - **Allowed Roles:** `[MANAGER, ADMIN]`
   - **Danger Level:** `MEDIUM`
   - **Reason:** Human-in-the-Loop approval/correction of OCR extractions with low AI confidence.
4. **`Invoice.Cancel` / `AdjustmentNote.Issue`**:
   - **Allowed Roles:** `[MANAGER, ADMIN]`
   - **Danger Level:** `HIGH`
   - **Reason:** Financial voiding and credit/debit adjustments affecting company accounts receivable.

---

### 3.3. Admin-Only Capabilities (Tenant Admin)
*(Administrative capabilities isolated within the tenant's boundaries)*

1. **Staff Identity Administration (`StaffUser.Invite`, `Update`, `Activate`, `Suspend`, `ResetPassword`, `AssignRoles`)**:
   - **Allowed Roles:** `[ADMIN]`
   - **Danger Level:** `CRITICAL`
   - **Reason:** Full control over employee access and privileges within the tenant.
2. **Role Permission Customization (`Role.AssignPermissions`)**:
   - **Allowed Roles:** `[ADMIN]`
   - **Danger Level:** `HIGH`
   - **Reason:** Assigns operational permission sets to tenant roles.
3. **AI Policy & Rule Configuration (`TenantAiConfig.Upsert`, `TenantRuleConfig.Upsert`)**:
   - **Allowed Roles:** `[ADMIN]`
   - **Danger Level:** `HIGH`
   - **Reason:** Sets token expenditure ceilings, approved AI providers, and auto-dispatch rules.
4. **Mail Provisioning & Purging (`MailDomain.Provision`, `Mailbox.Create`, `MailAlias.Create`, `MailQuarantine.Delete`)**:
   - **Allowed Roles:** `[ADMIN]`
   - **Danger Level:** `HIGH`
   - **Reason:** Tenant corporate email identity and security policy management.

---

### 3.4. System-Only Capabilities (Machine & Platform SRE)
*(Capabilities that should NEVER be invoked by human web portal users)*

1. **`AiExecution.Generate` / `AiExecution.Embed` / `AiPolicy.Evaluate`**:
   - **Allowed Roles:** `[SYSTEM]`
   - **Danger Level:** `HIGH`
   - **Reason:** Microservice-to-microservice LLM gateway with strict `x-service-id` token controls.
2. **`GpsPosition.Ingest` / `DevOpsAlert.Ingest`**:
   - **Allowed Roles:** `[SYSTEM]`
   - **Danger Level:** `LOW`
   - **Reason:** Telemetry streaming endpoints for IoT devices and SRE logging pipelines.
3. **`EscrowTransaction.Freeze` / `Release` / `Refund`**:
   - **Allowed Roles:** `[SYSTEM]`
   - **Danger Level:** `CRITICAL`
   - **Reason:** Direct movement of carrier escrow funds executed automatically upon verified milestone events (e.g. `Delivered`).
4. **`PricingRate.GetFloorRate` / `GetDynamicMargin`**:
   - **Allowed Roles:** `[SYSTEM]`
   - **Danger Level:** `HIGH`
   - **Reason:** Internal algorithmic margins that must remain confidential from human negotiators.
5. **`Tenant.Create` / `UpdateStatus` / `Delete` / `Tenant.UpdateProfile`**:
   - **Allowed Roles:** `[SYSTEM]` (Platform System Admin)
   - **Danger Level:** `CRITICAL`
   - **Reason:** Cross-tenant lifecycle onboarding and suspension.
6. **`DeadLetter.Requeue`**:
   - **Allowed Roles:** `[SYSTEM]` (Platform System Admin)
   - **Danger Level:** `MEDIUM`
   - **Reason:** Infrastructure queue recovery.

---

## 4. Shared Capabilities (>= 2 Roles)

The following table documents all capabilities accessible across multiple role tiers:

| Capability | Shared Roles | Service | Primary RPC / Method | Access Reason & Boundary |
| :--- | :--- | :--- | :--- | :--- |
| **Shipment.Create** | `[STAFF, MANAGER]` | `ShipmentWorkflow` | `CreateShipment` | Operators draft shipments; managers can create expedited/special shipments. |
| **Shipment.Get / List** | `[STAFF, MANAGER, ADMIN]` | `ShipmentWorkflow` | `GetShipment`, `ListShipments` | Universal read visibility across all tenant staff tiers. |
| **Shipment.UpdateStatus** | `[STAFF, MANAGER, SYSTEM]` | `ShipmentWorkflow` | `UpdateShipmentStatus` | Human operators update status; automated tracking triggers status changes on delivery. |
| **Milestone.Record** | `[STAFF, MANAGER, SYSTEM]` | `ShipmentWorkflow` | `AddShipmentMilestone` | Field staff log physical checkpoints; GPS geofence triggers log automated milestones. |
| **GpsPosition.GetCurrent / History** | `[STAFF, MANAGER, ADMIN]` | `GpsTracking` | `GetCurrentLocation`, `ListPositionHistory` | Real-time map monitoring and historical playback for all tenant staff. |
| **Geofence.Create / List / SetActive**| `[STAFF, MANAGER, ADMIN]` | `GpsTracking` | `CreateGeofence`, `ListGeofences`, `SetGeofenceActive` | Dispatchers set geofences for routes; managers/admins maintain master warehouse geofences. |
| **MonitoringAlert.List** | `[STAFF, MANAGER, ADMIN]` | `GpsTracking` | `ListMonitoringAlerts` | Real-time operational exception tracking. |
| **OcrJob.Submit / Get / List** | `[STAFF, MANAGER, ADMIN, SYSTEM]` | `DocumentOcr` | `SubmitOcrJob`, `GetDocumentJob`, `ListDocumentJobs` | Staff upload docs; system background workers process OCR jobs. |
| **Compliance.Evaluate** | `[STAFF, MANAGER, SYSTEM]` | `RegulatoryCompliance`| `EvaluateCompliance` | Operators run pre-checks; route planning agent runs automated compliance checks. |
| **RegulatorySource.Query** | `[STAFF, MANAGER, ADMIN, SYSTEM]` | `RegulatoryCompliance`| `QueryRegulations` | Universal legal search engine for all users and AI agents. |
| **KnowledgeDoc.Query** | `[STAFF, MANAGER, ADMIN, SYSTEM]` | `RegulatoryCompliance`| `QueryKnowledge` | Universal SOP / knowledge base search engine. |
| **ComplianceCopilot.Ask** | `[STAFF, MANAGER, ADMIN]` | `RegulatoryCompliance`| `GenerateGroundedAnswer` | Interactive AI assistant accessible across portal UI. |
| **StaffUser.Get** | `[STAFF, MANAGER, ADMIN]` | `IamTenant` | `GetUser` | User profile viewing and team directory lookup. |
| **Role.GetUserPermissions** | `[STAFF, MANAGER, ADMIN, SYSTEM]` | `IamTenant` | `GetUserPermissions` | Used by frontend to render permission gates and by backend auth interceptors. |
| **Auth.Login / Identify / Refresh** | `[STAFF, MANAGER, ADMIN]` | `IamTenant` | `Login`, `IdentifyUser`, `RefreshToken` | Universal public authentication entry points. |
| **Route.Get / List** | `[STAFF, MANAGER, ADMIN]` | `RoutePlanningAgent` | `GetRoute`, `ListRoutes` | Route planning inspection across dispatchers and supervisors. |
| **RouteApproval.ListPending** | `[MANAGER, ADMIN]` | `RoutePlanningAgent` | `ListPendingApprovals` | Supervisor approval queue monitoring. |
| **MailQuarantine.Get / List / Release**| `[MANAGER, ADMIN]` | `MailService` | `GetQuarantineRecord`, `ListQuarantineRecords`, `ReleaseQuarantine` | Security review of suspicious inbound emails. |
| **Invoice.Generate / CreateManual** | `[STAFF, MANAGER, SYSTEM]` | `BillingService` | `GenerateInvoice`, `CreateInvoice` | Billing clerks create invoices; delivery background worker triggers auto-generation. |
| **Invoice.GetDetail / List** | `[STAFF, MANAGER, ADMIN]` | `BillingService` | `GetInvoiceDetail`, `ListInvoices` | Financial inspection and auditing. |
| **EscrowWallet.GetBalance** | `[STAFF, MANAGER, ADMIN]` | `BillingService` | `GetWalletBalance` | Real-time carrier escrow wallet visibility. |
| **CostEstimation.Estimate** | `[STAFF, MANAGER, SYSTEM]` | `FinancialService` | `EstimateCost` | Freight estimators calculate shipping costs; billing service calculates invoice items. |
| **ExchangeRate.GetRate** | `[STAFF, MANAGER, ADMIN, SYSTEM]` | `FinancialService` | `GetExchangeRate` | Currency conversion rate provider for all modules. |

---

## 5. Dangerous Role Combinations Flagged for Security Review

> [!CAUTION]
> **Manual Security Review Required for `SYSTEM + Human Role` Shared Capabilities**

The following capabilities mix automated `SYSTEM` execution with human user roles (`STAFF`, `MANAGER`, `ADMIN`). These must be protected with distinct authorization checks:

1. **`Shipment.UpdateStatus` (`STAFF, MANAGER, SYSTEM`)**:
   - *Risk:* Automated IoT/delivery events can overwrite human operator status updates.
   - *Guard:* State machine validator (`Shipment.TransitionTo`) must prevent invalid state regressions regardless of caller identity.

2. **`Compliance.Evaluate` (`STAFF, MANAGER, SYSTEM`)**:
   - *Risk:* Machine-to-machine calls from `RoutePlanningAgent` must not inherit administrative privileges to create platform-wide compliance rules.
   - *Guard:* Background agents must pass explicit `TenantId` metadata; global regulation mutation must require `SYSTEM_ADMIN` role.

3. **`Invoice.Generate` (`STAFF, MANAGER, SYSTEM`)**:
   - *Risk:* Automatic post-delivery invoice creation must not trigger duplicate invoices if a billing clerk manually generated an advance invoice.
   - *Guard:* Enforce unique database index on `(TenantId, ShipmentId)` for generated invoices.

4. **`EscrowTransaction` Operations (`SYSTEM` ONLY)**:
   - *Risk:* If `FreezeEscrowAmount`, `ReleaseEscrowAmount`, or `RefundEscrowAmount` were accidentally exposed to `STAFF` or `MANAGER` via BFF, unauthorized fund transfers could occur.
   - *Guard:* Keep Escrow movement RPCs strictly internal (unreachable from BFF controllers); trigger solely via trusted transactional event consumers.
