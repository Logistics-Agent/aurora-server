# Đặc tả Regulatory Compliance RAG Service

Tài liệu mô tả `regulatory_compliance.RegulatoryComplianceService` trong `protos/regulatory_compliance.proto`.

## 1. Tổng quan

Regulatory Compliance ingest/version regulatory sources, chunk/embed nội dung, retrieve evidence theo jurisdiction/effective date và tạo cited shipment compliance evaluation.

Service không sở hữu shipment aggregate, OCR execution, notification delivery hoặc object storage. Kết quả là decision support có evidence; không thay thế legal approval/human review.

## 2. Dữ liệu sở hữu

* Regulatory documents, immutable versions và chunks.
* Embedding status/vector metadata.
* Retrieval traces.
* Compliance evaluations, findings và citations.
* Inbox/outbox records.

Shipment/document IDs là external snapshot references, không cross-service foreign keys.

## 3. gRPC API

| RPC | Chức năng |
| --- | --- |
| `IngestRegulatorySource` | Authorized source ingestion/versioning |
| `QueryRegulations` | Effective/jurisdiction/type-aware evidence retrieval |
| `EvaluateCompliance` | Idempotent cited shipment evaluation |
| `GetComplianceEvaluation` | Get tenant-owned evaluation và findings/citations |

## 4. Functional Requirements

### FR-01: Regulatory source ingestion

* Require authority, canonical URI, jurisdiction, type, language, version/effective dates và source content/hash.
* Validate content hash và bounded source size.
* Same source/version/hash xử lý idempotently; changed content tạo immutable version mới.
* Support tenant-visible và controlled platform-visible sources.
* Ingestion cần explicit permission; normal evaluation user không tự ghi regulation corpus.

### FR-02: Chunking và embeddings

* Deterministic chunk sequence, offsets, labels và content hash.
* Embedding provider interface replaceable; local deterministic provider cho test/development.
* Pending chunks được batch worker claim và persist vector/error state.
* Query không trả evidence từ version ngoài effective date/visibility scope.

### FR-03: Retrieval

* Require query, effective time, jurisdiction, language và valid regulation types.
* Filter trước khi ranking theo visibility, effective dates và jurisdiction.
* Return bounded top-k evidence, citations và evidence sufficiency.
* Persist retrieval trace để audit query/filter/result references.
* Generated explanation phải dựa trên returned evidence; không tạo citation giả.

### FR-04: Compliance evaluation

* Require idempotency key, shipment snapshot, cargo, origin/destination, jurisdictions, transport mode và effective time.
* OCR documents được nhận dưới dạng immutable snapshot; service không gọi OCR database.
* Validate cargo quantities/weights, dangerous-goods metadata và document confidence.
* Persist status, risk, findings, missing documents, assumptions, confidence và citations.
* Insufficient/conflicting evidence yêu cầu manual review và không được biểu diễn như compliant chắc chắn.
* Same tenant + idempotency key không tạo duplicate evaluation.

### FR-05: Events

Publish completed/failed result qua outbox; chi tiết tại [Compliance events](documents/events/regulatory-compliance-events.md).

## 5. Non-functional Requirements

* Tenant isolation cho sources tenant-visible, evaluations, findings, citations và traces.
* Platform sources chỉ được read theo visibility policy; không gán tenant giả.
* Deterministic local providers phải replaceable bởi Azure/provider adapters qua configuration.
* Embeddings, prompts, provider credentials và full chunks không đi qua integration event.
* Outbox worker uses explicit allowlist, bounded retry và PostgreSQL row locking.
* Evaluation confidence/risk luôn đi kèm evidence sufficiency và citations khi có.
* Errors trả gRPC status phù hợp và không expose stack trace/secrets.

Local development: gRPC `6004`, PostgreSQL `localhost:5437/aurora_regulatory_compliance`.

## 6. Test Cases đại diện

| ID | Scenario | Expected result |
| --- | --- | --- |
| CMP-TC-01 | Authorized valid ingestion | Version/chunks created idempotently |
| CMP-TC-02 | Wrong hash/permission | Reject và không persist partial source |
| CMP-TC-03 | Effective jurisdiction query | Chỉ evidence đúng scope/date được trả |
| CMP-TC-04 | No sufficient evidence | Insufficient result, không citation giả |
| CMP-TC-05 | Valid cited evaluation | Findings/citations/risk/confidence persisted |
| CMP-TC-06 | Missing required documents | Missing list và assumptions đúng |
| CMP-TC-07 | Duplicate evaluation key | Return existing/no duplicate graph |
| CMP-TC-08 | Cross-tenant get/query | Không leakage tenant source/evaluation |
| CMP-TC-09 | Evaluation failure | Failed state và failed outbox atomic |
| CMP-TC-10 | RabbitMQ publication | Completed/failed contract fields đúng |

## 7. Trạng thái triển khai

Ingestion, deterministic chunk/embedding pipeline, retrieval/citations, compliance evaluation, migration và completed/failed outbox publication đã implemented. Production legal corpus governance và cloud provider adapters vẫn cần deployment/lead integration.

