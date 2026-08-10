# TÀI LIỆU KỸ THUẬT VÀ NGHIỆP VỤ BILLING & SETTLEMENT SERVICE [CORE]

> **Phụ trách (Owner):** Đào Huỳnh  
> **Công nghệ:** NestJS (TypeScript), gRPC, Prisma ORM, PostgreSQL (Supabase Cloud - Schema `billing_service`), RabbitMQ Event-Driven (CloudEvents 1.0 Spec), Cockatiel Circuit Breaker, Redis Idempotency, Cloudflare R2 / AWS S3 Storage Mock  
> **Cổng giao tiếp gRPC:** `5004`  
> **File Hợp đồng gRPC:** `protos/billing.proto`  

---

## 1. TỔNG QUAN VÀ MỤC TIÊU PHÂN HỆ

Dịch vụ **Billing & Settlement Service** là phân hệ trung tâm xử lý tài chính, hóa đơn, công nợ và ví ký quỹ trong hệ thống SaaS Logistics Aurora. Phân hệ này chịu trách nhiệm:

1. **POD-Triggered Official Invoicing:** Lắng nghe Event `shipment.pod_uploaded` từ RabbitMQ. Chỉ phát hành hóa đơn chính thức khi có biên bản giao hàng `pod_s3_key`.
2. **Giao tiếp gRPC Nội bộ + Cockatiel Circuit Breaker:** Kết nối `FinancialService` (port `5003`) qua Circuit Breaker (tự động ngắt mạch sau 3 lỗi, fallback an toàn).
3. **Chống trùng lặp Idempotency Interceptor (`x-idempotency-key`):** Kiểm tra Redis key TTL 120s chống duplicate transactions.
4. **Debit Note / Credit Note Handling:** Xử lý phí phát sinh ngoài dự kiến (DEM/DET, kiểm hóa, cân xe) trong ACID Transaction.
5. **e-Invoice Gateway Adapter:** Ký số hóa đơn điện tử và phát hành Mã Cơ quan Thuế (`VNPTEInvoiceAdapter`).
6. **CloudEvents 1.0 Spec Standard:** Phát event `billing.invoice_created` và `billing.payment_received` ra RabbitMQ theo tiêu chuẩn CloudEvents.
7. **Dead Letter Queue (DLQ):** Tự động đẩy tin nhắn lỗi nack quá 3 lần vào queue `logistics_events.dlq`.
8. **Cron Job Tự động OVERDUE:** Mỗi đêm 00:05 UTC, tự động chuyển hóa đơn quá hạn sang `OVERDUE`.
9. **Terminus K8s Health Check:** Cung cấp `/healthz/liveness` và `/healthz/readiness`.

---

## 2. NGUYÊN TẮC THIẾT KẾ VÀ KIẾN TRÚC (CLEAN ARCHITECTURE)

```text
src/nestjs/billing-service/src/
├── config/                  # Read & Validate env vars via class-validator
├── common/
│   ├── interceptors/
│   │   ├── tenant.interceptor.ts      # Bóc tách x-tenant-id từ gRPC Metadata
│   │   └── idempotency.interceptor.ts # ★ Check x-idempotency-key trong Redis (TTL 120s)
│   ├── middleware/
│   │   └── correlation-id.middleware.ts # ★ Dynamic x-correlation-id propagation
│   ├── events/
│   │   └── cloud-event.factory.ts     # ★ CloudEvents 1.0 Spec Wrapper
│   └── filters/grpc-exception.filter.ts
├── infrastructure/
│   ├── prisma/                        # Prisma Client (Supabase PostgreSQL)
│   ├── jobs/overdue-invoice.cron.ts   # Cron Job tự động OVERDUE lúc 00:05 hàng ngày
│   ├── storage/storage.service.ts     # Presigned PDF URLs (S3/R2)
│   ├── einvoice/einvoice.adapter.ts   # ★ VNPTEInvoiceAdapter (Digital Signing & Tax Code)
│   ├── messaging/
│   │   ├── rabbitmq.service.ts        # CloudEvents Publisher
│   │   ├── dlq.service.ts             # ★ Dead Letter Queue Service (logistics_events.dlq)
│   │   └── event-handlers/shipment-completed.handler.ts # Idempotent POD Consumer
│   └── grpc-clients/
│       └── financial.grpc-client.ts   # ★ Cockatiel Circuit Breaker Client (port 5003)
├── application/
│   ├── use-cases/generate-invoice.use-case.ts # ACID Transaction + POD Validation
│   └── services/billing.service.ts    # RecordPayment, CancelInvoice, Debit/Credit Notes
├── interface/
│   ├── controllers/billing.controller.ts
│   └── dto/billing.dto.ts
└── health/
    └── health.controller.ts           # ★ Terminus K8s Probes (/healthz/liveness & readiness)
```

---

## 3. PROTOBUF CONTRACT & DATABASE SCHEMA

### 3.1. Protobuf Contract (`protos/billing.proto`)

```protobuf
syntax = "proto3";

package billing;

service BillingService {
  rpc GenerateInvoice(GenerateInvoiceRequest) returns (InvoiceResponse);
  rpc GetInvoiceDetail(GetInvoiceRequest) returns (InvoiceDetailResponse);
  rpc CheckCustomerCredit(CreditCheckRequest) returns (CreditCheckResponse);
  rpc RecordPayment(RecordPaymentRequest) returns (RecordPaymentResponse);
  rpc CancelInvoice(CancelInvoiceRequest) returns (InvoiceResponse);
  rpc IssueDebitNote(IssueDebitNoteRequest) returns (AdjustmentNoteResponse);   // ★ MỚI
  rpc IssueCreditNote(IssueCreditNoteRequest) returns (AdjustmentNoteResponse); // ★ MỚI

  // Escrow Wallet Operations
  rpc CreateEscrowWallet(CreateEscrowWalletRequest) returns (WalletResponse);
  rpc GetWalletBalance(GetWalletBalanceRequest) returns (WalletResponse);
  rpc FreezeEscrowAmount(FreezeEscrowRequest) returns (TransactionResponse);
  rpc ReleaseEscrowAmount(ReleaseEscrowRequest) returns (TransactionResponse);
  rpc RefundEscrowAmount(RefundEscrowRequest) returns (TransactionResponse);
}
```

---

## 4. HƯỚNG DẪN KHỞI CHẠY VÀ KIỂM THỬ

```powershell
# Chạy Billing Service
cd src/nestjs/billing-service
npm run start:dev

# Health Check
curl http://localhost:5004/healthz/liveness
curl http://localhost:5004/healthz/readiness
```
