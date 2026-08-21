# TÀI LIỆU KỸ THUẬT VÀ CHUẨN THIẾT KẾ (TECHNICAL SPECIFICATION & STANDARDS)

**Hệ thống Microservices Logistics Multi-Tenant & Multi-Agent (NestJS Ecosystem)**
*Tác giả: Principal System Architect & Senior Logistics Domain Expert*

---

## I. EXECUTIVE SUMMARY & ARCHITECTURAL PRINCIPLES

### 1. Tầm nhìn Kiến trúc

Hệ thống được thiết kế theo kiến trúc **Event-Driven, CQRS, và Async-First Microservices** nhằm giải quyết triệt để các thách thức trong ngành Digital Freight Brokerage & Forwarding:

* **Zero Single Point of Failure (SPOF) & Cascading Failures:** Loại bỏ tối đa các gRPC calls đồng bộ giữa các service trên luồng transaction. Chuyển sang Read-Model Caching (Redis) và Asynchronous Event-Driven Messaging (RabbitMQ).
* **Chính xác về Tài chính & Pháp lý:** Phân tách triệt để giữa **Quote (Báo giá)** và **Invoice (Hóa đơn)**. Hóa đơn chính thức chỉ được phát hành khi có **Proof of Delivery (POD)** hoặc hàng đã hạ cảng, kèm luồng xử lý **Debit/Credit Note** cho các chi phí phát sinh thực tế (DEM/DET, kiểm hóa, cân xe).
* **AI Safety & Deterministic Guardrails:** Kết hợp giữa **Deterministic Engine (NestJS)** chịu trách nhiệm ra quyết định con số/tài chính và **Generative AI Layer (Gemini)** chịu trách nhiệm diễn đạt ngôn ngữ tự nhiên. AI tuyệt đối không có quyền tự quyết định giá tiền mà không thông qua Engine kiểm duyệt.
* **Strict Multi-Tenancy & Data Isolation:** Phân lập tuyệt đối dữ liệu giữa các Tenancies từ tầng Database (Row-Level Security / Tenant ID filtering), Event Bus Header, Redis Key Namespace cho đến Cloud Storage (Cloudflare R2 / AWS S3 Path).

---

### 2. Sơ đồ Kiến trúc Tổng thể (High-Level Topology)

```
                                  [ Cloudflare DNS / WAF ]
                                             │
                                             ▼
                                  [ AWS Application LB ]
                                             │
                                             ▼
                                     [ Staff.BFF Gateway ]
                                             │
      ┌──────────────────────────────┬───────┴──────────────────────────────┐
      │ (Sync gRPC - Internal Only)  │ (Async Events - RabbitMQ AMQP)       │ (WebSocket)
      ▼                              ▼                                      ▼
┌───────────────────────────┐  ┌───────────────────────────┐  ┌───────────────────────────┐
│ Financial Service [CORE]  │  │ Billing Service [CORE]    │  │ Realtime Hub Service      │
│ - Rate & Tariff Engine    │  │ - Invoicing & Settlement  │  │ - Socket.io + Redis Adapter│
│ - Dynamic Margin Decay    │  │ - Debit/Credit Notes      │  │ - Offline Buffer & Ack    │
│ - FX Exchange Engine      │  │ - e-Invoice Integration   │  │ - Multi-Tenant Rooms      │
└─────────────┬─────────────┘  └─────────────┬─────────────┘  └───────────────────────────┘
              │                              │
              ▼                              ▼
┌───────────────────────────┐  ┌───────────────────────────┐
│ Customer Assistant [AI]   │  │ Negotiation Agent [AI]    │
│ - RAG Read Model (CQRS)   │  │ - Dynamic Margin Query    │
│ - Read-Only DB Replicas   │  │ - Strict Guardrail Engine │
│ - Gemini Function Calling │  │ - Human-in-the-Loop       │
└───────────────────────────┘  └───────────────────────────┘

```

---

## II. DETAILED TECHNICAL FLOWS & SAGA TRANSACTIONS

### 1. Chi tiết Luồng Kỹ thuật từng Service

#### A. Financial Service: Tariff Engine & Dynamic Margin

* **Quy đổi Chargeable Weight chuẩn Logistics:**
* **Air Freight:** $\text{Chargeable Weight (kg)} = \max\left(\text{Gross Weight}, \frac{\text{Volume } (m^3)}{0.006}\right) = \max\left(\text{Gross Weight}, \text{Volume } (m^3) \times 167\right)$
* **Road Freight:** $\text{Chargeable Weight (kg)} = \max\left(\text{Gross Weight}, \text{Volume } (m^3) \times 333\right)$
* **Sea Freight (LCL):** Tính theo **Revenue Ton (RT)** = $\max\left(\text{Gross Weight (Tấn)}, \text{Volume } (m^3)\right)$ (với tỷ lệ $1 m^3 = 1000 \text{ kg}$).


* **Currency Exchange Engine:** Tự động đồng bộ tỷ giá USD/VND/EUR từ Ngân hàng Nhà nước/Vietcombank daily lúc 00:05 UTC, cache trên Redis (`financial:fx:{tenant_id}:{currency}`) với TTL = 24h.
* **Dynamic Margin Calculator:** Tính toán biên độ đàm phán cho Negotiation Agent dựa trên thời gian còn lại tới giờ cut-off:

$$\text{Min Acceptable Price} = \text{Base Cost} \times \left(1 + \text{Base Margin \%} \times \left( \frac{T_{\text{remaining}}}{T_{\text{total}}} \right)^\gamma \right)$$



*(Trong đó $\gamma \ge 1$ là hệ số suy giảm lợi nhuận theo thời gian).*
* **Read-Optimization:** Mỗi khi Bảng giá (Base Rates) thay đổi, `FinancialService` publish Event `financial.rate_matrix.updated` lên RabbitMQ. Các service tiêu thụ (BFF, Negotiation) read-cache bảng giá vào Local Memory/Redis để gọi nội bộ với độ trễ $< 2\text{ms}$, loại bỏ việc gọi gRPC đồng bộ liên tục.

#### B. Billing Service: Quote vs. Invoice & Settlement

* **Phân tách Luồng Vận hành:**
1. **Quotation / Booking Confirmation:** Được phát hành khi chốt đàm phán. Không ghi nhận doanh thu kế toán.
2. **Invoice (Hóa đơn chính thức):** CHỈ được kích hoạt tạo tự động khi nhận Event `shipment.pod_uploaded` (đã có biên bản giao hàng) hoặc `shipment.container_discharged` (hàng đã hạ cảng).


* **Debit Note / Credit Note Handling:** Khi có phí phát sinh ngoài dự kiến (DEM/DET, kiểm hóa hải quan, cân xe lệch trọng lượng):
* Service nhận Event `shipment.extra_charge_incurred`.
* Tự động sinh **Debit Note** (Yêu cầu thu thêm) hoặc **Credit Note** (Giảm trừ/Hoàn tiền).


* **e-Invoice Gateway:** Adapter kết nối REST API với đơn vị phát hành Hóa đơn điện tử (VNPT / Viettel Invoice / MISA) để ký số và lấy Mã của Cơ quan Thuế.

#### C. Negotiation Agent: Human-in-the-Loop & Dynamic Guardrails

* **Guardrail Flow:**
1. Nhận đề xuất giá từ Khách hàng.
2. Query `Dynamic Margin` từ Redis Cache/Financial Service để xác định `Bottom Price` thực tế tại thời điểm $t$.
3. Thực thi `Deterministic Strategy Engine` (NestJS):
* Nếu $P_{\text{offer}} < P_{\text{bottom}}$: Quyết định = `COUNTER_OFFER` với giá $P_{\text{counter}} = \max(P_{\text{bottom}}, \text{Algorithmic Step})$.
* Nếu $Rounds \ge N_{\text{max}}$ HOẶC $Customer_{\text{tier}} == \text{'ENTERPRISE' Mais/VIP}$: Quyết định = `HUMAN_HANDOFF`.


4. Chuyển Quyết định + Bối cảnh sang **Gemini API** để sinh câu thoại hiển thị cho Khách hàng.


* **Human-in-the-Loop (Handoff):** Phát Event `negotiation.human_handoff_requested` qua RabbitMQ. `Realtime Hub` đẩy thông báo khẩn cấp tới Portal của Nhân viên Sales/Dispatcher kèm toàn bộ Lịch sử chat (Negotiation Logs).

#### D. Realtime Hub: Offline-First & Reliable Messaging

* **Stateless Cluster:** Socket.io Server kết nối với Redis Pub/Sub Adapter. Mọi Node Socket đều có thể gửi tin tới bất kỳ Client nào.
* **WebSocket Ack Mechanism:**
```
Client (App)                     Realtime Hub                     RabbitMQ
   │                                  │                              │
   │─── Establish WS Connection ─────►│                              │
   │    (JWT + TenantId)              │                              │
   │                                  │◄── Consumer (invoice.created)│
   │◄── Send Event (with MsgId) ──────│                              │
   │                                  │                              │
   │─── Send ACK (MsgId) ────────────►│                              │
   │                                  │─── Mark Delivered in Redis ─►│
   │                                  │    (Remove from Buffer)      │

```


* Nếu trong $5$ giây Hub không nhận được `ACK` từ Client: Lưu Message vào Redis Buffer (`stream:offline_msg:{tenant_id}:{user_id}`). Khi Client `reconnect`, Hub sẽ tự động xả Buffer (Flush) theo đúng thứ tự.



#### E. Customer Assistant: RAG Read Model (CQRS)

* **Read-Only Architecture:** Khách hàng hỏi "Đơn hàng của tôi đang ở đâu?", "Công nợ tháng này bao nhiêu?".
* Customer Assistant **KHÔNG TRUY VẤN TRỰC TIẾP** DB giao dịch của Billing hay Shipment. Nó truy vấn vào **Read-Model Database (Elasticsearch / PostgreSQL Read Replica)** được đồng bộ bất đồng bộ qua RabbitMQ Events.
* Sử dụng **Gemini Function Calling** chỉ định hướng truy vấn vào các Read APIs đã chuẩn hóa.

---

### 2. Quản lý Giao dịch Phức tạp: Saga Pattern (Choreography-based)

Luồng từ **Đặt hàng $\rightarrow$ Tính phí $\rightarrow$ Thực thi $\rightarrow$ Giao hàng $\rightarrow$ Hóa đơn $\rightarrow$ Quyết toán** được quản lý bằng **Choreography Saga**:

```
[Customer Assistant / FE] ──(1. Place Booking)──► [Shipment Workflow Service]
                                                         │
                                               (Event: shipment.created)
                                                         │
                                                         ▼
                                             [Financial Service]
                                             (Calculate Estimated Cost)
                                                         │
                                            (Event: cost.estimated)
                                                         │
                                                         ▼
                                             [Shipment Workflow Service]
                                             (Executes Transport / GPS Track)
                                                         │
                                            (Event: shipment.pod_uploaded)
                                                         │
                        ┌────────────────────────────────┴────────────────────────────────┐
                        ▼                                                                 ▼
             [Billing Service]                                              [Realtime Hub Service]
      (Generate Official Invoice)                                     (Notify Shipper & Accounting)
                        │
       (Event: invoice.issued)
                        │
                        ▼
         [e-Invoice Provider Adapter]
          (Sign & Issue Tax Invoice)

```

#### Compensating Transactions (Luồng đền bù khi có lỗi):

* Nếu `Billing Service` phát hiện Khách hàng đã vượt quá hạn mức tín dụng (`Credit Limit Exceeded`) khi lô hàng đã tạo:
1. Publish Event `billing.credit_check_failed`.
2. `Shipment Workflow Service` bắt event $\rightarrow$ Chuyển trạng thái Lô hàng sang `ON_HOLD_PENDING_PAYMENT` (Tạm dừng xuất kho/giao hàng).
3. `Notification Service` & `Realtime Hub` bắn cảnh báo tới Khách hàng và Kế toán.



---

## III. SERVICE CONTRACTS & DATA STANDARDS

### 1. Protobuf Schema Definitions

#### `financial.proto`

```protobuf
syntax = "proto3";

package aurora.financial.v1;

option go_package = "aurora/financial/v1;financialv1";

service FinancialService {
  rpc EstimateCost (EstimateCostRequest) returns (EstimateCostResponse);
  rpc GetCustomsDuty (GetCustomsDutyRequest) returns (CustomsDutyResponse);
  rpc GetDynamicMargin (GetDynamicMarginRequest) returns (GetDynamicMarginResponse);
}

enum TransportMode {
  TRANSPORT_MODE_UNSPECIFIED = 0;
  TRANSPORT_MODE_SEA = 1;
  TRANSPORT_MODE_AIR = 2;
  TRANSPORT_MODE_ROAD = 3;
}

message CargoSpec {
  double gross_weight_kg = 1;
  double length_cm = 2;
  double width_cm = 3;
  double height_cm = 4;
  double cargo_value_amount = 5;
  string cargo_value_currency = 6;
}

message EstimateCostRequest {
  string tenant_id = 1;
  string correlation_id = 2;
  TransportMode mode = 3;
  string origin_code = 4;
  string destination_code = 5;
  CargoSpec cargo = 6;
  string hs_code = 7;
}

message EstimateCostResponse {
  double base_freight_charge = 1;
  double chargeable_weight_calculated = 2;
  double port_handling_fee = 3;
  double customs_duty_fee = 4;
  double total_estimated_cost = 5;
  string currency = 6;
  double exchange_rate_applied = 7;
}

message GetDynamicMarginRequest {
  string tenant_id = 1;
  string shipment_id = 2;
  int64 remaining_seconds_to_cutoff = 3;
}

message GetDynamicMarginResponse {
  double list_price = 1;
  double min_acceptable_price = 2; // Bottom price computed dynamically
  string currency = 3;
}

```

#### `billing.proto`

```protobuf
syntax = "proto3";

package aurora.billing.v1;

service BillingService {
  rpc GenerateInvoiceFromPOD (GenerateInvoiceRequest) returns (GenerateInvoiceResponse);
  rpc IssueDebitNote (DebitNoteRequest) returns (DebitNoteResponse);
}

message GenerateInvoiceRequest {
  string tenant_id = 1;
  string correlation_id = 2;
  string shipment_id = 3;
  string customer_id = 4;
  string pod_document_s3_key = 5;
}

message GenerateInvoiceResponse {
  string invoice_id = 1;
  string invoice_number = 2;
  double total_amount = 3;
  string pdf_download_url = 4;
  string status = 5; // UNPAID, PAID
}

message DebitNoteRequest {
  string tenant_id = 1;
  string correlation_id = 2;
  string invoice_id = 3;
  string reason_code = 4; // DEMURRAGE, DETENTION, CUSTOMS_INSPECTION
  double extra_amount = 5;
  string description = 6;
}

message DebitNoteResponse {
  string debit_note_id = 1;
  double new_total_amount = 2;
}

```

---

### 2. Event Payloads Standard (RabbitMQ JSON Schema)

Tất cả các Event trong toàn bộ hệ thống BẮT BUỘC tuân thủ CloudEvents Spec Wrapper:

```json
{
  "specversion": "1.0",
  "type": "com.aurora.billing.invoice.issued",
  "source": "/services/billing-service",
  "id": "evt_9b1deb4d-3b7d-4bad-9bdd-2b0d7b3dcb6d",
  "time": "2026-08-10T15:00:00Z",
  "datacontenttype": "application/json",
  "tenant_id": "tenant_corp_alpha",
  "correlation_id": "corr_8f7b6a5c-4d3e-2f1a",
  "data": {
    "invoice_id": "inv_77123940",
    "invoice_number": "INV-202608-0089",
    "shipment_id": "shp_33019284",
    "customer_id": "cust_88291002",
    "subtotal": 15000000.00,
    "tax_amount": 1200000.00,
    "total_amount": 16200000.00,
    "currency": "VND",
    "pdf_s3_key": "tenants/tenant_corp_alpha/billing/invoices/2026/INV-202608-0089.pdf"
  }
}

```

---

### 3. Standards về Idempotency & Multi-Tenancy Isolation

#### A. Idempotency Rule (`x-idempotency-key`)

* Tất cả các API REST / gRPC tạo giao dịch (Billing, Cost Estimation, Settlement) phải yêu cầu HTTP Header `x-idempotency-key: <UUIDv4>`.
* **NestJS Implementation Strategy:**
1. Interceptor bắt `x-idempotency-key`.
2. Check trong Redis: `GET idempotency:{tenant_id}:{key}`.
3. Nếu tồn tại $\rightarrow$ Trả về ngay lập tức Cached Response.
4. Nếu chưa $\rightarrow$ Set Lock với TTL = 120 giây, xử lý logic, lưu Response vào Redis, sau đó trả về cho Client.



#### B. Storage Pathing Standards (Cloudflare R2 / AWS S3)

Tuyệt đối tuân thủ cấu trúc lưu trữ phân lập đa người thuê:

```text
s3://hrm-private-docs/tenants/{tenantId}/billing/invoices/{year}/{invoiceId}.pdf
s3://hrm-private-docs/tenants/{tenantId}/shipments/{shipmentId}/pod/{fileId}.png
s3://hrm-private-docs/tenants/{tenantId}/negotiations/{sessionId}/draft_contract.pdf

```

---

## IV. EDGE CASES, SECURITY & FALLBACK STRATEGIES

### 1. Resilience & Fallback Matrix

| Kịch bản Sự cố (Edge Case) | Cơ chế Phát hiện (Detection) | Chiến lược Xử lý & Fallback (Resilience) |
| --- | --- | --- |
| **`FinancialService` bị chậm/down** | NestJS Circuit Breaker (Resilience4j / Cockatiel) mở circuit khi error rate > 50% trong 10s. | **Fallback:** Trích xuất Bảng giá cũ gần nhất (`Last-Known-Good Rate`) từ Redis Read-Cache. Thêm Flag `is_estimated_fallback: true` trong response. |
| **Google Gemini API Timeout / Invalid JSON** | Timeout Interceptor (> 3.5 giây) hoặc JSON Validation Filter thất bại. | **Fallback:** Chuyển ngay sang **Deterministic Rule Engine**. Trả về câu hội thoại mặc định đóng gói sẵn trong template NestJS. |
| **Nghẽn mạng App Tài xế (Mất kết nối Socket)** | Realtime Hub không nhận được `ACK` cho message quan trọng. | **Buffer & Retry:** Đưa message vào Redis Stream `stream:offline_msg:{tenant}:{user}`. Khi tài xế Online lại, tự động PULL & FLUSH toàn bộ tin chưa đọc. |
| **AI Negotiation bị Prompt Injection** | Input chứa các chuỗi lệnh can thiệp system prompt ("Forget all rules..."). | **Strict Guardrail Layer:** NestJS Sanitize Input -> Chạy Strategy Engine chốt số tiền trước -> Ép Gemini CHỈ nhận Variable dạng JSON đã qua Validation. |

---

## 2. Race Condition & Concurrency Control

### Kịch bản

2 Tài xế cùng chốt nhận 1 đơn hàng (Shipment) HOẶC 2 Khách hàng cùng tranh chấp cơ hội đặt Container cuối cùng trên chuyến tàu.

### Giải pháp kỹ thuật: Redis Distributed Lock (Redlock)

```typescript
// NestJS Code Pattern for Concurrency Control
import { Injectable } from '@nestjs/common';
import Redlock from 'redlock';

@Injectable()
export class ShipmentBookingService {
  constructor(private readonly redlock: Redlock) {}

  async acceptShipmentOffer(tenantId: string, shipmentId: string, driverId: string) {
    const resourceKey = `locks:tenant:${tenantId}:shipment:${shipmentId}`;
    const ttl = 3000; // 3 seconds lock time

    try {
      const lock = await this.redlock.acquire([resourceKey], ttl);
      
      try {
        // 1. Double check shipment status inside lock
        const shipment = await this.shipmentRepo.findOne({ where: { id: shipmentId, tenantId } });
        if (shipment.status !== 'AVAILABLE') {
          throw new ConflictException('Lô hàng đã được tài xế khác tiếp nhận.');
        }

        // 2. Assign driver and change status
        shipment.status = 'ASSIGNED';
        shipment.driverId = driverId;
        await this.shipmentRepo.save(shipment);

        return { success: true, message: 'Nhận đơn thành công.' };
      } finally {
        // 3. Release lock
        await lock.release();
      }
    } catch (err) {
      throw new ConflictException('Hệ thống đang xử lý giao dịch khác, vui lòng thử lại.');
    }
  }
}

```

---

## V. PRODUCTION READINESS CHECKLIST

Dành cho **Đào Huỳnh** và Đội ngũ Kỹ thuật NestJS trước khi deploy lên môi trường Staging/Production (AWS EKS):

### 1. NestJS Service Architecture Review

* [x] Tất cả các Module đã khai báo `@nestjs/config` validation bằng `Joi` hoặc `class-validator`. Không còn biến `process.env` nào gọi trực tiếp trong code mà không qua ConfigService.
* [x] Tất cả DB Access Entities đã bọc Filter `tenant_id` mặc định (Prisma Middleware / TypeORM Subscriber).
* [x] gRPC Interceptors đã được cấu hình để chuyển đổi Exception từ NestJS thành gRPC Status Code chuẩn (`INVALID_ARGUMENT`, `NOT_FOUND`, `UNAUTHENTICATED`).

### 2. Database & Caching Optimization

* [x] Tạo đầy đủ Composite Indexes cho PostgreSQL:
```sql
CREATE INDEX idx_rates_lookup ON base_freight_rates (tenant_id, origin_code, destination_code, transport_mode);
CREATE INDEX idx_invoices_tenant_status ON invoices (tenant_id, status, due_date);
CREATE INDEX idx_negotiation_session ON negotiation_sessions (tenant_id, shipment_id, status);

```


* [x] Redis Adapter cho Socket.io đã được cấu hình Cluster Mode.

### 3. Reliability, Logging & Observability

* [x] Mọi Request/Event đều đã được inject `correlation_id` vào Header và ghi log dạng Structured JSON (Sử dụng `pino` hoặc `winston`) để OpenTelemetry/Loki gom log dễ dàng.
* [x] Đã cấu hình Health Check Endpoints (`/healthz/liveness`, `/healthz/readiness`) sử dụng `@nestjs/terminus` cho Kubernetes K8s Probes.
* [x] Đã thiết lập Dead Letter Queue (DLQ) cho RabbitMQ cho các message bị fail quá 3 lần retry.

---

*Tài liệu Kỹ thuật này chuẩn hóa toàn bộ kiến trúc cho 5 Microservices Core/AI. Đội ngũ Kỹ thuật thực thi đúng các tiêu chuẩn Data Contract, Saga Pattern và Fallback Matrix được mô tả ở trên.*