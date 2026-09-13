# DANH MỤC & ĐẶC TẢ CHỨNG TỪ VẬN HÀNH (LÔ HÀNG TENANT & HỆ THỐNG PLATFORM)

> **Mã tài liệu:** `DOC-AURORA-DOCUMENTS-02`  
> **Phiên bản:** 1.0.0  
> **Phạm vi áp dụng:** Phân định rõ ràng giữa chứng từ nghiệp vụ vận chuyển của **Tenant (Doanh nghiệp Logistics)** và hồ sơ chứng từ quản trị của **System - Platform (Aurora Core)**.

---

## PHẦN I: DANH MỤC CHỨNG TỪ LÔ HÀNG (TENANT OPERATIONAL DOCUMENTS)

Các chứng từ này phát sinh trong toàn bộ vòng đời thực thi vận chuyển hàng hóa xuất nhập khẩu và nội địa của khách hàng. Hệ thống Aurora tự động hóa việc tiếp nhận, phân loại qua OCR/LLM Parser, xác thực trường dữ liệu và liên kết trực tiếp vào thực thể `Shipment`.

```text
               ┌────────────────────────────────────────────────────────┐
               │         TIẾP NHẬN BỘ CHỨNG TỪ LÔ HÀNG (TENANT)         │
               └───────────────────────────┬────────────────────────────┘
                                           │
       ┌───────────────────┬───────────────┴───────────────┬───────────────────┐
       ▼                   ▼                               ▼                   ▼
┌──────────────┐   ┌───────────────┐               ┌───────────────┐   ┌───────────────┐
│ Commercial   │   │ Packing List  │               │    Bill of    │   │ Tờ khai       │
│ Invoice      │   │ (Phiếu đóng   │               │    Lading     │   │ Hải quan      │
│ (Hóa đơn TM) │   │  gói hàng)    │               │  (Vận đơn)    │   │ (Customs Dec) │
└──────┬───────┘   └───────┬───────┘               └───────┬───────┘   └───────┬───────┘
       │                   │                               │                   │
       └───────────────────┼───────────────────────────────┴───────────────────┘
                           ▼
              [DocumentOcr Microservice]
                           │  - Trích xuất OCR (LayoutLM / Vision LLM)
                           │  - Trả về JSON Schema chuẩn hóa
                           ▼
          [RegulatoryCompliance & ShipmentWorkflow]
                           │  - Đối soát tính nhất quán số liệu
                           │  - Kiểm tra rủi ro & hợp lệ
                           ▼
              [Gắn vào Hồ sơ Vận đơn điện tử]
```

---

### 1. Vận đơn (Bill of Lading - B/L)

#### 1.1. Mục đích & Nghiệp vụ
- Là chứng từ quan trọng nhất trong vận tải đường biển/đa phương thức, xác nhận quyền sở hữu hàng hóa, bằng chứng hợp đồng vận chuyển và biên lai giao nhận hàng hóa.
- Phân loại:
  - **Master B/L (MBL)**: Do hãng tàu phát hành cho công ty Forwarder.
  - **House B/L (HBL)**: Do Forwarder phát hành cho chủ hàng trực tiếp (Shipper).
  - **Sea Waybill**: Vận đơn không chuyển nhượng được, sử dụng khi không cần xuất trình B/L gốc để lấy hàng nhanh.

#### 1.2. Cấu trúc trường thông tin cốt lõi (JSON Schema trích xuất từ OCR)
```json
{
  "document_type": "BILL_OF_LADING",
  "bl_number": "ONE-SGN-2026-98124",
  "bl_category": "HOUSE_BL",
  "shipper": {
    "name": "VIETNAM AGRO EXPORT CO., LTD",
    "address": "Lot B2, Tan Binh Industrial Park, Ho Chi Minh City, Vietnam",
    "tax_code": "0314892812"
  },
  "consignee": {
    "name": "GLOBAL PACIFIC LOGISTICS INC",
    "address": "450 Long Beach Blvd, Los Angeles, CA 90802, USA"
  },
  "notify_party": {
    "name": "GLOBAL PACIFIC LOGISTICS INC (SAME AS CONSIGNEE)",
    "contact": "+1-562-555-0199"
  },
  "vessel_name": "ONE APUS",
  "voyage_number": "0082E",
  "port_of_loading": "Cat Lai Port, Ho Chi Minh City (VNCLI)",
  "port_of_discharge": "Port of Los Angeles (USLAX)",
  "final_destination": "Ontario Logistics Warehouse, CA, USA",
  "containers": [
    {
      "container_number": "TGHU9823412",
      "seal_number": "ONESEAL8821",
      "type": "40HC",
      "package_count": 1200,
      "package_unit": "CARTONS",
      "gross_weight_kg": 18450.00,
      "measurement_cbm": 58.40
    }
  ],
  "freight_terms": "FREIGHT_PREPAID",
  "issued_date": "2026-09-05"
}
```

---

### 2. Hóa đơn Thương mại (Commercial Invoice - C/I)

#### 2.1. Mục đích & Nghiệp vụ
- Do người bán (Exporter/Seller) phát hành cho người mua (Importer/Buyer) để yêu cầu thanh toán tiền hàng.
- Là căn cứ xác định trị giá tính thuế hải quan, lập hồ sơ hải quan và mở tín dụng thư (L/C).

#### 2.2. Cấu trúc trường thông tin cốt lõi
```json
{
  "document_type": "COMMERCIAL_INVOICE",
  "invoice_number": "INV-2026-0988",
  "invoice_date": "2026-09-02",
  "currency": "USD",
  "incoterms": "FOB",
  "incoterms_location": "Cat Lai Port, Vietnam",
  "seller": {
    "name": "VIETNAM AGRO EXPORT CO., LTD",
    "bank_account": "0071000982341 - VCB HCMC"
  },
  "buyer": {
    "name": "GLOBAL PACIFIC FOODS LLC",
    "payment_terms": "T/T 30 DAYS NET"
  },
  "line_items": [
    {
      "item_code": "CF-ROB-G1",
      "description": "Vietnamese Robusta Green Coffee Beans Grade 1",
      "hs_code": "0901.11.10",
      "quantity": 18.00,
      "unit": "MT",
      "unit_price": 2450.00,
      "total_amount": 44100.00
    }
  ],
  "subtotal": 44100.00,
  "discount": 0.00,
  "freight_insurance": 0.00,
  "total_amount": 44100.00
}
```

---

### 3. Phiếu đóng gói hàng hóa (Packing List - P/L)

#### 3.1. Mục đích & Nghiệp vụ
- Mô tả chi tiết quy cách đóng gói, số kiện, số lượng hàng hóa trong từng kiện, kích thước, trọng lượng tịnh (Net Weight) và trọng lượng cả bì (Gross Weight).
- Phục vụ quá trình bốc xếp hàng, kiểm tra tại kho bãi, kiểm hóa hải quan và xếp dỡ xe tải.

#### 3.2. Cấu trúc trường thông tin cốt lõi
```json
{
  "document_type": "PACKING_LIST",
  "packing_list_number": "PL-2026-0988",
  "reference_invoice": "INV-2026-0988",
  "total_packages": 300,
  "package_type": "JUTE_BAGS_ON_PALLETS",
  "total_net_weight_kg": 18000.00,
  "total_gross_weight_kg": 18450.00,
  "total_volume_cbm": 58.40,
  "package_breakdown": [
    {
      "pallet_id": "PLT-01 to PLT-15",
      "bags_per_pallet": 20,
      "weight_per_pallet_kg": 1230.00,
      "dimensions_cm": "120x100x160"
    }
  ]
}
```

---

### 4. Tờ khai Hải quan (Customs Declaration Form)

#### 4.1. Mục đích & Nghiệp vụ
- Chứng từ kê khai bắt buộc với cơ quan hải quan (tại Việt Nam qua hệ thống VNACCS/VCIS) để kiểm soát hàng hóa xuất khẩu/nhập khẩu, thu thuế và cấp phép thông quan.
- Xác định trạng thái phân luồng: **Xanh (1)**, **Vàng (2)**, **Đỏ (3)**.

#### 4.2. Cấu trúc trường thông tin cốt lõi
```json
{
  "document_type": "CUSTOMS_DECLARATION",
  "declaration_number": "105928371920",
  "customs_sub_department": "Chi cục HQ Cửa khẩu Cảng Sài Gòn KV1 (02CI)",
  "declaration_date": "2026-09-04 09:15:00",
  "declaration_type": "B11 (Xuất kinh doanh)",
  "clearance_status": "CLEARED_GREEN_CHANNEL",
  "exporter_tax_code": "0314892812",
  "total_tax_payable_vnd": 0,
  "inspection_notes": "Thông quan tự động theo luồng Xanh - Không yêu cầu xuất trình hồ sơ giấy",
  "attached_vessels": "ONE APUS / 0082E"
}
```

---

### 5. Các chứng từ bổ trợ khác của Tenant
- **Giấy chứng nhận xuất xứ (C/O - Certificate of Origin):** Hưởng ưu đãi thuế quan theo hiệp định (Form E, Form D, Form EUR.1, Form CPTPP).
- **Lệnh giao hàng (D/O - Delivery Order):** Chứng từ nhận hàng do hãng tàu/forwarder cấp cho Consignee.
- **Biên bản giao nhận / Bằng chứng giao hàng (POD - Proof of Delivery):** Hình ảnh chụp giao hàng thực tế, chữ ký xác nhận của tài xế và người nhận.
- **Phiếu xác nhận khối lượng container (VGM Certificate):** Đáp ứng công ước SOLAS.
- **Biên lai đóng thuế & Phí nâng hạ cảng (Port & Demurrage Debit Note):** Đối soát chi phí phụ trợ.

---

## PHẦN II: DANH MỤC HỒ SƠ CHỨNG TỪ CẤP NỀN TẢNG (SYSTEM - PLATFORM DOCUMENTS)

Khác với chứng từ hàng hóa của Tenant, **System - Platform Documents** là các hồ sơ, hợp đồng, chứng từ thanh toán và nhật ký kiểm toán giữa **Đơn vị vận hành nền tảng Aurora (Platform Provider)** và **Doanh nghiệp thuê bao (Tenant/Client)**.

```text
┌────────────────────────────────────────────────────────────────────────┐
               HỆ THỐNG HỒ SƠ QUẢN TRỊ NỀN TẢNG AURORA
└───────────────────────────────────┬────────────────────────────────────┘
                                    │
    ┌───────────────────────┬───────┴───────┬───────────────────────┐
    ▼                       ▼               ▼                       ▼
┌──────────────┐    ┌───────────────┐ ┌───────────────┐   ┌───────────────────┐
│ Hợp đồng     │    │ Hóa đơn phí   │ │ Báo cáo kiểm  │   │ Nhật ký Truy vết  │
│ Dịch vụ SaaS │    │ Nền tảng      │ │ toán bảo mật  │   │ Quyết định AI     │
│ & SLA Policy │    │ (Platform Sub)│ │ (Audit Logs)  │   │ (AI Governance)   │
└──────────────┘    └───────────────┘ └───────────────┘   └───────────────────┘
```

---

### 1. Hợp đồng Cung cấp Dịch vụ Nền tảng & Cam kết SLA (Tenant SaaS Agreement & SLA)

#### 1.1. Mục đích & Vai trò
- Xác lập tư cách pháp nhân, quyền và nghĩa vụ giữa Aurora Platform và Tenant.
- Định nghĩa gói đăng ký (Tier: Starter, Professional, Enterprise), số lượng tài khoản nhân sự tối đa, dung tích lưu trữ hồ sơ OCR, và hạn mức gọi API.
- Cam kết mức độ sẵn sàng của hệ thống (Uptime SLA):
  - Tier Enterprise: **99.9% Uptime** (thời gian gián đoạn tối đa < 43.8 phút/tháng).
  - RPO (Recovery Point Objective): < 5 phút mất mát dữ liệu.
  - RTO (Recovery Time Objective): < 30 phút khôi phục hoạt động.

#### 1.2. Cấu trúc dữ liệu cấu hình Tenant (`IamTenant` Domain)
```json
{
  "tenant_id": "tnt_acme_logistics_vn",
  "company_legal_name": "ACME LOGISTICS INTERNATIONAL CO., LTD",
  "tax_id": "0109823481",
  "subscription_tier": "ENTERPRISE",
  "contract_id": "AURORA-SaaS-2026-0042",
  "valid_from": "2026-01-01T00:00:00Z",
  "valid_to": "2026-12-31T23:59:59Z",
  "resource_quotas": {
    "max_active_staff_users": 150,
    "monthly_ocr_page_quota": 50000,
    "ai_token_quota_monthly": 100000000,
    "storage_limit_gb": 2048,
    "enable_custom_smtp_mailbox": true
  },
  "sla_commitments": {
    "guaranteed_uptime_pct": 99.9,
    "support_tier": "24_7_DEDICATED_ENGINEER",
    "incident_response_time_minutes": 15
  }
}
```

---

### 2. Bảng kê Quyết toán Phí Dịch vụ Nền tảng (Platform Subscription & Usage Statement)

#### 2.1. Mục đích & Nghiệp vụ
- Do hệ thống `financial-service` / `billing-service` của Aurora Platform tự động tổng hợp định kỳ hàng tháng (Monthly Billing Cycle).
- Bao gồm: Phí thuê bao cố định (Base Subscription) + Phí sử dụng tài nguyên biến đổi (Overage OCR pages, VROOM routing calls, AI Token consumption, GPS telemetry throughput).

#### 2.2. Mẫu hóa đơn quyết toán nền tảng
```json
{
  "statement_id": "AUR-STMT-2026-08",
  "billing_cycle": "2026-08-01 to 2026-08-31",
  "tenant_id": "tnt_acme_logistics_vn",
  "currency": "USD",
  "line_items": [
    {
      "description": "Enterprise Platform Base Subscription Fee",
      "quantity": 1,
      "rate": 1200.00,
      "amount": 1200.00
    },
    {
      "description": "OCR Document Extraction (12,450 pages included + 3,200 overage)",
      "quantity": 3200,
      "rate": 0.04,
      "amount": 128.00
    },
    {
      "description": "VROOM Multi-Stop Route Optimization API Calls",
      "quantity": 1540,
      "rate": 0.05,
      "amount": 77.00
    },
    {
      "description": "AI Assistant & RAG Compliance Query Tokens (in millions)",
      "quantity": 4.5,
      "rate": 15.00,
      "amount": 67.50
    }
  ],
  "subtotal": 1472.50,
  "vat_percentage": 10.0,
  "vat_amount": 147.25,
  "total_payable": 1619.75,
  "payment_due_date": "2026-09-15",
  "escrow_wallet_status": "AUTO_DEBITED_SUCCESSFUL"
}
```

---

### 3. Nhật ký Kiểm toán Bảo mật & Chứng chỉ Tuân thủ (System Audit Logs & Security Certification)

#### 3.1. Mục đích & Nghiệp vụ
- Quản lý bởi vi dịch vụ `audit-service` (Java Spring Boot 3) và `IamTenant`.
- Ghi nhận mọi tương tác quản trị nhạy cảm: thay đổi phân quyền người dùng, thay đổi chính sách CBAC, truy xuất dữ liệu vượt quyền, điều chỉnh ngưỡng an toàn tuyến đường.
- Phục vụ chứng nhận chuẩn an ninh thông tin **ISO/IEC 27001**, **SOC 2 Type II** và tuân thủ **Nghị định 13/2023/NĐ-CP**.

#### 3.2. Cấu trúc bản ghi Audit Log chuẩn
```json
{
  "audit_event_id": "aud_evt_991823101",
  "timestamp": "2026-09-10T14:32:11.204Z",
  "tenant_id": "tnt_acme_logistics_vn",
  "actor": {
    "user_id": "usr_adm_8812",
    "email": "admin@acmelogistics.com",
    "role": "TENANT_ADMIN",
    "ip_address": "118.69.182.44",
    "user_agent": "Mozilla/5.0 (Windows NT 10.0; Win64; x64) Chrome/128.0"
  },
  "action": "CAPABILITY_PERMISSION_GRANTED",
  "target_resource": "USER:usr_staff_4412",
  "details": {
    "granted_permission": "route_planning:approve",
    "reason": "Temporary delegation for Night Shift Supervisor",
    "expiration": "2026-09-15T00:00:00Z"
  },
  "security_verdict": "AUTHORIZED_AND_IMMUTABLE_STORED"
}
```

---

### 4. Hồ sơ Truy vết Quyết định AI (AI Agent Traceability & Governance Record)

#### 4.1. Mục đích & Nghiệp vụ
- Được quản lý bởi `ai-governance` (Spring Boot) và `devops-agent`.
- Đảm bảo mọi phản hồi từ `NegotiationAgent`, `CustomerAssistant`, hay `RegulatoryCompliance` đều có đầy đủ vết kiểm toán (Prompt, Context, Retrieved Citations, Model Version, Safety Score, Human Intervention Status) nhằm đáp ứng đạo luật AI (EU AI Act & Tiêu chuẩn AI có trách nhiệm).

#### 4.2. Cấu trúc bản ghi Traceability
```json
{
  "trace_id": "ai_trc_20260910_77192",
  "agent_service": "NegotiationAgentService",
  "tenant_id": "tnt_acme_logistics_vn",
  "session_id": "neg_sess_88124",
  "model_deployment": "gemini-1.5-pro / internal-v3",
  "input_context": {
    "customer_target_price": 1800.00,
    "system_cost_floor": 1650.00,
    "market_index_rate": 1920.00
  },
  "ai_proposed_offer": 1780.00,
  "governance_evaluation": {
    "profit_margin_valid": true,
    "regulatory_compliance_check": "PASSED",
    "safety_confidence_score": 0.96,
    "hitl_required": false
  },
  "action_executed": "SEND_COUNTER_OFFER_EMAIL"
}
```

---
*Tài liệu đối chiếu và chuẩn hóa chứng từ cho toàn bộ kỹ sư & chuyên viên vận hành Aurora.*
