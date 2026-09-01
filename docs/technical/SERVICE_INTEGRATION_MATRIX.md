# Aurora Server — Service Integration Matrix & Protocol Catalog

> **Audit Verification**: Every row below represents an active integration contract verified against source code, protobuf specifications, MassTransit/RabbitMQ topologies, gRPC interceptors, and BFF controllers.

---

## 1. Asynchronous Event Integration Matrix (RabbitMQ / Outbox)

| Producer Service | Published Event | RabbitMQ Exchange / Topic | Consumer Service(s) | Payload Contract / Schema | Flow Status | Audit Notes |
|---|---|---|---|---|---|---|
| **IamTenant** | `TenantAdminCreatedEvent` | `tenant_admin_created_event` | **MailService**, **AiGovernance** | `Shared.Events.TenantAdminCreatedEvent` | **VERIFIED IN CODE** | Provisions default tenant admin mailbox & sets initial token quota. |
| **IamTenant** | `TenantStaffCreatedEvent` | `tenant_staff_created_event` | **MailService** | `Shared.Events.TenantStaffCreatedEvent` | **VERIFIED IN CODE** | Auto-provisions personal employee mailbox on Stalwart. |
| **IamTenant** | `TenantStaffPasswordResetEvent` | `tenant_staff_password_reset_event` | **MailService** | `Shared.Events.TenantStaffPasswordResetEvent` | **VERIFIED IN CODE** | Syncs password reset to Stalwart server. |
| **IamTenant** | `RolePermissionsChangedEvent` | `role_permissions_changed_event` | **BFF / Redis Caches** | `Shared.Events.RolePermissionsChangedEvent` | **VERIFIED IN CODE** | Triggers eviction of cached user permissions in Redis. |
| **ShipmentWorkflow** | `ShipmentCreatedEvent` | `shipment.contracts.events.shipment_created` | **RealtimeHub**, **Notification** | `Shipment.Contracts.Events.ShipmentCreatedEvent` | **VERIFIED IN CODE** | Published via PostgreSQL Outbox processor. |
| **ShipmentWorkflow** | `ShipmentSubmittedEvent` | `shipment.contracts.events.shipment_submitted` | **RegulatoryCompliance**, **Notification** | `Shipment.Contracts.Events.ShipmentSubmittedEvent` | **VERIFIED IN CODE** | Triggers automated compliance validation against trade rules. |
| **ShipmentWorkflow** | `DocumentAttachedEvent` | `shipment.contracts.events.document_attached` | **DocumentOcr** | `Shipment.Contracts.Events.DocumentAttachedEvent` | **VERIFIED IN CODE** | Automatically initiates asynchronous OCR extraction pipeline. |
| **ShipmentWorkflow** | `ShipmentStatusChangedEvent` | `shipment.contracts.events.shipment_status_changed` | **Notification**, **RealtimeHub** | `Shipment.Contracts.Events.ShipmentStatusChangedEvent` | **VERIFIED IN CODE** | Broadcasts status transitions to WebSocket rooms. |
| **ShipmentWorkflow** | `ShipmentCompletedEvent` | `shipment.contracts.events.shipment_completed` | **billing-service**, **RealtimeHub** | `Shipment.Contracts.Events.ShipmentCompletedEvent` | **VERIFIED IN CODE** | Triggers automated invoice calculation upon POD receipt. |
| **ShipmentWorkflow** | `RouteAssignedEvent` | `shipment.contracts.events.route_assigned` | **GpsTracking** | `Shipment.Contracts.Events.RouteAssignedEvent` | **VERIFIED IN CODE** | Binds vehicle GPS telemetry feed to active shipment. |
| **DocumentOcr** | `DocumentOcrCompletedEvent` | `document_ocr.contracts.events.document_ocr_completed` | **Notification**, **RegulatoryCompliance** | `DocumentOcr.Contracts.Events.DocumentOcrCompletedEvent` | **VERIFIED IN CODE** | Ingests normalized document entities into compliance checker. |
| **DocumentOcr** | `DocumentOcrFailedEvent` | `document_ocr.contracts.events.document_ocr_failed` | **Notification** | `DocumentOcr.Contracts.Events.DocumentOcrFailedEvent` | **VERIFIED IN CODE** | Alerts staff operators to manual review requirement. |
| **RegulatoryCompliance**| `ComplianceEvaluationCompletedEvent` | `regulatory_compliance.contracts.events.compliance_evaluation_completed` | **Notification**, **RealtimeHub** | `RegulatoryCompliance.Contracts.Events.ComplianceEvaluationCompletedEvent` | **VERIFIED IN CODE** | Provides compliance score & findings to shipment timeline. |
| **RegulatoryCompliance**| `ComplianceEvaluationFailedEvent` | `regulatory_compliance.contracts.events.compliance_evaluation_failed` | **Notification** | `RegulatoryCompliance.Contracts.Events.ComplianceEvaluationFailedEvent` | **VERIFIED IN CODE** | Alerts customs clearance officer of regulatory block. |
| **RoutePlanningAgent** | `RouteCreatedEvent` | `route_created_event` | **Notification** | `Shared.Events.RouteCreatedEvent` | **VERIFIED IN CODE** | Route persisted and initialized. |
| **RoutePlanningAgent** | `RouteOptimizedEvent` | `route_optimized_event` | **Notification**, **RealtimeHub** | `Shared.Events.RouteOptimizedEvent` | **VERIFIED IN CODE** | Emitted following VROOM optimization completion. |
| **RoutePlanningAgent** | `RouteApprovalRequestedEvent` | `route_approval_requested_event` | **Notification** | `Shared.Events.RouteApprovalRequestedEvent` | **VERIFIED IN CODE** | High-risk route queued for Manager approval. |
| **RoutePlanningAgent** | `RouteApprovedEvent` | `route_approved_event` | **ShipmentWorkflow**, **Notification** | `Shared.Events.RouteApprovedEvent` | **VERIFIED IN CODE** | Transition to approved route ready for dispatch. |
| **RoutePlanningAgent** | `TenantRuleConfigChangedEvent` | `tenant_rule_config_changed_event` | **RoutePlanningAgent (Self-cluster)** | `Shared.Events.TenantRuleConfigChangedEvent` | **VERIFIED IN CODE** | Invalidates in-memory and Redis rule cache across nodes. |
| **GpsTracking** | `GpsPositionUpdatedEvent` | `gps_tracking.contracts.events.gps_position_updated` | **RealtimeHub** | `GpsTracking.Contracts.Events.GpsPositionUpdatedEvent` | **VERIFIED IN CODE** | Telemetry stream broadcasted to shipment tracking map. |
| **GpsTracking** | `GpsMonitoringAlertRaisedEvent` | `gps_tracking.contracts.events.gps_monitoring_alert_raised` | **Notification**, **RealtimeHub** | `GpsTracking.Contracts.Events.GpsMonitoringAlertRaisedEvent` | **VERIFIED IN CODE** | Triggers Geofence breach or signal loss alert. |
| **MailService** | `InboundEmailReceivedEvent` | `inbound_email_received_event` | **Notification**, **RealtimeHub** | `Shared.Events.InboundEmailReceivedEvent` | **VERIFIED IN CODE** | Email triaged and routed to thread queue. |
| **MailService** | `InboundEmailQuarantinedEvent`| `inbound_email_quarantined_event` | **Notification** | `Shared.Events.InboundEmailQuarantinedEvent` | **VERIFIED IN CODE** | Security threat flagged; email moved to quarantine store. |
| **MailService** | `OutboundEmailSentEvent` | `outbound_email_sent_event` | **RealtimeHub** | `Shared.Events.OutboundEmailSentEvent` | **VERIFIED IN CODE** | Mail successfully accepted by Stalwart SMTP relay. |
| **billing-service** | `billing.invoice.generated` | `logistics_events (billing.#)` | **RealtimeHub**, **Notification** | CloudEvents / JSON | **VERIFIED IN CODE** | Pushes invoice notification to customer and accounting. |
| **billing-service** | `billing.escrow.frozen` | `logistics_events (billing.#)` | **RealtimeHub** | CloudEvents / JSON | **VERIFIED IN CODE** | Live escrow balance update. |
| **billing-service** | `billing.escrow.released` | `logistics_events (billing.#)` | **RealtimeHub** | CloudEvents / JSON | **VERIFIED IN CODE** | Carrier payment release confirmed. |
| **negotiation-agent** | `negotiation.offer.submitted` | `logistics_events (negotiation.#)` | **RealtimeHub** | CloudEvents / JSON | **VERIFIED IN CODE** | Counter-bid pushed to real-time broker UI. |
| **ai-governance** | `ai.usage.tracked` | `ai.usage.tracked` | **billing-service** (SaaS Metering) | JSON / CloudEvent | **VERIFIED IN CODE** | Records token consumption against tenant tier limits. |
| **devops-agent** | `devops.incident.detected` | `devops.incident.detected` | **Notification** | JSON / CloudEvent | **VERIFIED IN CODE** | SRE alert broadcast for autonomous remediation. |

---

## 2. Synchronous gRPC Service & RPC Integration Matrix

| Calling Service / BFF | gRPC Target Service | Target Package & Service | RPC Method | Proto Contract File | Status | Authentication & Metadata Propagated |
|---|---|---|---|---|---|---|
| **Staff.Bff** / **Admin.Bff** | **IamTenant** | `auth.AuthService` | `Login`, `RefreshToken`, `Logout`, `IdentifyUser` | `protos/auth.proto` | **VERIFIED** | Public / Bearer Token |
| **Admin.Bff** / **System.Bff**| **IamTenant** | `iam.IamService` | `ListStaff`, `CreateStaff`, `AssignRoles`, `ListTenants` | `protos/iam_tenant.proto` | **VERIFIED** | `x-tenant-id`, `x-user-id`, `x-roles`, `x-permissions` |
| **Staff.Bff** | **ShipmentWorkflow** | `shipment.ShipmentWorkflowService` | `CreateShipment`, `GetShipment`, `ListShipments`, `SubmitShipment`, `UpdateShipmentStatus` | `protos/shipment_workflow.proto` | **VERIFIED** | `x-tenant-id`, `x-user-id`, `x-roles`, `x-permissions` |
| **Staff.Bff** | **RoutePlanningAgent** | `route_planning.RoutePlanningService` | `CreateRoute`, `GetRoute`, `ListRoutes`, `OptimizeRoute`, `ApproveRoute`, `RejectRoute` | `protos/route-planning-agent.proto` | **VERIFIED** | `x-tenant-id`, `x-user-id`, `x-roles`, `x-permissions` |
| **Admin.Bff** | **RoutePlanningAgent** | `route_planning.RoutePlanningService` | `GetTenantAiConfig`, `UpsertTenantAiConfig`, `UpsertTenantRuleConfig`, `PublishRiskPolicy` | `protos/route-planning-agent.proto` | **VERIFIED** | `x-tenant-id`, `x-user-id`, `x-roles`, `x-permissions` |
| **Staff.Bff** | **MailService** | `mail.MailSecurity` | `CreateDraftMessage`, `ListDrafts`, `SubmitOutboundMessage`, `ListThreads`, `ClaimThread`, `ReassignThread` | `protos/mail_platform.proto` | **VERIFIED** | `x-tenant-id`, `x-user-id`, `x-roles`, `x-permissions` |
| **Admin.Bff** | **MailService** | `mail.MailManagement` | `ProvisionDomain`, `CreateMailbox`, `CreateAlias`, `ResetPassword`, `GetAuditRecords` | `protos/mail_platform.proto` | **VERIFIED** | `x-tenant-id`, `x-user-id`, `x-roles`, `x-permissions` |
| **System.Bff** | **MailService** | `mail.MailManagement` | `RequeueDeadLetter`, `GetAuditRecords` | `protos/mail_platform.proto` | **VERIFIED** | `x-user-id`, `x-roles: SYSTEM_ADMIN` |
| **Staff.Bff** | **DocumentOcr** | `document_ocr.DocumentOcrService` | `SubmitDocumentJob`, `GetDocumentJob`, `ListDocumentJobs`, `ReviewDocumentJob` | `protos/document_ocr.proto` | **VERIFIED** | `x-tenant-id`, `x-user-id`, `x-roles`, `x-permissions` |
| **Staff.Bff** | **RegulatoryCompliance** | `regulatory_compliance.RegulatoryComplianceService` | `EvaluateCompliance`, `GetComplianceEvaluation`, `QueryRegulations`, `GenerateGroundedAnswer` | `protos/regulatory_compliance.proto` | **VERIFIED** | `x-tenant-id`, `x-user-id`, `x-roles`, `x-permissions` |
| **Admin.Bff** / **System.Bff**| **RegulatoryCompliance** | `regulatory_compliance.RegulatoryComplianceService` | `IngestRegulatorySource`, `IngestKnowledgeDocument` | `protos/regulatory_compliance.proto` | **VERIFIED** | `x-tenant-id`, `x-user-id`, `x-roles`, `x-permissions` |
| **Staff.Bff** | **GpsTracking** | `GpsTrackingService` | `GetCurrentLocation`, `ListPositionHistory`, `CreateGeofence`, `ListGeofences`, `ResolveMonitoringAlert` | `protos/gps_tracking.proto` | **VERIFIED** | `x-tenant-id`, `x-user-id`, `x-roles`, `x-permissions` |
| **Staff.Bff** | **Notification** | `NotificationService` | `RegisterDevice`, `RemoveDevice`, `SubscribeShipment`, `ListNotifications`, `GetUnreadCount`, `MarkNotificationRead`, `MarkAllNotificationsRead` | `protos/notification.proto` | **IMPLEMENTED** | `x-tenant-id`, `x-user-id`, `x-service-id: staff-bff`, `x-service-api-key` |
| **Staff.Bff** | **financial-service** | `financial.FinancialService` | `EstimateCost`, `GetCustomsDuty`, `GetMinAcceptableRate` | `protos/financial.proto` | **VERIFIED** | `x-tenant-id`, `x-user-id` |
| **Staff.Bff** | **billing-service** | `billing.BillingService` | `GenerateInvoice`, `GetInvoiceDetail`, `CheckCustomerCredit`, `ListInvoices`, `FreezeEscrowAmount`, `ReleaseEscrowAmount` | `protos/billing.proto` | **VERIFIED** | `x-tenant-id`, `x-user-id`, `x-roles`, `x-permissions` |
| **Staff.Bff** | **negotiation-agent** | `negotiation.NegotiationService` | `SubmitOffer`, `GetSessionHistory`, `GetDraftSuggestion` | `protos/negotiation.proto` | **VERIFIED** | `x-tenant-id`, `x-user-id` |
| **RoutePlanningAgent** | **RegulatoryCompliance** | `compliance_rag.ComplianceRag` | `CheckRouteCompliance` | `protos/compliance_rag.proto` | **VERIFIED** | `x-tenant-id`, `x-service-id: RoutePlanningAgent` |
| **billing-service** | **financial-service** | `financial.FinancialService` | `GetCustomsDuty`, `EstimateCost` | `protos/financial.proto` | **VERIFIED** | `x-tenant-id`, `x-service-id: BillingService` |
| **negotiation-agent** | **financial-service** | `financial.FinancialService` | `GetMinAcceptableRate` | `protos/financial.proto` | **VERIFIED** | `x-tenant-id`, `x-service-id: NegotiationAgent` |

---

## 3. AI Governance Gateway Integration Matrix

All microservices interacting with Foundation Models (LLMs) routes through `ai-governance` using standard governed capabilities:

| Calling Microservice | AI Capability Code (`capability_code`) | Governed Mode / Direct Fallback | Upstream Model / Provider Route | Purpose / Operational Flow |
|---|---|---|---|---|
| **MailService** | `mail.phishing_detection` | **Governed via gRPC** | Azure OpenAI / GPT-4o-mini | Analyzes inbound message headers, sender reputation, and body text for spear phishing and credential harvesting. |
| **MailService** | `mail.risk_scoring` | **Governed via gRPC** | Azure OpenAI / GPT-4o-mini | Computes composite security risk score before releasing emails to shared triage queues. |
| **DocumentOcr** | `ocr.invoice_extraction` | **Governed via gRPC** | Claude 3.5 Sonnet / GPT-4o Multimodal | Extracts structured invoice headers, supplier tax ID, line items, and totals from scanned PDFs/images. |
| **DocumentOcr** | `ocr.customs_extraction` | **Governed via gRPC** | Claude 3.5 Sonnet / GPT-4o Multimodal | Extracts HS codes, country of origin, declared customs value, and tariff items from Customs Declarations. |
| **DocumentOcr** | `ocr.bill_of_lading` | **Governed via gRPC** | Claude 3.5 Sonnet / GPT-4o Multimodal | Parses container numbers, vessel names, ports of loading/discharge, and carrier gross weights. |
| **RegulatoryCompliance**| `compliance.regulation_retrieval` | **Governed via gRPC (`Embed`)** | OpenAI `text-embedding-3-small` / Ada-002 | Generates 1536-dimensional semantic vectors for regulation search in `pgvector`. |
| **RegulatoryCompliance**| `compliance.grounded_qa` | **Governed via gRPC (`Generate`)** | Claude 3.5 Sonnet / GPT-4o | Synthesizes grounded compliance rulings with strict legal citations from retrieved knowledge chunks. |
| **RegulatoryCompliance**| `compliance.evidence_verification` | **Governed via gRPC (`Generate`)** | GPT-4o | Cross-verifies shipment manifest declarations against specific customs articles. |
| **RoutePlanningAgent** | `route.optimization_recommendation` | **Governed via gRPC** | GPT-4o / Claude 3.5 Sonnet | Translates human logistical constraints into VROOM solver payloads and explains route choices. |
| **RoutePlanningAgent** | `route.risk_assessment` | **Governed via gRPC** | GPT-4o-mini | Evaluates multi-hub waypoint sequences against weather, terrain, and driver duration limits. |
| **customer-assistant** | `assistant.intent_routing` | **Governed via gRPC** | GPT-4o-mini | Classifies user conversational queries into tracking, billing, compliance, or general support intents. |
| **customer-assistant** | `assistant.chat_completion` | **Governed via gRPC** | Claude 3.5 Sonnet / GPT-4o | Generates conversational customer responses incorporating live tool results. |
| **customer-assistant** | `assistant.conversation_summary` | **Governed via gRPC** | GPT-4o-mini | Condenses long multi-turn dialogue into concise session memory blocks. |
| **negotiation-agent** | `negotiation.strategy_offer` | **Governed via gRPC** | GPT-4o / Claude 3.5 Sonnet | Computes dynamic price concessions along negotiation curve based on market rate conditions. |
| **devops-agent** | `devops.incident_rca` | **Governed via gRPC** | Claude 3.5 Sonnet / GPT-4o | Performs automated root cause analysis from Kubernetes pod events, traces, and container logs. |
| **devops-agent** | `devops.rule_generation` | **Governed via gRPC** | GPT-4o | Synthesizes automated diagnostic rules and remediation steps for promotion to production rules engine. |

---

## 4. BFF-to-Backend Service Mapping & Endpoint Catalog

```
                    +───────────────────────────────────+
                    |           YARP Gateway            |
                    +─────────────────┬─────────────────+
                                      │
         ┌────────────────────────────┼────────────────────────────┐
         │ /api/v1/*                  │ /api/v1/admin/*            │ /api/v1/system/*
         v                            v                            v
  +──────────────+             +──────────────+             +──────────────+
  |  Staff.Bff   |             |  Admin.Bff   |             |  System.Bff  |
  +──────┬───────+             +──────┬───────+             +──────┬───────+
         │                            │                            │
         ├─ ShipmentsController       ├─ StaffController           ├─ TenantsController
         ├─ MailController            ├─ MailAdminController       ├─ MailSystemController
         ├─ RoutesController          ├─ AiConfigController        └─ SystemIngestionController
         ├─ ApprovalsController       ├─ RuleConfigController
         ├─ TrackingController        ├─ RolesController
         ├─ ComplianceController      └─ PlatformIngestionController
         ├─ DocumentsController
         ├─ BillingController
         ├─ FinancialController
         ├─ AssistantController
         └─ NotificationsController
```

### 4.1 Staff.Bff Controller Mappings
- **ShipmentsController** (`/api/v1/shipments`):
  - `POST /` -> `ShipmentWorkflowService.CreateShipment` (`shipments:create`)
  - `GET /{id}` -> `ShipmentWorkflowService.GetShipment` (`shipments:read`)
  - `GET /` -> `ShipmentWorkflowService.ListShipments` (`shipments:read`)
  - `PUT /{id}` -> `ShipmentWorkflowService.UpdateShipment` (`shipments:update`)
  - `POST /{id}/submit` -> `ShipmentWorkflowService.SubmitShipment` (`shipments:submit`)
  - `PATCH /{id}/status` -> `ShipmentWorkflowService.UpdateShipmentStatus` (`shipments:update`)
  - `POST /{id}/cancel` -> `ShipmentWorkflowService.CancelShipment` (`shipments:cancel`)
  - `DELETE /{id}` -> `ShipmentWorkflowService.DeleteDraftShipment` (`shipments:delete`)
  - `POST /import` -> `ShipmentWorkflowService.ImportShipments` (`shipments:import`)
  - Sub-resources: `/cargo`, `/locations`, `/documents`, `/milestones`, `/timeline`
- **MailController** (`/api/v1/mail`):
  - `POST /drafts` -> `MailSecurity.CreateDraftMessage` (`mail:draft:create`)
  - `GET /drafts` -> `MailSecurity.ListDrafts` (`mail:read`)
  - `GET /threads` -> `MailSecurity.ListThreads` (`mail:read`)
  - `POST /threads/{id}/claim` -> `MailSecurity.ClaimThread` (`mail:thread:claim`)
  - `POST /threads/{id}/reassign` -> `MailSecurity.ReassignThread` (`mail:thread:reassign`)
  - `POST /threads/{id}/unassign` -> `MailSecurity.UnassignThread` (`mail:thread:unassign`)
  - `GET /threads/{id}/assignment-history` -> `MailSecurity.GetThreadAssignmentHistory` (`mail:read`)
  - `POST /messages/outbound` -> `MailSecurity.SubmitOutboundMessage` (`mail:send`)
  - `GET /quarantine` -> `MailSecurity.ListQuarantineRecords` (`mail:quarantine:read`)
  - `POST /quarantine/{id}/release` -> `MailSecurity.ReleaseQuarantine` (`mail:quarantine:release`)
- **RoutesController** & **ApprovalsController** (`/api/v1/routes`, `/api/v1/approvals`):
  - `POST /routes` -> `RoutePlanningService.CreateRoute` (`route_planning:create`)
  - `POST /routes/{id}/optimize` -> `RoutePlanningService.OptimizeRoute` (`route_planning:optimize`)
  - `POST /routes/{id}/recommendation` -> `RoutePlanningService.GetRouteRecommendation` (`route_planning:read`)
  - `GET /approvals/routes` -> `RoutePlanningService.ListPendingApprovals` (`route_planning:approval:read`)
  - `POST /approvals/routes/{id}/approve` -> `RoutePlanningService.ApproveRoute` (`route_planning:approve`)
  - `POST /approvals/routes/{id}/reject` -> `RoutePlanningService.RejectRoute` (`route_planning:reject`)
- **TrackingController** (`/api/v1/tracking`):
  - `GET /{id}/current` -> `GpsTrackingService.GetCurrentLocation` (`shipments:read`)
  - `GET /{id}/history` -> `GpsTrackingService.ListPositionHistory` (`shipments:read`)
  - `POST /geofences` -> `GpsTrackingService.CreateGeofence` (`gps_tracking:geofence:manage`)
  - `GET /alerts` -> `GpsTrackingService.ListMonitoringAlerts` (`shipments:read`)
  - `POST /alerts/{id}/resolve` -> `GpsTrackingService.ResolveMonitoringAlert` (`shipments:update`)
- **ComplianceController** & **DocumentsController** (`/api/v1/compliance`, `/api/v1/documents`):
  - `POST /compliance/evaluate` -> `RegulatoryComplianceService.EvaluateCompliance` (`compliance:override` for manual overrides)
  - `POST /compliance/query` -> `RegulatoryComplianceService.QueryRegulations` (baseline read)
  - `POST /compliance/grounded-answer` -> `RegulatoryComplianceService.GenerateGroundedAnswer` (baseline read)
  - `POST /documents/ocr/jobs` -> `DocumentOcrService.SubmitDocumentJob` (baseline read)
  - `POST /documents/ocr/jobs/{id}/review` -> `DocumentOcrService.ReviewDocumentJob` (`ocr:review`)
  - `POST /documents/knowledge/ingest` -> `RegulatoryComplianceService.IngestKnowledgeDocument` (`documents:ingest`)
- **BillingController** & **FinancialController** (`/api/v1/billing`, `/api/v1/financial`):
  - `POST /billing/invoices/generate` -> `BillingService.GenerateInvoice` (`billing_settlement:invoice:create`)
  - `GET /billing/invoices` -> `BillingService.ListInvoices` (`billing_settlement:read`)
  - `GET /billing/credit/check` -> `BillingService.CheckCustomerCredit` (`billing_settlement:credit:check`)
  - `GET /billing/escrow/{walletId}` -> `BillingService.GetWalletBalance` (`billing_settlement:escrow:read`)
  - `POST /billing/escrow/release` -> `BillingService.ReleaseEscrowAmount` (`billing_settlement:settlement:manage`)
  - `POST /financial/estimate-cost` -> `FinancialService.EstimateCost` (`financial_tax:calculate`)
  - `POST /financial/customs-duty` -> `FinancialService.GetCustomsDuty` (`financial_tax:calculate`)

---

## 5. Identified Gaps & Missing Integrations

1. **Stalwart Direct DNS Hook**: Mail domain provisioning currently computes recommended DKIM/SPF/DMARC records in DB; automated DNS challenge provisioning requires external DNS registrar integration (e.g. Cloudflare API).
2. **Vietnamese E-Invoice Tax Authority Bridge**: `billing-service` contains an abstracted `EInvoiceAdapter` currently in mock mode; live integration with VNPT / Viettel HDDT API is required for production fiscal invoices.
3. **Multi-carrier Telematics Telemetry Ingestion**: `GpsTrackingService` exposes an internal gRPC `IngestPosition` endpoint; an external MQTT/HTTP broker gateway is needed to ingest raw hardware GPS tracker binary protocols (Teltonika, Queclink, Concox) into the protobuf format.
4. **Billing & Financial spec tests**: The NestJS `billing-service` and `financial-service` codebase operates with high functional quality but lacks unit `.spec.ts` test files in repository root compared to .NET and Java services.
