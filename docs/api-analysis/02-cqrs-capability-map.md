# Aurora Platform - CQRS & Application Capability Map

> **Document ID:** `DOC-API-02`  
> **Status:** Application Layer Discovery & CQRS Audit Complete  
> **Scope:** Full analysis of Commands, CommandHandlers, Queries, QueryHandlers, Application Services, UseCases, Orchestrators, and Facades across all 16 backend microservices (.NET 10, Java 21, NestJS).  
> **Architecture Reference:** `codex/requirement.md`, `codex/specs/logistics-architecture.md`, `docs/api-analysis/01-grpc-capability-map.md`

---

## 1. Primary CQRS & Application Capability Table

The following master table audits all application-layer capabilities, mapping their CQRS type, handler implementation, trigger mechanisms (gRPC, HTTP, Event), tenant isolation sensitivity, and BFF exposure recommendation.

| Service | Capability | Type | Handler | gRPC | HTTP | Event | Tenant | Potential BFF |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| **ShipmentWorkflow** | `CreateShipmentCommand` | Command | `CreateShipmentCommandHandler` | `ShipmentWorkflowService.CreateShipment` | `None` | `None` | Isolated (`ICurrentUserService`) | **YES** (Core Portal Creation) |
| **ShipmentWorkflow** | `SubmitShipmentCommand` | Command | `SubmitShipmentCommandHandler` | `ShipmentWorkflowService.SubmitShipment` | `None` | `None` | Isolated (`ICurrentUserService`) | **YES** (Workflow Submission) |
| **ShipmentWorkflow** | `UpdateShipmentCommand` | Command | `UpdateShipmentCommandHandler` | `ShipmentWorkflowService.UpdateShipment` | `None` | `None` | Isolated (`ICurrentUserService`) | **YES** (Draft/Info Editing) |
| **ShipmentWorkflow** | `UpdateShipmentStatusCommand` | Command | `UpdateShipmentStatusCommandHandler` | `ShipmentWorkflowService.UpdateShipmentStatus` | `None` | `None` | Isolated (`ICurrentUserService`) | **YES** (Manual Status Transition) |
| **ShipmentWorkflow** | `CancelShipmentCommand` | Command | `CancelShipmentCommandHandler` | `ShipmentWorkflowService.CancelShipment` | `None` | `None` | Isolated (`ICurrentUserService`) | **YES** (Shipment Cancellation) |
| **ShipmentWorkflow** | `DeleteDraftShipmentCommand` | Command | `DeleteDraftShipmentCommandHandler` | `ShipmentWorkflowService.DeleteDraftShipment` | `None` | `None` | Isolated (`ICurrentUserService`) | **YES** (Draft Purge) |
| **ShipmentWorkflow** | `AddCargoItemCommand` | Command | `AddCargoItemCommandHandler` | `ShipmentWorkflowService.AddCargoItem` | `None` | `None` | Isolated (`ICurrentUserService`) | **YES** (Cargo Sub-resource) |
| **ShipmentWorkflow** | `UpdateCargoItemCommand` | Command | `UpdateCargoItemCommandHandler` | `ShipmentWorkflowService.UpdateCargoItem` | `None` | `None` | Isolated (`ICurrentUserService`) | **YES** (Cargo Sub-resource) |
| **ShipmentWorkflow** | `RemoveCargoItemCommand` | Command | `RemoveCargoItemCommandHandler` | `ShipmentWorkflowService.RemoveCargoItem` | `None` | `None` | Isolated (`ICurrentUserService`) | **YES** (Cargo Sub-resource) |
| **ShipmentWorkflow** | `AddShipmentLocationCommand` | Command | `AddShipmentLocationCommandHandler` | `ShipmentWorkflowService.AddShipmentLocation` | `None` | `None` | Isolated (`ICurrentUserService`) | **YES** (Location Checkpoint) |
| **ShipmentWorkflow** | `UpdateShipmentLocationCommand`| Command | `UpdateShipmentLocationCommandHandler` | `ShipmentWorkflowService.UpdateShipmentLocation` | `None` | `None` | Isolated (`ICurrentUserService`) | **YES** (Location Checkpoint) |
| **ShipmentWorkflow** | `RemoveShipmentLocationCommand`| Command | `RemoveShipmentLocationCommandHandler` | `ShipmentWorkflowService.RemoveShipmentLocation` | `None` | `None` | Isolated (`ICurrentUserService`) | **YES** (Location Checkpoint) |
| **ShipmentWorkflow** | `AttachShipmentDocumentCommand` | Command | `AttachShipmentDocumentCommandHandler` | `ShipmentWorkflowService.AttachShipmentDocument` | `None` | `None` | Isolated (`ICurrentUserService`) | **YES** (Document Upload Link) |
| **ShipmentWorkflow** | `UpdateShipmentDocumentOcrCommand` | Command | `UpdateShipmentDocumentOcrCommandHandler` | `ShipmentWorkflowService.UpdateShipmentDocumentOcr` | `None` | `None` | Isolated (`ICurrentUserService`) | **NO** (Internal Async Callback) |
| **ShipmentWorkflow** | `RemoveShipmentDocumentCommand` | Command | `RemoveShipmentDocumentCommandHandler` | `ShipmentWorkflowService.RemoveShipmentDocument` | `None` | `None` | Isolated (`ICurrentUserService`) | **YES** (Document Deletion) |
| **ShipmentWorkflow** | `RecordShipmentMilestoneCommand` | Command | `RecordShipmentMilestoneCommandHandler` | `ShipmentWorkflowService.AddShipmentMilestone` | `None` | `None` | Isolated (`ICurrentUserService`) | **YES** (Operational Timeline) |
| **ShipmentWorkflow** | `ImportShipmentsCommand` | Command | `ImportShipmentsCommandHandler` | `ShipmentWorkflowService.ImportShipments` | `None` | `None` | Isolated (`ICurrentUserService`) | **YES** (Bulk CSV/Excel Import) |
| **ShipmentWorkflow** | `GetShipmentQuery` | Query | `GetShipmentQueryHandler` | `ShipmentWorkflowService.GetShipment` | `None` | `None` | Isolated (`ICurrentUserService`) | **YES** (Shipment Details) |
| **ShipmentWorkflow** | `ListShipmentsQuery` | Query | `ListShipmentsQueryHandler` | `ShipmentWorkflowService.ListShipments` | `None` | `None` | Isolated (`ICurrentUserService`) | **YES** (Shipment Data Grid) |
| **ShipmentWorkflow** | `GetShipmentTimelineQuery` | Query | `GetShipmentTimelineQueryHandler` | `ShipmentWorkflowService.GetShipmentTimeline` | `None` | `None` | Isolated (`ICurrentUserService`) | **YES** (Audit / Tracking Timeline)|
| **Notification** | `ListNotifications` | Query | Direct `NotificationDbContext` query | `NotificationService.ListNotifications` | `None` | `None` | Isolated (`RecipientUserId`) | **YES** (Notification Bell / Feed) |
| **Notification** | `MarkNotificationRead` | Command | Direct entity mutation | `NotificationService.MarkNotificationRead` | `None` | `None` | Isolated (`RecipientUserId`) | **YES** (Dismiss Notification) |
| **Notification** | `ListNotificationPreferences` | Query | Direct `NotificationDbContext` query | `NotificationService.ListNotificationPreferences` | `None` | `None` | Isolated (`RecipientUserId`) | **YES** (User Settings Modal) |
| **Notification** | `UpsertNotificationPreference` | Command | Direct entity mutation | `NotificationService.UpsertNotificationPreference` | `None` | `None` | Isolated (`RecipientUserId`) | **YES** (User Settings Modal) |
| **Notification** | `ShipmentNotificationConsumer` | Consumer | `ShipmentNotificationConsumer.Consume` | `None` | `None` | `Shipment*IntegrationEvent` | Isolated (`TenantId` in payload) | **NO** (Asynchronous Projection) |
| **Notification** | `DocumentOcrNotificationConsumer` | Consumer | `DocumentOcrNotificationConsumer.Consume` | `None` | `None` | `DocumentOcr*IntegrationEvent` | Isolated (`TenantId` in payload) | **NO** (Asynchronous Projection) |
| **Notification** | `GpsNotificationConsumer` | Consumer | `GpsNotificationConsumer.Consume` | `None` | `None` | `MonitoringAlertTriggered` | Isolated (`TenantId` in payload) | **NO** (Asynchronous Projection) |
| **Notification** | `ComplianceNotificationConsumer` | Consumer | `ComplianceNotificationConsumer.Consume` | `None` | `None` | `ComplianceEvaluationCompleted` | Isolated (`TenantId` in payload) | **NO** (Asynchronous Projection) |
| **GpsTracking** | `PositionIngestionService.IngestAsync` | Command | `PositionIngestionService` | `GpsTrackingService.IngestPosition` | `None` | `None` | Isolated (`ICurrentUserService`) | **NO** (IoT Edge Ingestion Only) |
| **GpsTracking** | `LocationQueryService.GetCurrentAsync` | Query | `LocationQueryService` | `GpsTrackingService.GetCurrentLocation` | `None` | `None` | Isolated (`ICurrentUserService`) | **YES** (Live Map Tracking) |
| **GpsTracking** | `LocationQueryService.ListHistoryAsync` | Query | `LocationQueryService` | `GpsTrackingService.ListPositionHistory` | `None` | `None` | Isolated (`ICurrentUserService`) | **YES** (Route Breadcrumb History) |
| **GpsTracking** | `MonitoringManagementService.CreateGeofenceAsync` | Command | `MonitoringManagementService` | `GpsTrackingService.CreateGeofence` | `None` | `None` | Isolated (`ICurrentUserService`) | **YES** (Geofence Setup) |
| **GpsTracking** | `MonitoringManagementService.ListGeofencesAsync` | Query | `MonitoringManagementService` | `GpsTrackingService.ListGeofences` | `None` | `None` | Isolated (`ICurrentUserService`) | **YES** (Geofence Map Layer) |
| **GpsTracking** | `MonitoringManagementService.SetGeofenceActiveAsync` | Command | `MonitoringManagementService` | `GpsTrackingService.SetGeofenceActive` | `None` | `None` | Isolated (`ICurrentUserService`) | **YES** (Geofence Toggle) |
| **GpsTracking** | `MonitoringManagementService.ListAlertsAsync` | Query | `MonitoringManagementService` | `GpsTrackingService.ListMonitoringAlerts` | `None` | `None` | Isolated (`ICurrentUserService`) | **YES** (Exception Alert Dashboard) |
| **GpsTracking** | `MonitoringManagementService.ResolveAlertAsync` | Command | `MonitoringManagementService` | `GpsTrackingService.ResolveMonitoringAlert` | `None` | `None` | Isolated (`ICurrentUserService`) | **YES** (Alert Dismissal / Resolve) |
| **GpsTracking** | `ShipmentTrackingConsumer` | Consumer | `ShipmentTrackingConsumer.Consume` | `None` | `None` | `Shipment*IntegrationEvent` | Isolated (`TenantId` in payload) | **NO** (Internal Route Sync) |
| **DocumentOcr** | `DocumentOcrJobService.SubmitOcrAsync` | Command | `DocumentOcrJobService` | `DocumentOcrService.SubmitOcrJob` | `None` | `None` | Isolated (`ICurrentUserService`) | **YES** (Document OCR Trigger) |
| **DocumentOcr** | `DocumentOcrJobService.GetAsync` | Query | `DocumentOcrJobService` | `DocumentOcrService.GetDocumentJob` | `None` | `None` | Isolated (`ICurrentUserService`) | **YES** (Job Status & Extracted JSON)|
| **DocumentOcr** | `DocumentOcrJobService.ListAsync` | Query | `DocumentOcrJobService` | `DocumentOcrService.ListDocumentJobs` | `None` | `None` | Isolated (`ICurrentUserService`) | **YES** (Document Processing Queue)|
| **DocumentOcr** | `DocumentOcrJobService.CancelAsync` | Command | `DocumentOcrJobService` | `DocumentOcrService.CancelDocumentJob` | `None` | `None` | Isolated (`ICurrentUserService`) | **YES** (Cancel Processing) |
| **DocumentOcr** | `DocumentOcrJobService.RetryAsync` | Command | `DocumentOcrJobService` | `DocumentOcrService.RetryDocumentJob` | `None` | `None` | Isolated (`ICurrentUserService`) | **YES** (Retry Failed OCR) |
| **DocumentOcr** | `DocumentOcrJobService.ReviewAsync` | Command | `DocumentOcrJobService` | `DocumentOcrService.ReviewDocumentJob` | `None` | `None` | Isolated (`ICurrentUserService`) | **YES** (Human-in-the-Loop Review) |
| **RegulatoryCompliance** | `ComplianceEvaluationService.EvaluateAsync` | Command | `ComplianceEvaluationService` | `RegulatoryComplianceService.EvaluateCompliance` | `None` | `None` | Isolated (`ICurrentUserService`) | **YES** (Pre-dispatch Compliance Check) |
| **RegulatoryCompliance** | `ComplianceEvaluationService.GetAsync` | Query | `ComplianceEvaluationService` | `RegulatoryComplianceService.GetComplianceEvaluation` | `None` | `None` | Isolated (`ICurrentUserService`) | **YES** (Compliance Report View) |
| **RegulatoryCompliance** | `RegulationRetrievalService.QueryAsync` | Query | `RegulationRetrievalService` | `RegulatoryComplianceService.QueryRegulations` | `None` | `None` | Global Platform + Tenant | **YES** (Legal Regulation Search) |
| **RegulatoryCompliance** | `RegulatoryIngestionService.IngestAsync` | Command | `RegulatoryIngestionService` | `RegulatoryComplianceService.IngestRegulatorySource` | `None` | `None` | Platform Admin Scope | **YES** (Admin / System Law Ingestion) |
| **RegulatoryCompliance** | `KnowledgeIngestionService.IngestAsync` | Command | `KnowledgeIngestionService` | `RegulatoryComplianceService.IngestKnowledgeDocument` | `None` | `None` | Global Platform + Tenant | **YES** (Knowledge Base Upload) |
| **RegulatoryCompliance** | `KnowledgeIngestionService.QueryAsync` | Query | `KnowledgeIngestionService` | `RegulatoryComplianceService.QueryKnowledge` | `None` | `None` | Global Platform + Tenant | **YES** (Knowledge Article Search) |
| **RegulatoryCompliance** | `GroundedAnswerService.GenerateAnswerAsync` | Command / Orchestrator | `GroundedAnswerService` | `RegulatoryComplianceService.GenerateGroundedAnswer` | `None` | `None` | Global Platform + Tenant | **YES** (AI Compliance Copilot) |
| **RegulatoryCompliance** | `DeterministicCitationValidator.Validate` | Facade / Validator | `DeterministicCitationValidator` | `RegulatoryComplianceService.ValidateGroundedEvidence` | `None` | `None` | Stateless | **NO** (Internal RAG Pipeline Guard) |
| **RegulatoryCompliance** | `DocumentOcrIntegrationConsumer` | Consumer | `DocumentOcrIntegrationConsumer.Consume` | `None` | `None` | `DocumentOcrCompletedIntegrationEvent` | Isolated (`TenantId` in payload) | **NO** (Auto Re-evaluation) |
| **MailService** | `ProvisionDomainCommand` | Command | `ProvisionDomainCommandHandler` | `MailManagement.ProvisionDomain` | `None` | `None` | Isolated (`ICurrentUserService`) | **YES** (Admin Domain Setup) |
| **MailService** | `CreateMailboxCommand` | Command | `CreateMailboxCommandHandler` | `MailManagement.CreateMailbox` | `None` | `None` | Isolated (`ICurrentUserService`) | **YES** (Admin Mailbox Provision) |
| **MailService** | `CreateAliasCommand` | Command | `CreateAliasCommandHandler` | `MailManagement.CreateAlias` | `None` | `None` | Isolated (`ICurrentUserService`) | **YES** (Admin Inbound Routing) |
| **MailService** | `CreateDraftMessageCommand` | Command | `CreateDraftMessageCommandHandler` | `MailSecurity.CreateDraftMessage` | `None` | `None` | Isolated (`ICurrentUserService`) | **YES** (Staff Email Composer) |
| **MailService** | `SubmitOutboundMessageCommand` | Command | `SubmitOutboundMessageCommandHandler` | `MailSecurity.SubmitOutboundMessage` | `None` | `None` | Isolated (`ICurrentUserService`) | **YES** (Staff Outbound Send) |
| **MailService** | `ReleaseQuarantineCommand` | Command | `ReleaseQuarantineCommandHandler` | `MailSecurity.ReleaseQuarantine` | `None` | `None` | Isolated (`ICurrentUserService`) | **YES** (Security Review Release) |
| **MailService** | `DeleteQuarantineCommand` | Command | `DeleteQuarantineCommandHandler` | `MailSecurity.DeleteQuarantine` | `None` | `None` | Isolated (`ICurrentUserService`) | **YES** (Security Review Purge) |
| **MailService** | `RequeueDeadLetterCommand` | Command | `RequeueDeadLetterCommandHandler` | `MailManagement.RequeueDeadLetter` | `None` | `None` | Platform Admin Scope | **YES** (System Operations) |
| **MailService** | `GetAuditRecordsQuery` | Query | `GetAuditRecordsQueryHandler` | `MailManagement.GetAuditRecords` | `None` | `None` | Isolated (`ICurrentUserService`) | **YES** (Admin Security Audit Log) |
| **MailService** | `GetDraftQuery` | Query | `GetDraftQueryHandler` | `MailSecurity.GetDraft` | `None` | `None` | Isolated (`ICurrentUserService`) | **YES** (Staff Email Editor) |
| **MailService** | `ListDraftsQuery` | Query | `ListDraftsQueryHandler` | `MailSecurity.ListDrafts` | `None` | `None` | Isolated (`ICurrentUserService`) | **YES** (Staff Outbox / Drafts) |
| **MailService** | `GetProcessedMessageQuery` | Query | `GetProcessedMessageQueryHandler` | `MailSecurity.GetProcessedMessage` | `None` | `None` | Isolated (`ICurrentUserService`) | **YES** (Email Security Inspection)|
| **MailService** | `ListProcessedMessagesQuery` | Query | `ListProcessedMessagesQueryHandler` | `MailSecurity.ListProcessedMessages` | `None` | `None` | Isolated (`ICurrentUserService`) | **YES** (Email Activity Feed) |
| **MailService** | `GetQuarantineRecordQuery` | Query | `GetQuarantineRecordQueryHandler` | `MailSecurity.GetQuarantineRecord` | `None` | `None` | Isolated (`ICurrentUserService`) | **YES** (Quarantine Inspection) |
| **MailService** | `ListQuarantineRecordsQuery` | Query | `ListQuarantineRecordsQueryHandler` | `MailSecurity.ListQuarantineRecords` | `None` | `None` | Isolated (`ICurrentUserService`) | **YES** (Quarantine Inbox) |
| **IamTenant** | `CreateTenantCommand` | Command | `CreateTenantCommandHandler` | `IamService.CreateTenant` | `None` | `None` | System Multi-tenant | **YES** (System Admin Onboarding) |
| **IamTenant** | `UpdateTenantStatusCommand` | Command | `UpdateTenantStatusCommandHandler` | `IamService.UpdateTenantStatus` | `None` | `None` | System Multi-tenant | **YES** (Suspend / Activate Tenant) |
| **IamTenant** | `UpdateTenantCommand` | Command | `UpdateTenantHandler` | **None** | `None` | `None` | System Multi-tenant | **YES** (Candidate Missing RPC) |
| **IamTenant** | `DeleteTenantCommand` | Command | `DeleteTenantCommandHandler` | `IamService.DeleteTenant` | `None` | `None` | System Multi-tenant | **YES** (System Admin Offboarding) |
| **IamTenant** | `CreateStaffCommand` | Command | `CreateStaffCommandHandler` | `IamService.InviteUser` | `None` | `None` | Isolated (`ICurrentUserService`) | **YES** (Tenant Admin Invite Staff) |
| **IamTenant** | `UpdateStaffCommand` | Command | `UpdateStaffCommandHandler` | `IamService.UpdateUser` | `None` | `None` | Isolated (`ICurrentUserService`) | **YES** (Edit Staff Profile) |
| **IamTenant** | `ActivateStaffCommand` | Command | `ActivateStaffCommandHandler` | `IamService.ActivateUser` | `None` | `None` | Isolated (`ICurrentUserService`) | **YES** (Unsuspend Staff) |
| **IamTenant** | `DeactivateStaffCommand` | Command | `DeactivateStaffCommandHandler` | `IamService.SuspendUser` | `None` | `None` | Isolated (`ICurrentUserService`) | **YES** (Suspend Staff) |
| **IamTenant** | `ResetStaffPasswordCommand` | Command | `ResetStaffPasswordCommandHandler` | `IamService.ResetUserPassword` | `None` | `None` | Isolated (`ICurrentUserService`) | **YES** (Admin Trigger Password Reset)|
| **IamTenant** | `AssignRolesCommand` | Command | `AssignRolesCommandHandler` | `IamService.AssignRoles` | `None` | `None` | Isolated (`ICurrentUserService`) | **YES** (Role Assignment) |
| **IamTenant** | `AssignPermissionsToRoleCommand` | Command | `AssignPermissionsToRoleCommandHandler` | `IamService.AssignPermissionsToRole` | `None` | `None` | Isolated (`ICurrentUserService`) | **YES** (Permission Customization) |
| **IamTenant** | `LoginCommand` | Command | `LoginCommandHandler` | `AuthService.Login` | `None` | `None` | Pre-Auth (Cognito Dispatch) | **YES** (User Sign-in) |
| **IamTenant** | `CompleteInvitationCommand` | Command | `CompleteInvitationCommandHandler` | `AuthService.CompleteInvitation` | `None` | `None` | Pre-Auth (Cognito Challenge) | **YES** (First-time Login) |
| **IamTenant** | `GetTenantQuery` | Query | `GetTenantQueryHandler` | `IamService.GetTenant` | `None` | `None` | System Multi-tenant | **YES** (Tenant Detail) |
| **IamTenant** | `ListTenantsQuery` | Query | `ListTenantsQueryHandler` | `IamService.ListTenants` | `None` | `None` | System Multi-tenant | **YES** (Tenant Management Grid) |
| **IamTenant** | `GetStaffQuery` | Query | `GetStaffQueryHandler` | `IamService.GetUser` | `None` | `None` | Isolated (`ICurrentUserService`) | **YES** (Staff Detail / Profile) |
| **IamTenant** | `ListStaffQuery` | Query | `ListStaffQueryHandler` | `IamService.GetManyUsers` | `None` | `None` | Isolated (`ICurrentUserService`) | **YES** (Staff Directory Grid) |
| **IamTenant** | `GetRoleQuery` | Query | `GetRoleQueryHandler` | `IamService.GetRole` | `None` | `None` | Global / Tenant Scope | **YES** (Role Detail) |
| **IamTenant** | `ListRolesQuery` | Query | `ListRolesQueryHandler` | `IamService.GetManyRoles` | `None` | `None` | Global / Tenant Scope | **YES** (Role Selection List) |
| **IamTenant** | `GetUserPermissionsQuery` | Query | `GetUserPermissionsQueryHandler` | `IamService.GetUserPermissions` | `None` | `None` | Isolated (`ICurrentUserService`) | **YES** (User Capability Matrix) |
| **IamTenant** | `IdentifyUserQuery` | Query | `IdentifyUserQueryHandler` | `AuthService.IdentifyUser` | `None` | `None` | Pre-Auth (Global Email Lookup) | **YES** (Smart Login Screen) |
| **IamTenant** | `ResolveTenantAuthClientQuery` | Query | `ResolveTenantAuthClientQueryHandler` | `None` (Internal) | `None` | `None` | Pre-Auth (Internal Lookup) | **NO** (Internal Cognito Router) |
| **RoutePlanningAgent** | `CreateRouteCommand` | Command | `CreateRouteCommandHandler` | `RoutePlanningService.CreateRoute` | `None` | `None` | Isolated (`ICurrentUserService`) | **YES** (Route Creation Wizard) |
| **RoutePlanningAgent** | `UpdateRouteCommand` | Command | `UpdateRouteCommandHandler` | `RoutePlanningService.UpdateRoute` | `None` | `None` | Isolated (`ICurrentUserService`) | **YES** (Route Editor) |
| **RoutePlanningAgent** | `DeleteRouteCommand` | Command | `DeleteRouteCommandHandler` | `RoutePlanningService.DeleteRoute` | `None` | `None` | Isolated (`ICurrentUserService`) | **YES** (Route Removal) |
| **RoutePlanningAgent** | `UpdateRouteStatusCommand` | Command | `UpdateRouteStatusCommandHandler` | `RoutePlanningService.UpdateRouteStatus` | `None` | `None` | Isolated (`ICurrentUserService`) | **YES** (Status Transition) |
| **RoutePlanningAgent** | `OptimizeRouteCommand` | Command | `OptimizeRouteCommandHandler` | `RoutePlanningService.OptimizeRoute` | `None` | `None` | Isolated (`ICurrentUserService`) | **YES** (Route Optimization Button) |
| **RoutePlanningAgent** | `ApproveRouteCommand` | Command | `ApproveRouteCommandHandler` | `RoutePlanningService.ApproveRoute` | `None` | `None` | Isolated (`ICurrentUserService`) | **YES** (Manager Approval Modal) |
| **RoutePlanningAgent** | `RejectRouteCommand` | Command | `RejectRouteCommandHandler` | `RoutePlanningService.RejectRoute` | `None` | `None` | Isolated (`ICurrentUserService`) | **YES** (Manager Rejection Modal) |
| **RoutePlanningAgent** | `RequestRouteRecommendationCommand`| Command / Orchestrator | `RequestRouteRecommendationCommandHandler` | `RoutePlanningService.GetRouteRecommendation` | `None` | `None` | Isolated (`ICurrentUserService`) | **YES** (AI Route Copilot) |
| **RoutePlanningAgent** | `UpsertTenantAiConfigCommand` | Command | `UpsertTenantAiConfigCommandHandler` | `RoutePlanningService.UpsertTenantAiConfig` | `None` | `None` | Isolated (`ICurrentUserService`) | **YES** (Admin AI Settings) |
| **RoutePlanningAgent** | `UpsertTenantRuleConfigCommand` | Command | `UpsertTenantRuleConfigCommandHandler` | `RoutePlanningService.UpsertTenantRuleConfig` | `None` | `None` | Isolated (`ICurrentUserService`) | **YES** (Admin Dispatch Rules) |
| **RoutePlanningAgent** | `GetRouteQuery` | Query | `GetRouteQueryHandler` | `RoutePlanningService.GetRoute` | `None` | `None` | Isolated (`ICurrentUserService`) | **YES** (Route Detail Map View) |
| **RoutePlanningAgent** | `ListRoutesQuery` | Query | `ListRoutesQueryHandler` | `RoutePlanningService.ListRoutes` | `None` | `None` | Isolated (`ICurrentUserService`) | **YES** (Routes Grid / List) |
| **RoutePlanningAgent** | `ListPendingApprovalsQuery` | Query | `ListPendingApprovalsQueryHandler` | `RoutePlanningService.ListPendingApprovals` | `None` | `None` | Isolated (`ICurrentUserService`) | **YES** (Approvals Dashboard) |
| **RoutePlanningAgent** | `GetTenantAiConfigQuery` | Query | `GetTenantAiConfigQueryHandler` | `RoutePlanningService.GetTenantAiConfig` | `None` | `None` | Isolated (`ICurrentUserService`) | **YES** (Admin AI Settings View) |
| **RoutePlanningAgent** | `ListTenantRuleConfigsQuery` | Query | `ListTenantRuleConfigsQueryHandler` | `RoutePlanningService.ListTenantRuleConfigs` | `None` | `None` | Isolated (`ICurrentUserService`) | **YES** (Admin Rules Table) |
| **ai-governance** | `GenerateAiCommand` | Command / Orchestrator | `ExecuteAiService.generate` | `AiExecutionService.Generate` | `None` | `None` | Isolated (`CurrentUserContext`) | **NO** (Internal AI Engine) |
| **ai-governance** | `EmbedAiCommand` | Command / Orchestrator | `ExecuteAiService.embed` | `AiExecutionService.Embed` | `None` | `None` | Isolated (`CurrentUserContext`) | **NO** (Internal Embedding Engine) |
| **ai-governance** | `GovernancePolicyService.evaluate` | Facade / Service | `GovernancePolicyService` | `AiGovernanceService.ExecutePolicy` | `None` | `None` | Isolated (`CurrentUserContext`) | **NO** (Internal Policy Pre-flight) |
| **devops-agent** | `IngestAlertCommand` | Command | `IngestAlertCommand.Handler` | `DevOpsIngestionService.IngestAlert` | `None` | `None` | Platform Scope | **NO** (SRE Telemetry Webhook) |
| **devops-agent** | `CreateRuleCommand` | Command | `CreateRuleCommand.Handler` | `DevOpsRuleService.CreateRule` | `None` | `None` | Platform Scope | **YES** (SRE Admin Portal) |
| **devops-agent** | `UpdateSelfConfigCommand` | Command | `UpdateSelfConfigCommand.Handler` | `DevOpsConfigService.UpdateSelfConfig` | `None` | `None` | Platform Scope | **YES** (SRE Config Panel) |
| **devops-agent** | `GetIncidentQueryHandler` | Query | `GetIncidentQueryHandler` | `DevOpsIncidentService.GetIncident` | `None` | `None` | Platform Scope | **YES** (SRE Incident Details) |
| **devops-agent** | `ListIncidentsQueryHandler` | Query | `ListIncidentsQueryHandler` | `DevOpsIncidentService.ListIncidents` | `None` | `None` | Platform Scope | **YES** (SRE Incident Queue) |
| **devops-agent** | `ListRulesQueryHandler` | Query | `ListRulesQueryHandler` | `DevOpsRuleService.ListExistingRules` | `None` | `None` | Platform Scope | **YES** (SRE Rules List) |
| **devops-agent** | `GetSelfConfigQueryHandler` | Query | `GetSelfConfigQueryHandler` | `DevOpsConfigService.GetSelfConfig` | `None` | `None` | Platform Scope | **YES** (SRE Config View) |
| **billing-service** | `GenerateInvoiceUseCase.execute` | UseCase / Command | `GenerateInvoiceUseCase` | `BillingService.GenerateInvoice` | `None` | `None` | Isolated (`TenantInterceptor`) | **YES** (Automated Invoicing) |
| **billing-service** | `BillingService.createInvoice` | Command | `BillingService` | `BillingService.CreateInvoice` | `None` | `None` | Isolated (`TenantInterceptor`) | **YES** (Manual Invoice Creation) |
| **billing-service** | `BillingService.getInvoiceDetail` | Query | `BillingService` | `BillingService.GetInvoiceDetail` | `None` | `None` | Isolated (`TenantInterceptor`) | **YES** (Invoice Detail PDF/View) |
| **billing-service** | `BillingService.listInvoices` | Query | `BillingService` | `BillingService.ListInvoices` | `None` | `None` | Isolated (`TenantInterceptor`) | **YES** (Finance Invoices Grid) |
| **billing-service** | `BillingService.checkCustomerCredit` | Query / Rule Check | `BillingService` | `BillingService.CheckCustomerCredit` | `None` | `None` | Isolated (`TenantInterceptor`) | **YES** (Credit Check Widget) |
| **billing-service** | `BillingService.recordPayment` | Command | `BillingService` | `@GrpcMethod` *(No Proto)* | `None` | `None` | Isolated (`TenantInterceptor`) | **YES** (Payment Recording) |
| **billing-service** | `BillingService.cancelInvoice` | Command | `BillingService` | `@GrpcMethod` *(No Proto)* | `None` | `None` | Isolated (`TenantInterceptor`) | **YES** (Invoice Cancellation) |
| **billing-service** | `BillingService.issueDebitNote` | Command | `BillingService` | `@GrpcMethod` *(No Proto)* | `None` | `None` | Isolated (`TenantInterceptor`) | **YES** (Debit Note Issue) |
| **billing-service** | `BillingService.issueCreditNote` | Command | `BillingService` | `@GrpcMethod` *(No Proto)* | `None` | `None` | Isolated (`TenantInterceptor`) | **YES** (Credit Note Issue) |
| **billing-service** | `BillingService.createEscrowWallet`| Command | `BillingService` | `BillingService.CreateEscrowWallet` | `None` | `None` | Isolated (`TenantInterceptor`) | **YES** (Carrier Wallet Onboarding)|
| **billing-service** | `BillingService.getWalletBalance` | Query | `BillingService` | `BillingService.GetWalletBalance` | `None` | `None` | Isolated (`TenantInterceptor`) | **YES** (Carrier Balance Widget) |
| **billing-service** | `BillingService.freezeEscrowAmount` | Command | `BillingService` | `BillingService.FreezeEscrowAmount` | `None` | `None` | Isolated (`TenantInterceptor`) | **NO** (Internal Workflow Action)|
| **billing-service** | `BillingService.releaseEscrowAmount`| Command | `BillingService` | `BillingService.ReleaseEscrowAmount`| `None` | `None` | Isolated (`TenantInterceptor`) | **NO** (Delivery Event Action) |
| **billing-service** | `BillingService.refundEscrowAmount` | Command | `BillingService` | `BillingService.RefundEscrowAmount` | `None` | `None` | Isolated (`TenantInterceptor`) | **NO** (Cancellation Action) |
| **financial-service** | `FinancialService.estimateCost` | Application Service / Calc | `FinancialService` | `FinancialService.EstimateCost` | `None` | `None` | Isolated (`TenantInterceptor`) | **YES** (Rate Calculator Widget) |
| **financial-service** | `FinancialService.getCustomsDuty` | Application Service / Calc | `FinancialService` | `FinancialService.GetCustomsDuty` | `None` | `None` | Isolated (`TenantInterceptor`) | **YES** (Duty Calculator Widget) |
| **financial-service** | `FinancialService.getMinAcceptableRate` | Application Service / Calc | `FinancialService` | `FinancialService.GetMinAcceptableRate` | `None` | `None` | Isolated (`TenantInterceptor`) | **NO** (Internal Pricing Floor) |
| **financial-service** | `FinancialService.getDynamicMargin` | Application Service / Calc | `FinancialService` | `@GrpcMethod` *(No Proto)* | `None` | `None` | Isolated (`TenantInterceptor`) | **NO** (Internal Dynamic Engine) |
| **financial-service** | `FinancialService.getExchangeRate` | Application Service / Calc | `FinancialService` | `@GrpcMethod` *(No Proto)* | `None` | `None` | Isolated (`TenantInterceptor`) | **YES** (Currency Conversion) |
| **negotiation-agent-service** | `NegotiationService.submitOffer` | Orchestrator / Command | `NegotiationService` | `@GrpcMethod` *(No Proto)* | `POST /negotiation/offer` | `None` | Isolated (`TenantId`) | **YES** (Interactive Bidding Desk)|
| **negotiation-agent-service** | `NegotiationService.getSessionHistory` | Query | `NegotiationService` | `@GrpcMethod` *(No Proto)* | `GET /negotiation/session/:id` | `None` | Isolated (`TenantId`) | **YES** (Bidding History Log) |
| **customer-assistant-service** | `ConversationalAssistantOrchestrator.handleMessage` | Orchestrator / Command | `ConversationalAssistantOrchestrator` | `None` | `POST /chat/message` | `None` | Isolated (`TenantContext`) | **YES** (Customer AI Chatbot) |
| **realtime-hub-service** | `EventsGateway.broadcastToTenant` | Push Facade | `EventsGateway` | `None` | `WebSocket /socket.io` | `RabbitMQ Consumer` | Isolated (`TenantId` room) | **YES** (Real-time Web UI Updates) |

---

## 2. Detailed Command Analysis

### 2.1. ShipmentWorkflow Commands

#### `CreateShipmentCommand`
- **Command:** `CreateShipmentCommand`
- **File:** `src/dotnet/ShipmentWorkflow/Application/Commands/Shipments/CreateShipmentCommand.cs`
- **Handler:** `CreateShipmentCommandHandler`
- **Handler File:** `src/dotnet/ShipmentWorkflow/Application/Commands/Shipments/CreateShipmentCommand.cs`
- **Business Purpose:** Creates a new multi-modal shipment entity in `Draft` state with optional initial cargo items, route stops, documents, and reference tracking codes.
- **Input:** `CreateShipmentCommand` (`Origin`, `Destination`, `EstimatedDelivery`, `CargoItems`, `Locations`, `Documents`, `Notes`, `TransportMode`, `ServiceLevel`, `DeclaredValue`, `DeclaredValueCurrency`).
- **Output:** `ShipmentDto`
- **Called By:** gRPC (`ShipmentWorkflowService.CreateShipment`)
- **Domain Operations:** Validates input parameters via `ShipmentCommandHelpers.EnsureShipmentValid`, creates `Shipment` aggregate root in `Draft` status, calculates volumetric and total weight.
- **Repositories:** `ShipmentWorkflowDbContext.Shipments`
- **External Services:** None
- **Events Published:** `ShipmentCreatedIntegrationEvent` (published via Transactional Outbox pattern).
- **Tenant-sensitive:** Strict Tenant Isolation (`ICurrentUserService.TenantId` assigned upon creation and filtered in EF Core).
- **Security-sensitive:** Standard authenticated user action (`Staff` / `Dispatcher` / `Manager`).
- **Potential BFF Capability:** **YES**
- **Reason:** Core entry point for shipment creation in the Staff portal.

#### `SubmitShipmentCommand`
- **Command:** `SubmitShipmentCommand`
- **File:** `src/dotnet/ShipmentWorkflow/Application/Commands/Shipments/SubmitShipmentCommand.cs`
- **Handler:** `SubmitShipmentCommandHandler`
- **Handler File:** `src/dotnet/ShipmentWorkflow/Application/Commands/Shipments/SubmitShipmentCommand.cs`
- **Business Purpose:** Advances a shipment from `Draft` state to `Submitted`, validating that minimum cargo and routing requirements are met and triggering downstream compliance and dispatch pipelines.
- **Input:** `SubmitShipmentCommand(Guid ShipmentId)`
- **Output:** `ShipmentDto`
- **Called By:** gRPC (`ShipmentWorkflowService.SubmitShipment`)
- **Domain Operations:** `shipment.Submit(timeProvider.GetUtcNow())` — enforces state machine rules.
- **Repositories:** `ShipmentWorkflowDbContext.Shipments`
- **Events Published:** `ShipmentStatusChangedIntegrationEvent` (Outbox).
- **Tenant-sensitive:** Yes (`TenantId` isolation).
- **Security-sensitive:** Yes.
- **Potential BFF Capability:** **YES**
- **Reason:** Primary workflow action transitioning draft preparation into active execution.

#### `UpdateShipmentCommand` / `UpdateShipmentStatusCommand` / `CancelShipmentCommand` / `DeleteDraftShipmentCommand`
- **Files:** `UpdateShipmentCommand.cs`, `UpdateShipmentStatusCommand.cs`, `CancelShipmentCommand.cs`, `DeleteDraftShipmentCommand.cs`
- **Handlers:** Colocated MediatR handlers.
- **Business Purpose:** Full lifecycle management and state transitions (`Draft` -> `Submitted` -> `Confirmed` -> `InTransit` -> `Delivered` / `Cancelled`).
- **Called By:** gRPC (`ShipmentWorkflowService.*`)
- **Events Published:** `ShipmentStatusChangedIntegrationEvent` on status changes; outbox cleanup on deletion.
- **Tenant-sensitive:** Yes.
- **Potential BFF Capability:** **YES**

#### Child Entity Commands (`ManageCargoCommands`, `ManageLocationCommands`, `ManageDocumentCommands`, `ManageMilestoneCommands`)
- **Files:** `ManageCargoCommands.cs`, `ManageLocationCommands.cs`, `ManageDocumentCommands.cs`, `ManageMilestoneCommands.cs`
- **Commands:** `AddCargoItemCommand`, `UpdateCargoItemCommand`, `RemoveCargoItemCommand`, `AddShipmentLocationCommand`, `UpdateShipmentLocationCommand`, `RemoveShipmentLocationCommand`, `AttachShipmentDocumentCommand`, `UpdateShipmentDocumentOcrCommand`, `RemoveShipmentDocumentCommand`, `RecordShipmentMilestoneCommand`.
- **Called By:** gRPC (`ShipmentWorkflowService.*`)
- **Special Case (`UpdateShipmentDocumentOcrCommand`):** Triggered when OCR parsing completes to enrich document metadata and normalized key-value fields.
- **Events Published:** `ShipmentMilestoneRecordedIntegrationEvent`, `ShipmentDocumentAttachedIntegrationEvent`, `ShipmentLocationAddedIntegrationEvent`.
- **Potential BFF Capability:** **YES** (except `UpdateShipmentDocumentOcrCommand` which is service-to-service).

#### `ImportShipmentsCommand`
- **File:** `ImportShipmentsCommand.cs`
- **Handler:** `ImportShipmentsCommandHandler`
- **Business Purpose:** High-throughput batch ingestion of shipments from external spreadsheets or ERP feeds with row-level validation.
- **Called By:** gRPC (`ShipmentWorkflowService.ImportShipments`)
- **Potential BFF Capability:** **YES** (Bulk upload in Staff BFF).

---

### 2.2. IAM & Authentication Commands

#### `CreateTenantCommand`
- **File:** `src/dotnet/IamTenant/Application/Commands/Tenants/CreateTenantCommand.cs`
- **Handler:** `CreateTenantCommandHandler`
- **Business Purpose:** Provisions a new customer tenant organization, assigns tenant code, creates default roles (`Admin`, `Manager`, `Staff`), and allocates initial AWS Cognito User Pool Client.
- **Called By:** gRPC (`IamService.CreateTenant`)
- **Repositories:** `IamTenantDbContext`
- **External Services:** `ICognitoAuthService` (creates Cognito App Client for tenant).
- **Tenant-sensitive:** Platform System Admin level.
- **Potential BFF Capability:** **YES** (System BFF).

#### `UpdateTenantCommand` *(Unexposed Capability)*
- **File:** `src/dotnet/IamTenant/Application/Commands/Tenants/UpdateTenantCommand.cs`
- **Handler:** `UpdateTenantHandler`
- **Business Purpose:** Updates tenant company metadata, business name, tax code, and subscription plan tier (`Standard` / `Premium`).
- **Input:** `UpdateTenantCommand(Guid Id, string Name, string? TaxCode, PlanType PlanType)`
- **Output:** `TenantDto`
- **Called By:** **Nowhere discovered** *(Missing from gRPC service!)*
- **Repositories:** `IamTenantDbContext.Tenants`
- **Tenant-sensitive:** System Multi-tenant.
- **Potential BFF Capability:** **YES** (System Admin Portal - Candidate Missing RPC).

#### `CreateStaffCommand` / `UpdateStaffCommand` / `ActivateStaffCommand` / `DeactivateStaffCommand` / `ResetStaffPasswordCommand` / `AssignRolesCommand` / `AssignPermissionsToRoleCommand`
- **Files:** `CreateStaffCommand.cs`, `UpdateStaffCommand.cs`, `ActivateStaffCommand.cs`, `DeactivateStaffCommand.cs`, `ResetStaffPasswordCommand.cs`, `AssignRolesCommand.cs`, `AssignPermissionsToRoleCommand.cs`
- **Business Purpose:** Enterprise identity administration within a tenant.
- **Called By:** gRPC (`IamService.*`)
- **External Services:** AWS Cognito (`AdminCreateUser`, `AdminEnableUser`, `AdminDisableUser`, `AdminResetUserPassword`).
- **Potential BFF Capability:** **YES** (Admin BFF).

#### `LoginCommand` / `CompleteInvitationCommand`
- **Files:** `LoginCommand.cs`, `CompleteInvitationCommand.cs`
- **Business Purpose:** User authentication and password initialization against tenant-specific Cognito Client.
- **Called By:** gRPC (`AuthService.Login`, `AuthService.CompleteInvitation`)
- **Potential BFF Capability:** **YES** (Staff BFF Public Auth).

---

### 2.3. Route Planning Agent Commands

#### `CreateRouteCommand` / `UpdateRouteCommand` / `DeleteRouteCommand` / `UpdateRouteStatusCommand` / `OptimizeRouteCommand`
- **Files:** `CreateRouteCommand.cs`, `UpdateRouteCommand.cs`, `DeleteRouteCommand.cs`, `UpdateRouteStatusCommand.cs`, `OptimizeRouteCommand.cs`
- **Business Purpose:** Manages multi-stop routes, stop sequencing, weight/volume validation, and TSP heuristic optimization.
- **Called By:** gRPC (`RoutePlanningService.*`)
- **Repositories:** `RoutePlanningDbContext`
- **Potential BFF Capability:** **YES** (Staff BFF).

#### `ApproveRouteCommand` / `RejectRouteCommand`
- **Files:** `ApproveRouteCommand.cs`, `RejectRouteCommand.cs`
- **Business Purpose:** Dual-control approval gate for hazardous, expensive, or high-risk AI-recommended routes.
- **Called By:** gRPC (`RoutePlanningService.ApproveRoute`, `RoutePlanningService.RejectRoute`)
- **Potential BFF Capability:** **YES** (Manager Approvals in Staff BFF).

#### `RequestRouteRecommendationCommand`
- **File:** `RequestRouteRecommendationCommand.cs`
- **Handler:** `RequestRouteRecommendationCommandHandler`
- **Business Purpose:** Orchestrates AI route recommendation by calling `AiExecutionService` and Compliance RAG, analyzing weather/traffic rules, and generating risk-scored proposals.
- **Called By:** gRPC (`RoutePlanningService.GetRouteRecommendation`)
- **External Services:** `IAiExecutionClient`, `IComplianceRagService`.
- **Potential BFF Capability:** **YES** (Staff BFF).

---

### 2.4. Billing & Financial Commands (NestJS)

#### `GenerateInvoiceUseCase.execute`
- **File:** `src/nestjs/billing-service/src/application/use-cases/generate-invoice.use-case.ts`
- **Business Purpose:** Generates an invoice for completed shipment by invoking `FinancialGrpcClient.estimateCost`, calculating taxes, itemizing freight/port/duty fees, and creating invoice in Prisma DB.
- **Called By:** gRPC (`BillingService.GenerateInvoice`)
- **External Services:** `FinancialService` (via gRPC), RabbitMQ (`invoice.generated`).
- **Potential BFF Capability:** **YES** (Staff BFF).

#### `BillingService.recordPayment` / `cancelInvoice` / `issueDebitNote` / `issueCreditNote`
- **File:** `src/nestjs/billing-service/src/application/services/billing.service.ts`
- **Business Purpose:** Post-invoicing adjustments, payment reconciliation, and dispute credit/debit notes.
- **Called By:** NestJS `@GrpcMethod` *(Note: Missing from `billing.proto`)*.
- **Potential BFF Capability:** **YES** (Candidate Protobuf Contract Alignment).

---

## 3. Detailed Query Analysis

### 3.1. ShipmentWorkflow Queries

#### `GetShipmentQuery`
- **Query:** `GetShipmentQuery(Guid Id)`
- **File:** `src/dotnet/ShipmentWorkflow/Application/Queries/Shipments/GetShipmentQuery.cs`
- **Handler:** `GetShipmentQueryHandler`
- **Business Purpose:** Retrieves the full aggregate details of a single shipment including cargo items, locations, attached documents, and milestones.
- **Query Shape:** **Detail**
- **Input:** `Guid Id`
- **Output:** `ShipmentDto`
- **Called By:** gRPC (`ShipmentWorkflowService.GetShipment`)
- **Repositories:** `ShipmentWorkflowDbContext.Shipments` (with EF Core query filter enforcing `TenantId`).
- **Tenant-sensitive:** Yes.
- **Potential BFF Capability:** **YES**

#### `ListShipmentsQuery`
- **Query:** `ListShipmentsQuery(int Page, int PageSize, string? StatusFilter, string? SearchTerm, DateTimeOffset? DateFrom, DateTimeOffset? DateTo)`
- **File:** `src/dotnet/ShipmentWorkflow/Application/Queries/Shipments/ListShipmentsQuery.cs`
- **Handler:** `ListShipmentsQueryHandler`
- **Business Purpose:** Multi-criteria filtered search, sorting, and pagination across tenant shipments.
- **Query Shape:** **List / Search / Pagination**
- **Output:** `PagedResult<ShipmentDto>` (`Items`, `TotalItems`, `Page`, `PageSize`, `TotalPages`)
- **Called By:** gRPC (`ShipmentWorkflowService.ListShipments`)
- **Potential BFF Capability:** **YES**

#### `GetShipmentTimelineQuery`
- **Query:** `GetShipmentTimelineQuery(Guid ShipmentId)`
- **File:** `src/dotnet/ShipmentWorkflow/Application/Queries/Shipments/GetShipmentTimelineQuery.cs`
- **Handler:** `GetShipmentTimelineQueryHandler`
- **Business Purpose:** Builds an ordered audit timeline reconstructing all state transitions, location updates, document uploads, and operational milestones.
- **Query Shape:** **Detail / Timeline**
- **Output:** `List<ShipmentMilestoneDto>`
- **Called By:** gRPC (`ShipmentWorkflowService.GetShipmentTimeline`)
- **Potential BFF Capability:** **YES**

---

### 3.2. IAM & Auth Queries

#### `GetTenantQuery` / `ListTenantsQuery`
- **Files:** `GetTenantQuery.cs`, `ListTenantsQuery.cs`
- **Query Shape:** Detail / Pagination
- **Called By:** gRPC (`IamService.GetTenant`, `IamService.ListTenants`)
- **Potential BFF Capability:** **YES** (System BFF).

#### `GetStaffQuery` / `ListStaffQuery`
- **Files:** `GetStaffQuery.cs`, `ListStaffQuery.cs`
- **Query Shape:** Detail / Pagination
- **Called By:** gRPC (`IamService.GetUser`, `IamService.GetManyUsers`)
- **Potential BFF Capability:** **YES** (Admin BFF).

#### `GetRoleQuery` / `ListRolesQuery`
- **Files:** `RoleQueries.cs`
- **Query Shape:** Detail / List
- **Called By:** gRPC (`IamService.GetRole`, `IamService.GetManyRoles`)
- **Potential BFF Capability:** **YES** (Admin BFF).

#### `GetUserPermissionsQuery`
- **File:** `GetUserPermissionsQuery.cs`
- **Query Shape:** Detail / Lookup
- **Business Purpose:** Evaluates all assigned roles and active permission codes for a user.
- **Called By:** gRPC (`IamService.GetUserPermissions`), BFF authorization middleware.
- **Potential BFF Capability:** **YES** (Staff BFF User Context).

#### `IdentifyUserQuery`
- **File:** `IdentifyUserQuery.cs`
- **Query Shape:** **Lookup** (Pre-Auth)
- **Business Purpose:** Resolves user existence, associated `TenantCode`, and `UserType` by email before login.
- **Called By:** gRPC (`AuthService.IdentifyUser`, `AuthService.ForgotPassword`, `AuthService.ConfirmForgotPassword`).
- **Potential BFF Capability:** **YES** (Staff BFF Public Auth).

#### `ResolveTenantAuthClientQuery`
- **File:** `ResolveTenantAuthClientQuery.cs`
- **Query Shape:** **Internal Lookup**
- **Business Purpose:** Looks up the AWS Cognito App Client ID associated with a tenant code and user type.
- **Called By:** Internal method in `AuthGrpcService` (`RefreshToken`, `ForgotPassword`).
- **Potential BFF Capability:** **NO** (Strictly internal to IAM service).

---

### 3.3. Route Planning Agent Queries

#### `GetRouteQuery` / `ListRoutesQuery`
- **Files:** `GetRouteQuery.cs`, `ListRoutesQuery.cs`
- **Query Shape:** Detail / Pagination
- **Called By:** gRPC (`RoutePlanningService.GetRoute`, `RoutePlanningService.ListRoutes`)
- **Potential BFF Capability:** **YES**

#### `ListPendingApprovalsQuery`
- **File:** `ListPendingApprovalsQuery.cs`
- **Query Shape:** **List / Dashboard**
- **Business Purpose:** Queries all pending route approval tickets awaiting manager intervention.
- **Called By:** gRPC (`RoutePlanningService.ListPendingApprovals`)
- **Potential BFF Capability:** **YES**

#### `GetTenantAiConfigQuery` / `ListTenantRuleConfigsQuery`
- **Files:** `GetTenantAiConfigQuery.cs`, `ListTenantRuleConfigsQuery.cs`
- **Query Shape:** Lookup / Pagination
- **Called By:** gRPC (`RoutePlanningService.GetTenantAiConfig`, `RoutePlanningService.ListTenantRuleConfigs`)
- **Potential BFF Capability:** **YES**

---

## 4. End-to-End Traces

### Trace 1: Shipment Creation Flow
```text
Staff User (Web UI)
    -> Staff BFF (POST /api/v1/shipments)
    -> gRPC (ShipmentWorkflowService.CreateShipment)
    -> CreateShipmentCommand
    -> CreateShipmentCommandHandler
    -> Shipment.Create() (Domain Entity)
    -> ShipmentWorkflowDbContext (PostgreSQL save)
    -> Transactional Outbox (ShipmentCreatedIntegrationEvent)
    -> MassTransit / RabbitMQ
    -> NotificationService (ShipmentNotificationConsumer)
    -> Push Notification / WebSocket Realtime Hub
```

### Trace 2: AI-Assisted Route Recommendation & Dual-Control Approval Flow
```text
Dispatcher (Web UI)
    -> Staff BFF (POST /api/v1/routes/{id}/recommendation)
    -> gRPC (RoutePlanningService.GetRouteRecommendation)
    -> RequestRouteRecommendationCommand
    -> RequestRouteRecommendationCommandHandler
    -> AiExecutionService (gRPC Generate) & RegulatoryComplianceService (EvaluateCompliance)
    -> Rule Evaluation & Risk Scoring
    -> Creates ApprovalRequest in PENDING status
    -> Returns Recommendation DTO to Dispatcher
    -------------------------------------------------
Manager (Web UI)
    -> Staff BFF (GET /api/v1/approvals/pending)
    -> gRPC (RoutePlanningService.ListPendingApprovals)
    -> Manager reviews AI rationale & clicks Approve
    -> Staff BFF (POST /api/v1/approvals/{id}/approve)
    -> ApproveRouteCommand
    -> ApproveRouteCommandHandler
    -> Route.Approve() & RoutePlanningDbContext.SaveChanges()
```

### Trace 3: Automated Invoice Generation Flow
```text
Delivery Completed Event
    -> MassTransit / RabbitMQ
    -> BillingService (gRPC / Background Trigger)
    -> GenerateInvoiceUseCase.execute()
    -> gRPC (FinancialService.EstimateCost)
    -> InvoiceDomainService.calculateTaxesAndTotals()
    -> PrismaService (PostgreSQL save Invoice & LineItems)
    -> RabbitMQ Event (invoice.generated)
    -> Customer Notification & Staff BFF Invoice Feed
```

---

## 5. Commands Not Exposed Through gRPC

1. **`UpdateTenantCommand`** (`src/dotnet/IamTenant/Application/Commands/Tenants/UpdateTenantCommand.cs`):
   - Fully implemented MediatR command and handler for updating tenant profile (name, tax code, plan type).
   - Currently omitted from `IamGrpcService.cs` (which only exposes `UpdateTenantStatusCommand`).

---

## 6. Queries Not Exposed Through gRPC

1. **`ResolveTenantAuthClientQuery`** (`src/dotnet/IamTenant/Application/Queries/Auth/ResolveTenantAuthClientQuery.cs`):
   - Resolves Cognito Client IDs internally within the auth pipeline.
   - Appropriately private; should not be exposed externally.

---

## 7. Existing gRPC Without CQRS (Direct Repository / Domain Service Pattern)

The following services implement domain services, JPA/Prisma services, or direct DbContext patterns instead of MediatR CQRS commands/queries:

1. **`NotificationService`** (`src/dotnet/Notification/GrpcServices/NotificationGrpcService.cs`):
   - Direct `NotificationDbContext` LINQ queries and entity updates.
2. **`GpsTrackingService`** (`src/dotnet/GpsTracking/GrpcServices/GpsTrackingGrpcService.cs`):
   - Delegates to application services (`PositionIngestionService`, `MonitoringManagementService`, `LocationQueryService`).
3. **`DocumentOcrService`** (`src/dotnet/DocumentOcr/GrpcServices/DocumentOcrGrpcService.cs`):
   - Delegates to `DocumentOcrJobService`.
4. **`RegulatoryComplianceService`** (`src/dotnet/RegulatoryCompliance/GrpcServices/RegulatoryComplianceGrpcService.cs`):
   - Delegates to `ComplianceEvaluationService`, `RegulationRetrievalService`, `KnowledgeIngestionService`, `GroundedAnswerService`.
5. **`BillingService` & `FinancialService`** (NestJS):
   - Delegates to `BillingService` class and `FinancialService` class.

---

## 8. Dead / Unreachable Application Capabilities

1. **`IamService.CreateCustomRole`, `UpdateRole`, `DeleteRole`**:
   - Stubs exist in `iam_tenant.proto`, but handlers throw `StatusCode.Unimplemented` because role definitions are static.
2. **`ComplianceRag.CheckRouteCompliance`**:
   - `RoutePlanningAgent` maintains a client stub to `ComplianceRag`, but no server implementation exists in the codebase.
3. **`UpdateTenantCommand`**:
   - Exists in application code, but is never invoked by any gRPC handler or HTTP controller.

---

## 9. Candidate Missing RPCs

The following capabilities are already fully supported by application code, but lack an appropriate gRPC contract for BFF consumption:

### 1. `UpdateTenant`
- **Proposed RPC Name:** `rpc UpdateTenant (UpdateTenantRequest) returns (TenantResponse)`
- **Existing Command/Query:** `IamTenant.Application.Commands.Tenants.UpdateTenantCommand`
- **Why Needed:** Allows System Admins in System BFF to update a tenant company name, tax ID code, and subscription plan tier without direct database intervention.
- **Required Request:**
  ```protobuf
  message UpdateTenantRequest {
    string id = 1;
    string name = 2;
    optional string tax_code = 3;
    common.PlanType plan_type = 4;
  }
  ```
- **Expected Response:** `TenantResponse` (`id`, `name`, `tenant_code`, `plan_type`, `status`, `created_at`)
- **Files Involved:**
  - [protos/iam_tenant.proto](file:///D:/IT/CD/aurora-server/protos/iam_tenant.proto)
  - [src/dotnet/IamTenant/GrpcServices/IamGrpcService.cs](file:///D:/IT/CD/aurora-server/src/dotnet/IamTenant/GrpcServices/IamGrpcService.cs)
  - [src/dotnet/IamTenant/Application/Commands/Tenants/UpdateTenantCommand.cs](file:///D:/IT/CD/aurora-server/src/dotnet/IamTenant/Application/Commands/Tenants/UpdateTenantCommand.cs)

### 2. `RecordPayment`
- **Proposed RPC Name:** `rpc RecordPayment (RecordPaymentRequest) returns (RecordPaymentResponse)`
- **Existing Command/Query:** `BillingService.recordPayment` in `billing.service.ts`
- **Why Needed:** Allows billing staff in Staff BFF to reconcile and record customer payments against open invoices.
- **Required Request:**
  ```protobuf
  message RecordPaymentRequest {
    string tenant_id = 1;
    string invoice_id = 2;
    double amount_paid = 3;
    string payment_method = 4;
    string transaction_ref = 5;
  }
  ```
- **Expected Response:** `RecordPaymentResponse` (`success`, `payment_id`, `new_invoice_status`, `remaining_balance`)
- **Files Involved:**
  - [protos/billing.proto](file:///D:/IT/CD/aurora-server/protos/billing.proto)
  - [src/nestjs/billing-service/src/interface/controllers/billing.controller.ts](file:///D:/IT/CD/aurora-server/src/nestjs/billing-service/src/interface/controllers/billing.controller.ts)
  - [src/nestjs/billing-service/src/application/services/billing.service.ts](file:///D:/IT/CD/aurora-server/src/nestjs/billing-service/src/application/services/billing.service.ts)

### 3. `IssueAdjustmentNote` (Debit / Credit Notes)
- **Proposed RPC Name:** `rpc IssueAdjustmentNote (IssueAdjustmentNoteRequest) returns (AdjustmentNoteResponse)`
- **Existing Command/Query:** `BillingService.issueDebitNote` and `BillingService.issueCreditNote`
- **Why Needed:** Supports post-invoicing invoice adjustments, price corrections, and dispute refunds.
- **Required Request:**
  ```protobuf
  message IssueAdjustmentNoteRequest {
    string tenant_id = 1;
    string invoice_id = 2;
    string note_type = 3; // DEBIT_NOTE or CREDIT_NOTE
    double amount = 4;
    string reason = 5;
  }
  ```
- **Expected Response:** `AdjustmentNoteResponse` (`note_id`, `invoice_id`, `note_number`, `type`, `amount`, `created_at`)
- **Files Involved:**
  - [protos/billing.proto](file:///D:/IT/CD/aurora-server/protos/billing.proto)
  - [src/nestjs/billing-service/src/interface/controllers/billing.controller.ts](file:///D:/IT/CD/aurora-server/src/nestjs/billing-service/src/interface/controllers/billing.controller.ts)

### 4. `SubmitNegotiationOffer` & `GetNegotiationSession`
- **Proposed RPC Name:** `rpc SubmitOffer (SubmitOfferRequest) returns (NegotiationResponse)`, `rpc GetSessionHistory (GetSessionHistoryRequest) returns (NegotiationSessionResponse)`
- **Existing Command/Query:** `NegotiationService.submitOffer`, `NegotiationService.getSessionHistory`
- **Why Needed:** Allows the Staff portal to negotiate freight rates with automated AI pricing agents and view negotiation dialogue history.
- **Required Request:** `SubmitOfferRequest` (`tenant_id`, `shipment_id`, `customer_id`, `offer_price`, `list_price`, `bottom_price`)
- **Expected Response:** `NegotiationResponse` (`session_id`, `round`, `decision`, `counter_offer_price`, `ai_speech`, `status`)
- **Files Involved:**
  - *New contract needed in `protos/`*
  - [src/nestjs/negotiation-agent-service/src/interface/controllers/negotiation.controller.ts](file:///D:/IT/CD/aurora-server/src/nestjs/negotiation-agent-service/src/interface/controllers/negotiation.controller.ts)
  - [src/nestjs/negotiation-agent-service/src/application/services/negotiation.service.ts](file:///D:/IT/CD/aurora-server/src/nestjs/negotiation-agent-service/src/application/services/negotiation.service.ts)
