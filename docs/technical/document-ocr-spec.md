# Đặc tả Document OCR Agent Service

Tài liệu mô tả `document_ocr.DocumentOcrService` trong `protos/document_ocr.proto`.

## 1. Tổng quan

Document OCR quản lý OCR jobs, đọc document qua provider-neutral storage reference, chạy extraction, normalize structured JSON, tính confidence/review flag và publish terminal events.

Service không sở hữu object storage, shipment aggregate, compliance decision hoặc notification delivery.

## 2. Dữ liệu sở hữu

* `DocumentOcrJob`: input references, lifecycle, normalized result, confidence và errors.
* `OcrProviderAttempt`: claim/attempt/provider request/result metadata.
* Inbox/outbox records.

External document/shipment IDs là optional references và không có cross-service foreign key.

## 3. gRPC API

| RPC | Chức năng |
| --- | --- |
| `SubmitDocumentJob` | Validate và enqueue idempotent OCR job |
| `GetDocumentJob` | Get tenant-owned job/result |
| `ListDocumentJobs` | Page/filter jobs theo status/external references |

## 4. Functional Requirements

### FR-01: Submission

* Require idempotency key, safe storage reference, file name, MIME type và bounded size.
* Validate allowed extension/MIME combinations và reject path traversal/unsafe URI.
* Tenant đến từ authenticated context; request không có TenantId.
* Same tenant + idempotency key trả existing job hoặc conflict-safe result.

### FR-02: Job lifecycle

```text
Queued -> Processing -> Completed
                    -> Failed
                    -> Queued (bounded transient retry)
                    -> Cancelled
```

* Worker claim job bằng PostgreSQL locking/lease để tránh concurrent processing.
* Heartbeat/claim expiry cho phép recovery worker crash.
* Transient failure retry có bounded attempts/backoff.
* Permanent, invalid hoặc unsupported failure đi terminal.

### FR-03: Extraction result

* Provider adapter trả typed fields, detected document type, references và confidence.
* Normalizer tạo deterministic JSON schema.
* Overall/field confidence nằm trong `[0,1]`.
* `NeedsReview` được derive từ confidence/policy, không do client tự quyết định.
* Provider credentials và raw binary không persist trong result/event.

### FR-04: Events

* Completed job tạo `DocumentOcrCompletedEvent`.
* Terminal failed job tạo `DocumentOcrFailedEvent`.
* Job state và outbox commit atomically.
* Chi tiết tại [Document OCR events](documents/events/document-ocr-events.md).

## 5. Non-functional Requirements

* Provider/content reader interfaces phải replaceable; deterministic local adapters dùng cho development/test.
* Không yêu cầu paid OCR credentials trong automated tests.
* Worker concurrency, lease, batch size, retry delay và limits cấu hình được.
* Tenant filters áp dụng cho job, attempts và inbox/outbox.
* Error message bounded và không expose stack trace/provider secrets.
* JSON serialization deterministic và contract-compatible.

Local development: gRPC `6003`, PostgreSQL `localhost:5436/aurora_document_ocr`.

## 6. Test Cases đại diện

| ID | Scenario | Expected result |
| --- | --- | --- |
| OCR-TC-01 | Submit valid PDF | Queued job returned, tenant/idempotency persisted |
| OCR-TC-02 | Duplicate idempotency key | Không duplicate job |
| OCR-TC-03 | Unsafe path/MIME/size | Reject trước processing |
| OCR-TC-04 | Successful worker processing | Completed result + outbox atomic |
| OCR-TC-05 | Low confidence | `NeedsReview = true` theo policy |
| OCR-TC-06 | Transient provider failure | Retry scheduled và attempt persisted |
| OCR-TC-07 | Permanent/exhausted failure | Failed event outbox được tạo |
| OCR-TC-08 | Concurrent workers | Một worker claim job |
| OCR-TC-09 | Cross-tenant get/list | Không leakage |
| OCR-TC-10 | RabbitMQ publication | Completed/failed contracts đúng |

## 7. Trạng thái triển khai

Submission/query, deterministic provider pipeline, job worker, bounded retry, migration và completed/failed outbox publication đã implemented. Production cloud OCR/storage adapter vẫn là deployment integration, không hard-code trong service.

