# Aurora Server — Comprehensive Technical Architecture & Service Inventory

> **Authoritative Status**: Audited directly against source code, protobuf contracts, Entity Framework & Prisma migrations, Flyway schemas, MassTransit & RabbitMQ event topologies, and test suites across `.NET 10`, `Java 21 / Spring Boot 3`, and `NestJS / Node.js`.

---

## 1. System Overview

Aurora is an enterprise-grade, multi-tenant SaaS logistics and supply chain execution platform. The system operates on a polyglot microservices architecture orchestrated via synchronous **gRPC** interfaces, asynchronous **RabbitMQ** event streaming (leveraging the Transactional Outbox Pattern for guaranteed delivery), and dedicated **Backend-For-Frontend (BFF)** gateways serving distinct user personas.

```
+─────────────────────────────────────────────────────────────────────────────+
|                                Frontend (SPA)                               |
+───────────────────────────────────────┬─────────────────────────────────────+
                                        │ HTTPS / WSS
                                        v
+─────────────────────────────────────────────────────────────────────────────+
|                        API Gateway (YARP Reverse Proxy)                     |
+───────────────┬───────────────────────┼───────────────────────┬─────────────+
                │                       │                       │
                v                       v                       v
      +──────────────────+    +──────────────────+    +──────────────────+
      |    Staff.Bff     |    |    Admin.Bff     |    |    System.Bff    |
      | (Staff Operators)|    |  (Tenant Admins) |    |  (System Admins) |
      +─────────┬────────+    +─────────┬────────+    +─────────┬────────+
                │                       │                       │
  gRPC (Metadata: x-tenant-id, x-user-id, x-roles, x-permissions)
                │                       │                       │
+───────────────┴───────────────────────┴───────────────────────┴─────────────+
|                          Microservices Ecosystem                            |
|                                                                             |
|  [.NET 10]                                                                  |
|  - IamTenant                - ShipmentWorkflow        - RoutePlanningAgent  |
|  - MailService              - DocumentOcr             - RegulatoryCompliance|
|  - GpsTracking              - Notification                                  |
|                                                                             |
|  [Java 21 / Spring Boot 3]                            [NestJS / Node.js]    |
|  - ai-governance            - devops-agent            - billing-service     |
|                                                       - financial-service   |
|                                                       - negotiation-agent   |
|                                                       - customer-assistant  |
|                                                       - realtime-hub        |
+─────────────────────────────────────────────────────────────────────────────+
```

---

## 2. Core Security & Capability Permission Model

Aurora enforces a strict **Capability-Based Access Control** model supplemented by **Role-Based Guardrails** and **Tenant Isolation**.

### 2.1 Metadata Propagation
All downstream gRPC communication propagates security context through standard gRPC metadata headers:
- `x-tenant-id`: Active tenant UUID (enforced across database queries via multi-tenant query filters).
- `x-user-id`: Authenticated user UUID.
- `x-user-role` / `x-roles`: Comma-separated assigned roles (`SYSTEM_ADMIN`, `TENANT_ADMIN`, `OPERATOR`, `CUSTOMS_OFFICER`, `FINANCE_OFFICER`, `STAFF`, `MANAGER`).
- `x-permissions`: Comma-separated list of granular capability permissions.
- `x-service-id`: Caller service identifier for internal service-to-service calls (e.g. `ai-governance` caller authentication).

### 2.2 Standard Capability Permissions (`PermissionConstants.cs`)
- **Mail**: `mail:read`, `mail:draft:create`, `mail:send`, `mail:thread:claim`, `mail:thread:read_all`, `mail:thread:reassign`, `mail:thread:unassign`, `mail:quarantine:read`, `mail:quarantine:release`, `mail:quarantine:delete`, `mail:audit:read`, `mail:domain:manage`, `mail:mailbox:manage`, `mail:system:manage`
- **Shipments**: `shipments:create`, `shipments:read`, `shipments:update`, `shipments:submit`, `shipments:cancel`, `shipments:delete`, `shipments:import`
- **Route Planning**: `route_planning:read`, `route_planning:create`, `route_planning:update`, `route_planning:optimize`, `route_planning:execute`, `route_planning:delete`, `route_planning:approval:read`, `route_planning:approve`, `route_planning:reject`, `route_planning:policy:manage`, `route_planning:policy:publish`
- **OCR**: `ocr:review`
- **Documents & Knowledge**: `documents:ingest`, `documents:manage`
- **Compliance**: `compliance:override`, `compliance:platform:ingest`
- **Financial**: `financial_tax:read`, `financial_tax:calculate`
- **Billing & Settlement**: `billing_settlement:read`, `billing_settlement:invoice:create`, `billing_settlement:invoice:update`, `billing_settlement:credit:check`, `billing_settlement:escrow:read`, `billing_settlement:settlement:manage`
- **GPS**: `gps_tracking:geofence:manage`
- **IAM**: `iam:user:read`, `iam:user:invite`, `iam:user:update`, `iam:role:read`, `iam:role:manage`, `iam:permission:manage`

---

## 3. Detailed Service-by-Service Audit

---

### 3.1 IamTenant Service
- **1. Responsibility / Bounded Context**: Tenant lifecycle management, user identity, invitations, role and capability permission assignments, authentication brokering (Cognito / Azure AD), and audit logging.
- **2. Technology / Runtime**: `.NET 10`, C#, ASP.NET Core gRPC, EF Core 10.
- **3. Database Ownership**: PostgreSQL (`iam_tenant` schema/database). Tables: `Tenants`, `Users`, `Roles`, `Permissions`, `RolePermissions`, `UserRoles`, `UserPermissions`, `AuditLogs`, `OutboxMessages`.
- **4. External Dependencies**: AWS Cognito (`AWSSDK.CognitoIdentityProvider`), Azure AD (optional integration), Redis (cache), PostgreSQL, RabbitMQ.
- **5. gRPC Services / RPCs**:
  - `auth.AuthService`: `IdentifyUser`, `Login`, `CompleteInvitation`, `RefreshToken`, `Logout`, `ValidateToken`, `ForgotPassword`, `ConfirmForgotPassword`
  - `iam.IamService`: `CreateTenant`, `GetTenant`, `UpdateTenantStatus`, `ListTenants`, `DeleteTenant`, `InviteUser`, `GetUser`, `GetManyUsers`, `UpdateUser`, `ActivateUser`, `ResetUserPassword`, `AssignRoles`, `SuspendUser`, `CreateCustomRole`, `GetRole`, `GetManyRoles`, `UpdateRole`, `DeleteRole`, `AssignPermissionsToRole`, `GetUserPermissions`
- **6. Commands**: `CreateTenantCommand`, `UpdateTenantCommand`, `UpdateTenantStatusCommand`, `DeleteTenantCommand`, `CreateStaffCommand`, `UpdateStaffCommand`, `ActivateStaffCommand`, `DeactivateStaffCommand`, `ResetStaffPasswordCommand`, `AssignRolesCommand`, `CreateCustomRoleCommand`, `UpdateRoleCommand`, `DeleteRoleCommand`, `AssignPermissionsToRoleCommand`.
- **7. Queries**: `IdentifyUserQuery`, `ResolveTenantAuthClientQuery`, `GetTenantQuery`, `ListTenantsQuery`, `GetStaffQuery`, `ListStaffQuery`, `GetRoleQuery`, `ListRolesQuery`, `GetUserPermissionsQuery`.
- **8. RabbitMQ Published Events**: `tenant_admin_created_event`, `tenant_staff_created_event`, `tenant_staff_password_reset_event`, `role_permissions_changed_event`.
- **9. RabbitMQ Consumed Events**: None.
- **10. AiGovernance Capabilities Used**: None.
- **11. BFF Exposing the Service**: `Admin.Bff` (`UsersController`, `StaffController`, `RolesController`), `System.Bff` (`TenantsController`), `BuildingBlocks.BFF` (`AuthController`).
- **12. REST APIs Exposed to FE**:
  - `POST /api/v1/auth/login`, `POST /api/v1/auth/refresh`, `POST /api/v1/auth/logout`, `POST /api/v1/auth/invite/complete`
  - `GET /api/v1/admin/staff`, `POST /api/v1/admin/staff`, `GET /api/v1/admin/staff/{id}`, `PUT /api/v1/admin/staff/{id}`, `POST /api/v1/admin/staff/{id}/activate`, `POST /api/v1/admin/staff/{id}/deactivate`, `POST /api/v1/admin/staff/{id}/reset-password`, `PUT /api/v1/admin/staff/{id}/roles`
  - `GET /api/v1/admin/roles`, `GET /api/v1/admin/roles/{id}`
  - `POST /api/v1/system/tenants`, `GET /api/v1/system/tenants`, `GET /api/v1/system/tenants/{id}`, `PATCH /api/v1/system/tenants/{id}/status`, `DELETE /api/v1/system/tenants/{id}`
- **13. Required Permissions**: `iam:user:read`, `iam:user:invite`, `iam:user:update`, `iam:role:read`, `iam:role:manage`, `iam:permission:manage`.
- **14. Resource-Level Authorization**: Enforces `tenant_id` scoping on all queries; `SYSTEM_ADMIN` role required for system tenant operations.
- **15. User Persona Usage**: System Admin (Tenants), Tenant Admin (Staff & Roles), All Users (Authentication).
- **16. Soft-Delete Behavior**: Soft deletion implemented on `Tenant`, `User`, `Role` (`IsDeleted`, `DeletedAt`); background `SoftDeleteCleanupWorker` purges records after retention threshold.
- **17. Current Implementation Status**: **COMPLETE**.
- **18. Test Coverage**: Integrated in test harness, manual verification suite, EF Core migration verification.
- **19. Known Gaps / Debt**: Azure AD sync is stubbed in favor of AWS Cognito as primary IdP.

---

### 3.2 AiGovernance Service
- **1. Responsibility / Bounded Context**: Centralized AI gateway, policy enforcement, rate limiting, quota tracking, provider key abstraction (Shared pool & BYOK), token budget reservation, audit logging of AI decisions.
- **2. Technology / Runtime**: `Java 21`, Spring Boot 3.3.x, `grpc-spring-boot-starter`, Flyway, Lettuce / Jedis.
- **3. Database Ownership**: PostgreSQL (`ai_governance` schema). Tables: `tenants`, `governance_policies`, `capability_configs`, `quota_limits`, `usage_records`, `decisions`, `provider_keys`, `provider_quotas`, `outbox_events`. Redis for Lua-based rate limiting (`reserve_capacity.lua`) & distributed locking.
- **4. External Dependencies**: OpenAI / Azure OpenAI API, Anthropic Claude API, Google Gemini API, PostgreSQL, Redis, RabbitMQ.
- **5. gRPC Services / RPCs**:
  - `ai_governance.AiGovernanceService`: `ExecutePolicy` (Policy pre-check)
  - `ai_governance.AiExecutionService`: `Generate` (Governed text/multimodal generation), `Embed` (Governed vector embedding)
- **6. Commands**: Internal handlers for `ExecutePolicyCommand`, `GenerateExecutionCommand`, `EmbedExecutionCommand`, `ReserveCapacityCommand`, `RecordUsageCommand`.
- **7. Queries**: `GetTenantAiConfigQuery`, `GetProviderQuotaQuery`, `GetDecisionHistoryQuery`.
- **8. RabbitMQ Published Events**: `ai.usage.tracked`, `ai.quota.exceeded`, `ai.policy.denied`.
- **9. RabbitMQ Consumed Events**: `tenant.created` (initializes default tenant quotas and policies).
- **10. AiGovernance Capabilities Supported**:
  - `mail.phishing_detection`, `mail.risk_scoring`
  - `ocr.invoice_extraction`, `ocr.customs_extraction`, `ocr.bill_of_lading`
  - `compliance.grounded_qa`, `compliance.regulation_retrieval`, `compliance.evidence_verification`
  - `route.optimization_recommendation`, `route.risk_assessment`
  - `negotiation.strategy_offer`, `negotiation.counter_proposal`
  - `assistant.intent_routing`, `assistant.chat_completion`, `assistant.conversation_summary`
  - `devops.incident_rca`, `devops.rule_generation`
- **11. BFF Exposing the Service**: `Admin.Bff` (`AiConfigController`), indirect through consuming backend services.
- **12. REST APIs Exposed to FE**:
  - `GET /api/v1/admin/ai-configs/{feature}`
  - `PUT /api/v1/admin/ai-configs/{feature}`
- **13. Required Permissions**: `route_planning:policy:manage` (for route AI configs), `system:manage` (for global provider slots).
- **14. Resource-Level Authorization**: Strict tenant boundary validation on API keys and token budgets; requires `x-service-id` header validation.
- **15. User Persona Usage**: System Admin (Provider pools), Tenant Admin (BYOK keys, Quota configs), Machine/Service-to-Service (All AI operations).
- **16. Soft-Delete Behavior**: Logical deactivation for policies (`is_active = false`), hard delete prevention on audit logs and usage ledgers.
- **17. Current Implementation Status**: **COMPLETE**.
- **18. Test Coverage**: **HIGH** (Extensive unit, integration, and capacity reservation tests in `com.aurora.aigovernance.*`).
- **19. Known Gaps / Debt**: Streaming RPC `GenerateStream` is defined as a forward-looking proto placeholder and not yet active.

---

### 3.3 MailService
- **1. Responsibility / Bounded Context**: Multi-tenant email management, mailbox provisioning, domain verification, SMTP/IMAP orchestration via Stalwart, outbound policy enforcement, phishing/spam/malware security analysis, and shared thread assignment & triage.
- **2. Technology / Runtime**: `.NET 10`, C#, ASP.NET Core gRPC, EF Core 10, MailKit, ClamAV client, SpamAssassin client.
- **3. Database Ownership**: PostgreSQL (`mail_service` schema/database). Tables: `mail_domains`, `mailboxes`, `mailbox_aliases`, `email_drafts`, `processed_messages`, `quarantine_records`, `thread_records`, `thread_assignments`, `thread_assignment_history`, `mail_audit_logs`, `outbox_messages`. Cloudflare R2 / S3 for raw MIME storage; Stalwart Server for email protocols; Redis for claim locks.
- **4. External Dependencies**: Stalwart Mail Server (Management API & SMTP), ClamAV daemon, SpamAssassin daemon, Cloudflare R2, PostgreSQL, Redis, RabbitMQ, AiGovernance gRPC.
- **5. gRPC Services / RPCs**:
  - `mail.MailManagement`: `ProvisionDomain`, `CreateMailbox`, `CreateAlias`, `ResetPassword`, `GetAuditRecords`, `RequeueDeadLetter`
  - `mail.MailSecurity`: `CreateDraftMessage`, `ListDrafts`, `GetDraft`, `SubmitOutboundMessage`, `GetProcessedMessage`, `ListProcessedMessages`, `GetQuarantineRecord`, `ListQuarantineRecords`, `ReleaseQuarantine`, `DeleteQuarantine`, `GetThread`, `ListThreads`, `ClaimThread`, `ReassignThread`, `UnassignThread`, `GetThreadAssignmentHistory`
- **6. Commands**: `CreateDraftCommand`, `SubmitOutboundMessageCommand`, `ClaimThreadCommand`, `ReassignThreadCommand`, `UnassignThreadCommand`, `ReleaseQuarantineCommand`, `DeleteQuarantineCommand`, `ProvisionDomainCommand`, `CreateMailboxCommand`, `ResetPasswordCommand`.
- **7. Queries**: `ListDraftsQuery`, `GetDraftQuery`, `ListThreadsQuery`, `GetThreadQuery`, `GetThreadAssignmentHistoryQuery`, `ListProcessedMessagesQuery`, `ListQuarantineRecordsQuery`, `GetAuditRecordsQuery`.
- **8. RabbitMQ Published Events**: `inbound_email_received_event`, `inbound_email_quarantined_event`, `outbound_email_sent_event`, `outbound_email_rejected_event`.
- **9. RabbitMQ Consumed Events**: `send_system_email_command`, `tenant_staff_created_event`, `tenant_admin_created_event`.
- **10. AiGovernance Capabilities Used**: `mail.phishing_detection`, `mail.risk_scoring`.
- **11. BFF Exposing the Service**: `Staff.Bff` (`MailController`, `NegotiationsController`), `Admin.Bff` (`MailAdminController`), `System.Bff` (`MailSystemController`).
- **12. REST APIs Exposed to FE**:
  - Staff: `POST /api/v1/mail/drafts`, `GET /api/v1/mail/drafts`, `GET /api/v1/mail/drafts/{id}`, `GET /api/v1/mail/threads`, `GET /api/v1/mail/threads/{id}`, `POST /api/v1/mail/threads/{id}/claim`, `POST /api/v1/mail/threads/{id}/reassign`, `POST /api/v1/mail/threads/{id}/unassign`, `GET /api/v1/mail/threads/{id}/assignment-history`, `POST /api/v1/mail/messages/outbound`, `GET /api/v1/mail/messages`, `GET /api/v1/mail/messages/{id}`, `GET /api/v1/mail/quarantine`, `GET /api/v1/mail/quarantine/{id}`, `POST /api/v1/mail/quarantine/{id}/release`
  - Admin: `POST /api/v1/admin/mail/domains`, `POST /api/v1/admin/mail/mailboxes`, `POST /api/v1/admin/mail/aliases`, `POST /api/v1/admin/mail/mailboxes/{id}/reset-password`, `DELETE /api/v1/admin/mail/quarantine/{id}`, `GET /api/v1/admin/mail/audit`
  - System: `POST /api/v1/system/mail/dead-letter/{id}/requeue`, `GET /api/v1/system/mail/audit`
- **13. Required Permissions**: Full suite of `mail:*` permissions (see Section 2.2).
- **14. Resource-Level Authorization**: Staff can only view/reply to threads assigned to them unless possessing `mail:thread:read_all`. Reassignment and unassignment require supervisory permissions (`mail:thread:reassign`, `mail:thread:unassign`).
- **15. User Persona Usage**: Staff (Daily email management & claiming), Manager (Thread supervision & reassignments), Tenant Admin (Domain & Mailbox provisioning), System Admin (Platform dead letter queues).
- **16. Soft-Delete Behavior**: Quarantine records and drafts support soft deletion and purge. Audit records are immutable.
- **17. Current Implementation Status**: **COMPLETE**.
- **18. Test Coverage**: **HIGH** (`tests/dotnet/MailService.Tests` contains unit, smoke, thread assignment, and BFF integration tests).
- **19. Known Gaps / Debt**: Live DKIM DNS auto-generation requires external registrar integration; currently generates recommended DNS TXT records.

---

### 3.4 ShipmentWorkflow Service
- **1. Responsibility / Bounded Context**: Core freight shipment lifecycle, state machine transitions (Draft -> Submitted -> Booked -> InTransit -> Delivered -> Completed / Cancelled), cargo inventory, multi-modal locations, document attachments, milestones, and audit history.
- **2. Technology / Runtime**: `.NET 10`, C#, ASP.NET Core gRPC, EF Core 10.
- **3. Database Ownership**: PostgreSQL (`shipment_workflow` schema/database). Tables: `shipments`, `cargo_items`, `shipment_documents`, `shipment_locations`, `shipment_milestones`, `shipment_status_histories`, `outbox_messages`.
- **4. External Dependencies**: PostgreSQL, RabbitMQ.
- **5. gRPC Services / RPCs**:
  - `shipment.ShipmentWorkflowService`: `CreateShipment`, `GetShipment`, `ListShipments`, `UpdateShipmentStatus`, `GetShipmentTimeline`, `SubmitShipment`, `UpdateShipment`, `CancelShipment`, `DeleteDraftShipment`, `AddCargoItem`, `UpdateCargoItem`, `RemoveCargoItem`, `AddShipmentLocation`, `UpdateShipmentLocation`, `RemoveShipmentLocation`, `AttachShipmentDocument`, `UpdateShipmentDocumentOcr`, `RemoveShipmentDocument`, `AddShipmentMilestone`, `ImportShipments`
- **6. Commands**: `CreateShipmentCommand`, `UpdateShipmentCommand`, `SubmitShipmentCommand`, `UpdateShipmentStatusCommand`, `CancelShipmentCommand`, `DeleteDraftShipmentCommand`, `AddCargoItemCommand`, `UpdateCargoItemCommand`, `RemoveCargoItemCommand`, `AddShipmentLocationCommand`, `UpdateShipmentLocationCommand`, `RemoveShipmentLocationCommand`, `AttachShipmentDocumentCommand`, `UpdateShipmentDocumentOcrCommand`, `RemoveShipmentDocumentCommand`, `AddShipmentMilestoneCommand`, `ImportShipmentsCommand`.
- **7. Queries**: `GetShipmentQuery`, `ListShipmentsQuery`, `GetShipmentTimelineQuery`.
- **8. RabbitMQ Published Events**: `ShipmentCreatedEvent`, `ShipmentSubmittedEvent`, `ShipmentUpdatedEvent`, `ShipmentStatusChangedEvent`, `ShipmentPickedUpEvent`, `ShipmentDeliveredEvent`, `ShipmentCompletedEvent`, `ShipmentCancelledEvent`, `CargoUpdatedEvent`, `DocumentAttachedEvent`, `RouteAssignedEvent`.
- **9. RabbitMQ Consumed Events**: None (Emits upstream domain events).
- **10. AiGovernance Capabilities Used**: None directly (integrates downstream via Document OCR and Compliance events).
- **11. BFF Exposing the Service**: `Staff.Bff` (`ShipmentsController`).
- **12. REST APIs Exposed to FE**:
  - `POST /api/v1/shipments`, `GET /api/v1/shipments/{id}`, `GET /api/v1/shipments`, `PUT /api/v1/shipments/{id}`, `POST /api/v1/shipments/{id}/submit`, `PATCH /api/v1/shipments/{id}/status`, `POST /api/v1/shipments/{id}/cancel`, `DELETE /api/v1/shipments/{id}`, `POST /api/v1/shipments/import`, `POST /api/v1/shipments/{id}/cargo`, `PUT /api/v1/shipments/{id}/cargo/{itemId}`, `DELETE /api/v1/shipments/{id}/cargo/{itemId}`, `POST /api/v1/shipments/{id}/locations`, `PUT /api/v1/shipments/{id}/locations/{locId}`, `DELETE /api/v1/shipments/{id}/locations/{locId}`, `POST /api/v1/shipments/{id}/documents`, `DELETE /api/v1/shipments/{id}/documents/{docId}`, `POST /api/v1/shipments/{id}/milestones`, `GET /api/v1/shipments/{id}/timeline`
- **13. Required Permissions**: `shipments:create`, `shipments:read`, `shipments:update`, `shipments:submit`, `shipments:cancel`, `shipments:delete`, `shipments:import`.
- **14. Resource-Level Authorization**: Multi-tenant isolation by `tenant_id`. Deletion and cancellation restricted to Draft/Pending states unless Manager permission is present.
- **15. User Persona Usage**: Staff (Creation, updating, milestone tracking), Manager (Cancellation, deletion, bulk import).
- **16. Soft-Delete Behavior**: Draft deletion performs hard delete if uncommitted, soft status transition (`CANCELLED`) once active.
- **17. Current Implementation Status**: **COMPLETE**.
- **18. Test Coverage**: **HIGH** (`src/dotnet/ShipmentWorkflow/Tests` contains state machine, aggregate expansion, query, and integration event tests).
- **19. Known Gaps / Debt**: Milestone ETA recalculation based on GPS alerts is handled via consumers rather than synchronous internal hooks.

---

### 3.5 RoutePlanningAgent Service
- **1. Responsibility / Bounded Context**: Vehicle route optimization, dynamic waypoint sequencing, risk policy evaluation, approval workflows for high-risk routes, and tenant routing policy management.
- **2. Technology / Runtime**: `.NET 10`, C#, ASP.NET Core gRPC, EF Core 10, VROOM Optimization Engine integration.
- **3. Database Ownership**: PostgreSQL (`route_planning` schema/database). Tables: `routes`, `stops`, `route_legs`, `route_approvals`, `tenant_ai_configs`, `tenant_rule_configs`, `tenant_risk_policies`, `tenant_risk_rules`, `outbox_messages`.
- **4. External Dependencies**: External VROOM engine (HTTP endpoint), ComplianceRag gRPC service, PostgreSQL, RabbitMQ, AiGovernance gRPC.
- **5. gRPC Services / RPCs**:
  - `route_planning.RoutePlanningService`: `CreateRoute`, `GetRoute`, `ListRoutes`, `UpdateRoute`, `DeleteRoute`, `UpdateRouteStatus`, `OptimizeRoute`, `ApproveRoute`, `RejectRoute`, `ListPendingApprovals`, `GetRouteRecommendation`, `GetTenantAiConfig`, `UpsertTenantAiConfig`, `UpsertTenantRuleConfig`, `ListTenantRuleConfigs`, `CreateRiskPolicyDraft`, `UpdateRiskPolicyDraft`, `SubmitRiskPolicyForReview`, `RejectRiskPolicy`, `PublishRiskPolicy`, `GetRiskPolicy`, `GetActiveRiskPolicy`, `ListRiskPolicyVersions`, `DeleteRiskPolicyDraft`
- **6. Commands**: `CreateRouteCommand`, `UpdateRouteCommand`, `DeleteRouteCommand`, `UpdateRouteStatusCommand`, `OptimizeRouteCommand`, `ApproveRouteCommand`, `RejectRouteCommand`, `RequestRouteRecommendationCommand`, `UpsertTenantAiConfigCommand`, `UpsertTenantRuleConfigCommand`, `CreateRiskPolicyDraftCommand`, `UpdateRiskPolicyDraftCommand`, `SubmitRiskPolicyCommand`, `RejectRiskPolicyCommand`, `PublishRiskPolicyCommand`, `DeleteRiskPolicyDraftCommand`.
- **7. Queries**: `GetRouteQuery`, `ListRoutesQuery`, `ListPendingApprovalsQuery`, `GetTenantAiConfigQuery`, `ListTenantRuleConfigsQuery`, `GetRiskPolicyQuery`, `GetActiveRiskPolicyQuery`, `ListRiskPolicyVersionsQuery`.
- **8. RabbitMQ Published Events**: `route_created_event`, `route_updated_event`, `route_deleted_event`, `route_status_changed_event`, `route_optimized_event`, `route_approval_requested_event`, `route_risk_evaluated_event`, `route_approved_event`, `route_rejected_event`, `tenant_ai_config_changed_event`, `tenant_rule_config_changed_event`, `tenant_risk_policy_created_event`, `tenant_risk_policy_submitted_event`, `tenant_risk_policy_rejected_event`, `tenant_risk_policy_published_event`, `tenant_risk_policy_superseded_event`.
- **9. RabbitMQ Consumed Events**: `tenant_rule_config_changed_event` (invalidates local rule caches).
- **10. AiGovernance Capabilities Used**: `route.optimization_recommendation`, `route.risk_assessment`.
- **11. BFF Exposing the Service**: `Staff.Bff` (`RoutesController`, `ApprovalsController`), `Admin.Bff` (`AiConfigController`, `RuleConfigController`).
- **12. REST APIs Exposed to FE**:
  - Staff: `POST /api/v1/routes`, `GET /api/v1/routes`, `GET /api/v1/routes/{id}`, `PUT /api/v1/routes/{id}`, `DELETE /api/v1/routes/{id}`, `PATCH /api/v1/routes/{id}/status`, `POST /api/v1/routes/{id}/optimize`, `POST /api/v1/routes/{id}/recommendation`
  - Approvals: `GET /api/v1/approvals/routes`, `POST /api/v1/approvals/routes/{id}/approve`, `POST /api/v1/approvals/routes/{id}/reject`
  - Admin: `GET /api/v1/admin/ai-configs/{feature}`, `PUT /api/v1/admin/ai-configs/{feature}`, `GET /api/v1/admin/rule-configs`, `PUT /api/v1/admin/rule-configs/{ruleName}`
- **13. Required Permissions**: `route_planning:read`, `route_planning:create`, `route_planning:update`, `route_planning:optimize`, `route_planning:execute`, `route_planning:delete`, `route_planning:approval:read`, `route_planning:approve`, `route_planning:reject`, `route_planning:policy:manage`, `route_planning:policy:publish`.
- **14. Resource-Level Authorization**: High-risk routes trigger approval flags requiring Manager approval permission (`route_planning:approve`). Draft risk policies cannot be published without `route_planning:policy:publish`.
- **15. User Persona Usage**: Staff (Route planning & optimization), Manager (Risk policy approvals), Tenant Admin (Policy & Rule tuning).
- **16. Soft-Delete Behavior**: Soft deletion on routes and policy drafts.
- **17. Current Implementation Status**: **COMPLETE**.
- **18. Test Coverage**: **HIGH** (`src/dotnet/RoutePlanningAgent/RoutePlanningAgent.Tests` covers AI parsing, commands, optimization, rules, governance, and policy lifecycle).
- **19. Known Gaps / Debt**: Live traffic-aware matrix calculations fallback to Haversine/OSRM when external matrix API key is unconfigured.

---

### 3.6 Document OCR Service
- **1. Responsibility / Bounded Context**: Asynchronous document text and entity extraction (Invoices, Bills of Lading, Customs Declarations, Packing Lists), OCR confidence scoring, human review queuing, and normalized JSON schema conversion.
- **2. Technology / Runtime**: `.NET 10`, C#, ASP.NET Core gRPC, EF Core 10, Background Workers.
- **3. Database Ownership**: PostgreSQL (`document_ocr` schema/database). Tables: `document_ocr_jobs`, `ocr_provider_attempts`, `outbox_messages`, `inbox_messages`. Local file system / Object storage for raw document artifacts.
- **4. External Dependencies**: AiGovernance gRPC (for multimodal LLM extraction), PostgreSQL, RabbitMQ.
- **5. gRPC Services / RPCs**:
  - `document_ocr.DocumentOcrService`: `SubmitDocumentJob`, `SubmitOcrJob`, `GetDocumentJob`, `ListDocumentJobs`, `CancelDocumentJob`, `RetryDocumentJob`, `ReviewDocumentJob`
- **6. Commands**: `SubmitDocumentJobCommand`, `SubmitOcrJobCommand`, `CancelDocumentJobCommand`, `RetryDocumentJobCommand`, `ReviewDocumentJobCommand`, `ProcessDocumentJobCommand`.
- **7. Queries**: `GetDocumentJobQuery`, `ListDocumentJobsQuery`.
- **8. RabbitMQ Published Events**: `DocumentOcrCompletedEvent`, `DocumentOcrFailedEvent`.
- **9. RabbitMQ Consumed Events**: `DocumentAttachedEvent` (auto-triggers OCR jobs from shipments).
- **10. AiGovernance Capabilities Used**: `ocr.invoice_extraction`, `ocr.customs_extraction`, `ocr.bill_of_lading`.
- **11. BFF Exposing the Service**: `Staff.Bff` (`DocumentsController`).
- **12. REST APIs Exposed to FE**:
  - `POST /api/v1/documents/ocr/jobs`
  - `GET /api/v1/documents/ocr/jobs/{id}`
  - `GET /api/v1/documents/ocr/jobs`
  - `POST /api/v1/documents/ocr/jobs/{id}/cancel`
  - `POST /api/v1/documents/ocr/jobs/{id}/retry`
  - `POST /api/v1/documents/ocr/jobs/{id}/review`
- **13. Required Permissions**: `ocr:review`, `documents:ingest`, `documents:manage`.
- **14. Resource-Level Authorization**: Tenant isolation enforced by `tenant_id`. Reviewing low-confidence OCR results requires `ocr:review`.
- **15. User Persona Usage**: Staff (Submit documents, view extracted data), Manager / Customs Officer (Human-in-the-loop review of disputed extractions).
- **16. Soft-Delete Behavior**: Soft deletion on OCR jobs (`IsDeleted`); underlying audit trail retained.
- **17. Current Implementation Status**: **COMPLETE**.
- **18. Test Coverage**: **HIGH** (`src/dotnet/DocumentOcr/Tests` covers contracts, persistence, provider abstractions, background workers, and PostgreSQL integration).
- **19. Known Gaps / Debt**: Complex tabular extraction on damaged physical scans defaults to human review flag when confidence falls below 0.85.

---

### 3.7 Regulatory Compliance Service
- **1. Responsibility / Bounded Context**: Trade regulation ingestion, semantic vector retrieval (pgvector), automated customs compliance evaluation against shipment manifests, grounded LLM QA generation, and evidence validation.
- **2. Technology / Runtime**: `.NET 10`, C#, ASP.NET Core gRPC, EF Core 10, `Pgvector.EntityFrameworkCore`.
- **3. Database Ownership**: PostgreSQL with `pgvector` extension (`regulatory_compliance` schema/database). Tables: `regulatory_documents`, `regulatory_document_versions`, `regulatory_chunks`, `knowledge_documents`, `knowledge_document_versions`, `knowledge_chunks`, `compliance_evaluations`, `compliance_findings`, `compliance_citations`, `retrieval_traces`, `outbox_messages`, `inbox_messages`.
- **4. External Dependencies**: AiGovernance gRPC (for text embeddings via `AiExecutionService.Embed` and grounded answers via `AiExecutionService.Generate`), PostgreSQL + pgvector, RabbitMQ.
- **5. gRPC Services / RPCs**:
  - `regulatory_compliance.RegulatoryComplianceService`: `EvaluateCompliance`, `GetComplianceEvaluation`, `QueryRegulations`, `IngestRegulatorySource`, `IngestKnowledgeDocument`, `QueryKnowledge`, `GenerateGroundedAnswer`, `ValidateGroundedEvidence`
  - `compliance_rag.ComplianceRag`: `CheckRouteCompliance`
- **6. Commands**: `EvaluateComplianceCommand`, `IngestRegulatorySourceCommand`, `IngestKnowledgeDocumentCommand`, `ValidateGroundedEvidenceCommand`.
- **7. Queries**: `GetComplianceEvaluationQuery`, `QueryRegulationsQuery`, `QueryKnowledgeQuery`, `GenerateGroundedAnswerQuery`.
- **8. RabbitMQ Published Events**: `ComplianceEvaluationCompletedEvent`, `ComplianceEvaluationFailedEvent`.
- **9. RabbitMQ Consumed Events**: `ShipmentSubmittedEvent` (automatically schedules compliance validation).
- **10. AiGovernance Capabilities Used**: `compliance.regulation_retrieval`, `compliance.grounded_qa`, `compliance.evidence_verification`.
- **11. BFF Exposing the Service**: `Staff.Bff` (`ComplianceController`, `DocumentsController`, `SearchController`), `Admin.Bff` (`PlatformIngestionController`), `System.Bff` (`SystemIngestionController`).
- **12. REST APIs Exposed to FE**:
  - `POST /api/v1/compliance/evaluate`
  - `GET /api/v1/compliance/evaluations/{id}`
  - `POST /api/v1/compliance/query`
  - `POST /api/v1/compliance/grounded-answer`
  - `POST /api/v1/documents/knowledge/ingest`
  - `POST /api/v1/documents/knowledge/query`
  - `POST /api/v1/admin/ingestion/regulatory-sources`
  - `POST /api/v1/admin/ingestion/knowledge-documents`
- **13. Required Permissions**: `compliance:override`, `compliance:platform:ingest`, `documents:ingest`, `documents:manage`.
- **14. Resource-Level Authorization**: Multi-tenant document scoping; platform regulatory documents marked `PUBLIC` visible across tenants, custom operational guides isolated per tenant.
- **15. User Persona Usage**: Customs Officer (Compliance evaluation & dispute resolution), Staff (Knowledge queries), Tenant/System Admin (Regulation ingestion).
- **16. Soft-Delete Behavior**: Versioned documents; retiring a document increments version and archives prior vector chunks.
- **17. Current Implementation Status**: **COMPLETE**.
- **18. Test Coverage**: **HIGH** (`src/dotnet/RegulatoryCompliance/Tests` covers evaluation, embedding vectors, grounded assistant, retrieval, and cross-tenant isolation).
- **19. Known Gaps / Debt**: Multi-language translation for foreign customs manifests requires pre-processing step.

---

### 3.8 GPS Tracking Service
- **1. Responsibility / Bounded Context**: Real-time vehicle telematics ingestion, geofence definitions (polygon & circular), presence detection, route deviation & delay alerts, signal loss watchdog monitoring.
- **2. Technology / Runtime**: `.NET 10`, C#, ASP.NET Core gRPC, EF Core 10, Background Workers.
- **3. Database Ownership**: PostgreSQL (`gps_tracking` schema/database). Tables: `gps_positions`, `current_locations`, `geofences`, `geofence_presences`, `monitoring_alerts`, `vehicle_shipment_assignments`, `shipment_tracking_states`, `consumed_integration_events`, `outbox_messages`.
- **4. External Dependencies**: PostgreSQL, RabbitMQ.
- **5. gRPC Services / RPCs**:
  - `GpsTrackingService`: `IngestPosition`, `GetCurrentLocation`, `ListPositionHistory`, `CreateGeofence`, `ListGeofences`, `SetGeofenceActive`, `ListMonitoringAlerts`, `ResolveMonitoringAlert`
- **6. Commands**: `IngestPositionCommand`, `CreateGeofenceCommand`, `SetGeofenceActiveCommand`, `ResolveMonitoringAlertCommand`, `AssignVehicleShipmentCommand`.
- **7. Queries**: `GetCurrentLocationQuery`, `ListPositionHistoryQuery`, `ListGeofencesQuery`, `ListMonitoringAlertsQuery`.
- **8. RabbitMQ Published Events**: `GpsPositionUpdatedEvent`, `GpsMonitoringAlertRaisedEvent`.
- **9. RabbitMQ Consumed Events**: `RouteAssignedEvent`, `ShipmentPickedUpEvent`, `ShipmentDeliveredEvent`.
- **10. AiGovernance Capabilities Used**: None.
- **11. BFF Exposing the Service**: `Staff.Bff` (`TrackingController`).
- **12. REST APIs Exposed to FE**:
  - `GET /api/v1/tracking/{id}/current`
  - `GET /api/v1/tracking/{id}/history`
  - `POST /api/v1/tracking/geofences`
  - `GET /api/v1/tracking/geofences`
  - `PATCH /api/v1/tracking/geofences/{id}/active`
  - `GET /api/v1/tracking/alerts`
  - `POST /api/v1/tracking/alerts/{id}/resolve`
- **13. Required Permissions**: `gps_tracking:geofence:manage`, `shipments:read`, `shipments:update`.
- **14. Resource-Level Authorization**: Position tracking scoped to tenant's active vehicles and assigned shipments.
- **15. User Persona Usage**: Operator / Staff (Real-time fleet monitoring, alert resolution), Manager (Geofence zone configuration).
- **16. Soft-Delete Behavior**: Geofences deactivated via status flag (`IsActive = false`); telemetry positions retained for historical audit.
- **17. Current Implementation Status**: **COMPLETE**.
- **18. Test Coverage**: **HIGH** (`src/dotnet/GpsTracking/Tests` covers domain, persistence, gRPC, ingestion, geofencing, and Postgres integration).
- **19. Known Gaps / Debt**: Real-time position stream to web clients delegates to `RealtimeHub` rather than maintaining direct gRPC server streaming to browsers.

---

### 3.9 Notification Service
- **1. Responsibility / Bounded Context**: Multi-channel notification delivery (In-App notifications, SMTP email dispatch), tenant notification preferences, delivery retry policies, event projection from domain events.
- **2. Technology / Runtime**: `.NET 10`, C#, ASP.NET Core gRPC, EF Core 10, Background Workers, MailKit / SMTP.
- **3. Database Ownership**: PostgreSQL (`notification` schema/database). Tables: `notification_messages`, `notification_preferences`, `notification_delivery_attempts`, `consumed_integration_events`.
- **4. External Dependencies**: SMTP Server, PostgreSQL, RabbitMQ.
- **5. gRPC Services / RPCs**:
  - `NotificationService`: `ListNotifications`, `MarkNotificationRead`, `ListNotificationPreferences`, `UpsertNotificationPreference`
- **6. Commands**: `MarkNotificationReadCommand`, `UpsertNotificationPreferenceCommand`, `DeliverNotificationCommand`.
- **7. Queries**: `ListNotificationsQuery`, `ListNotificationPreferencesQuery`.
- **8. RabbitMQ Published Events**: None (Leaf notification consumer).
- **9. RabbitMQ Consumed Events**:
  - `ShipmentStatusChangedEvent`, `ShipmentSubmittedEvent`
  - `ComplianceEvaluationCompletedEvent`, `ComplianceEvaluationFailedEvent`
  - `DocumentOcrCompletedEvent`, `DocumentOcrFailedEvent`
  - `GpsMonitoringAlertRaisedEvent`
- **10. AiGovernance Capabilities Used**: None.
- **11. BFF Exposing the Service**: `Staff.Bff` (`NotificationsController`).
- **12. REST APIs Exposed to FE**:
  - `GET /api/v1/notifications`
  - `PATCH /api/v1/notifications/{id}/read`
  - `GET /api/v1/notifications/preferences`
  - `PUT /api/v1/notifications/preferences`
- **13. Required Permissions**: Standard authenticated staff access (Scoped to own `user_id` and `tenant_id`).
- **14. Resource-Level Authorization**: Users can only read and mutate their own notifications and preferences.
- **15. User Persona Usage**: All Users.
- **16. Soft-Delete Behavior**: Notifications marked read/archived; hard deleted on user account purging.
- **17. Current Implementation Status**: **COMPLETE**.
- **18. Test Coverage**: **HIGH** (`src/dotnet/Notification/Tests` covers projectors, retry policies, domain factories, gRPC, and integration).
- **19. Known Gaps / Debt**: Push notifications (WebPush / FCM) planned for future mobile app integration.

---

### 3.10 Billing & Settlement Service
- **1. Responsibility / Bounded Context**: Invoice generation from completed shipments / POD uploads, line item rating, VAT calculation, credit score checking, escrow wallet balances, funds freeze/release, and e-invoice PDF generation.
- **2. Technology / Runtime**: `NestJS 10`, TypeScript, Node.js 20, Prisma ORM, `@grpc/grpc-js`, Redis.
- **3. Database Ownership**: PostgreSQL (`billing_service` via Prisma). Tables: `invoices`, `invoice_items`, `wallets`, `escrow_transactions`, `credit_limits`. S3/MinIO for PDF storage.
- **4. External Dependencies**: FinancialService (gRPC client), PostgreSQL, Redis, RabbitMQ, S3/MinIO.
- **5. gRPC Services / RPCs**:
  - `billing.BillingService`: `GenerateInvoice`, `GetInvoiceDetail`, `CheckCustomerCredit`, `CreateInvoice`, `GetInvoice`, `ListInvoices`, `UpdateInvoiceStatus`, `CreateEscrowWallet`, `GetWalletBalance`, `FreezeEscrowAmount`, `ReleaseEscrowAmount`, `RefundEscrowAmount`
- **6. Commands**: `GenerateInvoiceUseCase`, `FreezeEscrowUseCase`, `ReleaseEscrowUseCase`, `UpdateInvoiceStatusUseCase`.
- **7. Queries**: `GetInvoiceQuery`, `ListInvoicesQuery`, `CheckCreditQuery`, `GetWalletBalanceQuery`.
- **8. RabbitMQ Published Events**: `billing.invoice.generated`, `billing.invoice.paid`, `billing.escrow.frozen`, `billing.escrow.released`.
- **9. RabbitMQ Consumed Events**: `shipment.completed`, `shipment.pod_uploaded`.
- **10. AiGovernance Capabilities Used**: None.
- **11. BFF Exposing the Service**: `Staff.Bff` (`BillingController`).
- **12. REST APIs Exposed to FE**:
  - `POST /api/v1/billing/invoices/generate`
  - `GET /api/v1/billing/invoices`
  - `GET /api/v1/billing/invoices/{id}`
  - `PATCH /api/v1/billing/invoices/{id}/status`
  - `GET /api/v1/billing/credit/check`
  - `GET /api/v1/billing/escrow/{walletId}`
  - `POST /api/v1/billing/escrow/freeze`
  - `POST /api/v1/billing/escrow/release`
- **13. Required Permissions**: `billing_settlement:read`, `billing_settlement:invoice:create`, `billing_settlement:invoice:update`, `billing_settlement:credit:check`, `billing_settlement:escrow:read`, `billing_settlement:settlement:manage`.
- **14. Resource-Level Authorization**: Financial settlement releases require `billing_settlement:settlement:manage`.
- **15. User Persona Usage**: Finance Officer, Manager.
- **16. Soft-Delete Behavior**: Invoices are immutable financial documents (voided via credit memo / status update `VOIDED`).
- **17. Current Implementation Status**: **PRODUCTION-MVP READY**.
- **18. Test Coverage**: **LOW** (Missing dedicated unit/integration `.spec.ts` test files in repository; runtime verified via NestJS startup and manual test harnesses).
- **19. Known Gaps / Debt**: Integration with Vietnam tax authorities (HDDT e-invoice provider e.g. VNPT/Viettel) is currently mocked via `einvoice.adapter.ts`.

---

### 3.11 Financial & Tax Service
- **1. Responsibility / Bounded Context**: Multimodal shipping cost calculation, customs duty computation, currency exchange rate synchronization, minimum acceptable rate evaluation for negotiations.
- **2. Technology / Runtime**: `NestJS 10`, TypeScript, Node.js 20, Prisma ORM, `@grpc/grpc-js`, Redis.
- **3. Database Ownership**: PostgreSQL (`financial_service` via Prisma). Tables: `tax_rates`, `tariff_codes`, `exchange_rates`, `cost_matrix_entries`. Redis for rate caching (`rate-cache.service.ts`).
- **4. External Dependencies**: PostgreSQL, Redis, Central Bank Exchange API (Cron).
- **5. gRPC Services / RPCs**:
  - `financial.FinancialService`: `EstimateCost`, `GetCustomsDuty`, `GetMinAcceptableRate`
- **6. Commands**: `CalculateCostCommand`, `SyncExchangeRatesCommand`.
- **7. Queries**: `GetTariffQuery`, `GetExchangeRateQuery`.
- **8. RabbitMQ Published Events**: `financial.rate.updated`.
- **9. RabbitMQ Consumed Events**: None.
- **10. AiGovernance Capabilities Used**: None.
- **11. BFF Exposing the Service**: `Staff.Bff` (`FinancialController`).
- **12. REST APIs Exposed to FE**:
  - `POST /api/v1/financial/estimate-cost`
  - `POST /api/v1/financial/customs-duty`
- **13. Required Permissions**: `financial_tax:read`, `financial_tax:calculate`.
- **14. Resource-Level Authorization**: Scoped by tenant rate configurations.
- **15. User Persona Usage**: Operator, Finance Officer.
- **16. Soft-Delete Behavior**: Historical rate tables with effective date ranges.
- **17. Current Implementation Status**: **PRODUCTION-MVP READY**.
- **18. Test Coverage**: **LOW** (Missing automated Jest spec suite in `src/`).
- **19. Known Gaps / Debt**: Dynamic fuel surcharge API is simulated with static periodic table lookups.

---

### 3.12 Negotiation Agent Service
- **1. Responsibility / Bounded Context**: AI-assisted freight rate negotiation, counter-offer evaluation, concession curve strategy calculation, drafting counter-proposals, and automated bid suggestion.
- **2. Technology / Runtime**: `NestJS 10`, TypeScript, Node.js 20, Prisma ORM, `@grpc/grpc-js`.
- **3. Database Ownership**: PostgreSQL (`negotiation_service` via Prisma). Tables: `negotiation_sessions`, `negotiation_rounds`, `strategy_rules`.
- **4. External Dependencies**: AiGovernance gRPC (for LLM proposal generation), FinancialService (gRPC for floor price validation), PostgreSQL, RabbitMQ.
- **5. gRPC Services / RPCs**:
  - `negotiation.NegotiationService`: `SubmitOffer`, `GetSessionHistory`, `GetDraftSuggestion`
- **6. Commands**: `SubmitOfferCommand`, `AcceptOfferCommand`, `GenerateCounterProposalCommand`.
- **7. Queries**: `GetSessionHistoryQuery`, `GetDraftSuggestionQuery`.
- **8. RabbitMQ Published Events**: `negotiation.offer.submitted`, `negotiation.session.concluded`.
- **9. RabbitMQ Consumed Events**: None.
- **10. AiGovernance Capabilities Used**: `negotiation.strategy_offer`, `negotiation.counter_proposal`.
- **11. BFF Exposing the Service**: `Staff.Bff` (`NegotiationsController`).
- **12. REST APIs Exposed to FE**:
  - `POST /api/v1/negotiations/{id}/mail-draft`
- **13. Required Permissions**: `mail:draft:create`, `financial_tax:calculate`.
- **14. Resource-Level Authorization**: Multi-tenant session verification.
- **15. User Persona Usage**: Staff / Freight Broker.
- **16. Soft-Delete Behavior**: Sessions closed with final status (`ACCEPTED`, `REJECTED`, `EXPIRED`).
- **17. Current Implementation Status**: **PRODUCTION-MVP READY**.
- **18. Test Coverage**: **MEDIUM** (`negotiation.service.spec.ts`, `ai-governance-negotiation.client.spec.ts` present).
- **19. Known Gaps / Debt**: Direct live multi-party bidding socket rooms are bridged through `RealtimeHub`.

---

### 3.13 Customer Assistant Service
- **1. Responsibility / Bounded Context**: Conversational AI customer assistant, multi-intent routing (Tracking, Billing, Regulatory, General QA), conversation memory management, RAG context assembly from compliance service, summary generation.
- **2. Technology / Runtime**: `NestJS 10`, TypeScript, Node.js 20, Raw PostgreSQL / Knex migrations, Redis.
- **3. Database Ownership**: PostgreSQL (`customer_assistant` database). Tables: `conversations`, `messages`. Redis for session caching (`redis-conversation-cache.service.ts`).
- **4. External Dependencies**: AiGovernance gRPC, RegulatoryCompliance gRPC, ShipmentWorkflow / Billing gRPC clients, PostgreSQL, Redis.
- **5. gRPC Services / RPCs**: None (Exposes HTTP REST directly to BFF and WebSocket events).
- **6. Commands**: `CreateConversationCommand`, `SendMessageCommand`, `SummarizeConversationCommand`.
- **7. Queries**: `GetConversationHistoryQuery`, `ListConversationsQuery`.
- **8. RabbitMQ Published Events**: `assistant.conversation.completed`.
- **9. RabbitMQ Consumed Events**: None.
- **10. AiGovernance Capabilities Used**: `assistant.intent_routing`, `assistant.chat_completion`, `assistant.conversation_summary`.
- **11. BFF Exposing the Service**: `Staff.Bff` (`AssistantController`, `ChatController`).
- **12. REST APIs Exposed to FE**:
  - `POST /api/v1/assistant/conversations`
  - `POST /api/v1/assistant/conversations/{id}/messages`
  - `GET /api/v1/assistant/conversations/{id}`
  - `GET /api/v1/assistant/conversations`
- **13. Required Permissions**: Standard staff access (`shipments:read`, `billing_settlement:read`).
- **14. Resource-Level Authorization**: Corpus access policy (`assistant-corpus-access.policy.ts`) prevents unauthorized customer data leakage between tenants.
- **15. User Persona Usage**: Customer, Customer Support Staff.
- **16. Soft-Delete Behavior**: Conversations archived with timestamp.
- **17. Current Implementation Status**: **COMPLETE**.
- **18. Test Coverage**: **HIGH** (`intent-router.spec.ts`, `orchestrator.spec.ts`, `assistant-corpus-access.policy.spec.ts`, `conversational-prompt-builder.spec.ts`, `conversation-summary.service.spec.ts`).
- **19. Known Gaps / Debt**: Voice-to-text pipeline not yet integrated (text and structured payload only).

---

### 3.14 RealtimeHub Service
- **1. Responsibility / Bounded Context**: Real-time event broadcasting to frontend clients via WebSockets (`Socket.IO`), Redis adapter for horizontal scaling, offline event buffering, tenant/room multiplexing.
- **2. Technology / Runtime**: `NestJS 10`, TypeScript, Node.js 20, Socket.IO, Redis IORedis.
- **3. Database Ownership**: None (Stateless WebSocket server with Redis ephemeral buffer).
- **4. External Dependencies**: Redis (Socket.IO adapter & offline buffer), RabbitMQ (amqplib consumer).
- **5. gRPC Services / RPCs**: None.
- **6. Commands**: `BroadcastToTenant`, `BroadcastToShipment`, `BroadcastToUser`.
- **7. Queries**: `GetOfflineEvents`.
- **8. RabbitMQ Published Events**: None.
- **9. RabbitMQ Consumed Events**:
  - `billing.#`
  - `negotiation.#`
  - `shipment.#`
  - `financial.#`
- **10. AiGovernance Capabilities Used**: None.
- **11. BFF Exposing the Service**: Direct WebSocket connection (`/socket.io`) authenticated via JWT Bearer token query parameter (`ws-jwt.guard.ts`).
- **12. REST APIs Exposed to FE**:
  - `GET /health`
- **13. Required Permissions**: Valid JWT token with active `tenantId` and `userId`.
- **14. Resource-Level Authorization**: Client sockets join rooms matching `tenant:{tenantId}`, `user:{userId}`, and `shipment:{shipmentId}` only if authorized.
- **15. User Persona Usage**: All connected web client sessions.
- **16. Soft-Delete Behavior**: N/A (Ephemeral message broker).
- **17. Current Implementation Status**: **COMPLETE**.
- **18. Test Coverage**: **MEDIUM** (Verified in runtime smoke tests).
- **19. Known Gaps / Debt**: Webhook egress for third-party client integrations is not yet implemented.

---

### 3.15 DevOps Agent Service
- **1. Responsibility / Bounded Context**: Autonomous Kubernetes & cloud infrastructure diagnostics, incident RCA (Root Cause Analysis) generation, automated remediation approval pipeline, runbook execution, log/metric redaction.
- **2. Technology / Runtime**: `Java 21`, Spring Boot 3.3.x, `grpc-spring-boot-starter`, Flyway, Kubernetes Client.
- **3. Database Ownership**: PostgreSQL (`devops_agent` schema). Tables: `incidents`, `incident_timeline`, `rules`, `pending_rules`, `self_config`, `outbox_messages`.
- **4. External Dependencies**: Kubernetes API, Prometheus / AlertManager, DevOps RAG gRPC, AiGovernance gRPC, PostgreSQL, RabbitMQ.
- **5. gRPC Services / RPCs**:
  - `devops_agent.DevOpsIngestionService`: `IngestAlert`
  - `devops_agent.DevOpsIncidentService`: `ListIncidents`, `GetIncident`, `ApproveIncident`, `RejectIncident`
  - `devops_agent.DevOpsRuleService`: `ListExistingRules`, `CreateRule`, `UpdateRule`, `DeleteRule`, `ListPendingRules`, `ApprovePendingRule`, `RejectPendingRule`
  - `devops_agent.DevOpsConfigService`: `GetSelfConfig`, `UpdateSelfConfig`
  - `devops_rag.DevOpsRagService`: `QueryKnowledge`, `IngestKnowledge`
- **6. Commands**: `IngestAlertCommand`, `ApproveIncidentCommand`, `RejectIncidentCommand`, `CreateRuleCommand`, `ApproveRuleCommand`, `ExecuteRemediationActionCommand`.
- **7. Queries**: `GetIncidentQuery`, `ListIncidentsQuery`, `ListRulesQuery`, `GetSelfConfigQuery`.
- **8. RabbitMQ Published Events**: `devops.incident.detected`, `devops.incident.remediated`, `devops.rule.promoted`.
- **9. RabbitMQ Consumed Events**: None.
- **10. AiGovernance Capabilities Used**: `devops.incident_rca`, `devops.rule_generation`.
- **11. BFF Exposing the Service**: `System.Bff` (planned), internal ops tooling.
- **12. REST APIs Exposed to FE**: Internal diagnostics endpoints.
- **13. Required Permissions**: `SYSTEM_ADMIN` role gate.
- **14. Resource-Level Authorization**: Platform-level only.
- **15. User Persona Usage**: System Administrator, Site Reliability Engineer.
- **16. Soft-Delete Behavior**: Incidents archived; rules marked inactive.
- **17. Current Implementation Status**: **COMPLETE**.
- **18. Test Coverage**: **HIGH** (`src/java/devops-agent/src/test` covers RCA orchestration, dedup, state machine, outbox poller, security redaction, gRPC handlers).
- **19. Known Gaps / Debt**: Direct kubectl write operations in production require secondary approval gate.

---

### 3.16 BFF Architecture (`BuildingBlocks.BFF`, `Staff.Bff`, `Admin.Bff`, `System.Bff`, `API.Gateway`)
- **1. Responsibility / Bounded Context**: Aggregation, translation from REST/JSON to downstream gRPC, authentication cookie & JWT validation, permission enforcement (`[RequirePermission]`), response caching, distributed rate limiting, and YARP routing.
- **2. Technology / Runtime**: `.NET 10`, C#, ASP.NET Core, YARP, Microsoft IdentityModel, StackExchange.Redis, OpenTelemetry, Serilog.
- **3. Database Ownership**: None (Stateless BFFs with Redis for distributed cache & rate limit token buckets).
- **4. External Dependencies**: Downstream gRPC services, AWS Cognito / Azure AD, Redis, YARP Gateway.
- **5. BFF Variants**:
  - **API.Gateway**: YARP reverse proxy fronting `Staff.Bff`, `Admin.Bff`, and `System.Bff`.
  - **Staff.Bff**: Serves staff operators, logistics coordinators, customs officers, and finance staff.
  - **Admin.Bff**: Serves tenant administrators for user management, custom roles, mailbox configs, and AI budget limits.
  - **System.Bff**: Serves platform super-administrators for tenant onboarding, global ingestion, and dead-letter queues.
  - **BuildingBlocks.BFF**: Common library containing `RequirePermissionAttribute`, auth cookies, JWT handlers, gRPC client wrappers, deadline policies, rate limiting, and exception filters.
- **6. Security Model**: Double-layer authorization (BFF layer validates permissions and constructs gRPC metadata headers; downstream gRPC services validate presence of `x-tenant-id` and security context).
- **7. Current Implementation Status**: **COMPLETE**.
- **8. Test Coverage**: Integrated in `MailService.Tests` and runtime gateway harnesses.
- **9. Known Gaps / Debt**: GraphQL aggregation layer considered for future frontend data mesh.

---

## 4. Main End-to-End Business Flows

### Flow 1: Shipment Creation to POD Invoicing & Settlement
1. **Frontend** submits shipment via `Staff.Bff` (`POST /api/v1/shipments`).
2. `Staff.Bff` invokes `ShipmentWorkflowService.CreateShipment` over gRPC.
3. `ShipmentWorkflow` persists `Shipment` (status `DRAFT`) and writes `ShipmentCreatedEvent` to PostgreSQL Outbox.
4. Outbox Publisher broadcasts `ShipmentCreatedEvent` to RabbitMQ.
5. User attaches Bill of Lading (`POST /api/v1/shipments/{id}/documents`).
6. `ShipmentWorkflow` publishes `DocumentAttachedEvent`.
7. `DocumentOcr` consumes event, fetches document, calls `AiGovernanceService.Generate` (governed capability `ocr.bill_of_lading`), extracts entities, and publishes `DocumentOcrCompletedEvent`.
8. Operator submits shipment (`POST /api/v1/shipments/{id}/submit`).
9. `RegulatoryCompliance` consumes `ShipmentSubmittedEvent`, evaluates trade compliance against `pgvector` regulations, and publishes `ComplianceEvaluationCompletedEvent`.
10. Route is assigned, driver updates status to `DELIVERED`, and uploads Proof of Delivery (`POD`).
11. `ShipmentWorkflow` publishes `ShipmentCompletedEvent`.
12. `billing-service` consumes `ShipmentCompletedEvent`, verifies idempotency, checks customer credit via `billing.CheckCustomerCredit`, queries `financial-service` for duty/tax calculations, generates `Invoice` in PostgreSQL, and publishes `billing.invoice.generated`.
13. `realtime-hub-service` consumes `billing.invoice.generated` and pushes live notification to the customer's WebSocket room.

### Flow 2: Inbound Email Triage & Thread Claiming
1. Inbound email arrives at **Stalwart Mail Server**.
2. Stalwart webhook / pipe invokes `MailService`.
3. `MailService` executes ClamAV malware scan and SpamAssassin scoring.
4. `MailService` invokes `AiGovernanceService.Generate` for phishing detection and risk scoring (`mail.phishing_detection`, `mail.risk_scoring`).
5. If safe, `MailService` creates `ThreadRecord` and publishes `inbound_email_received_event`.
6. Staff operator views shared queue (`GET /api/v1/mail/threads`).
7. Operator claims thread (`POST /api/v1/mail/threads/{id}/claim`).
8. `MailService` acquires Redis claim lock, updates `AssignedUserId`, records `ThreadAssignmentHistory`, and returns updated thread DTO.
9. Staff drafts counter-proposal via `NegotiationsController` which queries `negotiation-agent-service` and creates draft in `MailService`.
10. Staff sends email (`POST /api/v1/mail/messages/outbound`).
11. `MailService` submits email to Stalwart SMTP queue and publishes `outbound_email_sent_event`.
