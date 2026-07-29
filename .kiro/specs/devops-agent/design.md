# Design Document — DevOps-Agent Service

## Tổng quan (Overview)

DevOps-Agent là một autonomous .NET microservice triển khai trên AKS, chịu trách nhiệm tự động hóa toàn bộ vòng đời xử lý sự cố hạ tầng — từ ingestion alert, phân loại severity, auto-remediation, RCA với LLM+RAG, quản lý approval workflow, đến audit trail.

Service hoạt động hoàn toàn độc lập với các tenant-facing service khác. Khi các downstream services không khả dụng, event ingestion và DLQ persistence vẫn tiếp tục bình thường.

### Vị trí trong hệ thống

```
Azure Monitor / Loki alertmanager
        │
        ▼ webhook / alertmanager payload
┌───────────────────────────────────────────────────────┐
│                    DevOps-Agent (.NET / AKS)          │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐ │
│  │ Ingestion &  │  │ Rule Engine  │  │ RCA Pipeline │ │
│  │ Dedup Layer  │  │ (2-layer)    │  │ (LLM + RAG)  │ │
│  └──────────────┘  └──────────────┘  └──────────────┘ │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐ │
│  │ Approval     │  │ Notification │  │ Audit Outbox │ │
│  │ State Mach.  │  │ Dispatcher   │  │ Worker       │ │
│  └──────────────┘  └──────────────┘  └──────────────┘ │
│  ┌──────────────┐  ┌──────────────┐                   │
│  │ Self-Config  │  │ Self-Monitor │                   │
│  │ Manager      │  │ & Resilience │                   │
│  └──────────────┘  └──────────────┘                   │
└───────────────────────────────────────────────────────┘
        │                   │                   │
        ▼                   ▼                   ▼
   RabbitMQ DLQ        RAG_Service          AuditLog
   (event queue)       (gRPC)               Service
        │                   │                   │
        ▼                   ▼                   ▼
     Redis             Cloudflare R2        PostgreSQL
     (dedup TTL)       (artifacts)          (local DB)
```


## Kiến trúc (Architecture)

### Các component nội bộ

| Component | Trách nhiệm |
|---|---|
| **IngestionController** | REST endpoint nhận webhook từ Azure Monitor và alertmanager từ Loki |
| **DedupService** | Tính SHA-256 Dedup_Key, kiểm tra Redis, gộp Events vào Incident với `correlation_id` |
| **SeverityClassifier** | Phân loại severity (Low/Medium/High/Critical) từ alert metadata |
| **RoutingDispatcher** | Điều phối Incident tới Rule Engine hoặc RCA Pipeline |
| **RuleEngineService** | Quản lý Existing_Rules (in-memory cache + DB), match và execute remediation |
| **UnknownIssueHandler** | Xử lý Low incidents không match rule — invoke RAG + LLM, tạo Pending_Rule |
| **RcaPipelineService** | RCA cho Medium/High/Critical — collect logs, redact PII, invoke LLM, tạo PR/artifact |
| **PiiRedactor** | 2-layer redaction: Azure AI Language PII Detection → custom regex/whitelist |
| **LlmAdapterFactory** | Resolve `ILlmAdapter` implementation từ Self_Config.model_provider |
| **ApprovalStateMachine** | Quản lý approval states riêng cho PR_Approval và Rule_Approval |
| **ApprovalScheduler** | Bull Queue job để xử lý timeout và escalation |
| **NotificationDispatcher** | Route notifications tới Email (SES+Stalwart), Dashboard, Telegram |
| **AuditOutboxWorker** | Poll `audit_event_outbox`, publish RabbitMQ, mark PUBLISHED trên delivery confirm |
| **SelfConfigManager** | Hot-reload Self_Config từ DB khi nhận `config.updated` event |
| **ArtifactStorageService** | Upload artifacts tới Cloudflare R2 qua Cloudflare Tunnel |
| **RagGrpcClient** | gRPC client wrapper cho RAG_Service (query + ingest) |
| **SelfMonitorService** | Expose `/health`, `/metrics`; giám sát DLQ depth, Redis connectivity |
| **DlqReprocessor** | Consume DLQ sau khi recover, FIFO, max 10 concurrent |
| **AntiFlappingTracker** | Redis ZSET sliding window 10 phút để phát hiện flapping Low incidents |

### Giao tiếp với các service bên ngoài

| Service ngoài | Giao thức | Hướng | Mục đích |
|---|---|---|---|
| Azure Monitor Action Group | HTTP webhook (inbound) | → DevOps-Agent | Nhận alert |
| Loki alertmanager | HTTP webhook (inbound) | → DevOps-Agent | Nhận alert container logs |
| RAG_Service | gRPC (TLS) | DevOps-Agent → | Query knowledge, ingest Knowledge_Entry |
| AuditLog_Service | RabbitMQ AMQP | DevOps-Agent → | Publish Audit_Events |
| Cloudflare R2 | HTTPS qua Cloudflare Tunnel | DevOps-Agent → | Lưu debug artifacts |
| Azure AI Language | HTTPS | DevOps-Agent → | PII Detection (Text Analytics) |
| Azure OpenAI Service | HTTPS | DevOps-Agent → | LLM completion cho RCA |
| Azure Key Vault | HTTPS (Workload Identity) | DevOps-Agent → | Lấy secrets |
| Redis | TCP | DevOps-Agent → | Dedup TTL, anti-flapping ZSET, rule cache |
| PostgreSQL | TCP | DevOps-Agent → | Persistent storage cho tất cả entities |
| BFF (Yarp) | HTTP/gRPC | BFF → DevOps-Agent | Admin API (incidents, rules, approvals, config) |
| RabbitMQ DLQ | AMQP | DevOps-Agent ⇄ | Event queue khi downtime, DLQ reprocessing |


## Components và Interfaces

### ILlmAdapter Interface

```typescript
export interface LlmResponse {
  content: string;
  usage: { promptTokens: number; completionTokens: number; totalTokens: number };
  model: string;
  finishReason: 'stop' | 'length' | 'content_filter' | 'error';
}

export interface ILlmAdapter {
  complete(
    prompt: string,
    context: string,
    config: LlmCallConfig,
  ): Promise<LlmResponse>;
  isAvailable(): Promise<boolean>;
}

export interface LlmCallConfig {
  maxTokens: number;
  temperature?: number;
  stopSequences?: string[];
}
```

### IRagGrpcClient Interface (DevOps-Agent proto)

```protobuf
// devops_rag.proto — sẽ tạo mới cho DevOps-Agent
syntax = "proto3";
package devops_rag;

service DevOpsRagService {
  rpc QueryKnowledge(QueryRequest) returns (QueryResponse);
  rpc IngestKnowledge(IngestRequest) returns (IngestResponse);
}

message QueryRequest {
  string error_signature = 1;
  string service_context = 2;
  string source_tag     = 3;  // "devops-agent"
  int32  top_k          = 4;
}

message QueryResponse {
  repeated KnowledgeEntry entries = 1;
}

message IngestRequest {
  string content       = 1;
  string source_tag    = 2;  // always "devops-agent"
  string correlation_id = 3;
  string artifact_type = 4;
}

message IngestResponse {
  string entry_id = 1;
  bool   success  = 2;
}

message KnowledgeEntry {
  string id      = 1;
  string content = 2;
  float  score   = 3;
}
```

### IAuditPublisher Interface

```typescript
export interface AuditEventPayload {
  actor: string;           // service identity hoặc user_id
  action_type: AuditActionType;
  target: string;          // resource identifier
  timestamp: string;       // UTC ISO 8601
  severity: Severity;
  result: 'SUCCESS' | 'FAILURE';
  correlation_id: string;
  metadata?: Record<string, unknown>;
}

export type AuditActionType =
  | 'INCIDENT_CREATED' | 'RULE_APPLIED' | 'ROLLBACK_EXECUTED'
  | 'RCA_STARTED' | 'PR_OPENED' | 'APPROVAL_REQUESTED'
  | 'APPROVAL_GRANTED' | 'APPROVAL_REJECTED' | 'ESCALATED'
  | 'RULE_PROMOTED' | 'RULE_REJECTED' | 'SELF_CONFIG_UPDATED'
  | 'KNOWLEDGE_ENTRY_CREATED';

export interface IAuditPublisher {
  publish(event: AuditEventPayload): Promise<void>;
}
```


## Data Models

### Bảng `devops_agent_self_config`

Không phân theo tenant — một bản ghi toàn cục cho DevOps-Agent.

| Cột | Kiểu | Ràng buộc | Mô tả |
|---|---|---|---|
| `id` | UUID | PK | Luôn là singleton row |
| `model_provider` | VARCHAR(50) | NOT NULL | Ví dụ: `azure_openai`, `gemini` |
| `model_name` | VARCHAR(100) | NOT NULL | Ví dụ: `gpt-4o`, `gemini-1.5-pro` |
| `api_endpoint` | TEXT | NOT NULL | Endpoint URL của LLM provider |
| `max_tokens_per_request` | INT | NOT NULL, > 0 | Giới hạn token mỗi request LLM |
| `alert_threshold_usd_per_day` | DECIMAL(10,4) | NOT NULL, >= 0 | Ngưỡng chi phí cảnh báo (USD/ngày) |
| `updated_by` | VARCHAR(100) | NOT NULL | user_id hoặc service identity |
| `updated_at` | TIMESTAMPTZ | NOT NULL | Thời điểm cập nhật cuối |

### Bảng `incidents`

| Cột | Kiểu | Ràng buộc | Mô tả |
|---|---|---|---|
| `id` | UUID | PK | Internal ID |
| `correlation_id` | VARCHAR(64) | UNIQUE NOT NULL | Định danh nhóm events |
| `dedup_key` | VARCHAR(64) | NOT NULL INDEX | SHA-256 hash |
| `source` | VARCHAR(50) | NOT NULL | `azure_monitor` / `loki` |
| `error_signature` | TEXT | NOT NULL | Mô tả lỗi chuẩn hóa |
| `severity` | ENUM | NOT NULL | `Low`/`Medium`/`High`/`Critical` |
| `original_severity` | ENUM | NOT NULL | Severity ban đầu trước khi escalate |
| `status` | VARCHAR(50) | NOT NULL | `OPEN`, `ROUTING_FAILED`, `RESOLVED`, v.v. |
| `flap_count` | INT | DEFAULT 0 | Số lần lặp lại trong sliding window |
| `affected_service` | VARCHAR(100) | | Tên service bị ảnh hưởng |
| `affected_tenant_id` | UUID | NULLABLE | Tenant liên quan (nếu có) |
| `alert_metadata` | JSONB | | Raw alert payload (đã redact) |
| `created_at` | TIMESTAMPTZ | NOT NULL DEFAULT NOW() | |
| `updated_at` | TIMESTAMPTZ | NOT NULL | |

### Bảng `debug_sessions`

| Cột | Kiểu | Ràng buộc | Mô tả |
|---|---|---|---|
| `id` | UUID | PK | |
| `incident_id` | UUID | FK → incidents.id | |
| `correlation_id` | VARCHAR(64) | NOT NULL | Denormalized cho lookup nhanh |
| `pipeline_type` | VARCHAR(50) | NOT NULL | `auto_remediation`, `rca_medium`, `rca_high_critical` |
| `status` | VARCHAR(50) | NOT NULL | `OPEN`, `PENDING_APPROVAL`, `RESOLVED`, `UNMATCHED_RULE`, `ROUTING_FAILED`, `ARTIFACT_UPLOAD_CRITICAL_FAILURE` |
| `rca_summary` | TEXT | NULLABLE | LLM-generated, đã redact |
| `proposed_action` | TEXT | NULLABLE | Đề xuất fix của LLM |
| `pr_url` | TEXT | NULLABLE | URL của PR (chỉ Medium severity) |
| `rag_timeout_occurred` | BOOLEAN | DEFAULT false | Đánh dấu nếu RAG timeout |
| `artifact_refs` | JSONB | | Array các R2 artifact keys |
| `warning_flags` | JSONB | | Array cảnh báo non-blocking |
| `llm_tokens_used` | INT | DEFAULT 0 | Tracking cost |
| `created_at` | TIMESTAMPTZ | NOT NULL DEFAULT NOW() | |
| `updated_at` | TIMESTAMPTZ | NOT NULL | |

### Bảng `existing_rules`

| Cột | Kiểu | Ràng buộc | Mô tả |
|---|---|---|---|
| `id` | UUID | PK | |
| `name` | VARCHAR(200) | NOT NULL | Tên rule |
| `error_signature_pattern` | TEXT | NOT NULL | Pattern để match incidents (regex hoặc exact) |
| `target_service` | VARCHAR(100) | NOT NULL | Service resource bị ảnh hưởng |
| `target_deployment` | VARCHAR(200) | NULLABLE | Deployment/resource cụ thể |
| `action_type` | VARCHAR(50) | NOT NULL | `restart_pod`, `adjust_config`, `rollback_deployment` |
| `action_params` | JSONB | NOT NULL | Parameters cho action |
| `scope_constraint` | JSONB | NOT NULL | Khai báo scope giới hạn tài nguyên bị ảnh hưởng |
| `promoted_from_pending_id` | UUID | NULLABLE FK → pending_rules | Truy vết nguồn gốc |
| `created_by` | VARCHAR(100) | NOT NULL | |
| `created_at` | TIMESTAMPTZ | NOT NULL DEFAULT NOW() | |
| `updated_at` | TIMESTAMPTZ | NOT NULL | |

### Bảng `pending_rules`

| Cột | Kiểu | Ràng buộc | Mô tả |
|---|---|---|---|
| `id` | UUID | PK | |
| `error_signature` | TEXT | NOT NULL | Từ RCA |
| `root_cause_summary` | TEXT | NOT NULL | LLM-generated, đã redact |
| `proposed_action` | TEXT | NOT NULL | Đề xuất fix |
| `confidence_score` | DECIMAL(5,4) | NOT NULL, [0,1] | LLM confidence |
| `source_correlation_id` | VARCHAR(64) | NOT NULL | Incident tạo ra rule này |
| `status` | VARCHAR(30) | NOT NULL | `PENDING_APPROVAL`, `APPROVED`, `REJECTED` |
| `rejection_reason` | TEXT | NULLABLE | Lý do reject |
| `reviewed_by` | VARCHAR(100) | NULLABLE | |
| `reviewed_at` | TIMESTAMPTZ | NULLABLE | |
| `created_at` | TIMESTAMPTZ | NOT NULL DEFAULT NOW() | |


### Bảng `pr_approval_records`

| Cột | Kiểu | Ràng buộc | Mô tả |
|---|---|---|---|
| `id` | UUID | PK | |
| `incident_id` | UUID | FK → incidents.id | |
| `correlation_id` | VARCHAR(64) | NOT NULL | |
| `pr_url` | TEXT | NOT NULL | URL của PR chờ duyệt |
| `original_severity` | ENUM | NOT NULL | Severity khi tạo (không đổi dù reclassify) |
| `status` | VARCHAR(30) | NOT NULL | `PENDING_APPROVAL_1`, `PENDING_APPROVAL_2`, `APPROVED`, `REJECTED`, `ESCALATED`, `EXPIRED` |
| `approver_1_id` | VARCHAR(100) | NULLABLE | |
| `approver_1_decision` | VARCHAR(20) | NULLABLE | `APPROVED` / `REJECTED` |
| `approver_1_comment` | TEXT | NULLABLE | |
| `approver_1_at` | TIMESTAMPTZ | NULLABLE | |
| `approver_2_id` | VARCHAR(100) | NULLABLE | Chỉ cho High/Critical |
| `approver_2_decision` | VARCHAR(20) | NULLABLE | |
| `approver_2_comment` | TEXT | NULLABLE | |
| `approver_2_at` | TIMESTAMPTZ | NULLABLE | |
| `timeout_minutes` | INT | NOT NULL DEFAULT 30 | Configurable, range 30–60 |
| `timeout_at` | TIMESTAMPTZ | NOT NULL | Thời điểm expire |
| `escalated_at` | TIMESTAMPTZ | NULLABLE | |
| `created_at` | TIMESTAMPTZ | NOT NULL DEFAULT NOW() | |
| `updated_at` | TIMESTAMPTZ | NOT NULL | |

### Bảng `rule_approval_records`

| Cột | Kiểu | Ràng buộc | Mô tả |
|---|---|---|---|
| `id` | UUID | PK | |
| `pending_rule_id` | UUID | FK → pending_rules.id | |
| `correlation_id` | VARCHAR(64) | NOT NULL | |
| `status` | VARCHAR(30) | NOT NULL | `PENDING_APPROVAL`, `APPROVED`, `REJECTED`, `ESCALATED`, `EXPIRED` |
| `approver_id` | VARCHAR(100) | NULLABLE | |
| `decision` | VARCHAR(20) | NULLABLE | `APPROVED` / `REJECTED` |
| `comment` | TEXT | NULLABLE | |
| `decided_at` | TIMESTAMPTZ | NULLABLE | |
| `timeout_minutes` | INT | NOT NULL DEFAULT 60 | |
| `timeout_at` | TIMESTAMPTZ | NOT NULL | |
| `escalated_at` | TIMESTAMPTZ | NULLABLE | |
| `created_at` | TIMESTAMPTZ | NOT NULL DEFAULT NOW() | |

### Bảng `audit_event_outbox`

Dead-letter buffer cho audit events. Worker poll và publish tới RabbitMQ.

| Cột | Kiểu | Ràng buộc | Mô tả |
|---|---|---|---|
| `id` | UUID | PK | |
| `correlation_id` | VARCHAR(64) | NOT NULL INDEX | Để dedup khi retry |
| `action_type` | VARCHAR(50) | NOT NULL | |
| `payload` | JSONB | NOT NULL | Full AuditEventPayload |
| `status` | VARCHAR(20) | NOT NULL DEFAULT 'PENDING' | `PENDING`, `PUBLISHED`, `FAILED` |
| `retry_count` | INT | NOT NULL DEFAULT 0 | |
| `next_retry_at` | TIMESTAMPTZ | NULLABLE | Exponential backoff schedule |
| `published_at` | TIMESTAMPTZ | NULLABLE | Khi nhận delivery confirm |
| `created_at` | TIMESTAMPTZ | NOT NULL DEFAULT NOW() | |

> **Knowledge_Entry**: Không có bảng local. Mọi thao tác trên Knowledge_Entry đều qua RAG_Service gRPC. DevOps-Agent không truy cập pgvector trực tiếp dưới bất kỳ hình thức nào.


## Luồng xử lý chính

### Case A: Ingest Event → Dedup → Phân loại → Route

```
1. Webhook/alertmanager payload đến IngestionController
2. DedupService tính SHA-256(source, error_signature, time_window_bucket)
3. Kiểm tra Redis: key tồn tại?
   → CÓ: discard event, tăng metric counter → KẾT THÚC
   → KHÔNG: lưu key vào Redis (TTL 30 phút)
4. Tạo hoặc merge Event vào Incident (correlation_id)
5. AntiFlappingTracker: kiểm tra ZSET trong 10 phút
   → Nếu flap_count > 3 cùng dedup_key: severity = Medium (escalate)
6. SeverityClassifier phân loại severity từ alert metadata
7. Lưu Incident + Debug_Session (severity, routing_decision, correlation_id)
8. RoutingDispatcher:
   → Low:              → RuleEngineService (Case B)
   → Medium/High/Crit: → RcaPipelineService (Case C)
   → Không xác định:   → mark ROUTING_FAILED, chặn dispatch
9. AuditOutboxWorker ghi INCIDENT_CREATED vào outbox
```

### Case B: Auto-Remediation Low Severity

```
1. RuleEngineService nhận Incident (severity=Low)
2. Match Incident.error_signature vs Existing_Rules cache
   → KHÔNG match: escalate sang UnknownIssueHandler
     a. RAG query: QueryKnowledge(error_signature, service_context)
        → Timeout 10s: dùng LLM base knowledge, log warning
     b. PII_Redactor xử lý incident context trước khi gửi LLM
     c. LLM RCA → proposed remediation
     d. Tạo Pending_Rule, gửi Rule Approval workflow
     e. Debug_Session status = UNMATCHED_RULE
     f. Notify Owner qua Email + Dashboard
   → CÓ match:
3. Validate scope: action chỉ được áp dụng lên declared target resource
4. Execute remediation action (restart_pod / adjust_config / rollback)
5. Ghi kết quả vào Debug_Session
6. AuditOutboxWorker: RULE_APPLIED (trong 10 giây sau khi execute)
7. Debug_Session status = RESOLVED
```

### Case C: RCA Medium/High → PR/Artifact → Approval → Production

```
1. RcaPipelineService nhận Incident (Medium/High/Critical)
2. Collect logs (trong 60 giây):
   a. Loki: raw container logs qua Loki HTTP API
   b. Azure Monitor: exception traces, KQL query results
3. PII_Redactor xử lý toàn bộ log text:
   a. Azure AI Language PII Detection (HTTP call)
   b. Custom regex/whitelist matcher
   c. Fallback nếu Azure AI Language unavailable: chỉ dùng step b, log warning
   d. FAIL HARD nếu cả 2 steps fail: chặn LLM call
4. Loại bỏ binary blobs, credentials, connection strings khỏi context
5. RAG query: QueryKnowledge(error_signature, service_context, source_tag="devops-agent")
   → Timeout 10s: fallback LLM base knowledge, log warning trong Debug_Session
6. LLM call (via ILlmAdapter → AzureOpenAiAdapter) với redacted context + RAG entries
7. LLM trả về RCA summary + proposed fix
8. Xử lý theo severity:
   → Medium:
     a. Tạo Git branch, commit proposed fix, open Pull Request
     b. Lưu PR URL vào Debug_Session
     c. Tạo pr_approval_records với status=PENDING_APPROVAL_1
     d. AuditOutboxWorker: PR_OPENED, APPROVAL_REQUESTED
   → High/Critical:
     a. Tạo recommendation artifact (không open PR)
     b. Tạo pr_approval_records với status=PENDING_APPROVAL_1 (cần 2 steps)
     c. AuditOutboxWorker: APPROVAL_REQUESTED
9. Upload artifacts lên R2: logs, rca, pr_diff (nếu có), approval
   → Key: devops-agent/incidents/{correlation_id}/artifacts/{type}/{ts}_{name}
   → Network error: retry 3x → continue with warning
   → Critical error: halt Debug_Session, status=ARTIFACT_UPLOAD_CRITICAL_FAILURE
10. IngestKnowledge tới RAG_Service (sau khi redact)
    → PII_Redactor fail: block KE creation, log failure
    → Success: gọi gRPC IngestKnowledge với source_tag="devops-agent"
11. ApprovalScheduler đặt Bull Queue job cho timeout (30-60 phút)
12. Notify approver qua Email + Dashboard
    → High/Critical + urgent: thêm Telegram notification
13. Khi approver submit decision:
    → REJECTED: mark Approval_Record, publish APPROVAL_REJECTED audit
    → APPROVED (Medium, 1 step): transition PENDING_APPROVAL_1 → APPROVED
    → APPROVED (High/Critical):  PENDING_APPROVAL_1 → PENDING_APPROVAL_2 → APPROVED
    → APPROVED: notify Owner, human thực hiện merge/deploy thủ công
14. Timeout hết: transition → ESCALATED
    → Nếu original_severity=Low (flapping escalation): Email only
    → Ngược lại: Email + Telegram
    → AuditOutboxWorker: ESCALATED
```


## Thiết kế các cơ chế kỹ thuật

### 4.1 Dedup & Correlation

**Dedup_Key formula:**
```
dedup_key = SHA-256(source + ":" + error_signature + ":" + time_window_bucket)
time_window_bucket = floor(alert_timestamp_utc / 300) * 300  // 5-minute bucket
```

**Redis key pattern:**
```
devops:dedup:{dedup_key}   TTL = 1800s (30 phút)
```

**Giá trị lưu trong Redis:** `correlation_id` của Incident tương ứng (để merge events).

**Correlation merge logic:**
- Khi dedup_key không tồn tại: tạo Incident mới, sinh `correlation_id = UUID v7`, lưu Redis.
- Khi dedup_key tồn tại: lấy `correlation_id` từ Redis, gộp Event vào Incident hiện có (tăng event_count, không tạo Debug_Session mới).

### 4.2 Anti-Flapping

**Cơ chế:** Redis Sorted Set (ZSET) với score = Unix timestamp.

```
Key pattern:  devops:flap:{dedup_key}
Member:       event_id (UUID)
Score:        event_timestamp_ms

Algorithm:
1. ZADD devops:flap:{key} {timestamp_ms} {event_id}
2. ZREMRANGEBYSCORE devops:flap:{key} 0 {now_ms - 600000}  // loại > 10 phút
3. count = ZCARD devops:flap:{key}
4. EXPIRE devops:flap:{key} 600
5. IF count > 3: escalate severity → Medium
```

**Escalation:** Ghi lại `original_severity = Low` trong Incident trước khi cập nhật `severity = Medium`. Field này được dùng để routing notification (7.7: escalation của Low → Email only).

### 4.3 Rule Cache Invalidation

```
Flow:
1. Admin cập nhật Existing_Rule qua API → DB update
2. Service emit event nội bộ (hoặc RabbitMQ `rule.updated` message)
3. RuleEngineService nhận event → đánh dấu cache_stale = true
4. Background reload task: fetch all rules từ DB vào Map mới
5. Atomic swap: cachedRules = newRulesMap (sau khi load xong)
6. Deadline: toàn bộ quá trình <= 5 giây

Stale-cache behavior:
- Trong thời gian reload: tiếp tục dùng cache cũ
- Mọi match request trong thời gian reload vẫn được phục vụ
- Không block bất kỳ Incident nào vì đang reload cache
```

### 4.4 Approval State Machine

**PR_Approval States (Medium):**
```
CREATED → PENDING_APPROVAL_1 → APPROVED | REJECTED | ESCALATED | EXPIRED
```

**PR_Approval States (High/Critical):**
```
CREATED → PENDING_APPROVAL_1 → PENDING_APPROVAL_2 → APPROVED | REJECTED | ESCALATED | EXPIRED
```

**Rule_Approval States:**
```
CREATED → PENDING_APPROVAL → APPROVED | REJECTED | ESCALATED | EXPIRED
```

**Invariant quan trọng:** Severity được snapshot tại thời điểm tạo Approval_Record (`original_severity`). Mọi reclassification sau đó không ảnh hưởng số bước approval đã khởi tạo.

**Timeout scheduler (Bull Queue):**
```typescript
// Khi tạo Approval_Record:
await approvalQueue.add(
  'check-timeout',
  { approvalId, approvalType },
  { delay: timeoutMinutes * 60 * 1000, jobId: `timeout:${approvalId}` }
);

// Job handler:
async handleTimeout({ approvalId, approvalType }) {
  const record = await fetchRecord(approvalId);
  if (record.status !== 'PENDING_APPROVAL_*') return; // đã được xử lý
  await transitionToEscalated(record);
  await notifyEscalation(record);
}
```

### 4.5 LLM Adapter Pattern

```typescript
// Concrete adapters
@Injectable()
export class AzureOpenAiAdapter implements ILlmAdapter {
  async complete(prompt, context, config): Promise<LlmResponse> { ... }
  async isAvailable(): Promise<boolean> { ... }
}

@Injectable()
export class GeminiAdapter implements ILlmAdapter {  // placeholder
  async complete(prompt, context, config): Promise<LlmResponse> { ... }
  async isAvailable(): Promise<boolean> { ... }
}

// Factory
@Injectable()
export class LlmAdapterFactory {
  constructor(
    private readonly selfConfigManager: SelfConfigManager,
    private readonly azure: AzureOpenAiAdapter,
    private readonly gemini: GeminiAdapter,
  ) {}

  getAdapter(): ILlmAdapter {
    const cfg = this.selfConfigManager.current();
    switch (cfg.model_provider) {
      case 'azure_openai': return this.azure;
      case 'gemini':       return this.gemini;
      default: throw new Error(`Unknown LLM provider: ${cfg.model_provider}`);
    }
  }
}
```


### 4.6 PII Redaction Pipeline

```
Input: raw text (log, exception, stack trace)
          │
          ▼
  ┌───────────────────────────┐
  │ Step 1: Azure AI Language │  HTTP call với timeout 5s
  │ Text Analytics PII API    │
  └───────────────────────────┘
       │ success              │ unavailable / timeout
       ▼                      ▼
  PII entities list      FALLBACK: log warning,
  (+ offsets)             continue with Step 2 only
       │
       ▼
  ┌────────────────────────────────────────┐
  │ Step 2: Custom Regex / Whitelist Scan  │  In-process, always runs
  │ - connection strings patterns          │
  │ - internal field names whitelist       │
  │ - credential key patterns              │
  └────────────────────────────────────────┘
       │ success              │ BOTH steps fail completely
       ▼                      ▼
  Replace entities       HARD BLOCK:
  with [CATEGORY]        - Block LLM call
  placeholders           - Block KE creation
  (preserve structure)   - Log failure, return error

Output: redacted_text with [PHONE_NUMBER], [EMAIL_ADDRESS],
        [PERSON_NAME], [CONNECTION_STRING], [CREDENTIAL], etc.
```

**Whitelist của sensitive field names** (được kiểm tra theo key pattern trong log JSON):
`password`, `secret`, `api_key`, `token`, `connection_string`, `private_key`, `access_key`, `credential`

### 4.7 Audit Outbox Pattern

```
Transactional flow (DB transaction):
  BEGIN;
    INSERT INTO audit_event_outbox (correlation_id, action_type, payload, status)
    VALUES ($1, $2, $3, 'PENDING')
    ON CONFLICT (correlation_id, action_type) DO NOTHING;  -- dedup
  COMMIT;

Background worker (polling interval: 5s):
  1. SELECT * FROM audit_event_outbox
     WHERE status = 'PENDING' AND (next_retry_at IS NULL OR next_retry_at <= NOW())
     LIMIT 50 FOR UPDATE SKIP LOCKED;
  2. FOR EACH event:
     a. Publish tới RabbitMQ exchange `audit.events`
     b. Nhận delivery confirm (publisher confirms mode)
     c. ON CONFIRM: UPDATE status = 'PUBLISHED', published_at = NOW()
     d. ON FAIL:
        - retry_count < 3: exponential backoff (5s, 15s, 45s), UPDATE next_retry_at
        - retry_count >= 3: UPDATE status = 'FAILED', log critical alert
```

**Dedup key trong outbox:** `UNIQUE(correlation_id, action_type)` — ngăn duplicate records từ retry storm.

### 4.8 Artifact Storage (R2 + Cloudflare Tunnel)

**Key structure:**
```
devops-agent/incidents/{correlation_id}/artifacts/{artifact_type}/{ISO8601_ts}_{filename}

Ví dụ:
devops-agent/incidents/corr-abc123/artifacts/rca/2025-01-15T10:30:00Z_rca_summary.txt
devops-agent/incidents/corr-abc123/artifacts/logs/2025-01-15T10:30:00Z_loki_raw.txt
devops-agent/incidents/corr-abc123/artifacts/pr_diff/2025-01-15T10:31:00Z_diff.patch
devops-agent/incidents/corr-abc123/artifacts/approval/2025-01-15T11:00:00Z_decision.json
```

**Metadata tags trên mỗi object:**
```
correlation_id: {correlation_id}
severity:       Low|Medium|High|Critical
tenant_id:      {tenant_id hoặc "system"}
created_at:     {ISO 8601 UTC}
artifact_type:  logs|rca|pr_diff|approval
```

**Upload error handling:**
```
Network error (5xx, timeout):
  → Retry 3x với exponential backoff: 2s, 4s, 8s
  → Sau 3 lần fail: continue Debug_Session, thêm warning_flag vào session, log non-blocking error

Critical error (auth failure, quota exceeded, R2 unavailable):
  → MAY halt Debug_Session
  → Cập nhật Debug_Session.status = 'ARTIFACT_UPLOAD_CRITICAL_FAILURE'
  → Publish RULE_APPLIED / RCA_STARTED audit event vẫn xảy ra để không mất audit trail
```

### 4.9 RAG Service Integration

```typescript
// gRPC client với timeout và fallback
@Injectable()
export class RagGrpcClient {
  async queryKnowledge(params: QueryParams): Promise<KnowledgeEntry[]> {
    try {
      const response = await this.client.QueryKnowledge(params, {
        deadline: Date.now() + 10_000  // 10 giây timeout
      });
      return response.entries;
    } catch (err) {
      if (isTimeoutError(err)) {
        this.logger.warn(`RAG timeout for correlation_id ${params.correlationId}`);
        return [];  // fallback: RCA tiếp tục với LLM base knowledge
      }
      throw err;
    }
  }

  async ingestKnowledge(params: IngestParams): Promise<IngestResponse> {
    // Luôn kèm source_tag = "devops-agent"
    return this.client.IngestKnowledge({
      ...params,
      source_tag: 'devops-agent'
    });
  }
}
```

### 4.10 Self-Config Hot Reload

```
Flow:
1. System_Admin PUT /api/v1/config/self → DB update
2. Service publish internal event hoặc RabbitMQ `config.updated`
3. SelfConfigManager nhận event → reload Self_Config từ DB
4. SelfConfigManager.current() trả về config mới
5. LlmAdapterFactory.getAdapter() dùng config mới cho mọi call tiếp theo
6. Toàn bộ quá trình < 60 giây, không restart service

Implementation:
@Injectable()
export class SelfConfigManager {
  private config: SelfConfig;

  current(): SelfConfig { return this.config; }

  async onConfigUpdated(): Promise<void> {
    this.config = await this.configRepo.findSingleton();
    this.logger.log('Self-Config reloaded');
  }
}
```


## API Endpoints

Tất cả endpoints được bảo vệ bởi JWT (Cognito). RBAC kiểm tra `systemRoles` từ token.

### Incidents

| Method | Path | Auth | Mô tả |
|---|---|---|---|
| `GET` | `/api/v1/incidents` | SYSTEM_ADMIN, TENANT_ADMIN | Danh sách incidents với filter (severity, status, tenant_id, date range) |
| `GET` | `/api/v1/incidents/:correlationId` | SYSTEM_ADMIN, TENANT_ADMIN | Chi tiết incident + debug session + artifacts |
| `POST` | `/api/v1/incidents/:correlationId/approve` | SYSTEM_ADMIN, TENANT_ADMIN | Submit approval/rejection cho PR Approval |

### Rules

| Method | Path | Auth | Mô tả |
|---|---|---|---|
| `GET` | `/api/v1/rules` | SYSTEM_ADMIN, TENANT_ADMIN | Danh sách existing rules với filter |
| `POST` | `/api/v1/rules` | SYSTEM_ADMIN, TENANT_ADMIN | Tạo rule mới thủ công |
| `PUT` | `/api/v1/rules/:id` | SYSTEM_ADMIN, TENANT_ADMIN | Cập nhật rule |
| `DELETE` | `/api/v1/rules/:id` | SYSTEM_ADMIN, TENANT_ADMIN | Xóa rule |
| `GET` | `/api/v1/rules/pending` | SYSTEM_ADMIN, TENANT_ADMIN | Danh sách pending rules chờ duyệt |
| `POST` | `/api/v1/rules/pending/:id/approve` | SYSTEM_ADMIN, TENANT_ADMIN | Duyệt pending rule |
| `POST` | `/api/v1/rules/pending/:id/reject` | SYSTEM_ADMIN, TENANT_ADMIN | Reject pending rule |

### Configuration

| Method | Path | Auth | Mô tả |
|---|---|---|---|
| `GET` | `/api/v1/config/self` | SYSTEM_ADMIN | Đọc Self_Config hiện tại |
| `PUT` | `/api/v1/config/self` | SYSTEM_ADMIN | Cập nhật Self_Config |

### Monitoring

| Method | Path | Auth | Mô tả |
|---|---|---|---|
| `GET` | `/health` | Public (k8s probe) | Health check: service status, DLQ depth, active sessions, Redis |
| `GET` | `/metrics` | Internal (Prometheus scrape) | Prometheus metrics |

### Response format (incidents detail)

```jsonc
{
  "incident": {
    "correlationId": "...",
    "severity": "High",
    "originalSeverity": "Low",
    "status": "PENDING_APPROVAL",
    "createdAt": "2025-01-15T10:30:00Z"
  },
  "debugSession": {
    "status": "PENDING_APPROVAL",
    "rcaSummary": "...",
    "proposedAction": "...",
    "prUrl": "https://github.com/...",
    "artifactRefs": ["devops-agent/incidents/.../artifacts/rca/..."],
    "warningFlags": ["RAG_TIMEOUT", "PII_FALLBACK_USED"]
  },
  "approval": {
    "type": "PR_APPROVAL",
    "status": "PENDING_APPROVAL_1",
    "timeoutAt": "2025-01-15T11:00:00Z"
  }
}
```


## Correctness Properties

*A property is a characteristic or behavior that should hold true across all valid executions of a system — essentially, a formal statement about what the system should do. Properties serve as the bridge between human-readable specifications and machine-verifiable correctness guarantees.*

DevOps-Agent có nhiều pure function và business logic layer phù hợp với property-based testing: hàm tính Dedup_Key, dedup idempotency, severity classification output, approval state machine transitions, PII redaction format, audit completeness, v.v. Các properties dưới đây được rút ra từ acceptance criteria sau khi loại bỏ redundancy.

**Property reflection (loại bỏ redundancy):**
- Property 1 (dedup idempotency) bao hàm 1.4. Property 2 (event grouping) là bổ sung không trùng.
- Property 3 (severity output) và Property 4 (Low → no LLM) tách biệt nhau, không trùng.
- Property 5 (anti-flapping) độc lập với Property 3.
- Property 6 (High/Critical = 2 steps) và Property 10 (no auto-merge) là hai invariants khác nhau trên approval machine.
- Property 7 (RBAC) gộp 3.7 và 6.7 thành một property tổng quát.
- Property 8 (Pending_Rule fields) và Property 9 (rejected rule exclusion) tách biệt.
- Property 11 (PII placeholder format) và Property 12 (PII fallback) bổ sung nhau.
- Property 13 (audit completeness) bao hàm 10.2 (required fields) — gộp thành một.
- Property 14 (outbox idempotency) và Property 15 (PENDING until confirmed) không trùng nhau.
- Property 16 (DLQ concurrency) và Property 17 (ingestion isolation) là hai invariants hệ thống riêng biệt.
- Property 18 (Telegram blocked if fields missing) và Property 19 (Telegram blocked for Low) không trùng.
- Property 20 (artifact key format) độc lập.
- Property 21 (RAG source_tag) và Property 22 (RAG timeout fallback) không trùng.
- Property 23 (cost alert không block RCA) độc lập.

---

### Property 1: Dedup Idempotency

*For any* event payload E, submitting E multiple times within the 30-minute TTL window must result in exactly one Incident being created — subsequent submissions must be silently discarded without creating new Debug_Sessions.

**Validates: Requirements 1.4**

---

### Property 2: Event Grouping trong Time Window

*For any* set of N events (N ≥ 2) with the same `(source, error_signature, time_window_bucket)` arriving within a 5-minute window, all N events must be grouped under a single `correlation_id` and must not create multiple separate Debug_Sessions.

**Validates: Requirements 1.6**

---

### Property 3: Severity Classification là tập đóng

*For any* alert metadata input, the severity classification output must be exactly one of `{Low, Medium, High, Critical}` — never null, undefined, or any other value.

**Validates: Requirements 2.1**

---

### Property 4: Low Severity không kích hoạt LLM

*For any* incident classified as `Low` severity that matches an Existing_Rule, the LLM adapter must never be invoked — `ILlmAdapter.complete()` call count must remain zero throughout the remediation lifecycle.

**Validates: Requirements 2.2, 3.3**

---

### Property 5: Anti-Flapping Escalation

*For any* Dedup_Key K, if more than 3 events with key K arrive within a 10-minute sliding window, the resulting Incident's severity must be `Medium` and `original_severity` must be `Low`.

**Validates: Requirements 2.5**

---

### Property 6: High/Critical luôn yêu cầu đúng 2 bước approval

*For any* incident with severity `High` or `Critical`, the PR_Approval state machine must require the sequence `PENDING_APPROVAL_1 → PENDING_APPROVAL_2 → APPROVED` and must never transition directly from `PENDING_APPROVAL_1` to `APPROVED`, regardless of any subsequent reclassification.

**Validates: Requirements 6.2**

---

### Property 7: RBAC cho mọi mutation — chỉ Admin roles

*For any* API request that mutates Existing_Rules, Pending_Rules, or submits an Approval decision, if the caller's `systemRoles` does not include `SYSTEM_ADMIN` or `TENANT_ADMIN`, the operation must be rejected with HTTP 403 and no mutation must occur.

**Validates: Requirements 3.7, 6.7**

---

### Property 8: Pending_Rule phải có đủ 6 trường bắt buộc

*For any* RCA output from which a Pending_Rule is created, the resulting record must contain all 6 required fields with non-null, non-empty values: `error_signature`, `root_cause_summary`, `proposed_action`, `confidence_score`, `source_correlation_id`, `created_at`.

**Validates: Requirements 4.2**

---

### Property 9: Rejected Pending_Rule không bao giờ trở thành Existing_Rule

*For any* Pending_Rule R that has been marked as `REJECTED`, querying Existing_Rules must never return R — R must not appear in the active rule set under any circumstances.

**Validates: Requirements 4.5**

---

### Property 10: Không bao giờ tự động merge hoặc deploy

*For any* incident at any severity level and any approval state, the system must never automatically trigger a Git merge, PR merge, or production deployment — these operations must always require explicit human action outside the system.

**Validates: Requirements 6.4**

---

### Property 11: PII Redaction — Format placeholder

*For any* text containing PII entities (email, phone, person name, credentials), the output of `PiiRedactor.redact()` must replace each detected entity with a category placeholder of the form `[CATEGORY_NAME]` and must preserve all non-PII surrounding structure and content.

**Validates: Requirements 8.2**

---

### Property 12: PII Fallback — Pipeline không bị block khi Azure AI Language unavailable

*For any* log text, when Azure AI Language service is unavailable or times out, `PiiRedactor.redact()` must still return a non-null redacted result by applying custom regex only, and the RCA pipeline must continue without throwing a blocking error.

**Validates: Requirements 8.6**

---

### Property 13: Audit Completeness — Mọi action type phải có đúng 1 event với đủ trường

*For any* execution of one of the 13 defined action types (`INCIDENT_CREATED`, `RULE_APPLIED`, `ROLLBACK_EXECUTED`, `RCA_STARTED`, `PR_OPENED`, `APPROVAL_REQUESTED`, `APPROVAL_GRANTED`, `APPROVAL_REJECTED`, `ESCALATED`, `RULE_PROMOTED`, `RULE_REJECTED`, `SELF_CONFIG_UPDATED`, `KNOWLEDGE_ENTRY_CREATED`), exactly one `AuditEvent` must be written to the outbox, and that event must contain all 7 required fields (`actor`, `action_type`, `target`, `timestamp`, `severity`, `result`, `correlation_id`) as non-null values.

**Validates: Requirements 10.1, 10.2**

---

### Property 14: Audit Outbox Idempotency trên Retry

*For any* Audit_Event with `(correlation_id, action_type)` pair C, regardless of how many times the publish is retried, the AuditLog must contain at most one record for C.

**Validates: Requirements 10.5**

---

### Property 15: Audit Event ở PENDING cho đến khi nhận delivery confirm

*For any* Audit_Event in the outbox, its `status` must remain `PENDING` until a RabbitMQ delivery confirmation is received — it must never be marked `PUBLISHED` based solely on the publish attempt without confirmation.

**Validates: Requirements 10.4**

---

### Property 16: DLQ Concurrency không vượt quá 10

*For any* DLQ batch reprocessing operation with N events (N > 10), the number of events being processed concurrently must never exceed 10 at any point in time.

**Validates: Requirements 11.3**

---

### Property 17: Event Ingestion không phụ thuộc downstream services

*For any* combination of downstream service failures (RAG_Service, AuditLog_Service, BFF, RCA pipeline), the event ingestion endpoint and DLQ write operation must still succeed and return a successful response.

**Validates: Requirements 11.5**

---

### Property 18: Telegram bị chặn khi thiếu trường bắt buộc

*For any* Telegram notification attempt where at least one of the required fields (`correlation_id`, `severity`, `action_summary`, `dashboard_link`) is null or empty, the Telegram message must NOT be sent and the omission must be logged as a warning.

**Validates: Requirements 7.4**

---

### Property 19: Telegram không được gửi cho Low severity auto-remediation

*For any* Low severity incident that is auto-remediated without flapping escalation, no Telegram notification must ever be triggered throughout the entire lifecycle of that incident.

**Validates: Requirements 7.5, 7.7**

---

### Property 20: R2 Artifact Key phải khớp template

*For any* artifact upload with inputs `(correlation_id, artifact_type, timestamp, filename)`, the resulting R2 object key must exactly match the pattern `devops-agent/incidents/{correlation_id}/artifacts/{artifact_type}/{timestamp}_{filename}` with no deviation.

**Validates: Requirements 5.7, 12.1**

---

### Property 21: RAG IngestKnowledge luôn có source_tag = "devops-agent"

*For any* `IngestKnowledge` gRPC call originating from DevOps-Agent, the `source_tag` field must always equal the string `"devops-agent"` — no other value is acceptable.

**Validates: Requirements 13.5**

---

### Property 22: RAG Timeout Fallback — RCA vẫn tiếp tục

*For any* RCA scenario where the RAG_Service gRPC call exceeds 10 seconds, the RCA pipeline must still produce an output (using LLM base knowledge), and the Debug_Session must be updated with a `RAG_TIMEOUT` warning flag rather than terminating with an error.

**Validates: Requirements 13.4**

---

### Property 23: Cost Alert không block Debug_Session

*For any* in-progress Debug_Session, triggering a cost threshold event (80% or 100% of `alert_threshold_usd_per_day`) must not terminate, pause, or modify the session's execution — only notifications must be sent.

**Validates: Requirements 9.3**


## Xử lý lỗi (Error Handling)

### Phân loại lỗi và chiến lược

| Loại lỗi | Chiến lược | Hành động |
|---|---|---|
| Redis không khả dụng | Non-blocking fallback | Log warning, continue ingestion với giả định "new event" (không dedup) |
| Azure AI Language timeout/unavailable | Fallback | Dùng custom regex only, log warning `PII_FALLBACK_USED` |
| Azure AI Language + Regex đều fail | Hard block | Block LLM call + KE creation, publish `FAILURE` audit event |
| RAG gRPC timeout (>10s) | Fallback | Tiếp tục RCA với LLM base knowledge, ghi `RAG_TIMEOUT` vào Debug_Session |
| LLM API rate limit / error | Retry with backoff | Pause Debug_Session, retry theo Self_Config retry policy |
| R2 upload network error | Retry 3x | Exponential backoff 2s/4s/8s, sau đó continue với warning |
| R2 upload critical error | MAY halt session | `ARTIFACT_UPLOAD_CRITICAL_FAILURE`, log critical alert |
| RabbitMQ publish fail | Outbox + retry | Giữ PENDING trong `audit_event_outbox`, retry tối đa 3 lần |
| DB query timeout | Exception bubble | Lỗi trả về caller, không corrupt state |
| Git operation fail | Log + abort PR | Mark Debug_Session với warning, không tạo PR partial state |
| Severity không xác định | Block + manual | `ROUTING_FAILED`, không dispatch bất kỳ pipeline nào |

### Graceful Degradation

```
Priority của ingestion (must-have):
  Event Ingestion → DLQ write: không phụ thuộc bất kỳ service nào khác

Optional services khi xử lý:
  RAG_Service unavailable → RCA vẫn chạy với LLM only
  PII Azure AI Language unavailable → Regex fallback
  Cloudflare R2 network error → Continue với warning
  RabbitMQ unavailable → Outbox buffer, retry khi recover
```

### DLQ Recovery Flow

```
Sau khi DevOps-Agent restart:
1. DlqReprocessor consume từ RabbitMQ DLQ theo FIFO order
2. Semaphore: max 10 concurrent goroutines/async tasks
3. Mỗi event: apply full dedup check (Redis) trước
4. Nếu dedup_key tồn tại: discard (đã xử lý trước đó)
5. Nếu không: process bình thường qua full ingestion pipeline
6. Rate limiting để tránh overload downstream services
```


## Chiến lược kiểm thử (Testing Strategy)

### Dual Testing Approach

DevOps-Agent có nhiều pure business logic layer (dedup, severity classification, PII redaction, state machine transitions, rule matching, audit completeness) phù hợp với property-based testing. Các integration points với external services được test bằng unit tests với mocks và integration tests riêng biệt.

### Property-Based Tests

**Thư viện:** `fast-check` (TypeScript/NestJS ecosystem)

**Cấu hình:** Minimum 100 iterations per property test.

**Tag format:** `Feature: devops-agent, Property {number}: {property_text}`

Mỗi property trong section "Correctness Properties" được implement bởi đúng 1 property-based test:

| Property | Test file | Generator strategy |
|---|---|---|
| P1: Dedup Idempotency | `dedup.service.spec.ts` | Arbitrary event payloads, repeat N times |
| P2: Event Grouping | `dedup.service.spec.ts` | N events trong cùng time window |
| P3: Severity là tập đóng | `severity-classifier.spec.ts` | Arbitrary alert metadata |
| P4: Low không kích hoạt LLM | `rule-engine.spec.ts` | Low incidents + matching rules, mock LLM |
| P5: Anti-Flapping | `anti-flapping.spec.ts` | Count > 3 trong 10 phút sliding window |
| P6: High/Critical = 2 bước | `approval-state-machine.spec.ts` | High/Critical incidents |
| P7: RBAC mutations | `rbac.spec.ts` | Arbitrary roles, mutation requests |
| P8: Pending_Rule fields | `unknown-issue-handler.spec.ts` | Arbitrary RCA outputs |
| P9: Rejected không thành Existing | `rule-approval.spec.ts` | Rejection scenarios |
| P10: No auto-merge/deploy | `approval-state-machine.spec.ts` | All states, all severity levels |
| P11: PII placeholder format | `pii-redactor.spec.ts` | Text với arbitrary PII patterns |
| P12: PII fallback non-blocking | `pii-redactor.spec.ts` | Azure AI Language mocked unavailable |
| P13: Audit completeness + fields | `audit-outbox.spec.ts` | All 13 action types |
| P14: Audit outbox idempotency | `audit-outbox.spec.ts` | Retry scenarios |
| P15: PENDING until confirmed | `audit-outbox.spec.ts` | Publish failure scenarios |
| P16: DLQ max 10 concurrent | `dlq-reprocessor.spec.ts` | Batch > 10 events |
| P17: Ingestion isolation | `ingestion.spec.ts` | Downstream failure combinations |
| P18: Telegram blocked if missing fields | `notification.spec.ts` | Missing field combinations |
| P19: Telegram blocked for Low | `notification.spec.ts` | Low severity lifecycle |
| P20: R2 key format | `artifact-storage.spec.ts` | Arbitrary (correlation_id, type, ts, name) |
| P21: RAG source_tag | `rag-grpc-client.spec.ts` | All ingest calls |
| P22: RAG timeout fallback | `rca-pipeline.spec.ts` | RAG mocked with delay > 10s |
| P23: Cost alert no block | `self-config.spec.ts` | Cost threshold events during active session |

### Unit Tests

Tập trung vào:
- Specific examples minh họa đúng behavior
- Integration points giữa các components
- Edge cases không được cover bởi property generators
- Error path validation

Ví dụ:
- `SeverityClassifier`: test với alert metadata cụ thể cho từng severity level
- `RuleEngineService`: test UNMATCHED_RULE escalation với incident cụ thể
- `ApprovalStateMachine`: test timeout transition với mock Bull Queue
- `NotificationDispatcher`: test channel routing với specific severity/escalation scenarios

### Integration Tests

- **Redis integration**: Dedup TTL, anti-flapping ZSET behavior với Redis thực
- **PostgreSQL integration**: CRUD operations, rule cache reload, outbox polling
- **RabbitMQ integration**: Publisher confirms, DLQ consumption order
- **gRPC client integration**: RAG_Service contract test với mock gRPC server
- **Cloudflare R2 integration**: Upload với Cloudflare Tunnel endpoint (test environment)

### Smoke Tests

- Health endpoint trả về đúng format khi service khởi động
- Self_Config singleton row tồn tại và có đủ required fields
- R2 lifecycle retention policy được configure đúng (90 ngày)
- Azure Key Vault secrets accessible từ Workload Identity

### Test Configuration

```typescript
// fast-check configuration
const FC_PARAMS = {
  numRuns: 100,              // Minimum per property
  timeout: 30_000,           // 30s per run
  seed: process.env.FC_SEED  // Reproducible failures
};

// Example property test structure
describe('Feature: devops-agent', () => {
  it('Property 1: Dedup Idempotency', async () => {
    // Feature: devops-agent, Property 1: Dedup Idempotency
    await fc.assert(
      fc.asyncProperty(
        arbitraryEventPayload(),
        fc.integer({ min: 2, max: 10 }),
        async (event, submitCount) => {
          // Reset Redis state
          await redis.del(`devops:dedup:*`);
          // Submit same event N times
          for (let i = 0; i < submitCount; i++) {
            await ingestionService.process(event);
          }
          const incidents = await incidentRepo.findBySignature(event.error_signature);
          expect(incidents).toHaveLength(1);
        }
      ),
      FC_PARAMS
    );
  });
});
```


## Rủi ro kỹ thuật và câu hỏi còn mở

### Rủi ro đã xác định

#### R1: Retry Storm sau khi RabbitMQ recover từ downtime dài

**Mô tả:** Sau downtime kéo dài, DLQ có thể tích lũy hàng trăm events. DlqReprocessor xử lý max 10 concurrent, nhưng nếu mỗi event trigger RCA pipeline (LLM call, RAG call), downstream services có thể bị overload.

**Giải pháp đề xuất:**
- Rate limit thêm ở tầng pipeline dispatch (không chỉ ở DlqReprocessor)
- Circuit breaker trước khi gọi LLM và RAG_Service
- Backpressure mechanism: nếu active Debug_Session count > threshold, pause DLQ consumption

#### R2: Race Condition giữa Stale Cache và Rule Update

**Mô tả:** Khi Admin update một rule đúng lúc DedupService đang match, có thể một số events được xử lý bởi rule cũ và một số bởi rule mới trong cùng một khoảng thời gian reload.

**Giải pháp đề xuất:**
- Atomic swap đảm bảo không có mixed state
- Rule versioning: mỗi rule có `version` field, audit log ghi cả rule version khi execute
- Stale cache window tối đa 5 giây — đủ nhỏ để không gây hậu quả nghiêm trọng

#### R3: Latency Cumulative trong RCA Pipeline

**Mô tả:** Một RCA pipeline hoàn chỉnh cho High severity bao gồm: log collection (up to 60s) + PII Azure AI Language call (~2s) + RAG gRPC query (~1-5s) + LLM call (~10-30s) + R2 upload (~2s). Tổng cộng có thể lên đến 90-100 giây.

**Giải pháp đề xuất:**
- Parallel execution: log collection từ Loki và Azure Monitor chạy song song
- PII detection và log collection chạy pipeline style, không chờ toàn bộ log về mới detect
- RAG query có thể chạy song song với log collection
- Timeout budget tracking trong Debug_Session

#### R4: Audit Outbox Growth khi RabbitMQ Down Kéo dài

**Mô tả:** Nếu RabbitMQ down > 1 giờ, outbox table có thể tích lũy hàng nghìn rows PENDING. Khi RabbitMQ recover, publish storm từ worker có thể overload cả RabbitMQ và PostgreSQL.

**Giải pháp đề xuất:**
- Worker batch size giới hạn (50 events/poll)
- Exponential backoff giữa các batch khi RabbitMQ vừa recover
- Alert sớm khi outbox PENDING count > threshold (ví dụ > 100 events)
- Partition outbox table theo `created_at` cho large deployments

#### R5: Cloudflare Tunnel Reliability

**Mô tả:** Cloudflare Tunnel là single path cho R2 uploads từ AKS. Nếu tunnel gặp sự cố, tất cả artifact uploads fail, gây nhiều Debug_Sessions có warning hoặc bị halt.

**Giải pháp đề xuất:**
- Health check Cloudflare Tunnel endpoint trước khi upload (circuit breaker)
- Metric: artifact_upload_failure_rate → alert khi > 5% trong 5 phút
- Design: artifact storage là non-critical cho Medium severity (warning only)
- Tương lai: pre-signed URL fallback nếu Tunnel không khả dụng

### Câu hỏi còn mở

| # | Câu hỏi | Impact | Cần quyết định bởi |
|---|---|---|---|
| Q1 | Severity classification sử dụng rule-based hay ML-based scoring? | Architecture của SeverityClassifier | System_Admin + Tech Lead |
| Q2 | Git operations (branch, commit, PR) dùng GitHub API hay GitLab API? Cần PAT hay OAuth app? | Implementation của RcaPipelineService | System_Admin |
| Q3 | Rule matching dùng exact match hay regex? Nếu regex: có cần sandbox để tránh ReDoS? | RuleEngineService security | Tech Lead |
| Q4 | Approval timeout configurable per-rule hay global từ Self_Config? | ApprovalStateMachine design | System_Admin |
| Q5 | Khi promote Pending_Rule thành Existing_Rule, có cần human review `scope_constraint` không? | Security của auto-remediation | System_Admin |
| Q6 | Dashboard notification là REST polling hay WebSocket push? | NotificationDispatcher implementation | Frontend Team |
| Q7 | DevOps-Agent có support multi-tenant (incidents có thể belong to specific tenant) hay là system-wide? | Data model cho affected_tenant_id | System_Admin |
| Q8 | `devops_rag.proto` cần được thiết kế mới hay dùng lại `compliance_rag.proto` pattern? | RAG gRPC contract | Tech Lead + RAG Team |

