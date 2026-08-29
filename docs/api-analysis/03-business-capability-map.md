# Aurora Platform - Business Capability Map

> **Document ID:** `DOC-API-03`  
> **Status:** Business Capability Discovery & Resource Decomposition Complete  
> **Scope:** Synthesis of technical RPCs and CQRS handlers into domain business resources across all 16 microservices.  
> **Architecture Reference:** `codex/requirement.md`, `codex/specs/logistics-architecture.md`, `docs/api-analysis/01-grpc-capability-map.md`, `docs/api-analysis/02-cqrs-capability-map.md`

---

## 1. Executive Summary

This document abstracts the technical gRPC contracts and CQRS command/query handlers into a **Business Capability Map**. All operations are grouped around concrete **Domain Business Resources**, assessing their lifecycle completeness (CRUD / workflow state machines), tenant scope, authorization context, and identifying business gaps.

---

## 2. Business Capability Matrix

| Resource | Business Action | Service | Proto RPC | CQRS / Implementation | Status |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **Shipment** | `CREATE` | `ShipmentWorkflow` | `ShipmentWorkflowService.CreateShipment` | `CreateShipmentCommand` | Fully Supported |
| **Shipment** | `GET` | `ShipmentWorkflow` | `ShipmentWorkflowService.GetShipment` | `GetShipmentQuery` | Fully Supported |
| **Shipment** | `LIST` | `ShipmentWorkflow` | `ShipmentWorkflowService.ListShipments` | `ListShipmentsQuery` | Fully Supported |
| **Shipment** | `UPDATE` | `ShipmentWorkflow` | `ShipmentWorkflowService.UpdateShipment` | `UpdateShipmentCommand` | Fully Supported |
| **Shipment** | `SUBMIT` | `ShipmentWorkflow` | `ShipmentWorkflowService.SubmitShipment` | `SubmitShipmentCommand` | Fully Supported |
| **Shipment** | `TRANSITION_STATUS` | `ShipmentWorkflow` | `ShipmentWorkflowService.UpdateShipmentStatus` | `UpdateShipmentStatusCommand` | Fully Supported |
| **Shipment** | `CANCEL` | `ShipmentWorkflow` | `ShipmentWorkflowService.CancelShipment` | `CancelShipmentCommand` | Fully Supported |
| **Shipment** | `DELETE_DRAFT` | `ShipmentWorkflow` | `ShipmentWorkflowService.DeleteDraftShipment` | `DeleteDraftShipmentCommand` | Fully Supported |
| **Shipment** | `IMPORT_BULK` | `ShipmentWorkflow` | `ShipmentWorkflowService.ImportShipments` | `ImportShipmentsCommand` | Fully Supported |
| **CargoItem** | `ADD` | `ShipmentWorkflow` | `ShipmentWorkflowService.AddCargoItem` | `AddCargoItemCommand` | Fully Supported |
| **CargoItem** | `UPDATE` | `ShipmentWorkflow` | `ShipmentWorkflowService.UpdateCargoItem` | `UpdateCargoItemCommand` | Fully Supported |
| **CargoItem** | `REMOVE` | `ShipmentWorkflow` | `ShipmentWorkflowService.RemoveCargoItem` | `RemoveCargoItemCommand` | Fully Supported |
| **ShipmentLocation** | `ADD` | `ShipmentWorkflow` | `ShipmentWorkflowService.AddShipmentLocation` | `AddShipmentLocationCommand` | Fully Supported |
| **ShipmentLocation** | `UPDATE` | `ShipmentWorkflow` | `ShipmentWorkflowService.UpdateShipmentLocation` | `UpdateShipmentLocationCommand` | Fully Supported |
| **ShipmentLocation** | `REMOVE` | `ShipmentWorkflow` | `ShipmentWorkflowService.RemoveShipmentLocation` | `RemoveShipmentLocationCommand` | Fully Supported |
| **ShipmentDocument** | `ATTACH` | `ShipmentWorkflow` | `ShipmentWorkflowService.AttachShipmentDocument` | `AttachShipmentDocumentCommand` | Fully Supported |
| **ShipmentDocument** | `UPDATE_OCR` | `ShipmentWorkflow` | `ShipmentWorkflowService.UpdateShipmentDocumentOcr` | `UpdateShipmentDocumentOcrCommand` | Fully Supported (Internal) |
| **ShipmentDocument** | `REMOVE` | `ShipmentWorkflow` | `ShipmentWorkflowService.RemoveShipmentDocument` | `RemoveShipmentDocumentCommand` | Fully Supported |
| **ShipmentMilestone** | `RECORD` | `ShipmentWorkflow` | `ShipmentWorkflowService.AddShipmentMilestone` | `RecordShipmentMilestoneCommand` | Fully Supported |
| **ShipmentMilestone** | `GET_TIMELINE` | `ShipmentWorkflow` | `ShipmentWorkflowService.GetShipmentTimeline` | `GetShipmentTimelineQuery` | Fully Supported |
| **Notification** | `LIST` | `Notification` | `NotificationService.ListNotifications` | `NotificationDbContext` query | Fully Supported |
| **Notification** | `MARK_READ` | `Notification` | `NotificationService.MarkNotificationRead` | `notification.MarkRead()` | Fully Supported |
| **NotificationPreference** | `LIST` | `Notification` | `NotificationService.ListNotificationPreferences` | `NotificationDbContext` query | Fully Supported |
| **NotificationPreference** | `UPSERT` | `Notification` | `NotificationService.UpsertNotificationPreference` | `NotificationPreference.Create/Update()` | Fully Supported |
| **GpsPosition** | `INGEST` | `GpsTracking` | `GpsTrackingService.IngestPosition` | `PositionIngestionService.IngestAsync` | Fully Supported (IoT Gateway) |
| **GpsPosition** | `GET_CURRENT` | `GpsTracking` | `GpsTrackingService.GetCurrentLocation` | `LocationQueryService.GetCurrentAsync` | Fully Supported |
| **GpsPosition** | `LIST_HISTORY` | `GpsTracking` | `GpsTrackingService.ListPositionHistory` | `LocationQueryService.ListHistoryAsync` | Fully Supported |
| **Geofence** | `CREATE` | `GpsTracking` | `GpsTrackingService.CreateGeofence` | `MonitoringManagementService.CreateGeofenceAsync` | Fully Supported |
| **Geofence** | `LIST` | `GpsTracking` | `GpsTrackingService.ListGeofences` | `MonitoringManagementService.ListGeofencesAsync` | Fully Supported |
| **Geofence** | `SET_ACTIVE` | `GpsTracking` | `GpsTrackingService.SetGeofenceActive` | `MonitoringManagementService.SetGeofenceActiveAsync` | Fully Supported |
| **MonitoringAlert** | `LIST` | `GpsTracking` | `GpsTrackingService.ListMonitoringAlerts` | `MonitoringManagementService.ListAlertsAsync` | Fully Supported |
| **MonitoringAlert** | `RESOLVE` | `GpsTracking` | `GpsTrackingService.ResolveMonitoringAlert` | `MonitoringManagementService.ResolveAlertAsync` | Fully Supported |
| **OcrJob** | `SUBMIT` | `DocumentOcr` | `DocumentOcrService.SubmitOcrJob` | `DocumentOcrJobService.SubmitOcrAsync` | Fully Supported |
| **OcrJob** | `GET` | `DocumentOcr` | `DocumentOcrService.GetDocumentJob` | `DocumentOcrJobService.GetAsync` | Fully Supported |
| **OcrJob** | `LIST` | `DocumentOcr` | `DocumentOcrService.ListDocumentJobs` | `DocumentOcrJobService.ListAsync` | Fully Supported |
| **OcrJob** | `CANCEL` | `DocumentOcr` | `DocumentOcrService.CancelDocumentJob` | `DocumentOcrJobService.CancelAsync` | Fully Supported |
| **OcrJob** | `RETRY` | `DocumentOcr` | `DocumentOcrService.RetryDocumentJob` | `DocumentOcrJobService.RetryAsync` | Fully Supported |
| **OcrJob** | `REVIEW` | `DocumentOcr` | `DocumentOcrService.ReviewDocumentJob` | `DocumentOcrJobService.ReviewAsync` | Fully Supported |
| **ComplianceEvaluation** | `EVALUATE` | `RegulatoryCompliance` | `RegulatoryComplianceService.EvaluateCompliance` | `ComplianceEvaluationService.EvaluateAsync` | Fully Supported |
| **ComplianceEvaluation** | `GET` | `RegulatoryCompliance` | `RegulatoryComplianceService.GetComplianceEvaluation` | `ComplianceEvaluationService.GetAsync` | Fully Supported |
| **RegulatorySource** | `INGEST` | `RegulatoryCompliance` | `RegulatoryComplianceService.IngestRegulatorySource` | `RegulatoryIngestionService.IngestAsync` | Fully Supported |
| **RegulatorySource** | `QUERY` | `RegulatoryCompliance` | `RegulatoryComplianceService.QueryRegulations` | `RegulationRetrievalService.QueryAsync` | Fully Supported |
| **KnowledgeDocument** | `INGEST` | `RegulatoryCompliance` | `RegulatoryComplianceService.IngestKnowledgeDocument` | `KnowledgeIngestionService.IngestAsync` | Fully Supported |
| **KnowledgeDocument** | `QUERY` | `RegulatoryCompliance` | `RegulatoryComplianceService.QueryKnowledge` | `KnowledgeIngestionService.QueryAsync` | Fully Supported |
| **ComplianceCopilot** | `GENERATE_ANSWER` | `RegulatoryCompliance` | `RegulatoryComplianceService.GenerateGroundedAnswer` | `GroundedAnswerService.GenerateAnswerAsync` | Fully Supported |
| **ComplianceCopilot** | `VALIDATE_EVIDENCE`| `RegulatoryCompliance` | `RegulatoryComplianceService.ValidateGroundedEvidence` | `DeterministicCitationValidator.Validate` | Fully Supported (Internal) |
| **Tenant** | `CREATE` | `IamTenant` | `IamService.CreateTenant` | `CreateTenantCommand` | Fully Supported |
| **Tenant** | `GET` | `IamTenant` | `IamService.GetTenant` | `GetTenantQuery` | Fully Supported |
| **Tenant** | `LIST` | `IamTenant` | `IamService.ListTenants` | `ListTenantsQuery` | Fully Supported |
| **Tenant** | `UPDATE_STATUS` | `IamTenant` | `IamService.UpdateTenantStatus` | `UpdateTenantStatusCommand` | Fully Supported |
| **Tenant** | `UPDATE_PROFILE` | `IamTenant` | *None* | `UpdateTenantCommand` | **Candidate Missing RPC** |
| **Tenant** | `DELETE` | `IamTenant` | `IamService.DeleteTenant` | `DeleteTenantCommand` | Fully Supported |
| **StaffUser** | `INVITE` | `IamTenant` | `IamService.InviteUser` | `CreateStaffCommand` | Fully Supported |
| **StaffUser** | `GET` | `IamTenant` | `IamService.GetUser` | `GetStaffQuery` | Fully Supported |
| **StaffUser** | `LIST` | `IamTenant` | `IamService.GetManyUsers` | `ListStaffQuery` | Fully Supported |
| **StaffUser** | `UPDATE` | `IamTenant` | `IamService.UpdateUser` | `UpdateStaffCommand` | Fully Supported |
| **StaffUser** | `ACTIVATE` | `IamTenant` | `IamService.ActivateUser` | `ActivateStaffCommand` | Fully Supported |
| **StaffUser** | `SUSPEND` | `IamTenant` | `IamService.SuspendUser` | `DeactivateStaffCommand` | Fully Supported |
| **StaffUser** | `RESET_PASSWORD` | `IamTenant` | `IamService.ResetUserPassword` | `ResetStaffPasswordCommand` | Fully Supported |
| **StaffUser** | `ASSIGN_ROLES` | `IamTenant` | `IamService.AssignRoles` | `AssignRolesCommand` | Fully Supported |
| **Role & Permission** | `GET_ROLE` | `IamTenant` | `IamService.GetRole` | `GetRoleQuery` | Fully Supported |
| **Role & Permission** | `LIST_ROLES` | `IamTenant` | `IamService.GetManyRoles` | `ListRolesQuery` | Fully Supported |
| **Role & Permission** | `ASSIGN_PERMISSIONS` | `IamTenant` | `IamService.AssignPermissionsToRole` | `AssignPermissionsToRoleCommand` | Fully Supported |
| **Role & Permission** | `GET_USER_PERMS` | `IamTenant` | `IamService.GetUserPermissions` | `GetUserPermissionsQuery` | Fully Supported |
| **AuthSession** | `IDENTIFY` | `IamTenant` | `AuthService.IdentifyUser` | `IdentifyUserQuery` | Fully Supported |
| **AuthSession** | `LOGIN` | `IamTenant` | `AuthService.Login` | `LoginCommand` | Fully Supported |
| **AuthSession** | `COMPLETE_INVITE` | `IamTenant` | `AuthService.CompleteInvitation` | `CompleteInvitationCommand` | Fully Supported |
| **AuthSession** | `REFRESH_TOKEN` | `IamTenant` | `AuthService.RefreshToken` | `cognitoService.RefreshTokenAsync` | Fully Supported |
| **AuthSession** | `LOGOUT` | `IamTenant` | `AuthService.Logout` | Stateless return | Fully Supported |
| **AuthSession** | `FORGOT_PASSWORD` | `IamTenant` | `AuthService.ForgotPassword` | `cognitoService.ForgotPasswordAsync` | Fully Supported |
| **AuthSession** | `CONFIRM_FORGOT` | `IamTenant` | `AuthService.ConfirmForgotPassword` | `cognitoService.ConfirmForgotPasswordAsync` | Fully Supported |
| **RoutePlan** | `CREATE` | `RoutePlanningAgent` | `RoutePlanningService.CreateRoute` | `CreateRouteCommand` | Fully Supported |
| **RoutePlan** | `GET` | `RoutePlanningAgent` | `RoutePlanningService.GetRoute` | `GetRouteQuery` | Fully Supported |
| **RoutePlan** | `LIST` | `RoutePlanningAgent` | `RoutePlanningService.ListRoutes` | `ListRoutesQuery` | Fully Supported |
| **RoutePlan** | `UPDATE` | `RoutePlanningAgent` | `RoutePlanningService.UpdateRoute` | `UpdateRouteCommand` | Fully Supported |
| **RoutePlan** | `DELETE` | `RoutePlanningAgent` | `RoutePlanningService.DeleteRoute` | `DeleteRouteCommand` | Fully Supported |
| **RoutePlan** | `UPDATE_STATUS` | `RoutePlanningAgent` | `RoutePlanningService.UpdateRouteStatus` | `UpdateRouteStatusCommand` | Fully Supported |
| **RoutePlan** | `OPTIMIZE` | `RoutePlanningAgent` | `RoutePlanningService.OptimizeRoute` | `OptimizeRouteCommand` | Fully Supported |
| **RoutePlan** | `RECOMMEND_AI` | `RoutePlanningAgent` | `RoutePlanningService.GetRouteRecommendation` | `RequestRouteRecommendationCommand` | Fully Supported |
| **RouteApproval** | `LIST_PENDING` | `RoutePlanningAgent` | `RoutePlanningService.ListPendingApprovals` | `ListPendingApprovalsQuery` | Fully Supported |
| **RouteApproval** | `APPROVE` | `RoutePlanningAgent` | `RoutePlanningService.ApproveRoute` | `ApproveRouteCommand` | Fully Supported |
| **RouteApproval** | `REJECT` | `RoutePlanningAgent` | `RoutePlanningService.RejectRoute` | `RejectRouteCommand` | Fully Supported |
| **TenantAiConfig** | `GET` | `RoutePlanningAgent` | `RoutePlanningService.GetTenantAiConfig` | `GetTenantAiConfigQuery` | Fully Supported |
| **TenantAiConfig** | `UPSERT` | `RoutePlanningAgent` | `RoutePlanningService.UpsertTenantAiConfig` | `UpsertTenantAiConfigCommand` | Fully Supported |
| **TenantRuleConfig** | `LIST` | `RoutePlanningAgent` | `RoutePlanningService.ListTenantRuleConfigs` | `ListTenantRuleConfigsQuery` | Fully Supported |
| **TenantRuleConfig** | `UPSERT` | `RoutePlanningAgent` | `RoutePlanningService.UpsertTenantRuleConfig` | `UpsertTenantRuleConfigCommand` | Fully Supported |
| **MailDomain** | `PROVISION` | `MailService` | `MailManagement.ProvisionDomain` | `ProvisionDomainCommand` | Fully Supported |
| **Mailbox** | `CREATE` | `MailService` | `MailManagement.CreateMailbox` | `CreateMailboxCommand` | Fully Supported |
| **MailAlias** | `CREATE` | `MailService` | `MailManagement.CreateAlias` | `CreateAliasCommand` | Fully Supported |
| **EmailDraft** | `CREATE` | `MailService` | `MailSecurity.CreateDraftMessage` | `CreateDraftMessageCommand` | Fully Supported |
| **EmailDraft** | `GET` | `MailService` | `MailSecurity.GetDraft` | `GetDraftQuery` | Fully Supported |
| **EmailDraft** | `LIST` | `MailService` | `MailSecurity.ListDrafts` | `ListDraftsQuery` | Fully Supported |
| **OutboundEmail** | `SUBMIT_SEND` | `MailService` | `MailSecurity.SubmitOutboundMessage` | `SubmitOutboundMessageCommand` | Fully Supported |
| **ProcessedEmail** | `GET` | `MailService` | `MailSecurity.GetProcessedMessage` | `GetProcessedMessageQuery` | Fully Supported |
| **ProcessedEmail** | `LIST` | `MailService` | `MailSecurity.ListProcessedMessages` | `ListProcessedMessagesQuery` | Fully Supported |
| **MailQuarantine** | `GET` | `MailService` | `MailSecurity.GetQuarantineRecord` | `GetQuarantineRecordQuery` | Fully Supported |
| **MailQuarantine** | `LIST` | `MailService` | `MailSecurity.ListQuarantineRecords` | `ListQuarantineRecordsQuery` | Fully Supported |
| **MailQuarantine** | `RELEASE` | `MailService` | `MailSecurity.ReleaseQuarantine` | `ReleaseQuarantineCommand` | Fully Supported |
| **MailQuarantine** | `DELETE` | `MailService` | `MailSecurity.DeleteQuarantine` | `DeleteQuarantineCommand` | Fully Supported |
| **MailAudit** | `GET_RECORDS` | `MailService` | `MailManagement.GetAuditRecords` | `GetAuditRecordsQuery` | Fully Supported |
| **DeadLetterQueue** | `REQUEUE` | `MailService` | `MailManagement.RequeueDeadLetter` | `RequeueDeadLetterCommand` | Fully Supported |
| **AiPolicy** | `EVALUATE` | `ai-governance` | `AiGovernanceService.ExecutePolicy` | `GovernancePolicyService.evaluate` | Fully Supported (Internal) |
| **AiExecution** | `GENERATE` | `ai-governance` | `AiExecutionService.Generate` | `ExecuteAiService.generate` | Fully Supported (Internal) |
| **AiExecution** | `EMBED` | `ai-governance` | `AiExecutionService.Embed` | `ExecuteAiService.embed` | Fully Supported (Internal) |
| **DevOpsAlert** | `INGEST` | `devops-agent` | `DevOpsIngestionService.IngestAlert` | `IngestAlertCommand` | Fully Supported (Webhook) |
| **DevOpsIncident** | `GET` | `devops-agent` | `DevOpsIncidentService.GetIncident` | `GetIncidentQueryHandler` | Fully Supported |
| **DevOpsIncident** | `LIST` | `devops-agent` | `DevOpsIncidentService.ListIncidents` | `ListIncidentsQueryHandler` | Fully Supported |
| **DevOpsIncident** | `APPROVE` | `devops-agent` | `DevOpsIncidentService.ApproveIncident` | Direct repo save + outbox | Fully Supported |
| **DevOpsIncident** | `REJECT` | `devops-agent` | `DevOpsIncidentService.RejectIncident` | Direct repo save + outbox | Fully Supported |
| **AutoRemediationRule** | `CREATE` | `devops-agent` | `DevOpsRuleService.CreateRule` | `CreateRuleCommand` | Fully Supported |
| **AutoRemediationRule** | `LIST` | `devops-agent` | `DevOpsRuleService.ListExistingRules` | `ListRulesQueryHandler` | Fully Supported |
| **AutoRemediationRule** | `UPDATE` | `devops-agent` | `DevOpsRuleService.UpdateRule` | Direct repo save | Fully Supported |
| **AutoRemediationRule** | `DELETE` | `devops-agent` | `DevOpsRuleService.DeleteRule` | Direct repo delete | Fully Supported |
| **AutoRemediationRule** | `LIST_PENDING` | `devops-agent` | `DevOpsRuleService.ListPendingRules` | `pendingRuleRepository` query | Fully Supported |
| **AutoRemediationRule** | `APPROVE_PENDING`| `devops-agent` | `DevOpsRuleService.ApprovePendingRule` | Direct promote + outbox | Fully Supported |
| **AutoRemediationRule** | `REJECT_PENDING` | `devops-agent` | `DevOpsRuleService.RejectPendingRule` | Direct reject + outbox | Fully Supported |
| **DevOpsSelfConfig** | `GET` | `devops-agent` | `DevOpsConfigService.GetSelfConfig` | `GetSelfConfigQueryHandler` | Fully Supported |
| **DevOpsSelfConfig** | `UPDATE` | `devops-agent` | `DevOpsConfigService.UpdateSelfConfig` | `UpdateSelfConfigCommand` | Fully Supported |
| **Invoice** | `GENERATE_AUTO` | `billing-service` | `BillingService.GenerateInvoice` | `GenerateInvoiceUseCase.execute` | Fully Supported |
| **Invoice** | `CREATE_MANUAL` | `billing-service` | `BillingService.CreateInvoice` | `BillingService.createInvoice` | Fully Supported |
| **Invoice** | `GET_DETAIL` | `billing-service` | `BillingService.GetInvoiceDetail` | `BillingService.getInvoiceDetail` | Fully Supported |
| **Invoice** | `LIST` | `billing-service` | `BillingService.ListInvoices` | `BillingService.listInvoices` | Fully Supported |
| **Invoice** | `UPDATE_STATUS` | `billing-service` | `BillingService.UpdateInvoiceStatus` | `BillingService.updateInvoiceStatus` | Fully Supported |
| **Invoice** | `RECORD_PAYMENT` | `billing-service` | `@GrpcMethod` *(No Proto)* | `BillingService.recordPayment` | **Candidate Proto Contract** |
| **Invoice** | `CANCEL` | `billing-service` | `@GrpcMethod` *(No Proto)* | `BillingService.cancelInvoice` | **Candidate Proto Contract** |
| **AdjustmentNote** | `ISSUE_DEBIT_NOTE` | `billing-service` | `@GrpcMethod` *(No Proto)* | `BillingService.issueDebitNote` | **Candidate Proto Contract** |
| **AdjustmentNote** | `ISSUE_CREDIT_NOTE`| `billing-service` | `@GrpcMethod` *(No Proto)* | `BillingService.issueCreditNote` | **Candidate Proto Contract** |
| **CustomerCredit** | `CHECK_CREDIT` | `billing-service` | `BillingService.CheckCustomerCredit` | `BillingService.checkCustomerCredit` | Fully Supported |
| **EscrowWallet** | `CREATE` | `billing-service` | `BillingService.CreateEscrowWallet` | `BillingService.createEscrowWallet` | Fully Supported |
| **EscrowWallet** | `GET_BALANCE` | `billing-service` | `BillingService.GetWalletBalance` | `BillingService.getWalletBalance` | Fully Supported |
| **EscrowTransaction** | `FREEZE` | `billing-service` | `BillingService.FreezeEscrowAmount` | `BillingService.freezeEscrowAmount` | Fully Supported |
| **EscrowTransaction** | `RELEASE` | `billing-service` | `BillingService.ReleaseEscrowAmount` | `BillingService.releaseEscrowAmount` | Fully Supported |
| **EscrowTransaction** | `REFUND` | `billing-service` | `BillingService.RefundEscrowAmount` | `BillingService.refundEscrowAmount` | Fully Supported |
| **CostEstimation** | `ESTIMATE_COST` | `financial-service` | `FinancialService.EstimateCost` | `FinancialService.estimateCost` | Fully Supported |
| **CustomsDuty** | `CALCULATE_DUTY` | `financial-service` | `FinancialService.GetCustomsDuty` | `FinancialService.getCustomsDuty` | Fully Supported |
| **PricingRate** | `GET_FLOOR_RATE` | `financial-service` | `FinancialService.GetMinAcceptableRate` | `FinancialService.getMinAcceptableRate` | Fully Supported |
| **PricingRate** | `GET_DYNAMIC_MARGIN`| `financial-service` | `@GrpcMethod` *(No Proto)* | `FinancialService.getDynamicMargin` | **Candidate Proto Contract** |
| **ExchangeRate** | `GET_RATE` | `financial-service` | `@GrpcMethod` *(No Proto)* | `FinancialService.getExchangeRate` | **Candidate Proto Contract** |
| **NegotiationSession** | `SUBMIT_OFFER` | `negotiation-agent-service` | `@GrpcMethod` *(No Proto)* | `NegotiationService.submitOffer` | **Candidate Proto Contract** |
| **NegotiationSession** | `GET_HISTORY` | `negotiation-agent-service` | `@GrpcMethod` *(No Proto)* | `NegotiationService.getSessionHistory` | **Candidate Proto Contract** |
| **AssistantChat** | `SEND_MESSAGE` | `customer-assistant-service`| `None` (HTTP REST) | `ConversationalAssistantOrchestrator.handleMessage` | Fully Supported |
| **RealtimeStream** | `PUSH_EVENT` | `realtime-hub-service` | `None` (WebSocket) | `EventsGateway.broadcastToTenant` | Fully Supported |

---

## 3. CRUD & Lifecycle Gap Analysis by Resource

### 3.1. Resource: `Shipment`
- **Domain Meaning:** The core logistics unit representing cargo transport from origin to destination across multiple transport legs.
- **Lifecycle Operations:**
  - `[x] CREATE` — `CreateShipmentCommand` (`Draft` status).
  - `[x] GET` — `GetShipmentQuery` (Includes child cargo, locations, documents, milestones).
  - `[x] LIST` — `ListShipmentsQuery` (Paged, filtered by status and dates).
  - `[x] UPDATE` — `UpdateShipmentCommand` (Allowed in `Draft` and `Submitted` states).
  - `[x] SUBMIT` — `SubmitShipmentCommand` (Transitions `Draft` -> `Submitted`).
  - `[x] TRANSITION_STATUS` — `UpdateShipmentStatusCommand` (Enforces state machine).
  - `[x] CANCEL` — `CancelShipmentCommand` (Soft cancellation with event publishing).
  - `[x] DELETE` — `DeleteDraftShipmentCommand` (Hard delete restricted strictly to `Draft` status).
  - `[x] IMPORT` — `ImportShipmentsCommand` (Batch CSV/Excel ingestion).
- **CRUD Assessment:** **Complete**. Hard delete is intentionally blocked for non-draft shipments to maintain compliance audit trails.

---

### 3.2. Resource: `CargoItem`, `ShipmentLocation`, `ShipmentDocument`
- **Domain Meaning:** Dependent child entities owned by the `Shipment` aggregate root.
- **Lifecycle Operations:**
  - `[x] ADD / ATTACH` — `AddCargoItemCommand`, `AddShipmentLocationCommand`, `AttachShipmentDocumentCommand`.
  - `[x] UPDATE` — `UpdateCargoItemCommand`, `UpdateShipmentLocationCommand`, `UpdateShipmentDocumentOcrCommand`.
  - `[x] REMOVE` — `RemoveCargoItemCommand`, `RemoveShipmentLocationCommand`, `RemoveShipmentDocumentCommand`.
- **CRUD Assessment:** **Complete**. Child entities inherit aggregate boundaries and cascade delete through `ShipmentWorkflowDbContext`.

---

### 3.3. Resource: `Tenant`
- **Domain Meaning:** Customer organization boundary owning isolated data and Cognito auth clients.
- **Lifecycle Operations:**
  - `[x] CREATE` — `CreateTenantCommand` (`IamService.CreateTenant`).
  - `[x] GET` — `GetTenantQuery` (`IamService.GetTenant`).
  - `[x] LIST` — `ListTenantsQuery` (`IamService.ListTenants`).
  - `[!] UPDATE_PROFILE` — `UpdateTenantCommand` (**Missing from gRPC!** Handler exists in code).
  - `[x] UPDATE_STATUS` — `UpdateTenantStatusCommand` (`IamService.UpdateTenantStatus`).
  - `[x] DELETE` — `DeleteTenantCommand` (`IamService.DeleteTenant`).
- **CRUD Assessment:** **Partially Missing (RPC Gap)**. Tenant profile updating (Name, TaxCode, PlanType) exists in MediatR CQRS code but is omitted from `IamGrpcService.cs`.

---

### 3.4. Resource: `StaffUser`
- **Domain Meaning:** Tenant employee identity managed via IAM and synchronized with AWS Cognito.
- **Lifecycle Operations:**
  - `[x] INVITE / CREATE` — `CreateStaffCommand` (Creates user record + Cognito user).
  - `[x] GET` — `GetStaffQuery`.
  - `[x] LIST` — `ListStaffQuery`.
  - `[x] UPDATE` — `UpdateStaffCommand`.
  - `[x] ACTIVATE` — `ActivateStaffCommand`.
  - `[x] SUSPEND` — `DeactivateStaffCommand`.
  - `[x] RESET_PASSWORD` — `ResetStaffPasswordCommand`.
  - `[x] ASSIGN_ROLES` — `AssignRolesCommand`.
  - `[ ] DELETE` — **NOT REQUIRED** (Soft suspension via `DeactivateStaffCommand` is required for audit logs).
- **CRUD Assessment:** **Complete**.

---

### 3.5. Resource: `Role & Permission`
- **Domain Meaning:** RBAC system defining operational authorization for tenant users.
- **Lifecycle Operations:**
  - `[x] GET` — `GetRoleQuery`.
  - `[x] LIST` — `ListRolesQuery`.
  - `[x] ASSIGN_PERMISSIONS` — `AssignPermissionsToRoleCommand`.
  - `[x] GET_PERMISSIONS` — `GetUserPermissionsQuery`.
  - `[X] CREATE / UPDATE / DELETE ROLE` — **DELIBERATELY DISABLED** (Throws `StatusCode.Unimplemented` because system roles `ADMIN`, `MANAGER`, `STAFF` are immutable constants).
- **CRUD Assessment:** **Complete by Design**.

---

### 3.6. Resource: `RoutePlan` & `RouteApproval`
- **Domain Meaning:** Multi-stop transport route with GPS waypoints, weight/volume constraints, AI optimization, and dual-control approval tickets.
- **Lifecycle Operations:**
  - `[x] CREATE` — `CreateRouteCommand`.
  - `[x] GET` — `GetRouteQuery`.
  - `[x] LIST` — `ListRoutesQuery`.
  - `[x] UPDATE` — `UpdateRouteCommand`.
  - `[x] DELETE` — `DeleteRouteCommand`.
  - `[x] OPTIMIZE` — `OptimizeRouteCommand` (Heuristic TSP solver).
  - `[x] RECOMMEND_AI` — `RequestRouteRecommendationCommand` (LLM + Compliance RAG).
  - `[x] LIST_PENDING_APPROVALS` — `ListPendingApprovalsQuery`.
  - `[x] APPROVE` — `ApproveRouteCommand`.
  - `[x] REJECT` — `RejectRouteCommand`.
- **CRUD Assessment:** **Complete**.

---

### 3.7. Resource: `Invoice`, `AdjustmentNote`, `EscrowWallet`
- **Domain Meaning:** Financial billing documents, payment reconciliation records, and carrier escrow wallets.
- **Lifecycle Operations:**
  - `[x] GENERATE / CREATE` — `GenerateInvoiceUseCase`, `BillingService.createInvoice`.
  - `[x] GET` — `BillingService.getInvoiceDetail`.
  - `[x] LIST` — `BillingService.listInvoices`.
  - `[x] UPDATE_STATUS` — `BillingService.updateInvoiceStatus`.
  - `[!] RECORD_PAYMENT` — `BillingService.recordPayment` (**Missing in Protobuf Contract**).
  - `[!] CANCEL` — `BillingService.cancelInvoice` (**Missing in Protobuf Contract**).
  - `[!] ISSUE_DEBIT_NOTE` — `BillingService.issueDebitNote` (**Missing in Protobuf Contract**).
  - `[!] ISSUE_CREDIT_NOTE` — `BillingService.issueCreditNote` (**Missing in Protobuf Contract**).
  - `[x] ESCROW_WALLET_CRUD` — `CreateEscrowWallet`, `GetWalletBalance`, `Freeze`, `Release`, `Refund`.
- **CRUD Assessment:** **Contract Gap**. All business operations are implemented in the NestJS application layer, but post-invoicing modifications (payments, debit/credit notes) lack proto declarations.

---

### 3.8. Resource: `DocumentOcrJob`
- **Domain Meaning:** Asynchronous document text and structured key-value extraction pipeline with Human-in-the-Loop review.
- **Lifecycle Operations:**
  - `[x] SUBMIT` — `DocumentOcrJobService.SubmitOcrAsync`.
  - `[x] GET` — `DocumentOcrJobService.GetAsync`.
  - `[x] LIST` — `DocumentOcrJobService.ListAsync`.
  - `[x] CANCEL` — `DocumentOcrJobService.CancelAsync`.
  - `[x] RETRY` — `DocumentOcrJobService.RetryAsync`.
  - `[x] REVIEW` — `DocumentOcrJobService.ReviewAsync` (Human verification / correction).
- **CRUD Assessment:** **Complete**.

---

### 3.9. Resource: `RegulatorySource` & `KnowledgeDocument`
- **Domain Meaning:** Vectorized legal regulations and platform SOP knowledge articles stored in pgvector.
- **Lifecycle Operations:**
  - `[x] INGEST` — `RegulatoryIngestionService.IngestAsync`, `KnowledgeIngestionService.IngestAsync`.
  - `[x] QUERY` — `RegulationRetrievalService.QueryAsync`, `KnowledgeIngestionService.QueryAsync`.
  - `[ ] UPDATE / DELETE` — **NOT REQUIRED** (Versioned append-only ingestion model with SHA-256 deduplication).
- **CRUD Assessment:** **Complete by Design**.

---

### 3.10. Resource: `DevOpsIncident` & `AutoRemediationRule`
- **Domain Meaning:** Infrastructure alert correlation, automated root-cause analysis (RCA), and remediation rule promotion.
- **Lifecycle Operations:**
  - `[x] INGEST_ALERT` — `IngestAlertCommand`.
  - `[x] GET_INCIDENT` / `LIST_INCIDENTS` — `GetIncidentQueryHandler`, `ListIncidentsQueryHandler`.
  - `[x] APPROVE_INCIDENT` / `REJECT_INCIDENT` — Dual-control approval gates for infrastructure actions.
  - `[x] RULE_CRUD` — `CreateRuleCommand`, `UpdateRule`, `DeleteRule`, `ListExistingRules`.
  - `[x] PENDING_RULE_WORKFLOW` — `ListPendingRules`, `ApprovePendingRule` (Promotes AI-discovered rule to active), `RejectPendingRule`.
- **CRUD Assessment:** **Complete**.

---

## 4. Missing Business Operations Categorization

### 4.1. Clearly Missing Business Operations
*(Application layer supports the capability, but external/BFF access is blocked by missing RPC contracts)*

1. **`Tenant.UpdateProfile`**:
   - `UpdateTenantCommand` exists in [UpdateTenantCommand.cs](file:///D:/IT/CD/aurora-server/src/dotnet/IamTenant/Application/Commands/Tenants/UpdateTenantCommand.cs) allowing update of tenant company name, tax code, and plan tier, but is missing from [iam_tenant.proto](file:///D:/IT/CD/aurora-server/protos/iam_tenant.proto) and [IamGrpcService.cs](file:///D:/IT/CD/aurora-server/src/dotnet/IamTenant/GrpcServices/IamGrpcService.cs).
2. **`Invoice.RecordPayment`**:
   - Billing clerk payment entry is implemented in [billing.service.ts](file:///D:/IT/CD/aurora-server/src/nestjs/billing-service/src/application/services/billing.service.ts), but missing in [billing.proto](file:///D:/IT/CD/aurora-server/protos/billing.proto).
3. **`Invoice.IssueAdjustmentNote` (Debit / Credit Notes)**:
   - Post-invoicing dispute adjustments are implemented in [billing.service.ts](file:///D:/IT/CD/aurora-server/src/nestjs/billing-service/src/application/services/billing.service.ts), but missing in [billing.proto](file:///D:/IT/CD/aurora-server/protos/billing.proto).
4. **`Negotiation.SubmitOffer` & `Negotiation.GetSessionHistory`**:
   - AI freight rate negotiation is implemented in [negotiation.service.ts](file:///D:/IT/CD/aurora-server/src/nestjs/negotiation-agent-service/src/application/services/negotiation.service.ts), but completely lacks a `.proto` file in [protos/](file:///D:/IT/CD/aurora-server/protos).

---

### 4.2. Potentially Missing Operations
*(Domain operations that are common in logistics, but not yet implemented in backend code)*

1. **`Shipment.Duplicate` / `CloneDraft`**:
   - Currently, creating recurring shipments requires re-entering all cargo items and locations from scratch.
2. **`ShipmentDocument.DownloadUrl`**:
   - Documents have `storage_reference`, but there is no dedicated gRPC presigned download URL generator (handled via S3 direct presigning in BFF/Gateway).
3. **`Notification.MarkAllAsRead`**:
   - Currently, notifications must be marked as read one by one via `MarkNotificationRead(id)`.

---

### 4.3. Deliberately Internal Operations
*(Operations that are properly restricted from public/BFF access)*

1. **`AiExecution.Generate` / `AiExecution.Embed`**: Internal gateway between backend microservices and LLM providers.
2. **`GpsTracking.IngestPosition`**: High-throughput telemetry endpoint for IoT hardware edge gateways.
3. **`DeterministicCitationValidator.Validate`**: Internal safety filter inside the compliance RAG pipeline.
4. **`ShipmentWorkflow.UpdateShipmentDocumentOcr`**: Internal callback from OCR worker to update shipment document metadata.
5. **`FinancialService.GetMinAcceptableRate`**: Confidential pricing floor calculation used internally by the automated negotiation agent.

---

### 4.4. Unknown / Deferred Operations

1. **`DevOpsRagService.IngestKnowledge`**: Protobuf stub exists in `devops_rag.proto`, but no server implementation is present in the repository.
2. **`AiGovernanceAdminService`**: Protobuf service is commented out as a future V2 feature.
