# TÀI LIỆU KỸ THUẬT VÀ NGHIỆP VỤ FINANCIAL & COST ESTIMATION SERVICE [AI + CORE]

> **Phụ trách (Owner):** Đào Huỳnh  
> **Công nghệ:** NestJS (TypeScript), gRPC, Prisma ORM, PostgreSQL (Supabase Cloud - Schema `financial_service`), Redis Rate Cache (< 2ms response), Cron FX Sync  
> **Cổng giao tiếp gRPC:** `5003`  
> **File Hợp đồng gRPC:** `protos/financial.proto`  

---

## 1. TỔNG QUAN VÀ MỤC TIÊU PHÂN HỆ

Dịch vụ **Financial & Cost Estimation Service** là phân hệ tính toán tài chính độc lập trong hệ thống SaaS Logistics Aurora, chịu trách nhiệm:

1. **Cost Estimation:** Tính toán cước cho SEA, AIR, ROAD (Chargeable Weight, Volumetric Divisor 5000/6000).
2. **Sub-2ms Redis Rate Caching:** Tối ưu hóa truy vấn bảng cước qua `RateCacheService` (Redis Key TTL 24h) đáp ứng phản hồi dưới `< 2ms`.
3. **Dynamic Margin Decay Engine:** Tính toán giá sàn đàm phán cho Negotiation Agent AI biến động theo giờ cut-off:
   $$\text{Min Acceptable Price} = \text{Base Cost} \times \left(1 + \text{Base Margin \%} \times \left( \frac{T_{\text{remaining}}}{T_{\text{total}}} \right)^\gamma \right)$$
4. **Currency Exchange Engine:** Tự động đồng bộ tỷ giá ngoại tệ USD/VND/EUR hàng ngày lúc 00:05 UTC (`ExchangeRateSyncCronJob`).
5. **Fuel Surcharge (FSC/EBS) & Cargo Insurance:** Phụ phí nhiên liệu biến động và phí bảo hiểm hàng hóa (0.3%).
6. **Terminus K8s Health Check:** Cung cấp `/healthz/liveness` và `/healthz/readiness`.

---

## 2. NGUYÊN TẮC THIẾT KẾ VÀ KIẾN TRÚC (CLEAN ARCHITECTURE)

```text
src/nestjs/financial-service/
├── prisma/
│   └── schema.prisma                  # Schema PostgreSQL (base_freight_rates, customs_duty_rates, exchange_rates)
├── src/
│   ├── config/                        # Validate env variables via class-validator
│   ├── common/
│   │   ├── interceptors/tenant.interceptor.ts
│   │   └── filters/grpc-exception.filter.ts
│   ├── domain/
│   │   └── services/cost-calculator.domain-service.ts # Pure math logic (Volumetric, FSC, Dynamic Margin Decay)
│   ├── infrastructure/
│   │   ├── cache/rate-cache.service.ts # ★ Redis Sub-2ms Rate Caching Service
│   │   ├── jobs/exchange-rate-sync.cron.ts # ★ Daily 00:05 UTC FX Sync Cron Job
│   │   └── prisma/prisma.service.ts
│   ├── application/
│   │   └── services/financial.service.ts # EstimateCost, GetCustomsDuty, GetDynamicMargin, GetExchangeRate
│   ├── interface/
│   │   ├── controllers/financial.controller.ts
│   │   └── dto/financial.dto.ts
│   └── health/
│       └── health.controller.ts       # ★ Terminus K8s Probes (/healthz/liveness & readiness)
```

---

## 3. PROTOBUF CONTRACT & DATABASE SCHEMA

### 3.1. Protobuf Contract (`protos/financial.proto`)

```protobuf
syntax = "proto3";

package financial;

service FinancialService {
  rpc EstimateCost(EstimateCostRequest) returns (EstimateCostResponse);
  rpc GetCustomsDuty(GetCustomsDutyRequest) returns (GetCustomsDutyResponse);
  rpc GetMinAcceptableRate(GetMinAcceptableRateRequest) returns (GetMinAcceptableRateResponse);
  rpc GetDynamicMargin(GetDynamicMarginRequest) returns (GetDynamicMarginResponse);   // ★ MỚI (TASK-001)
  rpc GetExchangeRate(GetExchangeRateRequest) returns (GetExchangeRateResponse);     // ★ MỚI (TASK-002)
}
```

---

## 4. HƯỚNG DẪN KHỞI CHẠY VÀ KIỂM THỬ

```powershell
# Chạy Financial Service
cd src/nestjs/financial-service
npm run start:dev

# Health Check Probes
curl http://localhost:5003/healthz/liveness
curl http://localhost:5003/healthz/readiness
```
