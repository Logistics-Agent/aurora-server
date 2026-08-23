# TÀI LIỆU KỸ THUẬT VÀ NGHIỆP VỤ CUSTOMER ASSISTANT AI SERVICE [AI + CQRS]

> **Phụ trách (Owner):** Đào Huỳnh  
> **Công nghệ:** NestJS (TypeScript), Gemini AI, CQRS Read Model Store, REST API  
> **Cổng giao tiếp:** HTTP `5007`  

---

## 1. TỔNG QUAN VÀ MỤC TIÊU PHÂN HỆ

Dịch vụ **Customer Assistant AI Service** là trợ lý ảo hỗ trợ khách hàng tra cứu vị trí lô hàng, kiểm tra công nợ và hướng dẫn thủ tục logistics qua hội thoại tự nhiên (NLP).

### Kiến trúc Đọc Độc Lập CQRS (Read-Only CQRS Architecture):
* **Không truy vấn DB Giao dịch:** Khi khách hàng hỏi "Đơn hàng của tôi đang ở đâu?", Customer Assistant **tuyệt đối KHÔNG TRUY VẤN TRỰC TIẾP DB GIAO DỊCH** của Billing hay Shipment Workflow để bảo vệ hiệu năng hệ thống.
* **Đọc từ Read-Model Replica:** Trợ lý ảo truy vấn vào `ReadModelStore` (Bản sao cơ sở dữ liệu read-only) được đồng bộ bất đồng bộ từ các sự kiện RabbitMQ (`shipment.status_changed`, `invoice.created`, `payment.received`).

---

## 2. NGUYÊN TẮC THIẾT KẾ VÀ KIẾN TRÚC (CLEAN ARCHITECTURE)

```text
src/nestjs/customer-assistant-service/
├── src/
│   ├── read-model/
│   │   └── read-model.store.ts       # ★ CQRS Read Model (Shipments & Invoices)
│   ├── application/
│   │   └── services/
│   │       └── assistant.service.ts  # ★ Intent Classifier + RAG Query + Gemini AI
│   ├── interface/
│   │   └── controllers/
│   │       └── assistant.controller.ts # REST API (/chat, /chat/customer/:id)
│   └── health/
│       └── health.controller.ts      # Terminus K8s Probes (/healthz/liveness & readiness)
```

---

## 3. LUỒNG PHÂN LOẠI INTENT & XỬ LÝ NLP

```
Khách hàng Chat
      │
      ▼
[AssistantController] ──► [CustomerAssistantService]
                                  │
                  (NLP Intent Classification)
                                  │
    ┌─────────────────────────────┼─────────────────────────────┐
    ▼                             ▼                             ▼
[TRACK_SHIPMENT]          [CHECK_BALANCE]               [GENERAL_HELP]
ReadModel.getShipment()   ReadModel.getBalanceSummary()  Trả lời thông tin chung
    │                             │                             │
    └─────────────────────────────┴─────────────────────────────┘
                                  │
                        (Gemini AI NLP Speech)
                                  │
                                  ▼
                        Trả lời cho Khách hàng
```

---

## 4. REST API ENDPOINTS

- `POST /chat`: Gửi tin nhắn trao đổi với Trợ lý AI.
- `GET /chat/customer/:id`: Lấy tóm tắt vị trí đơn hàng và công nợ của khách hàng.
- `GET /healthz/liveness`: K8s Liveness Probe.
- `GET /healthz/readiness`: K8s Readiness Probe.

---

## 5. HƯỚNG DẪN KIỂM THỬ (TESTING GUIDE)

### Postman Test — `POST /chat`

```json
{
  "customerId": "CUST-001",
  "message": "Đơn hàng shp_33019284 của tôi đang ở đâu?"
}
```
* **Kết quả kỳ vọng:** `intent: "TRACK_SHIPMENT"`, `replyMessage`: "Lô hàng shp_33019284... trạng thái IN_TRANSIT... Vị trí hiện tại: Vùng biển Biển Đông...".
