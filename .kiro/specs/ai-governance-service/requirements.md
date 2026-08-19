# Tài liệu Yêu cầu — AiGovernanceService

## Giới thiệu

AiGovernanceService là một microservice tập trung đóng vai trò **Policy Decision Point (PDP)** cho toàn bộ các khả năng AI hướng tenant trong nền tảng logistics/hải quan multi-tenant SynchroCustoms. Mọi agent service (Document OCR, Regulatory Compliance RAG, Customer Assistant, Route Planning, v.v.) đều phải gọi `ExecutePolicy()` trước khi thực hiện bất kỳ cuộc gọi AI nào. Service quyết định: request có được phép không, provider nào được dùng, giới hạn token là bao nhiêu, và mức độ tự động hóa là gì.

**Stack:** Spring Boot 3.x, Java 21 (virtual threads), grpc-java, Spring Data JPA + Hibernate + Flyway, Spring Data Redis (Lettuce), Spring AMQP (publisher confirms) với RabbitMQ, Micrometer + Prometheus, Logback + logstash-logback-encoder, Maven, JUnit 5 + Mockito + Testcontainers, PostgreSQL (DB per service), Redis, Azure Key Vault, AKS, ArgoCD/Terraform.

**Mô hình kiến trúc:** CQRS không có repository layer, DB per service.

---

## Bảng thuật ngữ

- **AiGovernanceService**: Microservice PDP trung tâm — đối tượng chính của tài liệu này.
- **ExecutePolicy**: gRPC unary endpoint đồng bộ, điểm vào duy nhất để kiểm tra chính sách AI.
- **PolicyDecision**: Kết quả trả về từ `ExecutePolicy` — `allowed`, `provider`, `maxTokens`, `requireApproval`, `reason`.
- **Plan**: Gói dịch vụ gán cho tenant — `STANDARD`, `ENTERPRISE`. Được seed qua Flyway, không có admin UI.
- **Capability**: Một tính năng AI cụ thể (VD: `OCR_EXTRACTION`, `COMPLIANCE_CHECK`, `ROUTE_PLANNING`). Mỗi Plan có bảng bật/tắt tĩnh cho từng Capability.
- **Quota**: Giới hạn sử dụng token theo `quota_type` và `period` (`DAY` hoặc `MONTH`). Lưu trong `plan_quotas`.
- **QuotaCounter**: Bộ đếm hiện tại trong Redis — key pattern: `quota:{tenantId}:{quotaType}:{periodKey}`, TTL = hết kỳ.
- **BufferThreshold**: Ngưỡng an toàn = `limit_value × 0.95`. `ExecutePolicy` chỉ cho phép khi `counter < BufferThreshold`.
- **AiUsageEvent**: Event bất đồng bộ do agent service publish lên RabbitMQ SAU KHI cuộc gọi AI hoàn thành. Consumer ghi vào Postgres rồi đồng bộ Redis.
- **AiPolicyDecisionEvent**: Event audit publish bởi AiGovernanceService sau mỗi `ExecutePolicy` — gửi tới AuditLog Service.
- **UsageRecord**: Bản ghi Postgres lưu giá trị sử dụng hiện tại: `(tenant_id, quota_type, period_key, current_value, updated_at)`.
- **Tenant**: Bản ghi tenant trong DB của service này — `(id, plan_id, cloud_ai_enabled, status)`.
- **AutomationLevel**: Mức tự động hóa tĩnh gán cho Plan — `MANUAL`, `RULES_ONLY`, `RULES_AI`, `FULL_AUTOMATION`.
- **Provider**: AI provider gán tĩnh cho Plan — `GEMINI` hoặc `AZURE_OPENAI`.
- **CloudAiEnabled**: Boolean trên Tenant — cho phép hay chặn tất cả cuộc gọi cloud AI cho tenant đó.
- **DLQ**: Dead Letter Queue trên RabbitMQ — giữ message thất bại để xử lý lại.
- **PeriodKey**: Chuỗi định danh kỳ quota — VD: `2025-07-15` (DAY) hoặc `2025-07` (MONTH).
- **Fail-Closed**: Chính sách từ chối mặc định khi service lỗi hoặc không có dữ liệu.
- **CQRS**: Command Query Responsibility Segregation — kiến trúc không có repository layer.

---

## Yêu cầu

### Yêu cầu 1: Cấu trúc Package và Kiến trúc Service

**User Story:** Là một developer, tôi muốn AiGovernanceService tuân theo cấu trúc package chuẩn CQRS không repository layer với phân lớp domain/application/infrastructure rõ ràng, để các tính năng mới có thể được thêm vào mà không phá vỡ ranh giới kiến trúc.

#### Acceptance Criteria

1. THE AiGovernanceService SHALL tổ chức source code theo cấu trúc package: `Domain` (entities, value objects, enums, domain events), `Application` (commands, queries, handlers, ports), `Infrastructure` (persistence, messaging, cache, grpc), `GrpcServices` (gRPC service implementations).
2. THE AiGovernanceService SHALL sử dụng Java 21 virtual threads (Project Loom) thông qua `spring.threads.virtual.enabled=true` để xử lý đồng thời các gRPC request mà không cần WebFlux hay reactive stack.
3. THE AiGovernanceService SHALL implement CQRS không có repository layer — command handlers và query handlers gọi trực tiếp các port interfaces (JPA `EntityManager` hoặc `NamedEntityGraph`) thay vì thông qua repository abstraction layer.
4. THE AiGovernanceService SHALL định nghĩa tất cả external dependency boundaries (Redis, PostgreSQL, RabbitMQ) như các port interfaces trong package `application.ports.out` và cung cấp implementations trong package `infrastructure`.
5. Domain entities SHALL có JPA annotations (`@Entity`, `@Table`, `@Column`, v.v.) đặt trực tiếp trên class — cùng class với domain logic. Domain entities SHALL NOT extend hay implement Spring/JPA types (kế thừa). Infrastructure persistence adapters SHALL sử dụng `EntityManager` để thao tác trực tiếp trên các annotated domain entities này, KHÔNG cần mapper layer hay separate JPA entity classes.
6. WHERE gRPC interceptor được cấu hình, THE AiGovernanceService SHALL trích xuất `tenantId` từ gRPC metadata header `x-tenant-id` và lưu vào thread-local context; service SHALL NOT tin tưởng `tenant_id` được cung cấp trong request body.
7. THE AiGovernanceService SHALL là một Maven single-module project. File `.proto` SHALL được đặt tại `root/protos/ai_governance.proto` (shared proto root của toàn dự án). `pom.xml` của service SHALL cấu hình `protobuf-maven-plugin` với `<protoSourceRoot>${project.basedir}/../../protos</protoSourceRoot>` để generate gRPC stubs từ shared location, KHÔNG dùng `src/main/proto`.
8. // TODO(v2): Thêm admin endpoints cho dynamic plan management khi admin UI được yêu cầu.


---

### Yêu cầu 2: Định nghĩa gRPC API (.proto)

**User Story:** Là một developer của agent service, tôi muốn có một .proto file đầy đủ và rõ ràng cho AiGovernanceService, để tôi có thể tích hợp `ExecutePolicy` vào service của mình mà không cần tra cứu thêm tài liệu.

#### Acceptance Criteria

1. THE AiGovernanceService SHALL định nghĩa file `ai_governance.proto` đặt tại `root/protos/ai_governance.proto` (shared proto root của toàn dự án) với `package aurora.governance.v1` và `option java_package = "com.aurora.governance.v1"`, chứa đủ 3 RPC: `ExecutePolicy`, `GetTenantPlan`, `GetCapabilities`.
2. WHEN định nghĩa `ExecutePolicyRequest`, THE proto file SHALL chứa các fields: `string tenant_id = 1`, `string capability_code = 2`, `int32 estimated_tokens = 3`.
3. WHEN định nghĩa `PolicyDecision`, THE proto file SHALL chứa: `bool allowed = 1`, `string provider = 2` (GEMINI | AZURE_OPENAI), `int32 max_tokens = 3`, `bool require_approval = 4`, `string reason = 5` (QUOTA_EXCEEDED | CAPABILITY_DISABLED | CLOUD_AI_DISABLED | PLAN_NOT_FOUND | INTERNAL_ERROR).
4. WHEN định nghĩa `TenantRequest`, THE proto file SHALL chứa `string tenant_id = 1`; `PlanInfo` SHALL chứa: `string plan_code`, `string provider`, `string automation_level`, `bool cloud_ai_default`.
5. WHEN định nghĩa `CapabilityList`, THE proto file SHALL chứa `repeated CapabilityInfo capabilities`, trong đó `CapabilityInfo` có: `string capability_code`, `bool enabled`.
6. THE proto file SHALL sử dụng `google.rpc.Status` qua `google/rpc/status.proto` cho error handling — không dùng custom error message fields; lỗi nghiệp vụ được biểu diễn bằng `PolicyDecision.allowed = false` + `reason`, còn lỗi hệ thống dùng gRPC status code `INTERNAL`.
7. WHEN gRPC call thất bại do lỗi nội bộ, THE AiGovernanceService SHALL trả về status code `UNAVAILABLE` với message chuẩn hóa; `tenant_id` và `capability_code` SHALL NOT xuất hiện trong gRPC error message để tránh thông tin nhạy cảm bị lộ.
8. THE AiGovernanceService SHALL cung cấp server reflection (`grpc.reflection.v1alpha.ServerReflection`) trong môi trường non-production để hỗ trợ tooling như grpcurl.
9. // TODO(v2): Thêm `rpc ReserveQuota` và `rpc CommitQuota` cho 2-phase quota reservation khi cần eliminate race window hoàn toàn.


---

### Yêu cầu 3: Flyway Migration — Schema và Seed Data

**User Story:** Là một developer, tôi muốn có Flyway migration scripts đầy đủ cho schema và dữ liệu seed ban đầu, để database được khởi tạo nhất quán trên mọi môi trường mà không cần can thiệp thủ công.

#### Acceptance Criteria

1. THE AiGovernanceService SHALL cung cấp migration `V1__create_schema.sql` tạo đủ 5 bảng: `plans`, `plan_capabilities`, `plan_quotas`, `tenants`, `usage_records` với đúng kiểu dữ liệu, constraints, và indexes.
2. WHEN tạo bảng `plans`, THE migration SHALL định nghĩa: `id UUID PRIMARY KEY`, `code VARCHAR(50) UNIQUE NOT NULL`, `name VARCHAR(100) NOT NULL`, `provider VARCHAR(50) NOT NULL` (CHECK IN ('GEMINI','AZURE_OPENAI')), `automation_level VARCHAR(50) NOT NULL` (CHECK IN ('MANUAL','RULES_ONLY','RULES_AI','FULL_AUTOMATION')), `cloud_ai_default BOOLEAN NOT NULL`.
3. WHEN tạo bảng `plan_capabilities`, THE migration SHALL định nghĩa: `plan_id UUID REFERENCES plans(id)`, `capability_code VARCHAR(100) NOT NULL`, PRIMARY KEY `(plan_id, capability_code)`, và index trên `(plan_id, capability_code)` để hỗ trợ lookup O(1).
4. WHEN tạo bảng `plan_quotas`, THE migration SHALL định nghĩa: `plan_id UUID REFERENCES plans(id)`, `quota_type VARCHAR(50) NOT NULL`, `limit_value BIGINT NOT NULL CHECK (limit_value > 0)`, `period VARCHAR(10) NOT NULL` CHECK IN ('DAY','MONTH'), PRIMARY KEY `(plan_id, quota_type, period)`.
5. WHEN tạo bảng `tenants`, THE migration SHALL định nghĩa: `id UUID PRIMARY KEY`, `plan_id UUID REFERENCES plans(id) NOT NULL`, `cloud_ai_enabled BOOLEAN NOT NULL DEFAULT false`, `status VARCHAR(20) NOT NULL DEFAULT 'ACTIVE'` CHECK IN ('ACTIVE','SUSPENDED','CANCELLED'), `created_at TIMESTAMP NOT NULL DEFAULT NOW()`, `updated_at TIMESTAMP NOT NULL DEFAULT NOW()`.
6. WHEN tạo bảng `usage_records`, THE migration SHALL định nghĩa: `id UUID PRIMARY KEY`, `tenant_id UUID NOT NULL`, `quota_type VARCHAR(50) NOT NULL`, `period_key VARCHAR(10) NOT NULL`, `current_value BIGINT NOT NULL DEFAULT 0`, `updated_at TIMESTAMP NOT NULL`, UNIQUE `(tenant_id, quota_type, period_key)`, index trên `(tenant_id, quota_type, period_key)`.
7. THE AiGovernanceService SHALL cung cấp migration `V2__seed_plans.sql` chứa đủ 3 plan records: `FREE` (provider=GEMINI, automation_level=MANUAL, cloud_ai_default=false), `STANDARD` (provider=GEMINI, automation_level=RULES_AI, cloud_ai_default=true), `ENTERPRISE` (provider=AZURE_OPENAI, automation_level=FULL_AUTOMATION, cloud_ai_default=true).
8. WHEN seed `plan_capabilities`, THE migration `V2__seed_plans.sql` SHALL bật tất cả capabilities cho ENTERPRISE, bật capabilities cơ bản (`OCR_EXTRACTION`, `COMPLIANCE_CHECK`) cho STANDARD, và tắt tất cả AI capabilities cho FREE.
9. WHEN seed `plan_quotas`, THE migration SHALL thiết lập: FREE — 0 tokens/day, STANDARD — 500,000 tokens/day và 10,000,000 tokens/month, ENTERPRISE — 5,000,000 tokens/day và 100,000,000 tokens/month.
10. THE AiGovernanceService SHALL cung cấp bảng `processed_events(request_id VARCHAR(100) PRIMARY KEY, processed_at TIMESTAMP NOT NULL)` trong `V1__create_schema.sql` để hỗ trợ idempotency check cho `AiUsageEvent` consumer; bảng này SHALL có index trên `processed_at` để hỗ trợ cleanup job trong tương lai.
11. IF Flyway migration thất bại trong quá trình startup, THEN THE AiGovernanceService SHALL từ chối khởi động và ghi log lỗi migration với tên script và error message rõ ràng.
12. THE AiGovernanceService SHALL tuân theo quy trình migration thủ công: tất cả SQL migration scripts SHALL được viết tay bởi developer. WHERE developer cần scaffold DDL ban đầu từ JPA entities, THE developer MAY sử dụng `spring.jpa.properties.jakarta.persistence.schema-generation.scripts.action=create` để Hibernate xuất DDL ra file tham khảo, SAU ĐÓ SHALL review và chỉnh sửa thủ công trước khi đưa vào file `V{n}__*.sql` chính thức — Hibernate DDL export là công cụ nháp, KHÔNG phải migration tool chính thức.


---

### Yêu cầu 4: Chiến lược Redis Key/TTL và Quota Counter

**User Story:** Là một architect, tôi muốn chiến lược Redis rõ ràng cho quota counter với mô hình check-then-report, để tôi hiểu được race window chấp nhận được và cách buffer threshold bảo vệ khỏi quota vượt mức.

#### Acceptance Criteria

1. THE AiGovernanceService SHALL sử dụng Redis key pattern `quota:{tenantId}:{quotaType}:{periodKey}` trong đó `periodKey` là `yyyy-MM-dd` cho kỳ DAY và `yyyy-MM` cho kỳ MONTH; key được lưu dưới dạng String với giá trị là số nguyên không âm biểu diễn tổng tokens đã dùng.
2. WHEN TTL cho Redis key được thiết lập, THE AiGovernanceService SHALL tính TTL = số giây còn lại đến hết kỳ hiện tại (hết ngày UTC cho DAY, hết tháng UTC cho MONTH) cộng thêm 300 giây buffer để tránh race condition giữa key expiration và rollover kỳ mới.
3. WHEN `ExecutePolicy` kiểm tra quota, THE AiGovernanceService SHALL thực hiện Redis `GET` read-only — TUYỆT ĐỐI KHÔNG ghi, INCR, hay DECR counter trong `ExecutePolicy`; nếu key không tồn tại trong Redis, THE service SHALL coi counter = 0 (không deny).
4. THE AiGovernanceService SHALL so sánh `counter < limit_value × 0.95` (BufferThreshold) — nếu `counter >= BufferThreshold`, THE service SHALL trả về `PolicyDecision.allowed = false` với reason `QUOTA_EXCEEDED`.
5. WHEN phân tích race window, THE AiGovernanceService documentation SHALL ghi nhận: trong khoảng thời gian giữa `ExecutePolicy` trả về `allowed=true` và `AiUsageEvent` được consumer xử lý, có thể có tối đa N request đồng thời đều vượt qua check nếu counter chưa được update; buffer 5% (limit × 0.05) là cơ chế hấp thụ overrun này — buffer tối thiểu là 25,000 tokens cho STANDARD day quota.
6. WHEN `AiUsageEvent` consumer cập nhật Redis, THE consumer SHALL sử dụng lệnh `SET quota:{tenantId}:{quotaType}:{periodKey} {newValue} EX {ttlSeconds}` với `newValue` = giá trị `current_value` đã được lưu trong Postgres, KHÔNG dùng `INCR` độc lập — điều này đảm bảo Redis là bản phản chiếu của Postgres thay vì source of truth độc lập.
7. IF Redis không khả dụng khi `ExecutePolicy` cố GET counter, THEN THE AiGovernanceService SHALL áp dụng fail-closed: trả về `PolicyDecision.allowed = false` với reason `INTERNAL_ERROR` và ghi log cảnh báo — KHÔNG fallback sang Postgres để đọc `usage_records` ở v1.
8. THE AiGovernanceService SHALL expose Micrometer gauge `governance.quota.counter{tenantId, quotaType, periodKey}` reflecting giá trị Redis hiện tại và counter `governance.quota.denied_total{reason}` để monitoring.
9. // TODO(v2): Implement Redis WATCH/MULTI/EXEC hoặc Lua script cho optimistic locking nếu race window cần được thu hẹp dưới 1 request.
10. // TODO(v2): Thêm circuit breaker với Resilience4j cho Redis — fallback sang Postgres read khi Redis down, thay vì fail-closed hoàn toàn.


---

### Yêu cầu 5: RabbitMQ — Exchanges, Queues, Routing Keys và Reliability

**User Story:** Là một developer, tôi muốn cấu hình RabbitMQ đầy đủ cho cả inbound `AiUsageEvent` consumer và outbound `AiPolicyDecisionEvent` publisher, với chiến lược retry/DLQ rõ ràng, để không có event nào bị mất khi hệ thống có lỗi thoáng qua.

#### Acceptance Criteria

1. THE AiGovernanceService SHALL khai báo inbound exchange `ai.usage.events` (type: `topic`, durable: true) và queue `ai-governance.usage-consumer` (durable: true, `x-dead-letter-exchange: ai.usage.events.dlx`) với routing key `ai.usage.#`; DLQ `ai-governance.usage-consumer.dlq` bind vào exchange `ai.usage.events.dlx`.
2. THE AiGovernanceService SHALL khai báo outbound exchange `ai.policy.decisions` (type: `topic`, durable: true) để publish `AiPolicyDecisionEvent`; routing key pattern: `ai.policy.decision.{tenantId}`.
3. WHEN publish `AiPolicyDecisionEvent`, THE AiGovernanceService SHALL sử dụng Spring AMQP `RabbitTemplate` với publisher confirms bật (`spring.rabbitmq.publisher-confirm-type=correlated`) và publisher returns bật (`spring.rabbitmq.publisher-returns=true`); message SHALL được đánh dấu `confirmed` chỉ sau khi nhận ACK từ broker.
4. WHEN `AiPolicyDecisionEvent` không nhận được ACK sau 5 giây, THE AiGovernanceService SHALL thực hiện retry tối đa 3 lần với exponential backoff (1s, 2s, 4s); nếu tất cả retry thất bại, THE service SHALL ghi log warning với `correlationId` và tiếp tục xử lý — publish audit là best-effort, KHÔNG block `ExecutePolicy` response.
5. WHEN `AiUsageEvent` consumer nhận được message, THE consumer SHALL xử lý trong transaction Postgres trước, đồng bộ Redis sau — nếu Postgres write thất bại, message SHALL được NACK và requeue; nếu Redis sync thất bại sau khi Postgres đã commit, THE consumer SHALL ghi log warning và KHÔNG rollback Postgres.
6. WHEN `AiUsageEvent` đã được requeue quá 3 lần (thông qua header `x-delivery-count`), THE AiGovernanceService consumer SHALL NACK message và để RabbitMQ chuyển sang DLQ `ai-governance.usage-consumer.dlq`; message trong DLQ SHALL được monitor và alert khi depth > 100.
7. THE `AiUsageEvent` message payload SHALL chứa: `tenantId`, `capabilityCode`, `tokensUsed`, `provider`, `requestId`, `occurredAt` (UTC ISO 8601); `tenantId` và `requestId` SHALL được dùng làm idempotency key để tránh duplicate usage write.
8. THE `AiPolicyDecisionEvent` message payload SHALL chứa: `tenantId`, `capabilityCode`, `allowed`, `provider`, `reason`, `requestId`, `decidedAt` (UTC ISO 8601); payload SHALL NOT chứa `estimated_tokens` hay bất kỳ thông tin nhạy cảm của request.
9. THE AiGovernanceService SHALL expose Micrometer counter `governance.rabbitmq.publish.confirmed_total` và `governance.rabbitmq.publish.failed_total` cho monitoring publisher confirms.
10. // TODO(v2): Implement Spring AMQP `MessageRecoverer` với scheduled DLQ replay job — hiện tại DLQ chỉ được monitor, không tự replay.


---

### Yêu cầu 6: ExecutePolicy — Luồng xử lý chi tiết và Error Handling

**User Story:** Là một developer implement AiGovernanceService, tôi muốn pseudocode chi tiết của `ExecutePolicy` với đầy đủ error handling và logging conventions, để tôi có thể implement đúng ngữ nghĩa nghiệp vụ mà không bỏ sót trường hợp biên.

#### Acceptance Criteria

1. WHEN `ExecutePolicy` nhận request, THE AiGovernanceService SHALL thực hiện 5 bước tuần tự sau (dừng ngay tại bước đầu tiên thất bại): (1) Load Tenant + Plan từ cache, (2) Kiểm tra Capability, (3) Kiểm tra CloudAiEnabled, (4) Kiểm tra Quota, (5) Trả về Allow + publish audit event.
2. WHEN Load Tenant + Plan (bước 1), THE AiGovernanceService SHALL sử dụng local in-process cache (Caffeine) với TTL 60 giây để tránh DB round-trip mỗi request; IF Tenant không tồn tại trong DB, THEN THE service SHALL trả về `allowed=false`, `reason=PLAN_NOT_FOUND` và ghi log `WARN` với `tenantId` (không log bất kỳ PII nào).
3. WHEN Capability check (bước 2), THE AiGovernanceService SHALL tra cứu `plan_capabilities` cho `(plan_id, capability_code)`; IF `enabled=false` hoặc record không tồn tại, THEN THE service SHALL trả về `allowed=false`, `reason=CAPABILITY_DISABLED`.
4. WHEN CloudAiEnabled check (bước 3), THE AiGovernanceService SHALL kiểm tra `tenant.cloud_ai_enabled`; IF `false`, THEN THE service SHALL trả về `allowed=false`, `reason=CLOUD_AI_DISABLED`.
5. WHEN Quota check (bước 4), THE AiGovernanceService SHALL: (a) tính `periodKey` từ thời điểm hiện tại (UTC), (b) GET Redis key `quota:{tenantId}:{quotaType}:{periodKey}`, (c) load `limit_value` từ `plan_quotas`, (d) so sánh `counter >= limit_value × 0.95`; IF vượt ngưỡng, THEN THE service SHALL trả về `allowed=false`, `reason=QUOTA_EXCEEDED`.
6. WHEN bước 5 (Allow), THE AiGovernanceService SHALL tính `requireApproval = (plan.automation_level == MANUAL || plan.automation_level == RULES_ONLY)` và trả về `PolicyDecision{allowed=true, provider=plan.provider, maxTokens=plan_quotas.limit_value, requireApproval, reason=""}`.
7. WHEN bất kỳ exception không mong muốn nào xảy ra trong `ExecutePolicy`, THE AiGovernanceService SHALL bắt exception, ghi log `ERROR` với stack trace (không có PII), và trả về `PolicyDecision{allowed=false, reason=INTERNAL_ERROR}` — TUYỆT ĐỐI KHÔNG để exception propagate thành gRPC `INTERNAL` error với stack trace.
8. WHEN ghi log trong `ExecutePolicy`, THE AiGovernanceService SHALL KHÔNG log: `estimated_tokens`, nội dung request body, bất kỳ thông tin nhạy cảm của tenant; CHỈ được log: `tenantId`, `capabilityCode`, `decision` (allowed/denied), `reason`, `durationMs`, `requestId`.
9. WHEN publish `AiPolicyDecisionEvent` sau Allow (bước 5), THE AiGovernanceService SHALL thực hiện publish bất đồng bộ, non-blocking trong separate virtual thread — thất bại publish KHÔNG được ảnh hưởng đến `PolicyDecision` response trả về client.
10. THE AiGovernanceService SHALL đo `governance.execute_policy.duration_ms` histogram và `governance.execute_policy.total{decision, reason}` counter cho mỗi `ExecutePolicy` call.


---

### Yêu cầu 7: Plan Management — CRUD và Tenant Provisioning

**User Story:** Là một system operator, tôi muốn quản lý Plan data và gán Plan cho Tenant thông qua các gRPC endpoints được bảo vệ, để tôi có thể onboard tenant mới và điều chỉnh capabilities mà không cần deploy lại service.

#### Acceptance Criteria

1. THE AiGovernanceService SHALL cung cấp `GetTenantPlan(TenantRequest) → PlanInfo` trả về plan hiện tại của tenant bao gồm: `plan_code`, `provider`, `automation_level`, `cloud_ai_default`.
2. THE AiGovernanceService SHALL cung cấp `GetCapabilities(TenantRequest) → CapabilityList` trả về danh sách tất cả capabilities và trạng thái `enabled/disabled` dựa trên plan của tenant.
3. WHEN một Tenant được tạo, THE AiGovernanceService SHALL gán `cloud_ai_enabled = plan.cloud_ai_default` làm giá trị mặc định; tenant có thể được update sau bởi system operator.
4. IF `tenant.status = 'SUSPENDED'` hoặc `'CANCELLED'`, THEN THE AiGovernanceService SHALL trả về `allowed=false`, `reason=PLAN_NOT_FOUND` trong `ExecutePolicy` — suspended tenant bị xử lý như tenant không tồn tại.
5. THE AiGovernanceService SHALL cache kết quả `GetTenantPlan` và `GetCapabilities` trong Caffeine local cache với TTL 60 giây và maximum 10,000 entries để giảm DB load.
6. WHEN cache miss xảy ra cho một `tenantId`, THE AiGovernanceService SHALL load Tenant + Plan + Capabilities trong một JPA query với `JOIN FETCH` để tránh N+1 queries.
7. // TODO(v2): Thêm gRPC admin endpoints `UpdateTenantCloudAi`, `AssignTenantPlan` được bảo vệ bởi SYSTEM_ADMIN role check.
8. // TODO(v2): Implement cache invalidation event qua RabbitMQ khi tenant plan thay đổi, thay vì chỉ dựa vào TTL expiry.


---

### Yêu cầu 8: AiUsageEvent Consumer — Ghi Usage và Đồng bộ Redis

**User Story:** Là một architect, tôi muốn AiUsageEvent consumer xử lý đúng thứ tự "Postgres trước, Redis sau" với idempotency, để không có token nào bị đếm hai lần và Redis luôn phản chiếu đúng giá trị Postgres.

#### Acceptance Criteria

1. THE AiGovernanceService SHALL lắng nghe `AiUsageEvent` từ queue `ai-governance.usage-consumer` và xử lý theo mô hình upsert: nếu `usage_records` record cho `(tenant_id, quota_type, period_key)` đã tồn tại thì cộng thêm `tokensUsed`; nếu chưa tồn tại thì INSERT với `current_value = tokensUsed`.
2. THE AiUsageEvent consumer SHALL đảm bảo idempotency bằng cách kiểm tra `requestId` trong bảng `processed_events(request_id, processed_at)` trước khi xử lý; IF `requestId` đã tồn tại, THEN consumer SHALL ACK message mà không thực hiện bất kỳ thay đổi nào.
3. WHEN ghi `usage_records` thành công trong Postgres, THE consumer SHALL thực hiện Redis SET: `SET quota:{tenantId}:{quotaType}:{periodKey} {newCurrentValue} EX {ttlSeconds}` trong đó `newCurrentValue` là giá trị `current_value` vừa commit trong Postgres — đây là sync operation, KHÔNG phải INCR.
4. WHEN tính `ttlSeconds` cho Redis SET, THE consumer SHALL tính số giây còn lại đến hết kỳ (UTC) cộng thêm 300 giây buffer; nếu `period = DAY` thì hết kỳ = 00:00:00 UTC ngày hôm sau; nếu `period = MONTH` thì hết kỳ = 00:00:00 UTC ngày đầu tiên của tháng sau.
5. IF Postgres write thất bại, THEN THE consumer SHALL NACK message với requeue=true; IF số lần delivery vượt quá 3 (header `x-delivery-count > 3`), THE consumer SHALL NACK với requeue=false để message vào DLQ.
6. IF Redis sync thất bại sau Postgres commit thành công, THEN THE consumer SHALL ghi log `WARN` với `tenantId`, `quotaType`, `periodKey`, tiếp tục ACK message — Redis inconsistency tự giải quyết khi có AiUsageEvent tiếp theo hoặc TTL expire.
7. THE consumer SHALL xử lý `AiUsageEvent` trong Spring AMQP `@RabbitListener` với `containerFactory` configured sử dụng virtual thread executor.
8. THE consumer SHALL ghi log structured với fields: `requestId`, `tenantId`, `capabilityCode`, `tokensUsed`, `newTotal`, `periodKey`, `redisSync` (success/failed) — KHÔNG log giá trị khác của tenant.


---

### Yêu cầu 9: Chiến lược kiểm thử

**User Story:** Là một developer, tôi muốn có chiến lược test toàn diện bao gồm unit test và integration test với Testcontainers, để các trường hợp deny, quota không bị leak, race window, và fail-closed đều được kiểm chứng tự động.

#### Acceptance Criteria

1. THE AiGovernanceService SHALL cung cấp unit tests cho tất cả deny cases trong `ExecutePolicy`: `PLAN_NOT_FOUND` (tenant không tồn tại), `CAPABILITY_DISABLED` (capability bị tắt), `CLOUD_AI_DISABLED` (tenant.cloud_ai_enabled=false), `QUOTA_EXCEEDED` (counter >= limit × 0.95), `INTERNAL_ERROR` (exception không mong muốn).
2. THE AiGovernanceService SHALL cung cấp integration test sử dụng Testcontainers với PostgreSQL và Redis chứng minh rằng: khi AI call thất bại sau khi `ExecutePolicy` trả về `allowed=true`, `AiUsageEvent` KHÔNG được publish và do đó Redis counter KHÔNG được increment — xác nhận "quota không bị leak".
3. THE AiGovernanceService SHALL cung cấp integration test mô phỏng race window: N concurrent `ExecutePolicy` calls khi counter nằm trong khoảng `[limit × 0.93, limit × 0.95]` — tất cả đều phải trả về `allowed=true` do Redis là read-only tại thời điểm check; test phải document số request có thể vượt qua trong kịch bản này.
4. THE AiGovernanceService SHALL cung cấp integration test fail-closed khi Redis down: dừng Redis container, gọi `ExecutePolicy` — phải trả về `allowed=false`, `reason=INTERNAL_ERROR`; khởi động lại Redis — service phải recover và cho phép request hợp lệ tiếp theo.
5. THE AiGovernanceService SHALL cung cấp integration test fail-closed khi Postgres down: dừng Postgres container, gọi `ExecutePolicy` với `tenantId` không có trong Caffeine cache — phải trả về `allowed=false`; Caffeine cache still warm → phải trả về quyết định đúng dựa trên cached data.
6. THE AiGovernanceService SHALL cung cấp integration test cho `AiUsageEvent` consumer idempotency: publish cùng một event với `requestId` giống nhau hai lần — `current_value` trong `usage_records` chỉ được tăng một lần.
7. THE AiGovernanceService SHALL cung cấp integration test cho `AiUsageEvent` consumer với Testcontainers RabbitMQ: sau khi consumer ghi Postgres thành công, Redis counter phải phản chiếu đúng giá trị mới trong vòng 1 giây.
8. THE AiGovernanceService SHALL cung cấp property-based test (JUnit 5 + jqwik hoặc equivalent) cho round-trip `periodKey` calculation: FOR ALL `Instant t` trong năm 2025, `calculatePeriodKey(t, DAY)` phải cho ra string đúng format `yyyy-MM-dd` và `calculateTtlSeconds(t, DAY)` phải cho ra giá trị trong range `(0, 86700]` (86400 + 300 buffer).
9. THE AiGovernanceService SHALL đạt tối thiểu 80% line coverage cho các classes trong package `domain` và `application`; coverage SHALL được đo bằng JaCoCo và báo cáo trong Maven build.
10. WHEN chạy integration tests với Testcontainers, THE test suite SHALL sử dụng `@Testcontainers` với reuse containers giữa các test methods trong cùng class để giảm overhead khởi tạo.


---

### Yêu cầu 10: Observability — Metrics, Logging và Health

**User Story:** Là một SRE, tôi muốn AiGovernanceService expose đầy đủ metrics Prometheus và structured logs, để tôi có thể monitor quota usage, policy decision rates, và RabbitMQ health mà không cần debug trực tiếp vào service.

#### Acceptance Criteria

1. THE AiGovernanceService SHALL expose Micrometer metrics tại `/actuator/prometheus` với các metrics bắt buộc: `governance.execute_policy.duration_ms` (histogram), `governance.execute_policy.total{decision, reason, capability_code}` (counter), `governance.quota.denied_total{tenant_id, reason}` (counter), `governance.rabbitmq.publish.confirmed_total` (counter), `governance.rabbitmq.publish.failed_total` (counter).
2. THE AiGovernanceService SHALL expose Spring Boot Actuator health endpoint tại `/actuator/health` với sub-indicators cho: PostgreSQL connectivity, Redis connectivity, RabbitMQ connectivity — mỗi indicator SHALL báo cáo `UP` hoặc `DOWN` độc lập.
3. WHEN ghi structured log, THE AiGovernanceService SHALL sử dụng logstash-logback-encoder để output JSON với các fields: `timestamp`, `level`, `logger`, `message`, `tenantId` (nếu có trong context), `requestId`, `durationMs`, `traceId` (từ Micrometer Tracing).
4. THE AiGovernanceService SHALL KHÔNG log bất kỳ PII nào — cụ thể: không log `estimated_tokens`, không log nội dung payload của `AiUsageEvent` ngoài `requestId` và `tenantId`, không log connection strings hay credentials.
5. THE AiGovernanceService SHALL tích hợp Micrometer Tracing với propagation header `traceparent` (W3C Trace Context) để distributed tracing qua các gRPC calls giữa agent services và AiGovernanceService.
6. WHEN DLQ depth của queue `ai-governance.usage-consumer.dlq` vượt quá 100 messages, THE AiGovernanceService SHALL tăng gauge metric `governance.dlq.depth{queue}` lên mức alert threshold để trigger Prometheus alerting rule bên ngoài service.


---

### Yêu cầu 11: V2 Extension Points và Backward Compatibility

**User Story:** Là một architect, tôi muốn v1 implementation có các extension points rõ ràng dưới dạng TODO comments và interface boundaries, để khi v2 features được phát triển không cần refactor lớn các class hiện có.

#### Acceptance Criteria

1. THE AiGovernanceService SHALL định nghĩa port interface `QuotaCheckPort` trong `application.ports.out` với methods: `long getCurrentCounter(String tenantId, String quotaType, String periodKey)` và `void syncCounter(String tenantId, String quotaType, String periodKey, long newValue, long ttlSeconds)` — v1 implementation dùng Redis; `// TODO(v2): thêm method reserveQuota() cho 2-phase reservation`.
2. THE AiGovernanceService SHALL định nghĩa port interface `PolicyAuditPort` trong `application.ports.out` với method `void publishDecision(AiPolicyDecisionEvent event)` — v1 implementation dùng RabbitMQ; `// TODO(v2): thêm publishDecisionWithRetry() với outbox pattern nếu audit reliability cần tăng`.
3. THE AiGovernanceService SHALL định nghĩa `ProviderRouter` interface với method `AiProvider selectProvider(Plan plan, String capabilityCode)` — v1 implementation trả về `plan.provider` tĩnh; `// TODO(v2): implement dynamic routing based on load/cost/SLA`.
4. THE AiGovernanceService SHALL định nghĩa `AutomationPolicyEvaluator` interface với method `boolean requiresApproval(AutomationLevel level, String capabilityCode)` — v1 implementation chỉ dùng plan-level `automation_level`; `// TODO(v2): per-capability automation overrides`.
5. WHEN proto file được thiết kế, THE AiGovernanceService SHALL dành reserved field numbers 10-19 trong `ExecutePolicyRequest` và 10-19 trong `PolicyDecision` để thêm fields v2 mà không breaking existing consumers; comment `// reserved for v2` SHALL được thêm vào proto.
6. THE AiGovernanceService SHALL cấu trúc Flyway migrations sao cho tất cả v1 schema nằm trong `V1__` và `V2__` — future migrations `V3__` trở đi có thể thêm columns/tables mà không sửa existing migrations.
7. IF một agent service gửi `ExecutePolicyRequest` với `capability_code` không tồn tại trong `plan_capabilities` của tenant's plan, THEN THE AiGovernanceService SHALL xử lý giống như `CAPABILITY_DISABLED` — forward-compatible với capabilities được thêm vào v2 chưa có trong plan hiện tại.
8. // TODO(v2): Implement `TenantOverridePolicy` table cho per-tenant quota overrides ngoài standard plan — `plan_quotas` là ceiling, tenant override là effective limit.
9. // TODO(v2): Implement `FeatureFlagPort` riêng biệt với `PlanCapabilityPort` để support runtime feature flags độc lập với plan definition.


---

### Yêu cầu 12: Bảo mật và Tenant Isolation

**User Story:** Là một security engineer, tôi muốn AiGovernanceService đảm bảo tenant isolation hoàn toàn và bảo vệ secrets đúng cách, để không có tenant nào có thể ảnh hưởng đến quota hay policy của tenant khác.

#### Acceptance Criteria

1. THE AiGovernanceService SHALL trích xuất `tenantId` từ gRPC metadata header `x-tenant-id` (được set bởi API Gateway sau khi xác thực JWT) và KHÔNG tin tưởng `tenant_id` field trong `ExecutePolicyRequest` body — nếu hai giá trị không khớp, THE service SHALL từ chối request với gRPC status `PERMISSION_DENIED`.
2. WHEN thực hiện bất kỳ DB query nào liên quan đến quota hay policy, THE AiGovernanceService SHALL luôn include `tenant_id` trong WHERE clause — TUYỆT ĐỐI KHÔNG có query nào lấy dữ liệu của tenant mà không filter theo `tenant_id`.
3. WHEN thực hiện Redis GET/SET, THE AiGovernanceService SHALL luôn sử dụng key đầy đủ `quota:{tenantId}:{quotaType}:{periodKey}` — KHÔNG có wildcard hay prefix-only operations.
4. THE AiGovernanceService SHALL lấy tất cả secrets (Redis password, PostgreSQL credentials, RabbitMQ credentials) từ Azure Key Vault thông qua Spring Cloud Azure Key Vault Secrets starter với Managed Identity — TUYỆT ĐỐI KHÔNG hardcode credentials trong application.yml hay container image.
5. THE AiGovernanceService SHALL enforce rằng mỗi `tenantId` trong `AiUsageEvent` phải tồn tại trong bảng `tenants` trước khi xử lý — nếu `tenantId` không hợp lệ, consumer SHALL NACK và ghi log `WARN`, không để message vào DLQ.
6. WHEN ghi log hay metrics, THE AiGovernanceService SHALL chỉ include `tenantId` dưới dạng opaque UUID — KHÔNG include tenant name, email, hay bất kỳ thông tin identifying khác.

