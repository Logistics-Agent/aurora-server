# KẾ HOẠCH TRIỂN KHAI CHI TIẾT — AURORA LOGISTICS MICROSERVICES

> **Dự án:** Aurora SaaS Logistics Platform  
> **Owner:** Đào Huỳnh  
> **Mục đích tài liệu:** Đối chiếu kiến trúc mục tiêu (Summary.md) với thực tế hiện tại và liệt kê BACKLOG công việc cụ thể để giao cho kỹ sư thực hiện.

---

## I. KẾT QUẢ ĐỐI CHIẾU (REALITY vs VISION)

### ✅ ĐÃ TRIỂN KHAI & ĐÚNG VỚI VISION

| # | Tính năng | Service | Ghi chú |
|---|---|---|---|
| 1 | Volumetric Weight (SEA/AIR) + Chargeable Weight | Financial | ✅ Đủ |
| 2 | Base Freight Rate (DB lookup + fallback formula) | Financial | ✅ Đủ |
| 3 | Port Handling Fees (THC/DOC/DEM/DET) | Financial | ✅ Đủ |
| 4 | Customs Duty + VAT by HS Code | Financial | ✅ Đủ |
| 5 | Fuel Surcharge FSC + EBS | Financial | ✅ Thêm xong |
| 6 | Cargo Insurance Fee | Financial | ✅ Thêm xong |
| 7 | MinAcceptableRate cho AI Agent (margin cơ bản) | Financial | ✅ Đủ |
| 8 | GenerateInvoice (Idempotency + ACID) | Billing | ✅ Đủ |
| 9 | PDF → S3/R2 Presigned URL (Multi-Tenant path) | Billing | ✅ Đủ |
| 10 | RecordPayment (UNPAID→PARTIALLY_PAID→PAID) | Billing | ✅ Thêm xong |
| 11 | CancelInvoice (chỉ UNPAID/OVERDUE) | Billing | ✅ Thêm xong |
| 12 | UpdateInvoiceStatus validation | Billing | ✅ Fix xong |
| 13 | Cron Job auto-OVERDUE (00:05 hàng ngày) | Billing | ✅ Thêm xong |
| 14 | Escrow Wallet: Freeze / Release / Refund | Billing | ✅ Đủ |
| 15 | CheckCustomerCredit (T+30, Credit Limit) | Billing | ✅ Đủ |
| 16 | WebSocket Gateway + JWT Auth | Realtime Hub | ✅ Đủ |
| 17 | Redis Pub/Sub Adapter (graceful fallback) | Realtime Hub | ✅ Đủ |
| 18 | Multi-Tenant Rooms (tenant/user/shipment) | Realtime Hub | ✅ Đủ |
| 19 | RabbitMQ → WebSocket Bridge (billing/shipment/negotiation/financial) | Realtime Hub | ✅ Đủ |
| 20 | Ping/Pong Heartbeat | Realtime Hub | ✅ Thêm xong |

---

### ❌ CHƯA TRIỂN KHAI — CẦN LÀM (Gap Analysis)

**Tổng: 18 tính năng còn thiếu**, chia làm 4 nhóm theo độ ưu tiên.

---

## II. BACKLOG CHI TIẾT THEO ĐỘ ƯU TIÊN

---

## 🔴 NHÓM 1 — CRITICAL (Ảnh hưởng trực tiếp nghiệp vụ tài chính)

### TASK-001: Dynamic Margin Decay Engine (Financial Service)

**Tại sao cần?**  
Summary.md định nghĩa công thức:
```
Min Acceptable Price = Base Cost × (1 + Base Margin% × (T_remaining / T_total)^γ)
```
Hiện tại `GetMinAcceptableRate` dùng margin **cố định** không theo thời gian. AI Negotiation Agent cần giá sàn biến động theo deadline cut-off để đàm phán tốt hơn (gần đến giờ cắt tàu → chấp nhận giá thấp hơn).

**Phạm vi công việc:**
- **[MODIFY]** `financial-service/src/domain/services/cost-calculator.domain-service.ts`  
  Thêm method `calculateDynamicMargin(costPrice, baseMarginPercent, remainingSeconds, totalSeconds, gamma)` theo công thức decay.
- **[MODIFY]** `financial-service/src/interface/dto/financial.dto.ts`  
  Thêm `GetDynamicMarginRequest` và `GetDynamicMarginResponse` (khớp với `financial.proto` trong Summary.md).
- **[MODIFY]** `financial-service/src/application/services/financial.service.ts`  
  Thêm method `getDynamicMargin(request, tenantId)` sử dụng domain service.
- **[MODIFY]** `financial-service/src/interface/controllers/financial.controller.ts`  
  Thêm `@GrpcMethod('FinancialService', 'GetDynamicMargin')`.

**Công thức cụ thể:**
```typescript
// gamma >= 1, mặc định gamma = 2
const decayFactor = Math.pow(remainingSeconds / totalSeconds, gamma);
const dynamicMarginPercent = baseMarginPercent * decayFactor;
const minAcceptablePrice = costPrice * (1 + dynamicMarginPercent / 100);
```

**Acceptance Criteria:**
- gRPC call `GetDynamicMargin` với `remainingSeconds=0` → trả về `minAcceptablePrice ≈ costPrice` (margin decay về 0)
- gRPC call với `remainingSeconds=totalSeconds` → trả về full margin

---

### TASK-002: Currency Exchange Rate Engine (Financial Service)

**Tại sao cần?**  
Logistics quốc tế tính cước theo USD nhưng xuất hóa đơn VND. Summary.md yêu cầu tỷ giá tự động cập nhật từ Ngân hàng Nhà nước/Vietcombank daily, cache Redis TTL 24h.

**Phạm vi công việc:**
- **[NEW]** `financial-service/src/domain/entities/exchange-rate.entity.ts`  
  Interface cho Exchange Rate.
- **[NEW]** `financial-service/src/infrastructure/jobs/exchange-rate-sync.cron.ts`  
  Cron `'5 0 * * *'` — Gọi Vietcombank Open API, lưu vào DB và cache Redis key `financial:fx:{tenant_id}:{from}_{to}` TTL 24h.  
  *(Phase 1: Mock data từ DB, Phase 2: Real API integration)*
- **[MODIFY]** `financial-service/prisma/schema.prisma`  
  Thêm bảng `exchange_rates` (id, tenant_id, from_currency, to_currency, rate, valid_date, source, created_at).
- **[MODIFY]** `financial-service/src/application/services/financial.service.ts`  
  Thêm method `getExchangeRate(from, to, tenantId)` — đọc từ Redis trước, fallback về DB.
- **[MODIFY]** `financial-service/src/interface/dto/financial.dto.ts`  
  Thêm `GetExchangeRateRequest`, `GetExchangeRateResponse`.
- **[MODIFY]** `financial-service/src/interface/controllers/financial.controller.ts`  
  Thêm `@GrpcMethod('FinancialService', 'GetExchangeRate')`.
- **[MODIFY]** `financial-service/src/billing/billing.module.ts`  
  Đăng ký `ExchangeRateSyncCronJob`.

**Acceptance Criteria:**
- Cron job chạy: Bảng `exchange_rates` có bản ghi `USD → VND` với `valid_date = today`
- gRPC `GetExchangeRate({from:'USD', to:'VND'})` trả về rate và timestamp
- Redis key `financial:fx:...:USD_VND` tồn tại với TTL ~24h

---

### TASK-003: Debit Note & Credit Note (Billing Service)

**Tại sao cần?**  
Summary.md định nghĩa rõ: Khi có phí phát sinh ngoài dự kiến (DEM/DET, kiểm hóa hải quan, cân xe lệch), Billing Service phải tự động sinh **Debit Note** (thu thêm) hoặc **Credit Note** (giảm trừ/hoàn tiền). Đây là nghiệp vụ bắt buộc trong logistics quốc tế và kế toán Việt Nam.

**Phạm vi công việc:**
- **[MODIFY]** `billing-service/prisma/schema.prisma`  
  Thêm bảng `adjustment_notes` (id, tenant_id, invoice_id, type: DEBIT/CREDIT, reason_code: DEMURRAGE/DETENTION/CUSTOMS_INSPECTION/WEIGHT_DISCREPANCY, amount, description, status, created_at).
- **[NEW]** `billing-service/src/interface/dto/billing.dto.ts`  
  Thêm `IssueDebitNoteRequest`, `IssueDebitNoteResponse`, `IssueCreditNoteRequest`, `IssueCreditNoteResponse`.
- **[NEW]** `billing-service/src/application/use-cases/issue-adjustment-note.use-case.ts`  
  Logic: Tạo adjustment_note, cập nhật `invoice.total_amount` += debitAmount hoặc -= creditAmount, tạo payment_record với amount âm cho credit note.
- **[MODIFY]** `billing-service/src/application/services/billing.service.ts`  
  Thêm `issueDebitNote()`, `issueCreditNote()`.
- **[MODIFY]** `billing-service/src/interface/controllers/billing.controller.ts`  
  Thêm `@GrpcMethod('BillingService', 'IssueDebitNote')` và `@GrpcMethod('BillingService', 'IssueCreditNote')`.
- **[NEW]** `billing-service/src/infrastructure/messaging/event-handlers/extra-charge.handler.ts`  
  Consumer lắng nghe RabbitMQ event `shipment.extra_charge_incurred` và tự động gọi `issueDebitNote`.

**Acceptance Criteria:**
- gRPC `IssueDebitNote` với `{invoiceId, reasonCode:'DEMURRAGE', extraAmount:500}` → Tạo adjustment_note, invoice.total_amount tăng $500
- gRPC `IssueCreditNote` → invoice.total_amount giảm, tự động phát event `billing.credit_note_issued`

---

### TASK-004: POD-Triggered Invoice Generation (Billing Service)

**Tại sao cần?**  
Summary.md nêu rõ: **Hóa đơn chính thức CHỈ được phát hành khi có Proof of Delivery (POD)**. Hiện tại `GenerateInvoice` được gọi trực tiếp qua gRPC bất cứ lúc nào — sai với nghiệp vụ.

**Phạm vi công việc:**
- **[MODIFY]** `billing-service/src/infrastructure/messaging/event-handlers/shipment-completed.handler.ts`  
  Rename/Refactor sang `pod-uploaded.handler.ts`. Đổi routing key từ `shipment.completed` sang `shipment.pod_uploaded` VÀ `shipment.container_discharged`. Validate `pod_document_s3_key` có trong payload không.
- **[MODIFY]** `billing-service/src/application/use-cases/generate-invoice.use-case.ts`  
  Thêm field `podDocumentS3Key` vào `GenerateInvoiceInput`. Lưu link POD vào DB cùng hóa đơn.
- **[MODIFY]** `billing-service/prisma/schema.prisma`  
  Thêm column `pod_s3_key` (String nullable) vào bảng `invoices`.

**Acceptance Criteria:**
- RabbitMQ publish event `shipment.pod_uploaded` với `podDocumentS3Key` → Invoice được tự động tạo
- Không có POD key → Invoice không được tạo, throw validation error

---

## 🟠 NHÓM 2 — HIGH (Kiến trúc & Độ tin cậy)

### TASK-005: Offline Buffer & ACK Mechanism (Realtime Hub)

**Tại sao cần?**  
Summary.md định nghĩa rõ cơ chế ACK: Hub gửi message kèm `msgId`, Client gửi lại `ACK(msgId)`. Nếu 5 giây không nhận ACK → lưu vào Redis Stream `stream:offline_msg:{tenant}:{user}`. Khi Client reconnect → tự động flush. Hiện tại Hub push-and-forget, không có đảm bảo tin nhắn được nhận.

**Phạm vi công việc:**
- **[MODIFY]** `realtime-hub-service/src/gateway/events.gateway.ts`  
  Wrap tất cả `server.to(room).emit(event, data)` thành `emitWithAck(room, event, data, msgId)`. Sau khi emit, set Redis key `ws:ack:pending:{msgId}` TTL=5s.
- **[NEW]** `realtime-hub-service/src/gateway/events.gateway.ts`  
  Thêm handler `@SubscribeMessage('ack')` — Client gửi `{msgId}` → Server xóa Redis pending key.
- **[NEW]** `realtime-hub-service/src/messaging/offline-buffer.service.ts`  
  Service dùng Redis Streams: `xadd(stream:offline_msg:{tenant}:{userId}, msgId, payload)` khi ACK timeout.
- **[MODIFY]** `realtime-hub-service/src/gateway/events.gateway.ts`  
  Xử lý `handleConnection` — sau khi xác thực JWT, gọi `offlineBuffer.flush(tenantId, userId)` để xả tin nhắn tồn đọng.

**Acceptance Criteria:**
- Server emit với `msgId`, Client không ACK trong 5s → message tồn tại trong Redis Stream
- Client reconnect → tự động nhận lại messages tồn đọng theo đúng thứ tự

---

### TASK-006: CloudEvents Spec Wrapper cho RabbitMQ Events

**Tại sao cần?**  
Summary.md quy định tất cả Events phải tuân thủ **CloudEvents 1.0 Spec** với các field bắt buộc: `specversion`, `type`, `source`, `id`, `time`, `datacontenttype`, `tenant_id`, `correlation_id`. Hiện tại các events đang dùng plain JSON không chuẩn.

**Phạm vi công việc:**
- **[NEW]** `billing-service/src/common/events/cloud-event.interface.ts`  
  Interface `CloudEvent<T>` với đầy đủ field theo spec.
- **[NEW]** `billing-service/src/common/events/cloud-event.factory.ts`  
  Factory method `createCloudEvent(type, source, tenantId, correlationId, data)` tự động điền `specversion`, `id` (UUIDv4), `time`.
- **[MODIFY]** `billing-service/src/infrastructure/messaging/rabbitmq.service.ts`  
  Wrap tất cả `publishInvoiceCreated()` và `publishPaymentReceived()` bằng `CloudEventFactory`.
- Tương tự cho `financial-service` khi publish `financial.rate_matrix.updated`.

**Acceptance Criteria:**
- Message trong RabbitMQ Exchange có đủ field CloudEvents spec
- `id` là UUID unique mỗi lần publish
- `correlation_id` được propagate từ incoming request tới outgoing event

---

### TASK-007: Idempotency Interceptor (Billing & Financial Service)

**Tại sao cần?**  
Summary.md định nghĩa rõ cơ chế `x-idempotency-key` với Redis lock TTL=120s để chống duplicate calls từ client retry. Hiện tại chỉ có idempotency ở level DB (check shipment_id đã có invoice chưa) chứ không có full Redis-backed idempotency.

**Phạm vi công việc:**
- **[NEW]** `billing-service/src/common/interceptors/idempotency.interceptor.ts`  
  NestJS Interceptor: Check `GET idempotency:{tenant_id}:{key}` trên Redis. Nếu có → return cached. Nếu chưa → `SET NX TTL=120s`, execute, lưu response.
- **[NEW]** `billing-service/src/common/decorators/idempotent.decorator.ts`  
  `@Idempotent()` decorator áp dụng cho các method cần bảo vệ.
- Áp dụng `@Idempotent()` cho: `generateInvoice`, `recordPayment`, `issueDebitNote`.
- Tương tự cho `financial-service` method `estimateCost`.

**Acceptance Criteria:**
- Gọi `RecordPayment` 2 lần liên tiếp với cùng `x-idempotency-key` → lần 2 trả về cached response, không tạo 2 payment records
- Redis key `idempotency:{tenantId}:{key}` tồn tại với TTL ~120s sau lần gọi đầu

---

### TASK-008: Health Check Endpoints (Terminus) — Tất cả 3 Services

**Tại sao cần?**  
Summary.md Production Checklist yêu cầu `/healthz/liveness` và `/healthz/readiness` cho K8s Probes. Thiếu điều này, container không thể deploy lên AWS EKS/Kubernetes đúng cách.

**Phạm vi công việc (lặp lại cho cả 3 services):**
- **[RUN]** `npm install @nestjs/terminus`
- **[NEW]** `*/src/health/health.controller.ts`  
  Controller với `/healthz/liveness` (app up?) và `/healthz/readiness` (DB + Redis up?).
- **[MODIFY]** `*/src/app.module.ts`  
  Thêm `TerminusModule`, `HttpModule`, `HealthController`.

**Acceptance Criteria:**
- `GET /healthz/liveness` → `200 { status: 'ok' }`
- `GET /healthz/readiness` → `200 { db: 'up', redis: 'up' }` hoặc `503` khi DB down

---

### TASK-009: Dead Letter Queue (DLQ) cho RabbitMQ

**Tại sao cần?**  
Summary.md Production Checklist yêu cầu DLQ cho messages fail quá 3 lần retry. Hiện tại nếu consumer xử lý lỗi, message bị nack và có thể loop vô hạn.

**Phạm vi công việc:**
- **[MODIFY]** `billing-service/src/infrastructure/messaging/event-handlers/shipment-completed.handler.ts`  
  Thêm retry counter trong message header. Sau 3 lần fail → publish sang Exchange `logistics_events.dlq` thay vì nack.
- **[NEW]** `billing-service/src/infrastructure/messaging/dlq.service.ts`  
  Service tạo DLQ Exchange + Queue khi startup. Log đầy đủ thông tin failed message.
- Tương tự cho `realtime-hub-service/src/messaging/mq-consumer.service.ts`.

**Acceptance Criteria:**
- Simulate consumer fail 3 lần → Message xuất hiện trong Queue `logistics_events.dlq`
- DLQ message giữ nguyên original payload + thêm field `error_reason`, `retry_count`, `failed_at`

---

## 🟡 NHÓM 3 — MEDIUM (AI Services chưa tồn tại)

> **Lưu ý:** 2 service sau chưa có trong `src/nestjs/`. Cần tạo mới hoàn toàn.

---

### TASK-010: Negotiation Agent Service [AI] — Khởi tạo

**Tại sao cần?**  
Summary.md mô tả đây là AI Service quan trọng nhất: Nhận đề xuất giá từ khách hàng → Query Dynamic Margin → Chạy Deterministic Strategy Engine → Gọi Gemini API sinh câu thoại → Phát event khi cần Human Handoff.

**Phạm vi công việc:**
- **[NEW]** `src/nestjs/negotiation-agent-service/` — Khởi tạo NestJS project mới.
- **[NEW]** `prisma/schema.prisma`  
  Bảng `negotiation_sessions` (id, tenant_id, shipment_id, customer_id, status, current_round, max_rounds, list_price, bottom_price, created_at).  
  Bảng `negotiation_messages` (id, session_id, round, sender: AI/CUSTOMER/HUMAN, message, offer_price, decision: ACCEPT/COUNTER/HANDOFF, created_at).
- **[NEW]** `src/domain/services/negotiation-strategy.domain-service.ts`  
  Pure logic: `determineDecision(offerPrice, bottomPrice, rounds, maxRounds, customerTier)`.  
  Rules: `offerPrice >= bottomPrice → ACCEPT`, `rounds >= maxRounds → HUMAN_HANDOFF`, `customerTier === VIP → HUMAN_HANDOFF`, else `COUNTER_OFFER`.
- **[NEW]** `src/application/services/negotiation.service.ts`  
  1. Gọi gRPC `GetDynamicMargin` từ Financial Service.  
  2. Chạy `NegotiationStrategyDomainService.determineDecision()`.  
  3. Gọi Gemini API (`@google/generative-ai`) với structured output để sinh câu thoại.  
  4. Lưu message vào DB.  
  5. Publish event `negotiation.human_handoff_requested` nếu cần.
- **[NEW]** gRPC interface: `SubmitOffer`, `GetNegotiationHistory`, `CloseNegotiation`.
- **[NEW]** `src/infrastructure/ai/gemini.client.ts`  
  Client wrapper cho `@google/generative-ai` với function calling + timeout 3.5s fallback.

**Acceptance Criteria:**
- gRPC `SubmitOffer({offerPrice: 1000, bottomPrice: 800})` → Decision: ACCEPT, Gemini sinh câu thoại
- `offerPrice: 500` → Decision: COUNTER_OFFER với giá counter > 800
- RabbitMQ event `negotiation.human_handoff_requested` được phát khi rounds >= maxRounds

---

### TASK-011: Customer Assistant Service [AI] — Khởi tạo

**Tại sao cần?**  
Summary.md: Khách hàng chat hỏi "Đơn hàng đang ở đâu?", "Công nợ tháng này bao nhiêu?" qua NLP. Service dùng **RAG + Read Model (CQRS)** — KHÔNG query thẳng DB giao dịch, mà đọc từ Read Replica/Elasticsearch được sync bằng Events.

**Phạm vi công việc:**
- **[NEW]** `src/nestjs/customer-assistant-service/` — Khởi tạo NestJS project mới.
- **[NEW]** Read Model DB setup: PostgreSQL Read Replica hoặc Elasticsearch index `customer_read_model` (shipment_status, invoice_summary per customer).
- **[NEW]** `src/infrastructure/messaging/read-model-sync.handler.ts`  
  Consumer lắng nghe events: `shipment.status_changed`, `invoice.created`, `payment.received` → Cập nhật Read Model.
- **[NEW]** `src/application/services/customer-assistant.service.ts`  
  Nhận câu hỏi tự nhiên → Gemini Function Calling xác định `intent` (track_shipment / check_balance / list_invoices) → Query Read Model → Gemini tổng hợp câu trả lời.
- **[NEW]** REST API (BFF-facing): `POST /chat`, `GET /conversations/{id}`.

**Acceptance Criteria:**
- POST `/chat` với `{"message": "Đơn hàng SHP-001 đang ở đâu?"}` → Trả lời đúng trạng thái từ Read Model
- Chat không bao giờ query trực tiếp `billing_service` DB — chỉ đọc Read Model

---

## 🔵 NHÓM 4 — LOW (Tối ưu & Production Hardening)

### TASK-012: Redis Rate Caching cho Financial Service

**Tại sao cần?**  
Summary.md: Mỗi khi Base Rates thay đổi → publish `financial.rate_matrix.updated`. BFF/Negotiation read-cache bảng giá vào Redis để gọi nội bộ < 2ms, tránh gRPC liên tục.

**Phạm vi công việc:**
- **[MODIFY]** `financial-service` — Sau khi admin update `base_freight_rates` → publish event `financial.rate_matrix.updated`.
- **[NEW]** `financial-service/src/infrastructure/cache/rate-cache.service.ts`  
  Warm Redis key `financial:rates:{tenant_id}:{route_key}` khi service khởi động và khi có update event.

---

### TASK-013: Circuit Breaker cho gRPC calls

**Tại sao cần?**  
Summary.md Fallback Matrix: Khi `FinancialService` chậm/down → dùng Last-Known-Good Rate từ Redis, thêm flag `is_estimated_fallback: true`. Hiện tại không có circuit breaker.

**Phạm vi công việc:**
- **[RUN]** `npm install cockatiel` trong `billing-service`.
- **[MODIFY]** `billing-service/src/infrastructure/grpc-clients/financial.grpc-client.ts`  
  Wrap `estimateCost()` call trong Cockatiel Circuit Breaker. Khi circuit open → return cached rate từ Redis + `isFallback: true`.

---

### TASK-014: Structured Logging với Correlation ID

**Tại sao cần?**  
Summary.md Production Checklist: Mọi Request/Event phải inject `correlation_id` vào log dạng Structured JSON cho OpenTelemetry/Loki.

**Phạm vi công việc:**
- **[RUN]** `npm install nestjs-pino pino-pretty` trong tất cả 3 services.
- **[NEW]** `*/src/common/middleware/correlation-id.middleware.ts`  
  Sinh UUIDv4 `correlation_id` nếu không có trong header, inject vào gRPC Metadata và Log context.
- **[MODIFY]** `*/src/app.module.ts` — Thêm `LoggerModule` từ `nestjs-pino`.

---

### TASK-015: e-Invoice Gateway Adapter (Billing Service)

**Tại sao cần?**  
Summary.md mô tả adapter kết nối VNPT/Viettel Invoice/MISA để ký số và lấy Mã Cơ quan Thuế. Bắt buộc cho pháp lý Việt Nam khi dùng thật.

**Phạm vi công việc:**
- **[NEW]** `billing-service/src/infrastructure/einvoice/einvoice.adapter.ts`  
  Interface `EInvoiceAdapter` với method `signAndIssue(invoiceId, tenantId)`.
- **[NEW]** `billing-service/src/infrastructure/einvoice/vnpt-einvoice.adapter.ts`  
  Implementation gọi VNPT API (Phase 1: Mock).
- **[MODIFY]** `billing-service/src/application/use-cases/generate-invoice.use-case.ts`  
  Sau khi generate invoice → gọi `eInvoiceAdapter.signAndIssue()`, lưu tax authority code.

---

## III. THỨ TỰ THỰC HIỆN ĐỀ XUẤT

```
SPRINT 1 (Tuần 1-2) — Critical Foundation:
  TASK-001: Dynamic Margin Decay Engine
  TASK-002: Exchange Rate Engine (Mock API trước)
  TASK-003: Debit Note / Credit Note
  TASK-004: POD-Triggered Invoice

SPRINT 2 (Tuần 3-4) — Architecture Hardening:
  TASK-005: Offline Buffer & ACK (Realtime Hub)
  TASK-006: CloudEvents Spec Wrapper
  TASK-007: Idempotency Interceptor (Redis)
  TASK-008: Health Check Endpoints (Terminus)
  TASK-009: Dead Letter Queue (DLQ)

SPRINT 3 (Tuần 5-8) — AI Services:
  TASK-010: Negotiation Agent Service [Khởi tạo + Strategy Engine]
  TASK-011: Customer Assistant Service [Khởi tạo + RAG Read Model]

SPRINT 4 (Ongoing) — Production Hardening:
  TASK-012: Redis Rate Caching
  TASK-013: Circuit Breaker (Cockatiel)
  TASK-014: Structured Logging (Pino + Correlation ID)
  TASK-015: e-Invoice Gateway (VNPT Mock)
```

---

## IV. NHỮNG GÌ ĐÚNG TRONG SUMMARY.MD VÀ ĐÃ REFLECTED

| Mục trong Summary.md | Trạng thái |
|---|---|
| Event-Driven + CQRS Principles | ✅ Đúng với code hiện tại |
| Multi-Tenant isolation (tenant_id mọi query) | ✅ Đã có TenantInterceptor |
| gRPC Exception Filter → Standard Status Codes | ✅ Đã có GrpcExceptionFilter |
| Zero Hardcoding (ConfigService + class-validator) | ✅ Đã có env.validation.ts |
| Choreography Saga Flow (diagram) | ✅ Logic đúng, Events đúng routing key |
| S3/R2 Pathing Standard | ✅ `tenants/{tenantId}/billing/...` |
| Composite DB Indexes (SQL) | ⚠️ Chưa có migration file tạo indexes |

> **Lưu ý Production Checklist:** Phần "đã check ✅" trong Summary.md thực tế **chưa phải toàn bộ đã done** — đây là checklist mục tiêu. Cụ thể: Composite Indexes chưa có file migration, Health Check chưa có endpoint, DLQ chưa có, Correlation ID chưa được inject xuyên suốt.

---

## V. TÓM TẮT NHANH CHO NGƯỜI THỰC HIỆN

**Trước khi bắt đầu Task nào, kỹ sư PHẢI:**
1. Đọc `AGENTS.md` ở root project
2. Chạy `git status --short`
3. Chạy `npm run build` để có baseline build pass
4. Implement đúng Active Phase trong `codex/plan.md`

**Stack bắt buộc:**
- NestJS 10 + TypeScript strict
- Prisma ORM + PostgreSQL (Supabase)
- `@nestjs/config` + `class-validator` — KHÔNG dùng `process.env` trực tiếp
- gRPC port: Financial=5003, Billing=5004, Realtime=5005, Negotiation=5006, CustomerAssistant=5007

**File tài liệu kỹ thuật chi tiết từng service (luôn cập nhật sau khi implement):**
- [billing_service_spec.md](file:///d:/aurora/aurora-server/docs/documents/huynh/billing_service_spec.md)
- [financial_service_spec.md](file:///d:/aurora/aurora-server/docs/documents/huynh/financial_service_spec.md)
- [realtime_hub_service_spec.md](file:///d:/aurora/aurora-server/docs/documents/huynh/realtime_hub_service_spec.md)
- [Sumary.md](file:///d:/aurora/aurora-server/docs/documents/huynh/Sumary.md) — Kiến trúc tổng thể & chuẩn thiết kế
