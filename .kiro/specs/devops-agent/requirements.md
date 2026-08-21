# Requirements Document — DevOps-Agent Service

## Introduction

DevOps-Agent là một autonomous .NET microservice trong hệ thống logistics multi-tenant Aurora chịu trách nhiệm:
- **Ingest & Dedup**: nhận alert/event từ Loki, Azure Monitor và các nguồn webhook, chống xử lý trùng lặp.
- **Classify & Route**: phân loại severity (Low / Medium / High / Critical) và routing tới luồng xử lý tương ứng.
- **Auto-Remediation**: tự động sửa lỗi Low severity theo rule engine mà không cần LLM.
- **RCA & Auto-Debug**: phân tích nguyên nhân gốc rễ (Root Cause Analysis) với LLM + RAG cho Medium/High, tạo PR và đề xuất fix.
- **Approval Workflow**: quản lý luồng duyệt 1 hoặc 2 bước tùy severity, với timeout và escalation.
- **Audit**: mọi hành động tự động được publish event tới AuditLog Service qua RabbitMQ.
- **Self-Monitoring**: Agent tự giám sát health của chính mình và xử lý lại event qua DLQ khi hồi phục.

Stack: .NET, gRPC, RabbitMQ, AKS, ACR, Azure Monitor, Azure Key Vault, Redis, PostgreSQL (DB per service), Loki, Grafana, Prometheus, ArgoCD, Terraform, BFF (Yarp), Cloudflare (R2, DNS), Auth qua Cognito.

---

## Glossary

- **DevOps_Agent**: Service tự động hóa DevOps, đối tượng chính của tài liệu này.
- **Incident**: Một sự cố hạ tầng/ứng dụng được xác định bởi một `correlation_id` duy nhất, có thể được tổng hợp từ nhiều alert.
- **Event**: Một alert đơn lẻ đến từ nguồn bên ngoài (Azure Monitor webhook, Loki alertmanager, v.v.) trước khi qua dedup.
- **Dedup_Key**: Hash định danh duy nhất cho một event, được tính từ `source + error_signature + time_window`.
- **Rule_Engine**: Thành phần 2 lớp xử lý auto-remediation: lớp Existing_Rules (đã duyệt, DB + cache) và lớp Unknown_Issues (RCA qua RAG, chờ duyệt).
- **Existing_Rule**: Rule đã được Owner/Admin duyệt, lưu trong DB, cache in-memory, chỉ Admin/Owner sửa được.
- **Pending_Rule**: Rule mới học từ RCA, chờ Owner duyệt để thành Existing_Rule chính thức.
- **Debug_Session**: Phiên làm việc của DevOps_Agent khi phân tích và xử lý một Incident, được gắn với một `correlation_id`.
- **RCA**: Root Cause Analysis — phân tích nguyên nhân gốc rễ bằng LLM + RAG.
- **Self_Config**: Cấu hình model AI riêng của DevOps_Agent (không phải tenant-based), do Owner/System_Admin thiết lập.
- **Tenant_AI_Config**: Bảng cấu hình AI theo tenant (cho OCR, RAG, Customer Assistant), DevOps_Agent chỉ đọc để thống kê.
- **Approval_Record**: Bản ghi một yêu cầu duyệt (Rule Approval hoặc PR Approval) với trạng thái, deadline, và approver.
- **Audit_Event**: Payload event publish lên AuditLog Service qua RabbitMQ, tuân theo schema tối thiểu đã chốt.
- **RAG_Service**: Service hiện có cung cấp khả năng Retrieval-Augmented Generation; DevOps_Agent giao tiếp qua API/gRPC.
- **Knowledge_Entry**: Một mục tri thức trong RAG Service, được tạo từ kết quả RCA đã qua redaction.
- **PII_Redactor**: Thành phần 2 lớp (Azure AI Language PII Detection + custom regex/whitelist) xóa thông tin nhạy cảm trước khi gửi LLM hoặc lưu Knowledge Base.
- **DLQ**: Dead Letter Queue trên RabbitMQ, giữ event khi DevOps_Agent down để xử lý lại khi hồi phục.
- **Flapping**: Hiện tượng lỗi Low severity lặp lại liên tục trong một khoảng thời gian ngắn.
- **Severity_Matrix**: Ma trận ánh xạ severity (Low/Medium/High/Critical) sang hành động tự động tương ứng.
- **Cloudflare_R2**: Object storage của Cloudflare, dùng lưu artifact (logs, PR diff, debug output).
- **AuditLog_Service**: Service ghi nhận audit trail chính thức của toàn hệ thống.
- **Owner**: Vai trò SystemAdmin/TenantAdmin cấp cao có quyền duyệt rule và thay đổi Self_Config.
- **System_Admin**: Vai trò quản trị hệ thống (mapped từ `SYSTEM_ADMIN` trong common.proto).
- **Tenant_Admin**: Vai trò quản trị tenant (mapped từ `TENANT_ADMIN` trong common.proto).
- **gRPC_Handler**: Command/Query handler nhận request từ BFF qua proto, thay thế REST Controller trong kiến trúc DevOps-Agent.

> **Note:** DevOps-Agent là internal system service — chỉ SYSTEM_ADMIN mới có quyền truy cập Admin API, quản lý Existing_Rules, Self_Config, và duyệt Rule/PR Approval Workflow. Tenant_Admin không có quyền thao tác trực tiếp với DevOps-Agent.

---

## Requirements

### Requirement 1: Event Ingestion và Deduplication

**User Story:** As a System_Admin, I want DevOps_Agent to ingest alerts from multiple sources without processing the same incident twice, so that duplicate alerts do not trigger redundant remediation actions.

#### Acceptance Criteria

1. WHEN an alert webhook is received from Azure Monitor Action Group, THE DevOps_Agent SHALL compute a Dedup_Key as the SHA-256 hash of `(source, error_signature, time_window_bucket)` where `time_window_bucket` is the 5-minute UTC bucket containing the alert timestamp.
2. WHEN an alertmanager payload is received from Loki, THE DevOps_Agent SHALL compute a Dedup_Key using the same algorithm as criterion 1.
3. WHEN a new Event arrives, THE DevOps_Agent SHALL check Redis for an existing Dedup_Key with TTL of 30 minutes before processing.
4. IF a Dedup_Key already exists in Redis, THEN THE DevOps_Agent SHALL discard the duplicate Event and increment a dedup counter metric without creating a new Debug_Session.
5. IF a Dedup_Key does not exist in Redis, THEN THE DevOps_Agent SHALL store the Dedup_Key in Redis with TTL of 30 minutes and assign or merge the Event into an Incident with a `correlation_id`.
6. WHEN multiple Events share the same Incident signature within a 5-minute window, THE DevOps_Agent SHALL group them under a single `correlation_id` instead of creating separate Debug_Sessions.
7. IF DevOps_Agent is unavailable when an Event arrives, THEN THE DevOps_Agent SHALL process Events from the RabbitMQ DLQ after recovery, applying dedup checks before processing each recovered Event.
8. WHEN publishing an Audit_Event to AuditLog_Service, THE DevOps_Agent SHALL apply dedup at the publish layer using the same `correlation_id` to prevent duplicate audit records for the same action.

---

### Requirement 2: Severity Classification và Routing

**User Story:** As a System_Admin, I want DevOps_Agent to automatically classify each Incident by severity and route it to the correct handling pipeline, so that critical issues receive human oversight and low-risk issues are resolved automatically.

#### Acceptance Criteria

1. WHEN an Incident is created, THE DevOps_Agent SHALL classify it into one of four severity levels: `Low`, `Medium`, `High`, or `Critical` based on the alert metadata, error type, and affected service tier.
2. WHEN an Incident is classified as `Low`, THE DevOps_Agent SHALL route it to the Rule_Engine for auto-remediation without invoking an LLM; Low severity incidents SHALL NOT bypass the Rule_Engine under any circumstances.
3. WHEN an Incident is classified as `Medium`, THE DevOps_Agent SHALL route it to the RCA pipeline using the model specified in Self_Config, and require 1 approval step before applying any fix to production.
4. WHEN an Incident is classified as `High` or `Critical`, THE DevOps_Agent SHALL route it to the RCA pipeline using the strongest available model from Self_Config, generate a recommendation only, and require 2 sequential approval steps before any production change.
5. WHEN a `Low` severity Incident recurs more than 3 times within a 10-minute sliding window for the same Dedup_Key signature, THE DevOps_Agent SHALL escalate the Incident severity to `Medium` and trigger the RCA pipeline (anti-flapping rule).
6. THE DevOps_Agent SHALL persist the severity classification, routing decision, and `correlation_id` in the Debug_Session record before dispatching to any pipeline.
7. IF the severity classification or routing decision cannot be determined, THEN THE DevOps_Agent SHALL block all production pipeline dispatch, mark the Debug_Session with status `ROUTING_FAILED`, and require manual intervention to restore proper routing.
8. WHEN an Incident is created, THE DevOps_Agent SHALL compute an ImpactScore (0–100) as a weighted composite of severity (20%), business_criticality (25%), error_rate (15%), affected_user_count (15%), blast_radius (10%), confidence (10%), and estimated_downtime (5%); the RoutingDispatcher SHALL use ImpactScore as the primary routing signal: <30 → IGNORE, 30–59 → NOTIFY, 60–79 → AUTO_HEAL, 80–100 → APPROVAL_REQUIRED.

---

### Requirement 3: Rule Engine — Existing Rules

**User Story:** As a System_Admin, I want DevOps_Agent to apply pre-approved deterministic rules for Low severity incidents, so that common known issues are resolved immediately without LLM cost or latency.

#### Acceptance Criteria

1. THE DevOps_Agent SHALL maintain a set of Existing_Rules loaded from the PostgreSQL database into an in-memory cache at startup and after any rule update event.
2. WHEN an Existing_Rule is created, updated, or deleted by an Owner or System_Admin, THE DevOps_Agent SHALL invalidate the in-memory rule cache and reload rules from the database within 5 seconds of receiving the cache-invalidation message; IF the rule cache reload takes longer than 5 seconds due to database latency, THE DevOps_Agent SHALL continue serving the existing stale cache until the fresh rules are fully loaded, then atomically replace the cache.
3. WHEN a `Low` severity Incident matches an Existing_Rule, THE DevOps_Agent SHALL execute the rule's remediation action (restart pod, adjust config, or rollback deployment) without human approval; THE DevOps_Agent SHALL limit each remediation action strictly to its declared target resource and SHALL NOT affect other deployments, services, or configurations.
4. WHEN an Existing_Rule executes any remediation action including rollback, pod restart, and config adjustment, THE DevOps_Agent SHALL apply that action only to the target deployment or configuration change, not any git branch or PR.
5. IF no Existing_Rule matches a `Low` severity Incident, THEN THE DevOps_Agent SHALL escalate the Incident to the Unknown_Issues pipeline and mark the Debug_Session with status `UNMATCHED_RULE`.
6. WHEN an Existing_Rule action completes, THE DevOps_Agent SHALL publish an Audit_Event to AuditLog_Service within 10 seconds containing at minimum: `actor`, `action_type`, `target`, `timestamp`, `severity`, `result`, `correlation_id`.
7. THE DevOps_Agent SHALL enforce that only users with `SYSTEM_ADMIN` system role are permitted to create, update, or delete Existing_Rules via gRPC command handlers.
8. WHEN an Existing_Rule is executed, THE DevOps_Agent SHALL read the target service's BusinessCriticality value from the service_criticality_registry and factor it into the ImpactScore computation; services with BusinessCriticality = Critical SHALL always receive ImpactScore >= 60 regardless of other metrics.

---

### Requirement 4: Rule Engine — Unknown Issues và Knowledge Learning

**User Story:** As a System_Admin, I want DevOps_Agent to learn from previously unseen issues and propose new rules for Owner approval, so that the system's automated coverage grows over time without manual rule authoring.

#### Acceptance Criteria

1. WHEN a `Low` severity Incident has no matching Existing_Rule, THE DevOps_Agent SHALL invoke the RAG_Service to retrieve relevant Knowledge_Entries and pass them together with the redacted Incident context to the LLM for RCA.
2. WHEN RCA produces a proposed remediation, THE DevOps_Agent SHALL create a Pending_Rule record containing: `error_signature`, `root_cause_summary`, `proposed_action`, `confidence_score`, `source_correlation_id`, `created_at`.
3. THE DevOps_Agent SHALL submit the Pending_Rule to the Rule Approval workflow and notify the Owner via Email and Dashboard.
4. WHEN an Owner approves a Pending_Rule, THE DevOps_Agent SHALL promote it to an Existing_Rule, persist it in the database, trigger cache invalidation, and publish an Audit_Event.
5. WHEN an Owner rejects a Pending_Rule, THE DevOps_Agent SHALL mark it as `REJECTED` with the rejection reason and publish an Audit_Event; the Pending_Rule SHALL NOT become an Existing_Rule.
6. WHEN a new Knowledge_Entry is created from an RCA result, THE DevOps_Agent SHALL apply PII_Redactor to the full RCA output before storing the Knowledge_Entry in RAG_Service; IF PII_Redactor fails to process the RCA output completely, THEN THE DevOps_Agent SHALL block creation of the Knowledge_Entry and SHALL log the failure without storing any unredacted content.
7. THE DevOps_Agent SHALL call the RAG_Service embedding API to generate and store the vector embedding for each new Knowledge_Entry; DevOps_Agent SHALL NOT access the pgvector database directly.
8. IF PII_Redactor fails to process the RCA output, THEN THE DevOps_Agent SHALL NOT create the Knowledge_Entry and SHALL log the failure.

---

### Requirement 5: RCA và Auto-Debug Pipeline (Medium/High/Critical)

**User Story:** As a System_Admin, I want DevOps_Agent to automatically analyze the root cause of medium and high severity incidents and propose a code fix via PR, so that engineers spend less time on triage and more time on review.

#### Acceptance Criteria

1. WHEN the RCA pipeline is triggered for a `Medium`, `High`, or `Critical` Incident, THE DevOps_Agent SHALL collect log context from both Loki (raw container logs) and Azure Monitor / Application Insights (exception traces, KQL query results) within 60 seconds.
2. WHEN collecting log context, THE DevOps_Agent SHALL apply PII_Redactor (Azure AI Language PII Detection followed by custom regex/whitelist) to all log text, exception messages, and stack traces before forwarding to the LLM.
3. WHEN PII_Redactor processes a log batch, THE DevOps_Agent SHALL redact any field matching the internal whitelist of sensitive field names in addition to PII categories detected by Azure AI Language.
4. THE DevOps_Agent SHALL send only plain-text redacted content to the LLM; binary blobs, credentials, and connection strings SHALL be excluded from LLM context.
5. WHEN the LLM produces an RCA summary and a proposed code fix, THE DevOps_Agent SHALL create a Git branch, commit the proposed fix, and open a Pull Request targeting the main branch for `Medium` severity Incidents.
6. WHEN an Incident is `High` or `Critical`, THE DevOps_Agent SHALL generate the RCA summary and proposed fix as a recommendation artifact containing an explicit RCA summary and proposed code fix components, and SHALL NOT automatically open a Pull Request without prior Owner approval.
7. THE DevOps_Agent SHALL store all debug artifacts (redacted log snippets, LLM prompt/response, PR diff) in Cloudflare_R2 under the key structure `devops-agent/incidents/{correlation_id}/artifacts/{artifact_type}/{timestamp}_{filename}`.
8. WHEN storing artifacts in Cloudflare_R2, THE DevOps_Agent SHALL use Cloudflare Tunnel from AKS to reach the R2 API endpoint and SHALL NOT expose R2 credentials outside the AKS cluster.
9. THE DevOps_Agent SHALL use an adapter pattern for LLM provider integration such that adding a new AI model provider requires implementing a defined interface without modifying the core RCA pipeline logic.

---

### Requirement 6: Approval Workflow

**User Story:** As a System_Admin, I want DevOps_Agent to enforce a configurable approval process before applying any production change, so that no automated action bypasses human review for medium and high severity incidents.

#### Acceptance Criteria

1. IF the incident severity is confirmed as MEDIUM and the proposed PR is ready, THEN THE DevOps_Agent SHALL create an Approval_Record with status `PENDING_APPROVAL_1`, notify the designated approver via Email (SES + Stalwart) and Dashboard, and start a configurable timeout timer (default 30 minutes, range 30–60 minutes).
2. WHEN a fix is ready that was prepared for a High or Critical Incident, THE DevOps_Agent SHALL create an Approval_Record with status `PENDING_APPROVAL_1`, require a first approval to transition to `PENDING_APPROVAL_2`, and require a second approval to transition to `APPROVED` before any production change is applied; THE DevOps_Agent SHALL apply the two-step approval workflow regardless of any subsequent reclassification of the Incident.
3. WHEN an approval timeout expires without action, THE DevOps_Agent SHALL transition the Approval_Record to status `ESCALATED`, send an escalation Email notification to the Owner, and record the escalation in the Audit_Event.
4. THE DevOps_Agent SHALL NOT merge a PR or deploy any change at any time, not only after timeout expiry; all merges and deploys require explicit human approval.
5. WHEN a Telegram notification is sent, THE DevOps_Agent SHALL use Telegram exclusively for urgent or time-critical approval requests and SHALL NOT send routine status updates via Telegram.
6. WHEN an approver submits an approval or rejection via the Dashboard, THE DevOps_Agent SHALL record the approver's identity (`actor`), decision (`APPROVED` or `REJECTED`), timestamp, and optional comment in the Approval_Record, then publish an Audit_Event.
7. THE DevOps_Agent SHALL enforce RBAC such that only users with `SYSTEM_ADMIN` system role may submit approvals for Rule Approval and PR Approval workflows via gRPC command handlers.
8. THE Rule_Approval_Workflow and PR_Approval_Workflow SHALL be implemented as two separate state machines sharing a common notification channel but maintaining independent Approval_Record tables.

---

### Requirement 7: Notification

**User Story:** As a System_Admin, I want to receive timely, channel-appropriate notifications about incidents and approval requests, so that I can respond quickly without being overwhelmed by noise.

#### Acceptance Criteria

1. WHEN an Incident is classified and a Debug_Session is opened, THE DevOps_Agent SHALL publish a notification to the Dashboard within 30 seconds of Incident creation.
2. WHEN a new Approval_Record is created, THE DevOps_Agent SHALL send an Email notification to the designated approver via SES + Stalwart within 60 seconds.
3. WHEN an Approval_Record transitions to `ESCALATED`, THE DevOps_Agent SHALL send both an Email and a Telegram message to the Owner within 30 seconds of the escalation event.
4. WHEN sending a Telegram notification, THE DevOps_Agent SHALL include: Incident `correlation_id`, severity level, summary of the proposed action, and a direct link to the Approval_Record in the Dashboard; IF any required field (`correlation_id`, `severity`, `action_summary`, `dashboard_link`) is missing, THEN THE DevOps_Agent SHALL NOT send the Telegram notification and SHALL log the omission as a warning.
5. THE DevOps_Agent SHALL NOT send Telegram notifications for routine Low severity auto-remediation events.
6. WHEN DevOps_Agent self-monitoring detects that the Agent service health is degraded, THE DevOps_Agent SHALL send a Telegram alert to System_Admin within 60 seconds.
7. WHEN an Approval_Record is escalated for an Incident that originated as `Low` severity, THE DevOps_Agent SHALL send escalation notification via Email only and SHALL NOT send a Telegram message.

---

### Requirement 8: PII Redaction và Data Security

**User Story:** As a System_Admin, I want all sensitive data to be redacted before leaving the internal network, so that no PII or secret credentials are exposed to external AI providers or stored in the Knowledge Base.

#### Acceptance Criteria

1. WHEN DevOps_Agent prepares context for an LLM call, THE PII_Redactor SHALL process all log text, exception messages, and stack traces through Azure AI Language PII Detection first, then apply custom regex patterns against an internal whitelist of sensitive field names.
2. WHEN PII_Redactor detects a PII entity, THE PII_Redactor SHALL replace the entity value with a category placeholder (e.g., `[PHONE_NUMBER]`, `[EMAIL_ADDRESS]`, `[PERSON_NAME]`) and SHALL preserve the surrounding log structure.
3. WHEN a Knowledge_Entry is created from an RCA result, THE DevOps_Agent SHALL apply PII_Redactor to the full RCA output before the Knowledge_Entry is submitted to RAG_Service for embedding and storage.
4. THE DevOps_Agent SHALL retrieve all secrets (Azure OpenAI API key, Cloudflare R2 credentials, RabbitMQ connection strings) exclusively from Azure Key Vault via Managed Identity / Workload Identity on AKS and SHALL NOT store secrets in environment variables or container images.
5. THE DevOps_Agent SHALL transmit data to Cloudflare_R2 via Cloudflare Tunnel from within AKS; plaintext credential values SHALL NOT appear in any log, metric, or trace output.
6. WHEN PII_Redactor fails to connect to Azure AI Language service, THE DevOps_Agent SHALL fall back to custom regex/whitelist-only redaction and SHALL log the fallback event as a warning without blocking the RCA pipeline.

---

### Requirement 9: Self-Configuration Management (DevOps-Agent Self Config)

**User Story:** As a System_Admin, I want to configure the AI model used by DevOps-Agent for RCA without impacting tenant AI configurations, so that I can independently upgrade or change the Agent's reasoning model.

#### Acceptance Criteria

1. THE DevOps_Agent SHALL maintain a Self_Config record containing: `model_provider`, `model_name`, `api_endpoint`, `max_tokens_per_request`, `alert_threshold_usd_per_day`, `updated_by`, `updated_at`; only users with `SYSTEM_ADMIN` role SHALL be permitted to read or write Self_Config via gRPC.
2. WHEN a System_Admin updates Self_Config via gRPC, THE DevOps_Agent SHALL apply the new configuration to all subsequent LLM calls within 60 seconds without requiring a service restart; IF a user without `SYSTEM_ADMIN` role attempts to update Self_Config, THE DevOps_Agent SHALL reject the request with an authorization error.
3. THE DevOps_Agent SHALL NOT automatically block or interrupt an in-progress RCA or Debug_Session when ANY individual API key's daily cost alert threshold is exceeded; THE DevOps_Agent SHALL rotate to the next available key in the llm_api_key_pool automatically; WHEN tokens_used_today for a key reaches the configured alert threshold percentage (default 80%), THE DevOps_Agent SHALL send an Email and Telegram alert to System_Admin without blocking the current session; WHEN ALL keys in the pool are exhausted or in cooldown, THE DevOps_Agent SHALL send a CRITICAL alert and pause new Debug_Sessions.
4. WHEN Self_Config is updated, THE DevOps_Agent SHALL publish an Audit_Event to AuditLog_Service containing: `actor`, `action_type: SELF_CONFIG_UPDATED`, `old_model_name`, `new_model_name`, `timestamp`, `correlation_id`.
5. THE DevOps_Agent SHALL read Tenant_AI_Config data exclusively for dashboard statistics and reporting; THE DevOps_Agent SHALL NOT use Tenant_AI_Config to select the LLM model for its own RCA or debug operations.
6. THE DevOps_Agent SHALL support adding a new LLM provider by implementing the `ILlmAdapter` interface without modifying the RCA core pipeline, conforming to the adapter pattern.
7. WHEN an LLM API call fails due to rate limiting or API errors (not cost threshold), THE DevOps_Agent MAY pause the Debug_Session and retry according to the configured retry policy.
8. THE DevOps_Agent SHALL maintain a pool of LLM API keys per provider in `llm_api_key_pool`; WHEN an LLM call receives HTTP 429 (rate limited), THE DevOps_Agent SHALL automatically rotate to the next available key within the same provider pool without failing the current Debug_Session; IF no key is available (all in cooldown or exhausted), THEN THE DevOps_Agent SHALL throw LlmKeyPoolExhaustedException and send a CRITICAL alert.

---

### Requirement 10: Audit Trail

**User Story:** As a System_Admin, I want every automated action of DevOps-Agent to be permanently recorded in AuditLog Service, so that I have a complete and tamper-evident history of all automated changes for compliance and incident review.

#### Acceptance Criteria

1. THE DevOps_Agent SHALL publish an Audit_Event to AuditLog_Service via RabbitMQ for each of the following action types: `INCIDENT_CREATED`, `RULE_APPLIED`, `ROLLBACK_EXECUTED`, `RCA_STARTED`, `PR_OPENED`, `APPROVAL_REQUESTED`, `APPROVAL_GRANTED`, `APPROVAL_REJECTED`, `ESCALATED`, `RULE_PROMOTED`, `RULE_REJECTED`, `SELF_CONFIG_UPDATED`, `KNOWLEDGE_ENTRY_CREATED`.
2. WHEN publishing an Audit_Event, THE DevOps_Agent SHALL include at minimum: `actor` (service identity or user id), `action_type`, `target` (resource identifier), `timestamp` (UTC ISO 8601), `severity`, `result` (`SUCCESS` or `FAILURE`), `correlation_id`.
3. THE DevOps_Agent SHALL NOT use Azure Service Bus for audit event publishing; AuditLog_Service SHALL be the exclusive consumer of audit events via RabbitMQ.
4. THE DevOps_Agent SHALL record the event in a local dead-letter buffer only when the publish attempt fails, regardless of RabbitMQ reported availability status; WHEN a publish attempt fails, THE DevOps_Agent SHALL retry with exponential backoff (max 3 retries, max delay 30 seconds); THE DevOps_Agent SHALL mark an Audit_Event as `PUBLISHED` only after receiving a delivery confirmation from RabbitMQ, and events in the dead-letter buffer SHALL remain in status `PENDING`.
5. WHEN Audit_Event publishing is retried, THE DevOps_Agent SHALL apply the same Dedup_Key check at the publish layer to prevent duplicate audit records from retry storms.
6. THE DevOps_Agent SHALL NOT modify or delete any Audit_Event after it has been successfully published to AuditLog_Service.

---

### Requirement 11: Self-Monitoring và Resilience

**User Story:** As a System_Admin, I want DevOps-Agent to be independently monitored and able to recover from its own failures, so that the Agent's health does not become a single point of failure for the broader system.

#### Acceptance Criteria

1. THE DevOps_Agent SHALL expose health check endpoints compatible with Azure Monitor and Prometheus scraping, reporting: service status, DLQ depth, active Debug_Session count, and Redis connectivity.
2. WHEN DevOps_Agent is restarted after a failure, THE DevOps_Agent SHALL consume and reprocess Events from the RabbitMQ DLQ in FIFO order, applying full dedup and severity classification before triggering any pipeline.
3. WHILE DevOps_Agent is processing a DLQ batch, THE DevOps_Agent SHALL process at most 10 Events concurrently to avoid overloading downstream services.
4. WHEN Azure Monitor or Prometheus detects that DevOps_Agent response time exceeds 30 seconds for more than 3 consecutive health checks, THE DevOps_Agent monitoring infrastructure SHALL trigger a Telegram alert to System_Admin.
5. THE DevOps_Agent SHALL NOT depend on any other tenant-facing service being available in order to ingest Events and write to the DLQ; Event ingestion and DLQ persistence SHALL function independently at all times regardless of any other system state.

---

### Requirement 12: Artifact Storage (Cloudflare R2)

**User Story:** As a System_Admin, I want all DevOps-Agent debug artifacts to be stored with a predictable structure and retention policy in Cloudflare R2, so that I can audit past incidents and the storage costs remain controlled.

#### Acceptance Criteria

1. WHEN storing a debug artifact in Cloudflare_R2, THE DevOps_Agent SHALL use the key structure: `devops-agent/incidents/{correlation_id}/artifacts/{artifact_type}/{timestamp}_{filename}` where `artifact_type` is one of: `logs`, `rca`, `pr_diff`, `approval`.
2. THE DevOps_Agent SHALL apply a lifecycle retention policy of 90 days to all objects under the `devops-agent/` prefix, after which Cloudflare_R2 SHALL automatically delete the objects.
3. WHEN uploading to Cloudflare_R2, THE DevOps_Agent SHALL route all traffic through the configured Cloudflare Tunnel endpoint from within AKS and SHALL NOT open a direct internet egress path for R2 uploads.
4. THE DevOps_Agent SHALL tag each R2 object with metadata: `correlation_id`, `severity`, `tenant_id` (if applicable), `created_at`, `artifact_type` for search and audit purposes.
5. IF an artifact upload failure is caused by a critical system error (not a transient network issue), THEN THE DevOps_Agent MAY halt the Debug_Session and mark it with status `ARTIFACT_UPLOAD_CRITICAL_FAILURE`; for routine network failures THE DevOps_Agent SHALL retry the upload up to 3 times with exponential backoff and, after exhausting retries, SHALL continue the Debug_Session with a warning flag and log the upload failure as a non-blocking error.

---

### Requirement 13: RAG Service Integration

**User Story:** As a System_Admin, I want DevOps-Agent to leverage the existing RAG Service for knowledge retrieval and storage, so that RCA quality improves over time without building a separate vector store.

#### Acceptance Criteria

1. WHEN DevOps_Agent needs to retrieve relevant knowledge for RCA, THE DevOps_Agent SHALL call the RAG_Service query endpoint via gRPC, passing the redacted `error_signature` and `service_context` as query parameters.
2. WHEN DevOps_Agent needs to store a new Knowledge_Entry, THE DevOps_Agent SHALL call the RAG_Service ingest endpoint via gRPC, passing the redacted RCA text; THE RAG_Service SHALL be responsible for generating and storing the vector embedding.
3. THE DevOps_Agent SHALL NOT access the pgvector database directly; all vector operations SHALL be performed by RAG_Service on behalf of DevOps_Agent; this prohibition applies to all purposes including debugging and monitoring.
4. WHEN the RAG_Service gRPC call exceeds a 10-second timeout, THE DevOps_Agent SHALL proceed with RCA using only the LLM's base knowledge, log the RAG timeout as a warning in the Debug_Session, and continue without retrying synchronously.
5. THE DevOps_Agent SHALL pass a `source_tag: "devops-agent"` field in every RAG_Service ingest call so that DevOps-Agent knowledge entries can be filtered independently from compliance and route-planning entries.

---

### Requirement 14: Tenant AI Model Configuration

**User Story:** As a Tenant_Admin, I want to configure which AI model is used for each AI-powered service (Chatbot, Routing, OCR, etc.) within my tenant, so that I can optimize performance and cost for each use case independently.

#### Acceptance Criteria

1. THE system SHALL allow a Tenant_Admin to configure the AI model for each tenant service independently: `chatbot`, `routing`, `ocr`, `customer_assistant`, where each service can have its own `model_provider` and `model_name`.
2. WHEN a Tenant is on the `Standard` plan, THE system SHALL restrict model selection to Standard-tier models only (e.g., Gemini Flash); Enterprise-tier models (e.g., Azure OpenAI GPT-4o) SHALL NOT be available for selection regardless of Tenant_Admin preference.
3. WHEN a Tenant is on the `Enterprise` plan, THE system SHALL unlock Enterprise-tier model options for Tenant_Admin selection across all tenant services.
4. THE SYSTEM (automated provisioning, not Tenant_Admin) SHALL assign default model configurations when a new Tenant is created, based on the tenant's subscription plan: Standard plan → Gemini Flash for all services; Enterprise plan → Azure OpenAI GPT-4o for all services.
5. WHEN a Tenant_Admin updates the model configuration for a specific service, THE system SHALL apply the new model to all subsequent AI calls for that service within 60 seconds without requiring a service restart.
6. THE Tenant_AI_Config SHALL store per-service configuration with fields: `tenant_id`, `service_name`, `model_provider`, `model_name`, `daily_token_limit`, `tokens_used_today`, `updated_by`, `updated_at`.
7. WHEN a tenant's `tokens_used_today` for a specific service reaches 80% of `daily_token_limit`, THE system SHALL send an early warning alert to the Tenant_Admin; WHEN it exceeds `daily_token_limit`, THE system SHALL block further AI calls for that service and send a second alert; DevOps_Agent SHALL NOT be affected by this token limit.
8. THE DevOps_Agent SHALL read Tenant_AI_Config data for dashboard statistics only and SHALL NOT use it to select models for RCA or any DevOps_Agent internal operations.
9. WHEN a SYSTEM operator grants an Enterprise plan upgrade to a Tenant, THE system SHALL unlock Enterprise-tier model selection for that tenant's Tenant_Admin; the Tenant_Admin CANNOT self-upgrade to Enterprise plan.
