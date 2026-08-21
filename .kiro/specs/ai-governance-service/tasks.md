# Kế hoạch Triển khai — AiGovernanceService

## Tổng quan

Triển khai AiGovernanceService theo kiến trúc phân lớp (Domain / Application / Infrastructure / GrpcServices) với grpc-java thuần (không dùng net.devh), virtual threads (Java 21), Caffeine cache, Redis quota counter, RabbitMQ quorum queue, Flyway migration, và Testcontainers integration tests. Mọi task đều có thể thực hiện bởi coding agent.

---

## Tasks

- [ ] 1. Project Foundation — pom.xml, cấu trúc thư mục, entry point, cấu hình ứng dụng
  - [ ] 1.1 Tạo `pom.xml` với grpc-java thuần (không dùng net.devh)
    - Khai báo `<parent>` Spring Boot 3.3.2, Java 21
    - Thêm dependencies: spring-boot-starter-data-jpa, data-redis, amqp, actuator, cache
    - Thêm grpc-java thuần: `grpc-stub`, `grpc-protobuf`, `grpc-netty-shaded`, `grpc-services`
    - Thêm caffeine, postgresql, flyway-core, flyway-database-postgresql
    - Thêm micrometer-registry-prometheus, logstash-logback-encoder
    - Thêm spring-cloud-azure-starter-keyvault-secrets
    - Thêm test deps: spring-boot-starter-test, jqwik 1.8.4, testcontainers (postgres, rabbitmq), testcontainers-redis, grpc-testing
    - Cấu hình `protobuf-maven-plugin` với `<protoSourceRoot>${project.basedir}/../../protos</protoSourceRoot>`
    - Cấu hình `jacoco-maven-plugin` với threshold 80% line coverage cho packages Domain và Application
    - **KHÔNG** có `<grpc-starter.version>` property; **KHÔNG** có dependency `net.devh`
    - _Yêu cầu: 1.7_

  - [ ] 1.2 Tạo cấu trúc package và `AiGovernanceApplication.java`
    - Tạo đầy đủ cây thư mục:
      `com.aurora.aigovernance/{Domain/{Entity,Enums,ValueObject}, Application/{Commands,Queries,Services/ports}, Infrastructure/{Persistence,Cache,Messaging,Config}, GrpcServices}`
    - Tạo `AiGovernanceApplication.java` với `@SpringBootApplication`
    - _Yêu cầu: 1.1_

  - [ ] 1.3 Tạo `application.yml` skeleton và `logback-spring.xml`
    - Cấu hình `spring.threads.virtual.enabled=true`
    - Cấu hình HikariCP: `maximum-pool-size=50`, `minimum-idle=10`, `connection-timeout=3000`, `max-lifetime=1800000`, `validation-timeout=2000`, `leak-detection-threshold=30000`
    - Cấu hình `grpc.server.port=9090`
    - Cấu hình Flyway, JPA (ddl-auto=validate), Redis, RabbitMQ (publisher-confirm-type=correlated, publisher-returns=true)
    - Cấu hình Caffeine spec: `maximumSize=10000,expireAfterWrite=60s`
    - Tạo `logback-spring.xml` dùng `logstash-logback-encoder` cho structured JSON output
    - _Yêu cầu: 1.2, 10.3_


- [ ] 2. Proto Contract — định nghĩa gRPC API
  - [ ] 2.1 Tạo `root/protos/ai_governance.proto`
    - Package `aurora.aigovernance.v1`, `option java_package = "com.aurora.aigovernance.grpc"`, `option java_multiple_files = true`
    - Import `google/rpc/status.proto`
    - Định nghĩa service `AiGovernanceService` với 3 RPC: `ExecutePolicy`, `GetTenantPlan`, `GetCapabilities`
    - Định nghĩa `ExecutePolicyRequest`: `tenant_id=1`, `capability_code=2`, `estimated_tokens=3`; comment `// reserved 10 to 19`
    - Định nghĩa `PolicyDecision`: `allowed=1`, `provider=2`, `max_tokens=3`, `require_approval=4`, `reason=5`; comment `// reserved 10 to 19`
    - Định nghĩa `TenantRequest`, `PlanInfo`, `CapabilityList`, `CapabilityInfo`
    - _Yêu cầu: 2.1, 2.2, 2.3, 2.4, 2.5, 11.5_

  - [ ]* 2.2 Kiểm tra protobuf-maven-plugin compile thành công
    - Chạy `mvn generate-sources` — verify stubs được generate tại `target/generated-sources`
    - Xác nhận class `AiGovernanceServiceGrpc`, `ExecutePolicyRequest`, `PolicyDecision` tồn tại
    - _Yêu cầu: 1.7, 2.1_

- [ ] 3. Domain Model — Entities, Enums, Value Objects
  - [ ] 3.1 Tạo các Enum classes
    - `AutomationLevel.java`: `MANUAL, RULES_ONLY, RULES_AI, FULL_AUTOMATION`
    - `AiProvider.java`: `GEMINI, AZURE_OPENAI`
    - `QuotaPeriod.java`: `DAY, MONTH`
    - `TenantStatus.java`: `ACTIVE, SUSPENDED, CANCELLED`
    - `DenyReason.java`: `QUOTA_EXCEEDED, CAPABILITY_DISABLED, CLOUD_AI_DISABLED, PLAN_NOT_FOUND, TENANT_SUSPENDED, INTERNAL_ERROR`
    - _Yêu cầu: 1.1_

  - [ ] 3.2 Tạo `Plan.java` và `PlanCapability.java` entities
    - `Plan`: `@Entity @Table(name="plans")`, fields: `code`, `name`, `provider` (AiProvider), `automationLevel`, `cloudAiDefault`; `@OneToMany` tới capabilities và quotas
    - `PlanCapability`: `@Entity @IdClass(PlanCapabilityId)`, fields: `plan` (FK), `capabilityCode`, `enabled`, `maxTokensPerCall`
    - `PlanCapabilityId`: `record(UUID plan, String capabilityCode) implements Serializable`
    - _Yêu cầu: 1.5, 3.2, 3.3_

  - [ ] 3.3 Tạo `PlanQuota.java`, `Tenant.java`, `UsageRecord.java`, `ProcessedEvent.java` entities
    - `PlanQuota`: `@Entity @IdClass(PlanQuotaId)`, fields: `plan`, `quotaType`, `period` (QuotaPeriod), `limitValue`
    - `PlanQuotaId`: `record(UUID plan, String quotaType, QuotaPeriod period) implements Serializable`
    - `Tenant`: `@Entity`, fields: `plan` (FK ManyToOne), `cloudAiEnabled`, `status` (TenantStatus)
    - `UsageRecord`: `@Entity @UniqueConstraint(tenant_id,quota_type,period_key)`, fields: `tenantId`, `quotaType`, `periodKey`, `currentValue`
    - `ProcessedEvent`: `@Entity @Table(name="processed_events")`, PK: `requestId` (String), `processedAt` (Instant)
    - _Yêu cầu: 1.5, 3.5, 3.6_

  - [ ] 3.4 Tạo Value Objects
    - `PolicyDecision.java`: record với `allowed, provider, maxTokens, requireApproval, reason`; static factory `deny(DenyReason)` và `allow(AiProvider, int, boolean)`
    - `TenantPlanContext.java`: record với `tenantId, status, cloudAiEnabled, planCode, provider, automationLevel, enabledCapabilities (Set<String>), quotaLimits (Map<PlanQuotaId,Long>), maxTokensPerCall (Map<String,Integer>)`; method `maxTokensFor(String capabilityCode)` với fallback 1000
    - `QuotaKey.java`: record với `tenantId, quotaType, periodKey`; method `toRedisKey()` → `"quota:{tenantId}:{quotaType}:{periodKey}"`
    - `PeriodKey.java`: record với `value, period`; static factories `forDay(Instant)` và `forMonth(Instant)`
    - _Yêu cầu: 4.1, 6.6_


- [ ] 4. Application Layer — Commands, Queries, Port Interfaces, Strategy Interfaces
  - [ ] 4.1 Tạo Commands và Queries
    - `ReportUsageCommand.java`: record với `requestId, tenantId (UUID), capabilityCode, tokensUsed (long)`
    - `ExecutePolicyQuery.java`: record với `tenantId (String), capabilityCode (String), estimatedTokens (int)`
    - `GetTenantPlanQuery.java`: record với `tenantId (String)`
    - `GetCapabilitiesQuery.java`: record với `tenantId (String)`
    - _Yêu cầu: 1.3_

  - [ ] 4.2 Tạo Port Interfaces trong `application/Services/ports/`
    - `QuotaCheckPort.java`: interface với `long getCurrentCounter(String, String, String)` và `void syncCounter(String, String, String, long, long)`; comment `// TODO(v2): long reserveQuota(...)`
    - `PolicyAuditPort.java`: interface với `void publishDecision(AiPolicyDecisionEventMessage event)`; comment `// TODO(v2): publishDecisionWithRetry — outbox pattern`
    - _Yêu cầu: 1.4, 11.1, 11.2_

  - [ ] 4.3 Tạo Strategy Interfaces trong `application/Services/`
    - `ProviderRouter.java`: interface với `AiProvider selectProvider(Plan plan, String capabilityCode)`; comment `// TODO(v2): dynamic routing`
    - `AutomationPolicyEvaluator.java`: interface với `boolean requiresApproval(AutomationLevel level, String capabilityCode)`; comment `// TODO(v2): per-capability overrides`
    - _Yêu cầu: 11.3, 11.4_

  - [ ] 4.4 Tạo `PeriodKeyCalculator.java`
    - `@Component`, method `dayKey(Instant now)` → `String yyyy-MM-dd` (UTC)
    - Method `monthKey(Instant now)` → `String yyyy-MM` (UTC)
    - Method `calculateTtlSeconds(Instant now, QuotaPeriod period)` → `long` giây còn lại đến hết kỳ + 300s buffer
      - DAY: end = ngày hôm sau 00:00:00 UTC
      - MONTH: end = ngày đầu tháng sau 00:00:00 UTC
    - _Yêu cầu: 4.2, 8.4, 9.8_

- [ ] 5. Flyway Migrations
  - [ ] 5.1 Tạo `V1__create_ai_governance_schema.sql`
    - Tạo bảng `plans` với đầy đủ constraints (CHECK provider, CHECK automation_level)
    - Tạo bảng `plan_capabilities` với `max_tokens_per_call INTEGER NOT NULL DEFAULT 4000 CHECK (max_tokens_per_call > 0)`
    - Tạo bảng `plan_quotas` với CHECK `limit_value >= 0`, CHECK period IN ('DAY','MONTH')
    - Tạo bảng `tenants` với CHECK status, indexes trên `plan_id` và `status`
    - Tạo bảng `usage_records` với UNIQUE `(tenant_id, quota_type, period_key)`, index lookup
    - Tạo bảng `processed_events` với index trên `processed_at` (hỗ trợ cleanup job)
    - _Yêu cầu: 3.1, 3.2, 3.3, 3.4, 3.5, 3.6, 3.10_

  - [ ] 5.2 Tạo `V2__seed_plans_and_capabilities.sql`
    - INSERT 3 plans: FREE (GEMINI, MANUAL, cloud_ai_default=false), STANDARD (GEMINI, RULES_AI, true), ENTERPRISE (AZURE_OPENAI, FULL_AUTOMATION, true)
    - INSERT `plan_capabilities` với cột `max_tokens_per_call`: FREE=1000, STANDARD=4000, ENTERPRISE=8000
    - INSERT `plan_quotas`: FREE=0 tokens, STANDARD=500K/day+10M/month, ENTERPRISE=5M/day+100M/month
    - _Yêu cầu: 3.7, 3.8, 3.9_

- [ ] 6. Infrastructure — Persistence (JPA Repositories)
  - [ ] 6.1 Tạo 6 JpaRepository interfaces
    - `PlanJpaRepository extends JpaRepository<Plan, UUID>`
    - `PlanCapabilityJpaRepository extends JpaRepository<PlanCapability, PlanCapabilityId>`; comment `// TODO(v2): unknown capability_code → treat as CAPABILITY_DISABLED`
    - `PlanQuotaJpaRepository extends JpaRepository<PlanQuota, PlanQuotaId>`
    - `TenantJpaRepository extends JpaRepository<Tenant, UUID>` — tránh N+1 nhưng KHÔNG JOIN FETCH 2 collection cùng lúc (MultipleBagFetchException / Cartesian product):
      - Query 1: `@Query("SELECT t FROM Tenant t JOIN FETCH t.plan p JOIN FETCH p.capabilities WHERE t.id = :tenantId")`
      - Query 2: `@Query("SELECT t FROM Tenant t JOIN FETCH t.plan p JOIN FETCH p.quotas WHERE t.id = :tenantId")`
      - `TenantCacheService` gọi cả 2 query, merge kết quả trong memory (Hibernate persistence context tự merge nếu cùng transaction)
    - `UsageRecordJpaRepository extends JpaRepository<UsageRecord, UUID>` với `findByTenantIdAndQuotaTypeAndPeriodKey`
    - `ProcessedEventJpaRepository extends JpaRepository<ProcessedEvent, String>`
    - _Yêu cầu: 1.5, 7.6_


- [ ] 7. Infrastructure — Cache (Caffeine + Redis)
  - [ ] 7.1 Tạo `CacheConfig.java`
    - `@Configuration @EnableCaching`
    - Khai báo `CaffeineCacheManager` với cache name `tenantContext`
    - Spec: `maximumSize=10000, expireAfterWrite=60s`
    - _Yêu cầu: 7.5_

  - [ ] 7.2 Tạo `TenantCacheService.java`
    - `@Service`, method `getTenantContext(String tenantId)` → `TenantPlanContext`
    - `@Cacheable(value="tenantContext", key="#tenantId")` — cache miss gọi `TenantJpaRepository` với JOIN FETCH query
    - Build `TenantPlanContext`: pre-compute `enabledCapabilities` (Set), `quotaLimits` (Map), `maxTokensPerCall` (Map từ `PlanCapability.maxTokensPerCall`)
    - Trả về `null` nếu tenant không tồn tại (caller xử lý null → PLAN_NOT_FOUND)
    - Comment `// TODO(v2): Cache invalidation event via RabbitMQ khi tenant plan thay đổi`
    - _Yêu cầu: 6.6, 7.5, 7.6_

  - [ ] 7.3 Tạo `QuotaRedisAdapter.java` (implements `QuotaCheckPort`)
    - `@Component`, inject `StringRedisTemplate`
    - `getCurrentCounter`: `GET quota:{tenantId}:{quotaType}:{periodKey}` — trả về 0 nếu key không tồn tại; throw exception (không wrap) nếu Redis không khả dụng
    - `syncCounter`: `SET quota:{key} {newValue} EX {ttlSeconds}` — KHÔNG dùng INCR
    - Comment `// TODO(v2): Resilience4j circuit breaker — fallback sang Postgres khi Redis down`
    - _Yêu cầu: 4.3, 4.6, 4.7, 11.1_

- [ ] 8. Infrastructure — gRPC Server (grpc-java thuần)
  - [ ] 8.1 Tạo `GrpcTenantInterceptor.java`
    - `@Component implements ServerInterceptor`
    - Trích xuất header `x-tenant-id` từ `Metadata`
    - Trả về `Status.UNAUTHENTICATED` nếu header null hoặc blank
    - Lưu tenantId vào `io.grpc.Context` qua `GrpcTenantContext.TENANT_ID_CONTEXT_KEY`
    - Tạo `GrpcTenantContext.java` với static `Context.Key<String> TENANT_ID_CONTEXT_KEY` và `getCurrentTenantId()`
    - _Yêu cầu: 1.6, 12.1_

  - [ ] 8.2 Tạo `GrpcServerLifecycle.java`
    - `@Component implements SmartLifecycle`
    - `@Value("${grpc.server.port:9090}")` inject port
    - `start()`: dùng `ServerBuilder.forPort(port)`, `addService(policyGrpcService)`, `addService(tenantInfoGrpcService)`, `addService(new HealthStatusManager().getHealthService())` (grpc.health.v1), `intercept(tenantInterceptor)`, `executor(Executors.newVirtualThreadPerTaskExecutor())`
    - `stop()`: `server.shutdown()`, `awaitTermination(30, SECONDS)`, fallback `shutdownNow()`
    - `getPhase()` trả về `Integer.MAX_VALUE`
    - _Yêu cầu: 1.2, 2.8_

- [ ] 9. Application Services — PolicyDecisionService
  - [ ] 9.1 Implement `AutomationPolicyEvaluatorImpl.java` (v1) và `ProviderRouterImpl.java` (v1)
    - `AutomationPolicyEvaluatorImpl`: `requiresApproval` trả về `true` khi `level == MANUAL || level == RULES_ONLY`; `false` khi `RULES_AI || FULL_AUTOMATION`
    - `ProviderRouterImpl`: `selectProvider` trả về `plan.provider()` tĩnh; comment `// TODO(v2): dynamic routing`
    - _Yêu cầu: 11.3, 11.4_

  - [ ] 9.2 Implement `PolicyDecisionService.java`
    - Inject: `TenantCacheService`, `QuotaCheckPort`, `PolicyAuditPort`, `PeriodKeyCalculator`, `AutomationPolicyEvaluator`, `ProviderRouter`, `MeterRegistry`
    - Implement 5 bước tuần tự (dừng tại bước fail đầu tiên):
      1. Load `TenantPlanContext` từ cache; null → `PLAN_NOT_FOUND`; status == SUSPENDED → `TENANT_SUSPENDED`; status != ACTIVE → `PLAN_NOT_FOUND`
      2. `ctx.enabledCapabilities().contains(capabilityCode)` → `CAPABILITY_DISABLED`
      3. `ctx.cloudAiEnabled()` → `CLOUD_AI_DISABLED`
      4. GET Redis counter; `counter >= dayLimit * 0.95` → `QUOTA_EXCEEDED`; Redis exception → `INTERNAL_ERROR`; comment quota pool chung
      5. Allow: `maxTokens = ctx.maxTokensFor(capabilityCode)` — KHÔNG dùng dayLimit; publish audit async trong virtual thread
    - Logging: CHỈ log `tenantId, capabilityCode, decision, reason, durationMs, requestId` — KHÔNG log `estimated_tokens`, PII
    - Catch-all: `catch(Exception e)` → log ERROR + trả về `INTERNAL_ERROR`
    - Micrometer: `governance.execute_policy.duration_ms` histogram, `governance.execute_policy.total{decision,reason,capability_code}` counter
    - Comment `// TODO(v2): Tách quota_type riêng theo từng capability`
    - _Yêu cầu: 6.1, 6.2, 6.3, 6.4, 6.5, 6.6, 6.7, 6.8, 6.9, 6.10_

  - [ ] 9.3 Implement `QuotaSyncService.java`
    - `@Service`, method `@Transactional processUsage(ReportUsageCommand)`
    - Kiểm tra `processedEventRepository.existsById(requestId)` → idempotent skip
    - Upsert `usage_records` bằng atomic SQL (KHÔNG dùng JPA read-modify-write vì nguy cơ lost update khi nhiều thread song song):
      ```sql
      INSERT INTO usage_records (id, tenant_id, quota_type, period_key, current_value)
      VALUES (:id, :tenantId, :quotaType, :periodKey, :tokensUsed)
      ON CONFLICT (tenant_id, quota_type, period_key)
      DO UPDATE SET current_value = usage_records.current_value + EXCLUDED.current_value
      ```
      Dùng `@Modifying @Query(nativeQuery=true)` trên `UsageRecordJpaRepository`
    - Save `ProcessedEvent(requestId, Instant.now())`
    - Sau COMMIT: gọi `quotaCheckPort.syncCounter(...)` với TTL tính từ `PeriodKeyCalculator`
    - Redis sync fail: log WARN, không rollback
    - Structured log: `requestId, tenantId, capabilityCode, tokensUsed, newTotal, periodKey, redisSync`
    - _Yêu cầu: 8.1, 8.2, 8.3, 8.4, 8.6, 8.8_


- [ ] 10. GrpcServices Layer — PolicyGrpcService và TenantInfoGrpcService
  - [ ] 10.1 Implement `PolicyGrpcService.java`
    - `@Component` (KHÔNG có `@GrpcService` — GrpcServerLifecycle tự inject)
    - Extends `AiGovernanceServiceGrpc.AiGovernanceServiceImplBase`
    - `executePolicy`: (1) lấy `metaTenantId` từ `GrpcTenantContext`; (2) verify `request.getTenantId().equals(metaTenantId)` → `PERMISSION_DENIED`; (3) delegate `policyDecisionService.execute(new ExecutePolicyQuery(...))`; (4) map `PolicyDecision` domain → proto message; (5) outer catch → `allowed=false, reason=INTERNAL_ERROR` (không leak stack trace)
    - `getTenantPlan`: gọi `tenantCacheService.getTenantContext(tenantId)`, map sang `PlanInfo` proto
    - `getCapabilities`: gọi `tenantCacheService.getTenantContext(tenantId)`, map capabilities sang `CapabilityList` proto
    - Comment `// TODO(v2): Admin endpoints với SYSTEM_ADMIN role check`
    - _Yêu cầu: 1.6, 2.7, 6.7, 7.1, 7.2, 12.1_

  - [ ] 10.2 Implement `TenantInfoGrpcService.java`
    - `@Component`, extends `AiGovernanceServiceGrpc.AiGovernanceServiceImplBase` hoặc định nghĩa service riêng
    - Hỗ trợ `GetTenantPlan` và `GetCapabilities` với logic tương tự trong `PolicyGrpcService`
    - _Yêu cầu: 7.1, 7.2_

- [ ] 11. Infrastructure — RabbitMQ Publisher
  - [ ] 11.1 Tạo `RabbitMqConfig.java`
    - Khai báo constants: `USAGE_EXCHANGE, USAGE_QUEUE, USAGE_DLX, USAGE_DLQ, USAGE_ROUTING_KEY, DECISION_EXCHANGE`
    - Beans: `TopicExchange usageExchange()`, `FanoutExchange usageDlx()`
    - `Queue usageQueue()`: `QueueBuilder.durable(...).quorum().withArgument("x-dead-letter-exchange", USAGE_DLX).build()` — quorum queue bắt buộc để có header `x-delivery-count`
    - `Queue usageDlq()`: durable, không quorum
    - `Binding usageBinding()`, `Binding dlqBinding()`
    - `TopicExchange decisionExchange()`
    - `SimpleRabbitListenerContainerFactory virtualThreadListenerContainerFactory(...)`: dùng `SimpleAsyncTaskExecutor` với virtual threads
    - _Yêu cầu: 5.1, 5.2, 8.7_

  - [ ] 11.2 Tạo Message POJOs
    - `AiUsageEventMessage.java`: record với `requestId, tenantId, capabilityCode, tokensUsed, provider, occurredAt`
    - `AiPolicyDecisionEventMessage.java`: record với `requestId, tenantId, capabilityCode, allowed, provider, reason, decidedAt` — KHÔNG có `estimated_tokens`
    - _Yêu cầu: 5.7, 5.8_

  - [ ] 11.3 Implement `AiPolicyDecisionPublisher.java` (implements `PolicyAuditPort`)
    - `@Component`, inject `RabbitTemplate`
    - Cấu hình `RabbitTemplate` với `ConfirmCallback` và `ReturnsCallback`
    - `publishDecision`: publish tới `ai.policy.decisions`, routing key `ai.policy.decision.{tenantId}`
    - Retry logic: tối đa 3 lần với exponential backoff (1s, 2s, 4s) khi không nhận ACK sau 5s — KHÔNG dùng Thread.sleep trên ConfirmCallback (sẽ block IO thread của RabbitMQ client):
      - Dùng `Executors.newVirtualThreadPerTaskExecutor()` để schedule retry trong virtual thread riêng
      - Hoặc dùng `CompletableFuture.delayedExecutor(delay, TimeUnit.MILLISECONDS, virtualThreadExecutor)`
    - Sau 3 retry thất bại: log WARN với `correlationId` — không block `ExecutePolicy`
    - Micrometer: `governance.rabbitmq.publish.confirmed_total` counter và `governance.rabbitmq.publish.failed_total` counter
    - Comment `// TODO(v2): publishDecisionWithRetry — outbox pattern`
    - _Yêu cầu: 5.3, 5.4, 5.8, 5.9, 11.2_

- [ ] 12. Infrastructure — AiUsageEvent Consumer và HikariCP Config
  - [ ] 12.1 Tạo `HikariConfig.java`
    - `@Configuration`, override HikariCP bean với `maximum-pool-size=50`, `minimum-idle=10`, `connection-timeout=3000`, `max-lifetime=1800000`, `validation-timeout=2000`, `leak-detection-threshold=30000`
    - Comment giải thích tại sao `connection-timeout=3000` (fail nhanh, tránh pin virtual thread)
    - _Yêu cầu: 1.2_

  - [ ] 12.2 Implement `AiUsageEventConsumer.java`
    - `@Component`, `@RabbitListener(queues="ai-governance.usage-consumer", containerFactory="virtualThreadListenerContainerFactory")`
    - Parse `x-delivery-count` header từ `MessageProperties`
    - Validate tenant tồn tại trong `tenants` table — NACK (requeue=false, không DLQ) nếu không hợp lệ
    - Kiểm tra idempotency qua `ProcessedEventJpaRepository.existsById(requestId)` — ACK nếu đã xử lý
    - Gọi `quotaSyncService.processUsage(new ReportUsageCommand(...))` trong try/catch
    - Postgres success → `basicAck`
    - Postgres fail + `deliveryCount <= 3` → `basicNack(requeue=true)`
    - Postgres fail + `deliveryCount > 3` → `basicNack(requeue=false)` → DLQ; log ERROR
    - _Yêu cầu: 8.1, 8.2, 8.5, 8.7, 12.5_


- [ ] 13. Checkpoint — Build và kiểm thử toàn bộ code đã viết
  - Đảm bảo `mvn compile` thành công
  - Đảm bảo Flyway migrations chạy đúng (validate với in-memory PostgreSQL nếu có)
  - Đảm bảo gRPC stubs được generate thành công
  - Hỏi người dùng nếu có vấn đề cần làm rõ trước khi tiếp tục viết tests

- [ ] 14. Observability — Metrics, Health Indicators, DLQ Gauge
  - [ ] 14.1 Wire Micrometer metrics trong các service
    - `PolicyDecisionService`: `governance.execute_policy.duration_ms` (Timer/histogram), `governance.execute_policy.total{decision, reason, capability_code}` (Counter)
    - `QuotaRedisAdapter`: `governance.quota.counter{tenantId, quotaType, periodKey}` (Gauge phản chiếu Redis value), `governance.quota.denied_total{reason}` (Counter)
    - `AiPolicyDecisionPublisher`: `governance.rabbitmq.publish.confirmed_total`, `governance.rabbitmq.publish.failed_total`
    - _Yêu cầu: 4.8, 6.10, 10.1_

  - [ ] 14.2 Implement Custom Health Indicators và DLQ gauge
    - Tạo `DlqDepthHealthIndicator.java` (hoặc dùng RabbitMQ management API): gauge metric `governance.dlq.depth{queue}` — giá trị = số message trong DLQ
    - Cấu hình `management.endpoint.health.show-details=always`, bật sub-indicators cho PostgreSQL, Redis, RabbitMQ
    - _Yêu cầu: 10.2, 10.6_

- [ ] 15. Unit Tests — PolicyDecisionService, PeriodKeyCalculator, QuotaRedisAdapter, AutomationPolicyEvaluator
  - [ ] 15.1 Implement `PolicyDecisionServiceTest.java` (JUnit 5 + Mockito)
    - 10 test cases:
      1. `executePolicyWhenTenantNotFound_returnsDenyPlanNotFound`
      2. `executePolicyWhenTenantSuspended_returnsDenyTenantSuspended`
      3. `executePolicyWhenCapabilityDisabled_returnsDenyCapabilityDisabled`
      4. `executePolicyWhenCloudAiDisabled_returnsDenyCloudAiDisabled`
      5. `executePolicyWhenQuotaExceeded_returnsDenyQuotaExceeded` (counter >= limit * 0.95)
      6. `executePolicyWhenRedisThrowsException_returnsDenyInternalError`
      7. `executePolicyWhenAllConditionsPass_returnsAllow`
      8. `executePolicyWhenManualPlan_requiresApproval`
      9. `executePolicyWhenFullAutomation_doesNotRequireApproval`
      10. `executePolicyWhenMultipleFailConditions_returnsFirstFailReason`
    - _Yêu cầu: 9.1_

  - [ ]* 15.2 Implement `PeriodKeyCalculatorPropertyTest.java` (jqwik)
    - **Property 2**: `dayTtlAlwaysInValidRange` — TTL DAY ∈ (300, 86700] với 1000 tries
    - **Property 3**: `monthTtlAlwaysInValidRange` — TTL MONTH ∈ (300, 2678700]
    - **Property 4**: `dayKeyAlwaysValidFormat` — format `yyyy-MM-dd`, parseable
    - **Property 5**: `quotaDenyWhenCounterAtOrAboveThreshold` — `counter >= limit * 0.95` → deny
    - `@Provide Arbitrary<Instant> instantsIn2025()`: range `[2025-01-01, 2025-12-31]`
    - _Yêu cầu: 9.3, 9.8_

  - [ ]* 15.3 Implement `QuotaRedisAdapterTest.java` và `AutomationPolicyEvaluatorTest.java`
    - `QuotaRedisAdapterTest`: mock `StringRedisTemplate`; verify GET returns 0 khi key null; verify SET gọi đúng với TTL
    - `AutomationPolicyEvaluatorTest`: verify MANUAL→true, RULES_ONLY→true, RULES_AI→false, FULL_AUTOMATION→false (validates Property 6)
    - _Yêu cầu: 9.1_

- [ ] 16. Integration Tests — Testcontainers
  - [ ] 16.1 Implement `ExecutePolicyIntegrationTest.java`
    - `@Testcontainers @SpringBootTest`, reuse `PostgreSQLContainer` và `RedisContainer` trong cùng class
    - Test `quotaNotLeakedWhenAiCallFails`: ExecutePolicy→allowed; không publish AiUsageEvent; Redis counter vẫn = 0 sau 2s
    - Test `raceWindow_allRequestsPassWhenCounterBelowThreshold`: counter = limit×0.93; 10 concurrent calls → tất cả allowed=true; document race window
    - Test `failClosedWhenRedisDown`: stop Redis; ExecutePolicy → `allowed=false, INTERNAL_ERROR`; restart Redis; valid request → allowed=true
    - Test `failClosedWhenPostgresDownAndCacheMiss`: evict Caffeine; stop Postgres; ExecutePolicy → allowed=false
    - Test `cacheWarmWhenPostgresDown_returnsCorrectDecision`: warm cache; stop Postgres; ExecutePolicy → đúng decision từ cache
    - _Yêu cầu: 9.2, 9.3, 9.4, 9.5, 9.10_

  - [ ] 16.2 Implement `AiUsageEventConsumerTest.java`
    - `@Testcontainers`, containers: `PostgreSQLContainer`, `RedisContainer`, `RabbitMQContainer`
    - Test `consumerIdempotency_sameRequestIdTwice_onlyIncrementsOnce`: publish event 2 lần cùng `requestId`; assert `current_value` tăng đúng 1 lần
    - Test `consumerUpdatesRedisAfterPostgresCommit`: publish event; wait max 1s; assert Redis GET = đúng giá trị
    - _Yêu cầu: 9.6, 9.7, 9.10_

  - [ ] 16.3 Xác nhận JaCoCo coverage >= 80% cho Domain và Application packages
    - Chạy `mvn test jacoco:check` — đảm bảo build không fail vì coverage
    - Nếu coverage thiếu: thêm unit tests bổ sung cho các class chưa được cover
    - _Yêu cầu: 9.9_

- [ ] 17. Final Checkpoint — Toàn bộ tests pass, build thành công
  - Chạy `mvn clean verify` — tất cả unit tests và integration tests phải pass
  - JaCoCo report tại `target/site/jacoco/index.html`
  - Hỏi người dùng nếu có vấn đề cần làm rõ


---

## Ghi chú

- Task con đánh dấu `*` là tùy chọn — có thể bỏ qua để MVP nhanh hơn, nhưng khuyến nghị giữ lại để đảm bảo correctness
- Mỗi task tham chiếu yêu cầu cụ thể (Req X.Y) để truy xuất ngược
- Checkpoint tại task 13 và 17 đảm bảo validation tăng dần
- **grpc-java thuần**: không có `@GrpcService` annotation — `GrpcServerLifecycle` tự `addService()` khi Spring context khởi động
- **Quorum queue bắt buộc**: `x-delivery-count` header chỉ tồn tại trên quorum queue — classic queue không có header này, retry logic sẽ không hoạt động
- **maxTokens trong PolicyDecision**: lấy từ `ctx.maxTokensFor(capabilityCode)` (per-call limit), KHÔNG phải `dayLimit` (period ceiling)

---

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1", "1.2"] },
    { "id": 1, "tasks": ["1.3", "2.1", "3.1"] },
    { "id": 2, "tasks": ["2.2", "3.2", "3.3", "3.4", "11.2"] },
    { "id": 3, "tasks": ["4.1", "4.2", "4.3", "4.4", "5.1"] },
    { "id": 4, "tasks": ["5.2", "6.1"] },
    { "id": 5, "tasks": ["7.1", "7.2", "7.3", "8.1"] },
    { "id": 6, "tasks": ["9.1", "11.1", "12.1"] },
    { "id": 7, "tasks": ["9.2", "9.3", "11.3"] },
    { "id": 8, "tasks": ["10.1", "10.2", "12.2"] },
    { "id": 9, "tasks": ["8.2"] },
    { "id": 10, "tasks": ["13"] },
    { "id": 11, "tasks": ["14.1", "14.2"] },
    { "id": 12, "tasks": ["15.1", "15.2", "15.3"] },
    { "id": 13, "tasks": ["16.1", "16.2"] },
    { "id": 14, "tasks": ["16.3"] },
    { "id": 15, "tasks": ["17"] }
  ]
}
```
