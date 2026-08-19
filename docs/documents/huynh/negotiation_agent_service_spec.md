# TÀI LIỆU KỸ THUẬT VÀ NGHIỆP VỤ NEGOTIATION AGENT AI SERVICE [AI + CORE]

> **Phụ trách (Owner):** Đào Huỳnh  
> **Công nghệ:** NestJS (TypeScript), Gemini 1.5 Flash AI, Prisma ORM, PostgreSQL (Supabase Cloud - Schema `negotiation_service`), gRPC & REST API  
> **Cổng giao tiếp:** HTTP `5006` / gRPC `5006`  
> **File Hợp đồng gRPC:** `protos/negotiation.proto`  

---

## 1. TỔNG QUAN VÀ MỤC TIÊU PHÂN HỆ

Dịch vụ **Negotiation Agent AI Service** là phân hệ trí tuệ nhân tạo chuyên biệt chịu trách nhiệm tự động đàm phán cước phí vận chuyển với khách hàng/chủ hàng.

### Nguyên tắc An toàn AI cốt lõi (AI Safety & Deterministic Guardrails):
1. **Phân tách trách nhiệm (Separation of Concerns):**  
   - **Deterministic Engine (NestJS):** Chịu trách nhiệm 100% tính toán giá tiền, quyết định `ACCEPT`, `COUNTER_OFFER`, `HUMAN_HANDOFF` hoặc `REJECT`. AI tuyệt đối KHÔNG ĐƯỢC TỰ Ý QUYẾT ĐỊNH CON SỐ TÀI CHÍNH.
   - **Generative AI Layer (Gemini 1.5 Flash):** Chịu trách nhiệm chuyển quyết định đã chốt thành câu hội thoại tiếng Việt tự nhiên, lịch sự, chuyên nghiệp.
2. **Quy tắc Chuyển cho Con người (Human-in-the-Loop Handoff):**  
   - Tự động chuyển cuộc thoại cho Chuyên viên Sales/Dispatcher khi số vòng đàm phán vượt quá hạn mức (`currentRound >= maxRounds`).
   - Tự động chuyển cho Con người nếu Khách hàng thuộc phân khúc `VIP` hoặc `ENTERPRISE`.

---

## 2. NGUYÊN TẮC THIẾT KẾ VÀ KIẾN TRÚC (CLEAN ARCHITECTURE)

```text
src/nestjs/negotiation-agent-service/
├── prisma/
│   └── schema.prisma                  # PostgreSQL Schema (negotiation_sessions, negotiation_messages)
├── src/
│   ├── domain/
│   │   └── services/
│   │       └── negotiation-strategy.domain-service.ts # ★ Pure Deterministic Guardrail Engine
│   ├── infrastructure/
│   │   ├── ai/
│   │   │   └── gemini.client.ts       # ★ Gemini 1.5 Flash Wrapper + Timeout 3.5s & Fallback
│   │   └── prisma/
│   │       └── prisma.service.ts      # DB Connection
│   ├── application/
│   │   └── services/
│   │       └── negotiation.service.ts # Orchestrator Strategy + Gemini + DB Save
│   ├── interface/
│   │   └── controllers/
│   │       └── negotiation.controller.ts # REST & gRPC Handlers
│   └── health/
│       └── health.controller.ts       # Terminus K8s Probes (/healthz/liveness & readiness)
```

---

## 3. LOGIC NGHIỆP VỤ & CÔNG THỨC TOÁN HỌC

### 3.1. Quy tắc Quyết định Đàm phán (Deterministic Strategy Rules)

$$\text{Decision} = \begin{cases} 
\text{HUMAN\_HANDOFF} & \text{nếu } Customer_{\text{tier}} \in \{\text{'VIP'}, \text{'ENTERPRISE'}\} \\
\text{ACCEPT} & \text{nếu } P_{\text{offer}} \ge P_{\text{bottom}} \\
\text{HUMAN\_HANDOFF} & \text{nếu } Round_{\text{current}} \ge Round_{\text{max}} \\
\text{COUNTER\_OFFER} & \text{ngược lại}
\end{cases}$$

### 3.2. Công thức Bước giá Đàm phán (Counter Offer Step Formula)

$$\text{Counter Offer Price} = \max\left(P_{\text{bottom}}, P_{\text{offer}} + (P_{\text{list}} - P_{\text{offer}}) \times 0.4\right)$$

*Ví dụ:* Price List = $1500, Bottom = $1200, Offer = $1000.  
➔ Counter = $\max(1200, 1000 + (1500 - 1000) \times 0.4) = 1000 + 200 = \$1200$.

---

## 4. CHI TIẾT SCHEMA DATABASE & API CONTRACTS

### 4.1. Database Schema (`prisma/schema.prisma`)

1. **`negotiation_sessions`**:
   - `id` (UUID), `tenant_id` (String), `shipment_id` (String), `customer_id` (String), `status` (`OPEN`/`ACCEPTED`/`REJECTED`/`HANDOFF`/`EXPIRED`), `current_round` (Int), `max_rounds` (Int), `list_price` (Float), `bottom_price` (Float).
2. **`negotiation_messages`**:
   - `id` (UUID), `session_id` (String FK), `round` (Int), `sender` (`AI`/`CUSTOMER`/`HUMAN`), `message` (String), `offer_price` (Float), `decision` (`ACCEPT`/`COUNTER_OFFER`/`HUMAN_HANDOFF`).

### 4.2. REST API Endpoints
- `POST /negotiation/offer`: Gửi đề xuất giá mới.
- `GET /negotiation/session/:id`: Lấy toàn bộ lịch sử đàm phán.
- `GET /healthz/liveness`: K8s Liveness Probe.
- `GET /healthz/readiness`: K8s Readiness Probe.

---

## 5. HƯỚNG DẪN KIỂM THỬ (TESTING GUIDE)

### Postman Test — `POST /negotiation/offer`
```json
{
  "tenantId": "a0000000-0000-0000-0000-000000000001",
  "shipmentId": "shipment-999",
  "customerId": "CUST-001",
  "offerPrice": 1100,
  "listPrice": 1500,
  "bottomPrice": 1200,
  "customerTier": "STANDARD"
}
```
* **Kết quả kỳ vọng:** `decision: "COUNTER_OFFER"`, `counterOfferPrice: 1260`, `aiSpeech`: "Cảm ơn đề xuất $1100... giá tốt nhất là $1260...".
