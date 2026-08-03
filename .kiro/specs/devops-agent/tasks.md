# Implementation Plan: DevOps-Agent Service

## Overview

DevOps-Agent là một autonomous Java Spring Boot microservice triển khai trên AKS. Kế hoạch triển khai theo 5 phases:
- **Phase 1 — Foundation**: Project structure, DI, Spring Data JPA, Redis, RabbitMQ wiring, gRPC server
- **Phase 2 — Core Domain**: Entities, enums, interfaces, dedup, severity/impact classifier, rule engine, PII redactor
- **Phase 3 — Pipelines**: RCA pipeline, approval state machines, artifact storage, RAG gRPC client
- **Phase 4 — gRPC Handlers & Integration**: gRPC service handlers, audit outbox worker, notification dispatcher, background jobs
- **Phase 5 — Testing**: Property-based tests (P1–P24), unit tests, integration tests

Stack: Spring Boot 3.x · Spring Data JPA + Hibernate + Flyway · Spring AMQP (RabbitMQ) · grpc-java · grpc-spring-boot-starter · Spring Data Redis (Lettuce) · jqwik · JUnit 5 · Mockito · Quartz · azure-security-keyvault-secrets · AWS SDK for Java v2 (S3-compatible R2) · Micrometer + Prometheus · OpenTelemetry Java

Package root: `com.aurora.devopsagent.*`

---

## Tasks

- [ ] 1. Phase 1 — Project Foundation & Infrastructure Wiring

  - [ ] 1.1 Khởi tạo Spring Boot gRPC project và cấu hình Maven multi-module
    - Tạo Maven multi-module project với các modules: `aurora-java-shared` (dùng chung cho các Java services sau này), `devops-agent-grpc-api` (proto + stubs), `devops-agent-application`, `devops-agent-domain`, `devops-agent-infrastructure`, `devops-agent-tests`
    - Thêm dependencies: `grpc-spring-boot-starter` (yidongnan/grpc-spring-boot-starter hoặc LogNet), `spring-ai-core` (Spring AI framework — tương đương Semantic Kernel trong .NET), `logback-classic`, `logstash-logback-encoder`, `opentelemetry-spring-boot-starter`, `micrometer-registry-prometheus`
    - Cấu hình `application.yml`, `.editorconfig`, `pom.xml` root (Java 21, compiler plugin, protobuf-maven-plugin)
    - Tạo `protos/devops_agent.proto` định nghĩa `DevOpsAgentService` với các RPCs: `IngestAlert` (nhận alert từ BFF), `ListIncidents`, `GetIncident`, `ApproveIncident`, `RejectIncident`, `ListExistingRules`, `CreateRule`, `UpdateRule`, `DeleteRule`, `ListPendingRules`, `ApprovePendingRule`, `RejectPendingRule`, `GetSelfConfig`, `UpdateSelfConfig` — import `common.proto` cho `SystemRole` enum
    - _Requirements: 11.1 (health endpoint), 9.1 (SYSTEM_ADMIN auth via gRPC)_

  - [ ] 1.2 Cấu hình Spring Data JPA + Hibernate + Flyway với DevOpsAgent DbContext
    - Tạo các `@Entity` classes và `JpaRepository<T, UUID>` interfaces trong `com.aurora.devopsagent.infrastructure.persistence`
    - Đăng ký connection string từ Azure Key Vault (`azure-security-keyvault-secrets` + `azure-identity`)
    - Thêm dependencies: `spring-boot-starter-data-jpa`, `postgresql` JDBC driver, `flyway-core`, `azure-security-keyvault-secrets`, `azure-identity`
    - Flyway tự động chạy migration khi Spring Boot khởi động (mặc định)
    - _Requirements: 8.4 (secrets from Key Vault only)_

  - [ ] 1.3 Cấu hình Spring Data Redis (Lettuce) client
    - Thêm dependency `spring-boot-starter-data-redis`
    - Tạo `RedisService` interface và implementation trong `com.aurora.devopsagent.infrastructure`
    - Inject `RedisConnectionFactory` từ DI, connection string từ Key Vault
    - Implement helper methods: `setWithTtl`, `get`, `delete`, `zAdd`, `zRangeByScore`, `zCard`, `zRemRangeByScore` dùng `StringRedisTemplate` + `ZSetOperations`
    - _Requirements: 1.3 (dedup Redis TTL), 2.5 (anti-flapping ZSET)_

  - [ ] 1.4 Cấu hình Spring AMQP + RabbitMQ
    - Thêm dependency `spring-boot-starter-amqp`
    - Cấu hình `RabbitTemplate` với publisher confirms mode cho audit outbox
    - Định nghĩa `@Bean Queue`, `@Bean DirectExchange`, `@Bean Binding` cho exchange `audit.events` và DLQ consumer bindings trong `@Configuration` class
    - Connection string từ Key Vault
    - _Requirements: 10.3 (RabbitMQ only, not Azure Service Bus), 1.7 (DLQ processing)_

  - [ ] 1.5 Cấu hình gRPC Server Authentication Interceptor + RBAC
    - Implement `AuthInterceptor` (`ServerInterceptor`): đọc gRPC metadata headers (`x-user-id`, `x-tenant-id`, `x-role-ids`, `x-trace-id`, `x-permission-version`) từ request, populate `CurrentUserContext` (scoped per-call) — follow pattern của `AuthInterceptor.cs` trong shared .NET codebase
    - Tạo `@GrpcSystemAdminOnly` guard (custom annotation + AOP aspect hoặc ServerInterceptor) kiểm tra `currentUser.roleIds` chứa `SYSTEM_ADMIN`, throw `StatusRuntimeException(Status.PERMISSION_DENIED)` nếu không có quyền
    - Đăng ký interceptor qua `@GrpcGlobalServerInterceptor` cho tất cả gRPC services
    - Viết unit test: gRPC call không có SYSTEM_ADMIN trong metadata → nhận `StatusRuntimeException(PERMISSION_DENIED)` dùng `grpc-testing` (`InProcessServerBuilder` + `InProcessChannelBuilder`)
    - _Requirements: 3.7, 6.7, 9.1 (SYSTEM_ADMIN only via gRPC)_

  - [ ] 1.6 Cấu hình OpenTelemetry, Micrometer + Prometheus metrics, Logback structured logging
    - Thêm `opentelemetry-spring-boot-starter`, `micrometer-registry-prometheus`
    - Định nghĩa custom counters: `Counter.builder("devops_agent_dedup_discarded_total").register(meterRegistry)`, `devops_agent_incidents_created_total`, `devops_agent_dlq_depth`
    - Cấu hình Logback với MDC enrichers: `MDC.put("correlationId", ...)`, `MDC.put("incidentId", ...)`, `MDC.put("severity", ...)`
    - _Requirements: 11.1 (Prometheus scraping)_

  - [ ] 1.7 Xây dựng `aurora-java-shared` Core Library (Tái sử dụng cho các Java services tương lai)
    - Implement `ClientMetadataInterceptor` (`ClientInterceptor`): tự động forward `x-user-id`, `x-tenant-id`, `x-role-ids`, `x-trace-id` khi DevOps-Agent đóng vai gRPC client gọi sang các microservices khác (như RAG Service)
    - Implement `ExceptionInterceptor` (`ServerInterceptor`): map Java domain exceptions (`NotFoundException`, `ConflictException`, `ForbiddenException`, `DomainException`) sang gRPC `Status` codes (`NOT_FOUND`, `ALREADY_EXISTS`, `PERMISSION_DENIED`, `INVALID_ARGUMENT`) tương đương `ExceptionInterceptor.cs` bên .NET
    - Implement `BaseEntity` (UUID v7 Generator), `AuditableEntity`, `@EntityListeners(AuditEntityListener.class)` để tự động điền `createdAt`, `createdBy`, `updatedAt`, `updatedBy`
    - Implement `GrpcPaginationUtils`: helper class chuyển đổi giữa Spring Data `Pageable`/`Page<T>` và Proto `PageRequest`/`PageResponse` messages
    - Định nghĩa `GrpcMetadataKeys` constants (`x-user-id`, `x-tenant-id`, `x-role-ids`, `x-trace-id`, `x-permission-version`)
    - _Requirements: Chuẩn hóa kiến trúc dùng chung cho hệ sinh thái Java_


- [ ] 2. Phase 2 — Core Domain: Entities, Enums, Interfaces

  - [ ] 2.1 Định nghĩa Domain Entities và Enums
    - Tạo `Severity` enum (`Low`, `Medium`, `High`, `Critical`) trong `com.aurora.devopsagent.domain.enums`
    - Tạo entities: `Incident`, `DebugSession`, `ExistingRule`, `PendingRule`, `PrApprovalRecord`, `RuleApprovalRecord`, `AuditEventOutbox`, `DevOpsAgentSelfConfig` trong `com.aurora.devopsagent.domain.entity`
    - Tạo entity `TenantAiConfig` và `ModelTierRegistry` (read-only, no writes từ DevOps-Agent)
    - Đảm bảo tất cả entity có đúng constraints theo data model trong design.md (dùng JPA annotations: `@Table`, `@Column`, `@UniqueConstraint`)
    - _Requirements: 1–14 (tất cả entities)_

  - [ ] 2.2 Tạo JPA entity configurations và initial Flyway migration
    - Cấu hình JPA annotations cho mỗi entity trong `com.aurora.devopsagent.infrastructure.persistence`
    - Cấu hình UNIQUE constraints: `incidents.correlation_id`, `audit_event_outbox.(correlation_id, action_type)`, `tenant_ai_configs.(tenant_id, service_name)` qua `@UniqueConstraint`
    - Cấu hình ENUM columns dùng `@Enumerated(EnumType.STRING)`
    - Tạo Flyway migration `V1__initial_schema.sql` và seed `model_tier_registry`
    - _Requirements: Data models từ design.md_

  - [ ] 2.3 Implement DedupService và AntiFlappingTracker
    - Tạo `DedupService` interface và implementation trong `com.aurora.devopsagent.application`
    - Implement `computeDedupKey(source, errorSignature, alertTimestamp)` dùng `SHA-256` với time_window_bucket = `floor(ts/300)*300`
    - Implement `checkAndStore(dedupKey, correlationId, ttl=1800s)` — kiểm tra Redis, lưu nếu không tồn tại; dùng `redisTemplate.opsForValue().set(key, val, Duration.ofSeconds(1800))`
    - Tạo `AntiFlappingTracker` dùng Redis ZSET: `zSetOps.add(...)`, `zSetOps.removeRangeByScore(...)`, `zSetOps.size(...)`, `redisTemplate.expire(key, Duration)` với window 10 phút
    - _Requirements: 1.1–1.6, 2.5_

  - [ ]* 2.4 Viết property tests cho DedupService (P1, P2)
    - **Property 1: Dedup Idempotency** — submit N lần cùng payload → đúng 1 Incident được tạo
    - **Property 2: Event Grouping** — N events cùng `(source, error_signature, time_window_bucket)` → 1 `correlation_id`, 0 Debug_Sessions mới
    - Dùng `jqwik`, `@Property(tries = 100)`, mock Redis với `RedisService` fake in-memory
    - _Requirements: 1.4, 1.6_

  - [ ] 2.5 Implement SeverityClassifier
    - Tạo `SeverityClassifier` interface và implementation trong `com.aurora.devopsagent.application`
    - Input: alert metadata JSONB (source, error_type, affected_service_tier, alert_labels)
    - Output: luôn là 1 trong `{Low, Medium, High, Critical}`, không bao giờ null
    - Implement rule-based scoring: error_type keywords × affected_service_tier matrix → severity
    - Ghi lại `original_severity` trước khi anti-flapping escalation
    - _Requirements: 2.1, 2.6, 2.7_

  - [ ]* 2.6 Viết property test cho SeverityClassifier (P3)
    - **Property 3: Severity Classification là tập đóng** — bất kỳ alert metadata nào → output ∈ {Low, Medium, High, Critical}
    - Tạo `@Provide Arbitrary<AlertMetadata>` generator với các trường random hợp lệ trong jqwik
    - _Requirements: 2.1_

  - [ ] 2.7 Implement PiiRedactor (2-layer: Azure AI Language + custom regex)
    - Tạo `PiiRedactor` interface và implementation trong `com.aurora.devopsagent.application`
    - Layer 1: gọi Azure AI Language Text Analytics PII API (HTTP via `RestClient`, timeout 5s), map entities thành `[CATEGORY_NAME]` placeholders
    - Layer 2: custom regex patterns cho `password`, `secret`, `api_key`, `token`, `connection_string`, `private_key`, `access_key`, `credential` — luôn chạy
    - Fallback: nếu Layer 1 timeout/unavailable → log warning `PII_FALLBACK_USED`, chỉ dùng Layer 2
    - Hard block: nếu cả 2 fail → throw `PiiRedactionFailedException extends RuntimeException`, block LLM call và KE creation
    - _Requirements: 8.1–8.6_

  - [ ]* 2.8 Viết property tests cho PiiRedactor (P11, P12)
    - **Property 11: PII placeholder format** — text với PII → output chứa `[CATEGORY_NAME]` placeholders, cấu trúc còn lại được bảo toàn
    - **Property 12: PII fallback non-blocking** — Azure AI Language mocked unavailable → trả về non-null result, không throw
    - _Requirements: 8.2, 8.6_


- [ ] 3. Phase 2 (cont.) — Rule Engine

  - [ ] 3.1 Implement RuleEngineService với in-memory cache + atomic reload
    - Tạo `RuleEngineService` interface và implementation trong `com.aurora.devopsagent.application`
    - Load `ExistingRule` records từ DB vào `Collections.unmodifiableMap(...)` tại startup
    - Implement `match(incident)` — match `error_signature` vs `error_signature_pattern` (exact + regex)
    - Implement `executeRemediation(rule, incident)` — validate scope_constraint trước khi execute
    - Implement atomic cache reload: `cache_stale = true` → background fetch → `AtomicReference<T>.set(...)` swap → hoàn thành trong ≤5s; serve stale cache trong thời gian reload
    - _Requirements: 3.1–3.4, 3.7_

  - [ ]* 3.2 Viết property test cho RuleEngineService (P4)
    - **Property 4: Low severity không kích hoạt LLM** — Low incidents với matching rule → `LlmAdapter.complete(...)` call count = 0
    - Mock `LlmAdapter` với Mockito để verify `verify(llmAdapter, never()).complete(...)`
    - _Requirements: 2.2, 3.3_

  - [ ] 3.3 Implement UnknownIssueHandler (Low severity không match rule)
    - Tạo `UnknownIssueHandler` trong `com.aurora.devopsagent.application`
    - Flow: RAG query (với 10s timeout fallback) → PII redact incident context → LLM RCA call → tạo `PendingRule` với 6 required fields
    - Mark `DebugSession.status = UNMATCHED_RULE`
    - Submit tới Rule Approval workflow, notify Owner qua Email + Dashboard
    - _Requirements: 3.5, 4.1–4.3, 4.6–4.8_

  - [ ]* 3.4 Viết property test cho UnknownIssueHandler (P8)
    - **Property 8: Pending_Rule phải có đủ 6 trường bắt buộc** — bất kỳ RCA output nào → record có non-null: `error_signature`, `root_cause_summary`, `proposed_action`, `confidence_score`, `source_correlation_id`, `created_at`
    - _Requirements: 4.2_

  - [ ] 3.5 Implement Rule CRUD repositories + RBAC enforcement
    - Tạo `ExistingRuleRepository` và `PendingRuleRepository` (JpaRepository) trong `com.aurora.devopsagent.infrastructure.persistence`
    - Implement promote Pending_Rule → Existing_Rule (transaction: insert ExistingRule, update PendingRule.status = APPROVED, trigger cache invalidation)
    - Enforce: chỉ SYSTEM_ADMIN mới được tạo/sửa/xóa rule (guard trong Application layer + gRPC AuthInterceptor)
    - _Requirements: 3.7, 4.4, 4.5_

  - [ ]* 3.6 Viết property tests cho Rule Approval (P7, P9)
    - **Property 7: RBAC mutations** — gRPC call với role ≠ SYSTEM_ADMIN metadata → `Status.PERMISSION_DENIED`, không có mutation trong DB
    - **Property 9: Rejected Pending_Rule không thành Existing_Rule** — sau khi reject → query ExistingRules không trả về rule đó
    - _Requirements: 3.7, 6.7, 4.5_


- [ ] 4. Phase 3 — Pipelines: RCA, Approval State Machine, Artifact Storage

  - [ ] 4.1 Implement LlmAdapter interface với Spring AI Framework (tương đương Semantic Kernel trong .NET)
    - Thêm dependencies `spring-ai-azure-openai-starter` và `spring-ai-google-gemini-starter`
    - Định nghĩa `LlmAdapter` interface (`CompleteAsync`, `IsAvailableAsync`), `LlmResponse`, `LlmUsage`, `LlmCallConfig` records trong `com.aurora.devopsagent.domain`
    - Implement `AzureOpenAiAdapter` và `GeminiAdapter` bọc quanh Spring AI `ChatModel` (`OpenAiChatModel` và `VertexAiGeminiChatModel`) + Resilience4j `@Retry` & `@CircuitBreaker`
    - Implement `LlmAdapterFactory.getAdapter()` — resolve `ChatModel` bean tương ứng từ `SelfConfigManager.getCurrent().getModelProvider()`
    - _Requirements: 5.9, 9.6_

  - [ ] 4.2 Implement RcaPipelineService (Medium/High/Critical)
    - Tạo `RcaPipelineService` trong `com.aurora.devopsagent.application`
    - Collect logs từ Loki HTTP API và Azure Monitor KQL trong 60s (parallel với `CompletableFuture.allOf(...)`)
    - Apply `PiiRedactor` trên toàn bộ log text, exception messages, stack traces; loại bỏ binary blobs/credentials
    - RAG query qua `RagGrpcClient` với 10s deadline (`.withDeadlineAfter(10, TimeUnit.SECONDS)`); fallback khi timeout (log `RAG_TIMEOUT` vào DebugSession)
    - LLM call qua `LlmAdapter` với redacted context + RAG entries
    - Medium: tạo Git branch + commit + open PR (GitHub API client); lưu PR URL vào DebugSession
    - High/Critical: tạo recommendation artifact, KHÔNG mở PR tự động
    - Upload artifacts lên R2; tạo `PrApprovalRecord` với đúng initial state; ghi audit outbox
    - _Requirements: 5.1–5.9_

  - [ ] 4.3 Implement Approval State Machines (PR Approval + Rule Approval)
    - Tạo `PrApprovalStateMachine` và `RuleApprovalStateMachine` trong `com.aurora.devopsagent.application`
    - PR Approval states: `PENDING_APPROVAL_1 → APPROVED|REJECTED|ESCALATED|EXPIRED` (Medium); `PENDING_APPROVAL_1 → PENDING_APPROVAL_2 → APPROVED|REJECTED|ESCALATED|EXPIRED` (High/Critical)
    - Snapshot `original_severity` tại thời điểm tạo Approval_Record — không bao giờ thay đổi dù reclassify
    - Enforce: không bao giờ tự động merge/deploy; `approve(...)` chỉ chuyển state, không trigger git operation
    - Enforce: chỉ SYSTEM_ADMIN mới được gọi `approve(...)` / `reject(...)`
    - _Requirements: 6.1–6.8_

  - [ ]* 4.4 Viết property tests cho Approval State Machine (P6, P10)
    - **Property 6: High/Critical luôn yêu cầu đúng 2 bước approval** — High/Critical incident → state machine không thể chuyển trực tiếp `PENDING_APPROVAL_1 → APPROVED`
    - **Property 10: Không bao giờ tự động merge hoặc deploy** — bất kỳ state nào, bất kỳ severity nào → không có git merge/deploy side effect
    - _Requirements: 6.2, 6.4_

  - [ ] 4.5 Implement ApprovalTimeoutWorker (@Scheduled component)
    - Tạo `@Component ApprovalTimeoutWorker` trong `com.aurora.devopsagent.infrastructure.backgroundjobs`
    - `@Scheduled(fixedDelay = 30_000)` poll interval; query `WHERE timeout_at <= NOW() AND status LIKE 'PENDING_APPROVAL%'`
    - Transition → `ESCALATED`, ghi `escalated_at`, publish audit event `ESCALATED`
    - Routing notification: nếu `original_severity = Low` → Email only; ngược lại → Email + Telegram
    - _Requirements: 6.3, 7.3, 7.7_

  - [ ]* 4.6 Viết property test cho AntiFlappingTracker (P5)
    - **Property 5: Anti-Flapping Escalation** — bất kỳ dedup_key K với >3 events trong 10 phút → severity = Medium, original_severity = Low
    - Dùng in-memory Redis fake, inject controlled timestamps
    - _Requirements: 2.5_

  - [ ] 4.7 Implement ArtifactStorageService (Cloudflare R2 via AWS SDK for Java v2 S3-compatible)
    - Tạo `ArtifactStorageService` interface và implementation trong `com.aurora.devopsagent.infrastructure`
    - Dùng `software.amazon.awssdk:s3` với `S3Client.builder().endpointOverride(URI.create(...)).build()`, custom endpoint = Cloudflare Tunnel URL từ Key Vault
    - Implement `upload(correlationId, artifactType, timestamp, filename, content)` — build key theo template
    - Tag mỗi object: `correlation_id`, `severity`, `tenant_id`, `created_at`, `artifact_type`
    - Retry policy: network error → 3x exponential backoff (2s/4s/8s) → continue với warning; critical error → throw `ArtifactUploadCriticalException extends RuntimeException`
    - _Requirements: 5.7, 5.8, 12.1–12.5_

  - [ ]* 4.8 Viết property test cho ArtifactStorageService (P20)
    - **Property 20: R2 Artifact Key phải khớp template** — bất kỳ `(correlation_id, artifact_type, timestamp, filename)` → key = `devops-agent/incidents/{correlation_id}/artifacts/{artifact_type}/{timestamp}_{filename}`
    - _Requirements: 5.7, 12.1_

  - [ ] 4.9 Implement RagGrpcClient (Giao tiếp với RAG Service qua gRPC)
    - Reference proto contract `devops_rag.proto` từ RAG Service (định nghĩa RPC `QueryKnowledge` và `IngestKnowledge`) vào `devops-agent-grpc-api`
    - Generate Java gRPC client stubs (`DevOpsRagServiceGrpc.DevOpsRagServiceBlockingStub`) dùng `protobuf-maven-plugin`
    - Implement `RagGrpcClient` trong `com.aurora.devopsagent.infrastructure.grpc` kết nối tới RAG Service endpoint (được inject từ Key Vault / `application.yml`)
    - Đăng ký `ClientMetadataInterceptor` (từ `aurora-java-shared`) lên gRPC `ManagedChannel` để tự động forward `x-trace-id`, `x-user-id`, `x-tenant-id` khi gọi RAG Service
    - Thiết lập strict 10s deadline trên mọi RPC call qua `.withDeadlineAfter(10, TimeUnit.SECONDS)`
    - `ingestKnowledge(...)` luôn set `source_tag = "devops-agent"` — không có code path nào override
    - Catch `StatusRuntimeException` (`DEADLINE_EXCEEDED`, `UNAVAILABLE`) → trả về empty result, đánh dấu warning flag `RAG_TIMEOUT` vào DebugSession, tiếp tục luồng RCA mà không throw ngoại lệ chặn pipeline
    - _Requirements: 13.1–13.5_

  - [ ]* 4.10 Viết property tests cho RagGrpcClient (P21, P22)
    - **Property 21: RAG IngestKnowledge luôn có source_tag = "devops-agent"** — bất kỳ ingest call nào → captured request.SourceTag == "devops-agent"
    - **Property 22: RAG Timeout Fallback** — RAG gRPC mocked delay >10s → RCA vẫn produce output, DebugSession có `RAG_TIMEOUT` warning flag
    - _Requirements: 13.4, 13.5_


- [ ] 5. Checkpoint — Phase 1-3 Validation
  - Ensure all unit tests và property tests (P1–P12, P20–P22) pass
  - Build: `./mvnw clean compile` không có lỗi hoặc warning
  - Chạy migrations: Flyway tự chạy khi start; kiểm tra `./mvnw flyway:migrate` thành công trên dev PostgreSQL
  - Ask the user if questions arise.

- [ ] 6. Phase 4 — gRPC Handlers, Audit Outbox, Notification, Self-Config

  - [ ] 6.1 Implement IngestionGrpcHandler (IngestAlert RPC)
    - Tạo `IngestionGrpcHandler` (`@GrpcService`) trong `com.aurora.devopsagent.grpc` — implement `IngestAlert` RPC từ `devops_agent.proto`
    - Nhận `IngestAlertRequest` từ BFF (BFF nhận HTTP webhook từ Azure Monitor/LogBack rồi forward qua gRPC): parse alert metadata, source type (`azure_monitor` / `LogBack`)
    - Gọi `DedupService.checkAndStore(...)` → nếu duplicate trả về `IngestAlertResponse(duplicated=true)`; nếu mới → tạo Incident + DebugSession → dispatch tới `RoutingDispatcher`
    - Event ingestion không phụ thuộc downstream: nếu DB/Redis down → vẫn write tới DLQ
    - _Requirements: 1.1–1.7, 11.5_

  - [ ]* 6.2 Viết property test cho Event Ingestion isolation (P17)
    - **Property 17: Event Ingestion không phụ thuộc downstream services** — mock RAG/AuditLog/RCA failing → ingestion RPC vẫn return OK (không throw), DLQ write vẫn xảy ra
    - _Requirements: 11.5_

  - [ ] 6.3 Implement IncidentGrpcHandler (SYSTEM_ADMIN only)
    - Tạo `IncidentGrpcHandler` (`@GrpcService`) trong `com.aurora.devopsagent.grpc` — implement RPCs từ `devops_agent.proto`
    - `ListIncidents` RPC — filter: severity, status, tenant_id, date range; phân trang qua proto pagination messages
    - `GetIncident` RPC — trả về incident + debug_session + approval + artifact_refs theo response message trong proto
    - `ApproveIncident` / `RejectIncident` RPCs — delegate tới `PrApprovalStateMachine.approve(...)` hoặc `reject(...)`; enforce `@GrpcSystemAdminOnly`
    - _Requirements: 6.6, 6.7_

  - [ ] 6.4 Implement RuleGrpcHandler (SYSTEM_ADMIN only)
    - Tạo `RuleGrpcHandler` (`@GrpcService`) trong `com.aurora.devopsagent.grpc` — implement RPCs từ `devops_agent.proto`; enforce `@GrpcSystemAdminOnly` trên tất cả RPCs
    - `ListExistingRules` — danh sách ExistingRules với filter; `CreateRule` — tạo manual; `UpdateRule` — update; `DeleteRule` — delete
    - `ListPendingRules` — danh sách PendingRules; `ApprovePendingRule`; `RejectPendingRule`
    - Trigger cache invalidation sau mọi write operation (emit internal `rule.updated` event)
    - _Requirements: 3.1–3.7, 4.4–4.5_

  - [ ] 6.5 Implement SelfConfigGrpcHandler + SelfConfigManager hot-reload
    - Tạo `SelfConfigGrpcHandler` (`@GrpcService`) trong `com.aurora.devopsagent.grpc`; enforce `@GrpcSystemAdminOnly`
    - `GetSelfConfig` RPC — trả về SelfConfig hiện tại; `UpdateSelfConfig` RPC — update và publish `config.updated` event
    - `SelfConfigManager`: `volatile SelfConfig current`; `onConfigUpdated()` reload từ DB, đảm bảo mọi LLM call tiếp theo dùng config mới trong ≤60s
    - Publish Audit_Event `SELF_CONFIG_UPDATED` với `old_model_name`, `new_model_name`
    - _Requirements: 9.1–9.4, 9.6_

  - [ ] 6.6 Implement AuditOutboxWorker (@Scheduled component + outbox pattern)
    - Tạo `@Component AuditOutboxWorker` trong `com.aurora.devopsagent.infrastructure.backgroundjobs`
    - `@Scheduled(fixedDelay = 5_000)` poll interval; native query `SELECT ... FOR UPDATE SKIP LOCKED LIMIT 50`
    - Publish tới RabbitMQ exchange `audit.events` qua `RabbitTemplate`; nhận publisher confirms
    - ON CONFIRM: `status = PUBLISHED`, `published_at = NOW()`
    - ON FAIL: exponential backoff (5s, 15s, 45s), max 3 retries; sau đó `status = FAILED`, log critical
    - Dedup: `ON CONFLICT (correlation_id, action_type) DO NOTHING` khi INSERT qua `@Transactional` + catch `DataIntegrityViolationException`
    - _Requirements: 10.1–10.6_

  - [ ]* 6.7 Viết property tests cho AuditOutbox (P13, P14, P15)
    - **Property 13: Audit Completeness** — mỗi 1 trong 13 action types → đúng 1 AuditEvent với đủ 7 fields non-null
    - **Property 14: Audit Outbox Idempotency** — retry cùng `(correlation_id, action_type)` N lần → AuditLog chứa đúng 1 record
    - **Property 15: PENDING until confirmed** — publish attempt không có delivery confirm → status vẫn là PENDING
    - _Requirements: 10.1, 10.2, 10.4, 10.5_

  - [ ] 6.8 Implement NotificationDispatcher (Email + Dashboard + Telegram)
    - Tạo `NotificationDispatcher` interface và implementation trong `com.aurora.devopsagent.application`
    - Email: SES + Stalwart SMTP client; Dashboard: publish event tới internal channel (WebSocket hoặc SSE endpoint)
    - Telegram: chỉ gửi khi `correlation_id`, `severity`, `action_summary`, `dashboard_link` đều non-empty; KHÔNG gửi cho Low severity auto-remediation (trừ escalation từ Low → Medium)
    - Escalation routing: `original_severity = Low` → Email only; ngược lại → Email + Telegram
    - _Requirements: 7.1–7.7_

  - [ ]* 6.9 Viết property tests cho NotificationDispatcher (P18, P19)
    - **Property 18: Telegram bị chặn khi thiếu trường bắt buộc** — bất kỳ combination thiếu field nào → Telegram KHÔNG được gửi, log warning
    - **Property 19: Telegram không gửi cho Low severity auto-remediation** — Low incident lifecycle không có flapping → zero Telegram calls
    - _Requirements: 7.4, 7.5, 7.7_

  - [ ] 6.10 Implement DlqReprocessor (@RabbitListener + Semaphore max 10)
    - Tạo `@Component DlqReprocessor` trong `com.aurora.devopsagent.infrastructure.backgroundjobs`
    - `@RabbitListener(queues = "devops.agent.dlq")` consume từ RabbitMQ DLQ theo FIFO order sau khi service restart
    - `new Semaphore(10)` giới hạn concurrent processing
    - Mỗi event: apply full dedup check trước (Redis), nếu dedup_key tồn tại → discard; nếu không → full ingestion pipeline
    - _Requirements: 11.2, 11.3_

  - [ ]* 6.11 Viết property test cho DlqReprocessor (P16)
    - **Property 16: DLQ Concurrency không vượt quá 10** — batch N > 10 events → tại bất kỳ thời điểm nào số event đang xử lý concurrent ≤ 10
    - Dùng `Semaphore` tracking counter trong test, assert `maxObservedConcurrency ≤ 10`
    - _Requirements: 11.3_

  - [ ] 6.12 Implement SelfMonitorService + HealthController
    - `GET /actuator/health` — Spring Boot Actuator built-in; custom health indicator: dlq_depth (RabbitMQ queue depth), active_debug_session_count, redis_connectivity
    - `GET /actuator/prometheus` — Micrometer Prometheus exposition format
    - Khi response time > 30s cho >3 consecutive health checks → gửi Telegram alert tới System_Admin
    - _Requirements: 11.1, 11.4_

  - [ ] 6.13 Implement SelfConfig cost monitoring + alert
    - Trong `SelfConfigManager`: track `tokens_used_today` × cost_per_token → daily_cost_usd
    - Khi daily_cost đạt 80% `alert_threshold_usd_per_day` → Email + Telegram alert
    - Khi daily_cost đạt 100% threshold → Email + Telegram alert; KHÔNG block hay pause bất kỳ Debug_Session nào
    - Khi cost monitoring service itself fails → alert tới System_Admin
    - Tenant_AI_Config: implement `TenantAiConfigReadRepository` (JpaRepository read-only) cho dashboard stats endpoint ONLY (no write)
    - _Requirements: 9.3, 9.5, 14.7, 14.8_

  - [ ]* 6.14 Viết property test cho cost monitoring (P23)
    - **Property 23: Cost Alert không block Debug_Session** — trigger cost threshold event trong khi Debug_Session đang active → session không bị terminate/pause/modify, chỉ notification được gửi
    - _Requirements: 9.3_


- [ ] 7. Phase 4 (cont.) — Tenant AI Config Management (Req 14)

  - [ ] 7.1 Implement TenantAiConfig domain và Flyway migration
    - Tạo `@Entity TenantAiConfig` với fields: `tenant_id`, `service_name`, `model_provider`, `model_name`, `daily_token_limit`, `tokens_used_today`, `subscription_plan`, `updated_by`, `updated_at`, `created_at`
    - UNIQUE constraint `(tenant_id, service_name)` qua `@UniqueConstraint`
    - Seed `model_tier_registry` table với Standard/Enterprise tier entries
    - Tạo Flyway migration `V2__add_tenant_ai_config.sql`
    - _Requirements: 14.1, 14.6_

  - [ ] 7.2 Implement plan-gating logic và TenantAiConfig update API
    - Service method `updateTenantAiConfig(tenantId, serviceName, modelProvider, modelName, updatedBy)` — chỉ Tenant_Admin của tenant đó được gọi (auth kiểm tra tại BFF/IAM layer, không phải DevOps-Agent)
    - Plan-gating: `Standard` plan → chỉ `gemini` provider Standard-tier; `Enterprise` → cả hai providers
    - Kiểm tra `model_tier_registry` để validate tier compatibility với subscription_plan
    - Default provisioning: `SYSTEM` operator tạo default config khi tenant mới được tạo
    - DevOps-Agent chỉ expose dashboard stats endpoint (read-only); không expose config management API
    - _Requirements: 14.2–14.5, 14.7, 14.9_

  - [ ] 7.3 Implement daily token reset job
    - `@Component TokenResetJob` với `@Scheduled(cron = "0 0 0 * * *")` — reset `tokens_used_today = 0` lúc 00:00 UTC hàng ngày cho tất cả TenantAiConfig records
    - Khi tenant service vượt `daily_token_limit` → block AI calls cho service đó, gửi alert cho Tenant_Admin
    - DevOps-Agent internal LLM calls KHÔNG bị ảnh hưởng bởi tenant token limit
    - _Requirements: 14.7_

- [ ] 8. Checkpoint — Phase 4 Validation
  - Build và run: `./mvnw clean compile` + `./mvnw test -pl devops-agent-tests`
  - Verify tất cả property tests P1–P23 pass
  - Verify health endpoint (`/actuator/health`) trả về đúng format
  - Kiểm tra Flyway migrations không có conflicts
  - Ask the user if questions arise.


- [ ] 9. Phase 5 — Integration Tests & Smoke Tests

  - [ ] 9.1 Integration tests: Redis (dedup TTL + anti-flapping ZSET)
    - Test dedup TTL = 1800s: lưu key → verify tồn tại → sau 30 phút (mock time) → verify đã expire
    - Test anti-flapping ZSET: `zSetOps.add` / `zSetOps.removeRangeByScore` behavior với Redis thực (Testcontainers)
    - _Requirements: 1.3, 2.5_

  - [ ]* 9.2 Integration tests: PostgreSQL (CRUD, rule cache reload, outbox polling)
    - Test: ExistingRule CRUD với Spring Data JPA + `@Transactional`
    - Test: atomic cache reload trong ≤5s khi có rule update
    - Test: `SELECT ... FOR UPDATE SKIP LOCKED` không deadlock với concurrent workers (Testcontainers PostgreSQL)
    - _Requirements: 3.1, 3.2, 4.7 (outbox)_

  - [ ]* 9.3 Integration tests: RabbitMQ (publisher confirms, DLQ order)
    - Test: publish Audit_Event → nhận delivery confirm → status = PUBLISHED
    - Test: DLQ consumption FIFO order sau khi consumer restart (Testcontainers RabbitMQ)
    - _Requirements: 10.4, 11.2_

  - [ ]* 9.4 Integration tests: gRPC contract (RAG_Service mock server)
    - Tạo mock gRPC server cho `DevOpsRagService` dùng `io.grpc:grpc-inprocess` với `InProcessServerBuilder`
    - Test: QueryKnowledge với deadline 10s → mocked delay → `Status.DEADLINE_EXCEEDED` → empty result returned
    - Test: IngestKnowledge → captured request có `source_tag = "devops-agent"`
    - _Requirements: 13.1–13.5_

  - [ ]* 9.5 Integration tests: Cloudflare R2 (Cloudflare Tunnel endpoint, test env)
    - Test: upload artifact → verify key format theo template
    - Test: download artifact để confirm content integrity
    - Test: lifecycle retention policy tag được set
    - _Requirements: 12.1–12.4_

  - [ ] 9.6 Smoke tests: startup validation
    - Health endpoint `GET /actuator/health` trả về 200 với đúng fields khi service khởi động
    - SelfConfig singleton row tồn tại và có đủ required fields sau Flyway migrations
    - R2 lifecycle retention policy = 90 ngày được configure
    - Azure Key Vault secrets accessible từ Workload Identity (mocked trong test env với `DefaultAzureCredentialBuilder().build()`)
    - _Requirements: 9.1, 11.1, 12.2, 8.4_

- [ ] 10. Final Checkpoint — Ensure All Tests Pass
  - `./mvnw test -pl devops-agent-tests` — all tests green
  - `./mvnw package -P production -DskipTests` — no errors
  - Verify không có test suppression (no `@Disabled`, no commented-out assertions)
  - Ask the user if questions arise.

---

## Notes

- Tasks đánh dấu `*` là optional và có thể bỏ qua cho MVP nhanh hơn, nhưng bắt buộc cho production readiness
- Mỗi phase phải có baseline build pass trước khi bắt đầu phase tiếp theo
- Property tests dùng `jqwik với @Property(tries = 100)` minimum
- Mọi secret phải lấy từ Azure Key Vault qua Workload Identity — không có hardcode, không có env var production
- gRPC RPCs (trừ health/metrics HTTP Actuator endpoints) yêu cầu gRPC metadata `x-role-ids` chứa `SYSTEM_ADMIN`; enforce tại cả gRPC AuthInterceptor và Application layer
- `original_severity` phải được snapshot tại thời điểm tạo Approval_Record và không bao giờ thay đổi
- DevOps-Agent không bao giờ tự trigger git merge hay production deploy — chỉ chuyển state approval


## Task Dependency Graph

```json
{
  "waves": [
    {
      "id": 0,
      "tasks": ["1.1", "1.2", "1.3", "1.4", "1.5", "1.6"]
    },
    {
      "id": 1,
      "tasks": ["2.1", "2.2"]
    },
    {
      "id": 2,
      "tasks": ["2.3", "2.5", "2.7"]
    },
    {
      "id": 3,
      "tasks": ["2.4", "2.6", "2.8", "3.1", "4.1"]
    },
    {
      "id": 4,
      "tasks": ["3.2", "3.3", "4.7", "4.9"]
    },
    {
      "id": 5,
      "tasks": ["3.4", "3.5", "4.2", "4.10"]
    },
    {
      "id": 6,
      "tasks": ["3.6", "4.3", "4.5", "4.8"]
    },
    {
      "id": 7,
      "tasks": ["4.4", "4.6", "6.1", "6.5", "6.6"]
    },
    {
      "id": 8,
      "tasks": ["6.2", "6.3", "6.4", "6.7", "6.8", "6.10", "6.12", "6.13", "7.1"]
    },
    {
      "id": 9,
      "tasks": ["6.9", "6.11", "6.14", "7.2"]
    },
    {
      "id": 10,
      "tasks": ["7.3"]
    },
    {
      "id": 11,
      "tasks": ["9.1", "9.6"]
    },
    {
      "id": 12,
      "tasks": ["9.2", "9.3", "9.4", "9.5"]
    }
  ]
}
```
